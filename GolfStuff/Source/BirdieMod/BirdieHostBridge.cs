using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// Mod-owned Mirror ClientRpc bridge — server→all clients direction.
//
// Architecture summary
// ─────────────────────
// 1. EnsureHandlersRegistered() sets up reflection caches and calls
//    RemoteProcedureCalls.RegisterDelegate(playerInventoryType, methodName,
//      RemoteCallType.ClientRpc, delegate, requiresAuthority=false).
//    The returned UInt16 is the deterministic function-hash both sides use.
//    This is called once per process lifetime (not per session).
//
// 2. BroadcastToClients(active, featureMask) (host path):
//    • Gets the host's local PlayerInventory netId and ComponentIndex.
//    • Writes bool and ulong to a pooled NetworkWriter.
//    • Forges an RpcMessage{netId, componentIndex, functionHash, payload}
//      and calls NetworkServer.SendToAll<RpcMessage>(msg, 0) via reflection.
//
// 3. Client side — ClientHandleSyncHostConfig(obj, reader, conn) is the
//    RemoteCallDelegate invoked by Mirror's ClientRpc dispatcher.
//    It reads active and featureMask, then updates IsUnderHostControl
//    and ReceivedFeatureMask accordingly.

internal static class BirdieHostBridge
{
    private const string RpcMethodName =
        "System.Void PlayerInventory::BirdieCmdSyncHostConfig(System.Boolean,System.UInt64)";

    // ── public state ─────────────────────────────────────────────────────────

    internal static bool IsUnderHostControl;
    internal static ulong ReceivedFeatureMask;

    // ── private state ─────────────────────────────────────────────────────────

    private static bool initialized;
    private static ushort registeredRpcHash;

    // ── Mirror reflection cache ──────────────────────────────────────────────

    private static Type mirrorNbType;        // Mirror.NetworkBehaviour
    private static Type mirrorReaderType;    // Mirror.NetworkReader
    private static Type mirrorConnType;      // Mirror.NetworkConnectionToClient
    private static Type mirrorRpcType;       // Mirror.RemoteCalls.RemoteProcedureCalls
    private static Type mirrorRcdType;       // Mirror.RemoteCalls.RemoteCallDelegate
    private static Type mirrorRctType;       // Mirror.RemoteCalls.RemoteCallType
    private static Type mirrorRpcMsgType;    // Mirror.RpcMessage
    private static Type mirrorNsType;        // Mirror.NetworkServer
    private static Type mirrorNwpType;       // Mirror.NetworkWriterPool
    private static Type mirrorNwExtType;     // Mirror.NetworkWriterExtensions
    private static Type mirrorNrExtType;     // Mirror.NetworkReaderExtensions

    private static MethodInfo mirrorRegisterDelegate;
    private static MethodInfo mirrorNsSendToAll;          // NetworkServer.SendToAll<RpcMessage>
    private static MethodInfo mirrorWriterExtWriteBool;   // NetworkWriterExtensions.WriteBool
    private static MethodInfo mirrorWriterExtWriteULong;  // NetworkWriterExtensions.WriteULong
    private static MethodInfo mirrorWriterPoolGet;
    private static MethodInfo mirrorWriterPoolReturn;
    private static MethodInfo mirrorWriterToArraySegment;
    private static MethodInfo mirrorReaderExtReadBool;    // NetworkReaderExtensions.ReadBool
    private static MethodInfo mirrorReaderExtReadULong;   // NetworkReaderExtensions.ReadULong

    private static PropertyInfo mirrorNbNetId;            // NetworkBehaviour.netId
    private static PropertyInfo mirrorNbComponentIndex;   // NetworkBehaviour.ComponentIndex

    private static FieldInfo mirrorRpcNetId;
    private static FieldInfo mirrorRpcComponentIndex;
    private static FieldInfo mirrorRpcFunctionHash;
    private static FieldInfo mirrorRpcPayload;

    // ── Game reflection cache ────────────────────────────────────────────────

    private static PropertyInfo cachedLocalPlayerInventoryProperty; // GameManager.LocalPlayerInventory

    // ── public API ───────────────────────────────────────────────────────────

    // Idempotent. Safe to call every frame — returns immediately after first call.
    internal static void EnsureHandlersRegistered()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        try
        {
            if (!CollectReflectionCaches())
            {
                BirdieLog.Warning("[Birdie] Host bridge: Mirror reflection incomplete — host config broadcast unavailable.");
                return;
            }

            RegisterRpcHandler();
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Host bridge init: " + ex.Message);
        }
    }

    // Host path: forge an RpcMessage targeting the local PlayerInventory and
    // broadcast it to all connected clients via NetworkServer.SendToAll.
    // When active==false, featureMask is always sent as ulong.MaxValue so
    // clients know to unlock everything.
    internal static void BroadcastToClients(bool active, ulong featureMask)
    {
        EnsureHandlersRegistered();

        // Guard: must be running as server.
        try
        {
            Type nsType = mirrorNsType;
            if (nsType != null)
            {
                PropertyInfo activeProp = nsType.GetProperty(
                    "active",
                    BindingFlags.Public | BindingFlags.Static);
                if (activeProp != null && !(bool)activeProp.GetValue(null, null))
                {
                    BirdieLog.Warning("[Birdie] Host bridge: NetworkServer is not active — cannot broadcast.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Host bridge (server check): " + ex.Message);
            return;
        }

        if (registeredRpcHash == 0 ||
            mirrorNbNetId == null ||
            mirrorNbComponentIndex == null ||
            mirrorRpcMsgType == null ||
            mirrorNsSendToAll == null ||
            mirrorWriterExtWriteBool == null ||
            mirrorWriterExtWriteULong == null ||
            mirrorWriterPoolGet == null ||
            mirrorWriterPoolReturn == null ||
            mirrorWriterToArraySegment == null ||
            cachedLocalPlayerInventoryProperty == null)
        {
            BirdieLog.Warning("[Birdie] Host bridge: broadcast not ready (reflection incomplete).");
            return;
        }

        try
        {
            Component inventory = cachedLocalPlayerInventoryProperty.GetValue(null, null) as Component;
            if (inventory == null)
            {
                BirdieLog.Warning("[Birdie] Host bridge: LocalPlayerInventory is null.");
                return;
            }

            uint netId = (uint)mirrorNbNetId.GetValue(inventory, null);
            byte componentIndex = (byte)mirrorNbComponentIndex.GetValue(inventory, null);

            // When deactivating, always send ulong.MaxValue so clients unlock everything.
            ulong maskToSend = active ? featureMask : ulong.MaxValue;

            // Serialise bool + ulong arguments.
            object writer = mirrorWriterPoolGet.Invoke(null, null);
            mirrorWriterExtWriteBool.Invoke(null, new object[] { writer, active });
            mirrorWriterExtWriteULong.Invoke(null, new object[] { writer, maskToSend });
            ArraySegment<byte> payload = (ArraySegment<byte>)mirrorWriterToArraySegment.Invoke(writer, null);

            // Build the RpcMessage.
            object rpcMsg = Activator.CreateInstance(mirrorRpcMsgType);
            mirrorRpcNetId.SetValue(rpcMsg, netId);
            mirrorRpcComponentIndex.SetValue(rpcMsg, componentIndex);
            mirrorRpcFunctionHash.SetValue(rpcMsg, registeredRpcHash);
            mirrorRpcPayload.SetValue(rpcMsg, payload);

            // Send to all clients — channel 0 = Channels.Reliable.
            mirrorNsSendToAll.Invoke(null, new object[] { rpcMsg, 0 });

            // Return the writer to the pool after send.
            mirrorWriterPoolReturn.Invoke(null, new object[] { writer });
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Host bridge (broadcast): " + ex.Message);
        }
    }

    // ── client handler (invoked by Mirror's ClientRpc dispatcher) ─────────────

    // Signature matches RemoteCallDelegate:
    //   void(NetworkBehaviour, NetworkReader, NetworkConnectionToClient)
    // Called via DynamicMethod wrapper; parameters arrive as object.
    private static void ClientHandleSyncHostConfig(object nbObj, object readerObj, object connObj)
    {
        try
        {
            if (readerObj == null)
            {
                return;
            }

            if (mirrorReaderExtReadBool == null || mirrorReaderExtReadULong == null)
            {
                BirdieLog.Warning("[Birdie] Host bridge client handler: reflection incomplete.");
                return;
            }

            bool active = (bool)mirrorReaderExtReadBool.Invoke(null, new object[] { readerObj });
            ulong mask = (ulong)mirrorReaderExtReadULong.Invoke(null, new object[] { readerObj });

            if (active)
            {
                IsUnderHostControl = true;
                ReceivedFeatureMask = mask;
                BirdieLog.Msg("[Birdie] Host bridge: host control active, featureMask=0x" + mask.ToString("X16"));
            }
            else
            {
                IsUnderHostControl = false;
                ReceivedFeatureMask = ulong.MaxValue;
                BirdieLog.Msg("[Birdie] Host bridge: host control deactivated (all features unlocked).");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Host bridge client handler: " + ex.Message);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static bool CollectReflectionCaches()
    {
        Assembly mirrorAssembly = null;
        Assembly gameAssembly = null;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly a = assemblies[i];
            string name = a.GetName().Name;
            if (name == "Mirror")
            {
                mirrorAssembly = a;
            }
            else if (name == "Assembly-CSharp")
            {
                gameAssembly = a;
            }
        }

        if (mirrorAssembly == null || gameAssembly == null)
        {
            return false;
        }

        // ── Mirror types ─────────────────────────────────────────────────────

        mirrorNbType = mirrorAssembly.GetType("Mirror.NetworkBehaviour");
        mirrorReaderType = mirrorAssembly.GetType("Mirror.NetworkReader");
        mirrorConnType = mirrorAssembly.GetType("Mirror.NetworkConnectionToClient");
        mirrorRpcType = mirrorAssembly.GetType("Mirror.RemoteCalls.RemoteProcedureCalls");
        mirrorRcdType = mirrorAssembly.GetType("Mirror.RemoteCalls.RemoteCallDelegate");
        mirrorRctType = mirrorAssembly.GetType("Mirror.RemoteCalls.RemoteCallType");
        mirrorRpcMsgType = mirrorAssembly.GetType("Mirror.RpcMessage");
        mirrorNsType = mirrorAssembly.GetType("Mirror.NetworkServer");
        mirrorNwpType = mirrorAssembly.GetType("Mirror.NetworkWriterPool");
        mirrorNwExtType = mirrorAssembly.GetType("Mirror.NetworkWriterExtensions");
        mirrorNrExtType = mirrorAssembly.GetType("Mirror.NetworkReaderExtensions");

        if (mirrorNbType == null || mirrorReaderType == null || mirrorConnType == null ||
            mirrorRpcType == null || mirrorRcdType == null || mirrorRctType == null ||
            mirrorRpcMsgType == null || mirrorNsType == null || mirrorNwpType == null ||
            mirrorNwExtType == null || mirrorNrExtType == null)
        {
            return false;
        }

        // ── Mirror methods ───────────────────────────────────────────────────

        // RemoteProcedureCalls.RegisterDelegate(Type, string, RemoteCallType, RemoteCallDelegate, bool) → UInt16
        mirrorRegisterDelegate = mirrorRpcType.GetMethod(
            "RegisterDelegate",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        // NetworkServer.SendToAll<RpcMessage>(RpcMessage, int)
        MethodInfo sendToAllOpenGeneric = mirrorNsType.GetMethod(
            "SendToAll",
            BindingFlags.Public | BindingFlags.Static);
        if (sendToAllOpenGeneric != null)
        {
            mirrorNsSendToAll = sendToAllOpenGeneric.MakeGenericMethod(mirrorRpcMsgType);
        }

        // NetworkWriterExtensions.WriteBool(NetworkWriter, bool)
        mirrorWriterExtWriteBool = mirrorNwExtType.GetMethod(
            "WriteBool",
            BindingFlags.Public | BindingFlags.Static);

        // NetworkWriterExtensions.WriteULong(NetworkWriter, ulong)
        mirrorWriterExtWriteULong = mirrorNwExtType.GetMethod(
            "WriteULong",
            BindingFlags.Public | BindingFlags.Static);

        // NetworkWriterPool.Get() and Return(NetworkWriterPooled)
        mirrorWriterPoolGet = mirrorNwpType.GetMethod(
            "Get",
            BindingFlags.Public | BindingFlags.Static);
        mirrorWriterPoolReturn = mirrorNwpType.GetMethod(
            "Return",
            BindingFlags.Public | BindingFlags.Static);

        // NetworkWriter.ToArraySegment()
        Type mirrorNwType = mirrorAssembly.GetType("Mirror.NetworkWriter");
        if (mirrorNwType != null)
        {
            mirrorWriterToArraySegment = mirrorNwType.GetMethod(
                "ToArraySegment",
                BindingFlags.Public | BindingFlags.Instance);
        }

        // NetworkReaderExtensions.ReadBool(NetworkReader)
        mirrorReaderExtReadBool = mirrorNrExtType.GetMethod(
            "ReadBool",
            BindingFlags.Public | BindingFlags.Static);

        // NetworkReaderExtensions.ReadULong(NetworkReader)
        mirrorReaderExtReadULong = mirrorNrExtType.GetMethod(
            "ReadULong",
            BindingFlags.Public | BindingFlags.Static);

        // ── Mirror properties / fields ───────────────────────────────────────

        mirrorNbNetId = mirrorNbType.GetProperty(
            "netId",
            BindingFlags.Public | BindingFlags.Instance);
        mirrorNbComponentIndex = mirrorNbType.GetProperty(
            "ComponentIndex",
            BindingFlags.Public | BindingFlags.Instance);

        mirrorRpcNetId = mirrorRpcMsgType.GetField("netId");
        mirrorRpcComponentIndex = mirrorRpcMsgType.GetField("componentIndex");
        mirrorRpcFunctionHash = mirrorRpcMsgType.GetField("functionHash");
        mirrorRpcPayload = mirrorRpcMsgType.GetField("payload");

        // ── Game types ───────────────────────────────────────────────────────

        Type playerInventoryType = gameAssembly.GetType("PlayerInventory");
        Type gameManagerType = gameAssembly.GetType("GameManager");

        if (gameManagerType != null)
        {
            cachedLocalPlayerInventoryProperty = gameManagerType.GetProperty(
                "LocalPlayerInventory",
                BindingFlags.Public | BindingFlags.Static);
        }

        // Sanity: enough to be useful?
        return mirrorRegisterDelegate != null &&
               mirrorNsSendToAll != null &&
               mirrorWriterExtWriteBool != null &&
               mirrorWriterExtWriteULong != null &&
               mirrorWriterPoolGet != null &&
               mirrorWriterToArraySegment != null &&
               mirrorReaderExtReadBool != null &&
               mirrorReaderExtReadULong != null &&
               mirrorNbNetId != null &&
               mirrorNbComponentIndex != null &&
               mirrorRpcNetId != null &&
               playerInventoryType != null;
    }

    private static void RegisterRpcHandler()
    {
        if (mirrorRegisterDelegate == null || mirrorRcdType == null ||
            mirrorNbType == null || mirrorReaderType == null || mirrorConnType == null ||
            mirrorRctType == null)
        {
            return;
        }

        // Resolve playerInventoryType for RegisterDelegate first arg.
        Type playerInventoryType = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            playerInventoryType = assemblies[i].GetType("PlayerInventory");
            if (playerInventoryType != null)
            {
                break;
            }
        }

        if (playerInventoryType == null)
        {
            BirdieLog.Warning("[Birdie] Host bridge: PlayerInventory type not found.");
            return;
        }

        // Build a DynamicMethod matching RemoteCallDelegate:
        //   void(Mirror.NetworkBehaviour, Mirror.NetworkReader, Mirror.NetworkConnectionToClient)
        // The method forwards its args (as object) to ClientHandleSyncHostConfig.
        DynamicMethod dm = new DynamicMethod(
            "BirdieHostConfigRpcHandler",
            typeof(void),
            new Type[] { mirrorNbType, mirrorReaderType, mirrorConnType },
            typeof(BirdieHostBridge).Module,
            skipVisibility: true);

        MethodInfo handlerMethod = typeof(BirdieHostBridge).GetMethod(
            "ClientHandleSyncHostConfig",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (handlerMethod == null)
        {
            BirdieLog.Warning("[Birdie] Host bridge: ClientHandleSyncHostConfig not found via reflection.");
            return;
        }

        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);   // Mirror.NetworkBehaviour → object
        il.Emit(OpCodes.Ldarg_1);   // Mirror.NetworkReader    → object
        il.Emit(OpCodes.Ldarg_2);   // Mirror.NetworkConnectionToClient → object
        il.Emit(OpCodes.Call, handlerMethod);
        il.Emit(OpCodes.Ret);

        Delegate remoteCallDelegate = dm.CreateDelegate(mirrorRcdType);

        // RemoteCallType.ClientRpc enum value.
        object clientRpcEnumValue = Enum.Parse(mirrorRctType, "ClientRpc");

        // Register and capture the returned hash.
        // Signature: UInt16 RegisterDelegate(Type, string, RemoteCallType, RemoteCallDelegate, bool)
        object result = mirrorRegisterDelegate.Invoke(
            null,
            new object[] { playerInventoryType, RpcMethodName, clientRpcEnumValue, remoteCallDelegate, false });

        registeredRpcHash = (ushort)result;

        if (registeredRpcHash == 0)
        {
            BirdieLog.Warning("[Birdie] Host bridge: RegisterDelegate returned hash 0 — may indicate a collision.");
        }
    }
}

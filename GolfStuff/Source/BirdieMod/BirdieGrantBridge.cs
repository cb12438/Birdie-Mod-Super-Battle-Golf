using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// Mod-owned Mirror command bridge.
//
// Architecture summary
// ─────────────────────
// 1. EnsureInitialized() sets up reflection caches and calls
//    RemoteProcedureCalls.RegisterDelegate(playerInventoryType, methodName,
//      RemoteCallType.Command, delegate, requiresAuthority=false).
//    The returned UInt16 is the deterministic function-hash that both sides use.
//    This is called once per process lifetime (not per session).
//
// 2. RequestGrant(inventory, itemTypeInt) (client path):
//    • Gets the inventory's netId and ComponentIndex from NetworkBehaviour.
//    • Writes itemTypeInt to a pooled NetworkWriter.
//    • Forges a CommandMessage{netId, componentIndex, functionHash, payload}
//      and calls NetworkClient.Send<CommandMessage>(msg, 0) via reflection.
//
// 3. Server side — ServerHandleGrantItem(obj, reader, conn) is the
//    RemoteCallDelegate invoked by Mirror's command dispatcher.
//    It reads itemTypeInt, calls ServerTryAddItem on the PlayerInventory,
//    and reads MaxUses from the ItemCollection.
//
// Note: the server (host) must be running this mod for the client path to
// work in multiplayer, because the handler is registered by this code.

internal static class BirdieGrantBridge
{
    // The custom method-name key we register in Mirror's RPC table.
    // Mirror hashes this string to a UInt16; both sides must agree on the string.
    private const string CommandMethodName =
        "System.Void PlayerInventory::BirdieCmdGrantItem(System.Int32)";

    // ── state ────────────────────────────────────────────────────────────────

    private static bool initialized;
    private static ushort registeredCommandHash;

    // ── Mirror reflection cache ──────────────────────────────────────────────

    private static Type mirrorNbType;        // Mirror.NetworkBehaviour
    private static Type mirrorReaderType;    // Mirror.NetworkReader
    private static Type mirrorConnType;      // Mirror.NetworkConnectionToClient
    private static Type mirrorRpcType;       // Mirror.RemoteCalls.RemoteProcedureCalls
    private static Type mirrorRcdType;       // Mirror.RemoteCalls.RemoteCallDelegate
    private static Type mirrorRctType;       // Mirror.RemoteCalls.RemoteCallType
    private static Type mirrorCmdMsgType;    // Mirror.CommandMessage
    private static Type mirrorNcType;        // Mirror.NetworkClient
    private static Type mirrorNwpType;       // Mirror.NetworkWriterPool
    private static Type mirrorNwExtType;     // Mirror.NetworkWriterExtensions
    private static Type mirrorNrExtType;     // Mirror.NetworkReaderExtensions

    private static MethodInfo mirrorRegisterDelegate;
    private static MethodInfo mirrorNcSend;             // NetworkClient.Send<CommandMessage>
    private static MethodInfo mirrorWriterExtWriteInt;  // NetworkWriterExtensions.WriteInt
    private static MethodInfo mirrorWriterPoolGet;
    private static MethodInfo mirrorWriterPoolReturn;
    private static MethodInfo mirrorWriterToArraySegment;
    private static MethodInfo mirrorReaderExtReadInt;   // NetworkReaderExtensions.ReadInt

    private static PropertyInfo mirrorNbNetId;           // NetworkBehaviour.netId
    private static PropertyInfo mirrorNbComponentIndex;  // NetworkBehaviour.ComponentIndex

    private static FieldInfo mirrorCmdNetId;
    private static FieldInfo mirrorCmdComponentIndex;
    private static FieldInfo mirrorCmdFunctionHash;
    private static FieldInfo mirrorCmdPayload;

    // ── Game reflection cache (used inside the server handler) ───────────────

    private static MethodInfo cachedServerTryAddItemMethod;
    private static Type cachedItemTypeEnumType;
    private static PropertyInfo cachedAllItemsProperty;
    private static MethodInfo cachedTryGetItemDataMethod;
    private static PropertyInfo cachedItemDataMaxUsesProperty;

    // ── public API ───────────────────────────────────────────────────────────

    // Returns true when the command handler was successfully registered and
    // the hash is non-zero. Used by the client dispatch path to decide whether
    // to fall through to the host-only warning.
    internal static bool IsReady()
    {
        return initialized && registeredCommandHash != 0;
    }

    // One-time setup. Called lazily before first use. Idempotent.
    internal static void EnsureInitialized()
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
                BirdieLog.Warning("[Birdie] Grant bridge: Mirror reflection incomplete — client item grant unavailable.");
                return;
            }

            RegisterCommandHandler();
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Grant bridge init: " + ex.Message);
        }
    }

    // Client path: forge a CommandMessage targeting the local PlayerInventory
    // and send it to the server. Falls back with a log if bridge is not ready.
    internal static void RequestGrant(Component inventory, int itemTypeInt)
    {
        EnsureInitialized();

        if (registeredCommandHash == 0 ||
            mirrorNbNetId == null ||
            mirrorNbComponentIndex == null ||
            mirrorCmdMsgType == null ||
            mirrorNcSend == null ||
            mirrorWriterExtWriteInt == null ||
            mirrorWriterPoolGet == null ||
            mirrorWriterPoolReturn == null ||
            mirrorWriterToArraySegment == null)
        {
            BirdieLog.Warning("[Birdie] Item spawner: grant bridge not ready (reflection incomplete).");
            return;
        }

        try
        {
            uint netId = (uint)mirrorNbNetId.GetValue(inventory, null);
            byte componentIndex = (byte)mirrorNbComponentIndex.GetValue(inventory, null);

            // Serialise the single int argument.
            object writer = mirrorWriterPoolGet.Invoke(null, null);
            mirrorWriterExtWriteInt.Invoke(null, new object[] { writer, itemTypeInt });
            ArraySegment<byte> payload = (ArraySegment<byte>)mirrorWriterToArraySegment.Invoke(writer, null);

            // Build the CommandMessage.
            object cmdMsg = Activator.CreateInstance(mirrorCmdMsgType);
            mirrorCmdNetId.SetValue(cmdMsg, netId);
            mirrorCmdComponentIndex.SetValue(cmdMsg, componentIndex);
            mirrorCmdFunctionHash.SetValue(cmdMsg, registeredCommandHash);
            mirrorCmdPayload.SetValue(cmdMsg, payload);

            // Send to server — channel 0 = Channels.Reliable.
            // Send is called before returning the writer so that Mirror's internal
            // serialisation of payload (WriteArraySegmentAndSize) reads valid bytes.
            mirrorNcSend.Invoke(null, new object[] { cmdMsg, 0 });

            // Return the writer to the pool after send.
            mirrorWriterPoolReturn.Invoke(null, new object[] { writer });
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Item spawner (client bridge): " + ex.Message);
        }
    }

    // ── server handler (invoked by Mirror's command dispatcher) ─────────────

    // Signature matches RemoteCallDelegate: (NetworkBehaviour, NetworkReader, NetworkConnectionToClient).
    // Called via DynamicMethod wrapper; parameters arrive as object for reflection safety.
    private static void ServerHandleGrantItem(object nbObj, object readerObj, object connObj)
    {
        try
        {
            if (nbObj == null || readerObj == null)
            {
                return;
            }

            if (mirrorReaderExtReadInt == null ||
                cachedServerTryAddItemMethod == null ||
                cachedItemTypeEnumType == null)
            {
                BirdieLog.Warning("[Birdie] Grant bridge server handler: reflection incomplete.");
                return;
            }

            int itemTypeInt = (int)mirrorReaderExtReadInt.Invoke(null, new object[] { readerObj });

            object itemTypeValue = Enum.ToObject(cachedItemTypeEnumType, itemTypeInt);
            int maxUses = GetItemMaxUses(itemTypeInt);
            if (maxUses <= 0)
            {
                maxUses = 1;
            }

            // nbObj is the PlayerInventory component — identical to the host path.
            bool added = (bool)cachedServerTryAddItemMethod.Invoke(
                nbObj,
                new object[] { itemTypeValue, maxUses });

            if (!added)
            {
                BirdieLog.Warning("[Birdie] Item spawner (bridge server): inventory full or item invalid.");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Grant bridge server handler: " + ex.Message);
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
        mirrorCmdMsgType = mirrorAssembly.GetType("Mirror.CommandMessage");
        mirrorNcType = mirrorAssembly.GetType("Mirror.NetworkClient");
        mirrorNwpType = mirrorAssembly.GetType("Mirror.NetworkWriterPool");
        mirrorNwExtType = mirrorAssembly.GetType("Mirror.NetworkWriterExtensions");
        mirrorNrExtType = mirrorAssembly.GetType("Mirror.NetworkReaderExtensions");

        if (mirrorNbType == null || mirrorReaderType == null || mirrorConnType == null ||
            mirrorRpcType == null || mirrorRcdType == null || mirrorRctType == null ||
            mirrorCmdMsgType == null || mirrorNcType == null || mirrorNwpType == null ||
            mirrorNwExtType == null || mirrorNrExtType == null)
        {
            return false;
        }

        // ── Mirror methods ───────────────────────────────────────────────────

        // RemoteProcedureCalls.RegisterDelegate(Type, string, RemoteCallType, RemoteCallDelegate, bool) → UInt16
        mirrorRegisterDelegate = mirrorRpcType.GetMethod(
            "RegisterDelegate",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        // NetworkClient.Send<CommandMessage>(CommandMessage, int)
        MethodInfo sendOpenGeneric = mirrorNcType.GetMethod(
            "Send",
            BindingFlags.Public | BindingFlags.Static);
        if (sendOpenGeneric != null)
        {
            mirrorNcSend = sendOpenGeneric.MakeGenericMethod(mirrorCmdMsgType);
        }

        // NetworkWriterExtensions.WriteInt(NetworkWriter, int)
        mirrorWriterExtWriteInt = mirrorNwExtType.GetMethod(
            "WriteInt",
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

        // NetworkReaderExtensions.ReadInt(NetworkReader)
        mirrorReaderExtReadInt = mirrorNrExtType.GetMethod(
            "ReadInt",
            BindingFlags.Public | BindingFlags.Static);

        // ── Mirror properties / fields ───────────────────────────────────────

        mirrorNbNetId = mirrorNbType.GetProperty(
            "netId",
            BindingFlags.Public | BindingFlags.Instance);
        mirrorNbComponentIndex = mirrorNbType.GetProperty(
            "ComponentIndex",
            BindingFlags.Public | BindingFlags.Instance);

        mirrorCmdNetId = mirrorCmdMsgType.GetField("netId");
        mirrorCmdComponentIndex = mirrorCmdMsgType.GetField("componentIndex");
        mirrorCmdFunctionHash = mirrorCmdMsgType.GetField("functionHash");
        mirrorCmdPayload = mirrorCmdMsgType.GetField("payload");

        // ── Game types ───────────────────────────────────────────────────────

        Type playerInventoryType = gameAssembly.GetType("PlayerInventory");
        cachedItemTypeEnumType = gameAssembly.GetType("ItemType");

        if (playerInventoryType != null)
        {
            cachedServerTryAddItemMethod = playerInventoryType.GetMethod(
                "ServerTryAddItem",
                BindingFlags.Public | BindingFlags.Instance);
        }

        Type itemCollectionType = gameAssembly.GetType("ItemCollection");
        if (itemCollectionType != null)
        {
            cachedTryGetItemDataMethod = itemCollectionType.GetMethod(
                "TryGetItemData",
                BindingFlags.Public | BindingFlags.Instance);
        }

        Type itemDataType = gameAssembly.GetType("ItemData");
        if (itemDataType != null)
        {
            cachedItemDataMaxUsesProperty = itemDataType.GetProperty(
                "MaxUses",
                BindingFlags.Public | BindingFlags.Instance);
        }

        Type gameManagerType = gameAssembly.GetType("GameManager");
        if (gameManagerType != null)
        {
            cachedAllItemsProperty = gameManagerType.GetProperty(
                "AllItems",
                BindingFlags.Public | BindingFlags.Static);
        }

        // Sanity: enough to be useful?
        return mirrorRegisterDelegate != null &&
               mirrorNcSend != null &&
               mirrorWriterExtWriteInt != null &&
               mirrorWriterPoolGet != null &&
               mirrorWriterToArraySegment != null &&
               mirrorReaderExtReadInt != null &&
               mirrorNbNetId != null &&
               mirrorNbComponentIndex != null &&
               mirrorCmdNetId != null &&
               playerInventoryType != null &&
               cachedServerTryAddItemMethod != null &&
               cachedItemTypeEnumType != null;
    }

    private static void RegisterCommandHandler()
    {
        if (mirrorRegisterDelegate == null || mirrorRcdType == null ||
            mirrorNbType == null || mirrorReaderType == null || mirrorConnType == null ||
            mirrorRctType == null)
        {
            return;
        }

        // Resolve playerInventoryType again (needed for RegisterDelegate first arg).
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
            BirdieLog.Warning("[Birdie] Grant bridge: PlayerInventory type not found.");
            return;
        }

        // Build a DynamicMethod matching RemoteCallDelegate:
        //   void(Mirror.NetworkBehaviour, Mirror.NetworkReader, Mirror.NetworkConnectionToClient)
        // The method forwards its args (as object) to ServerHandleGrantItem.
        DynamicMethod dm = new DynamicMethod(
            "BirdieGrantItemCommandHandler",
            typeof(void),
            new Type[] { mirrorNbType, mirrorReaderType, mirrorConnType },
            typeof(BirdieGrantBridge).Module,
            skipVisibility: true);

        MethodInfo handlerMethod = typeof(BirdieGrantBridge).GetMethod(
            "ServerHandleGrantItem",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (handlerMethod == null)
        {
            BirdieLog.Warning("[Birdie] Grant bridge: ServerHandleGrantItem not found via reflection.");
            return;
        }

        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);   // Mirror.NetworkBehaviour → object (implicit ref cast)
        il.Emit(OpCodes.Ldarg_1);   // Mirror.NetworkReader    → object
        il.Emit(OpCodes.Ldarg_2);   // Mirror.NetworkConnectionToClient → object
        il.Emit(OpCodes.Call, handlerMethod);
        il.Emit(OpCodes.Ret);

        Delegate remoteCallDelegate = dm.CreateDelegate(mirrorRcdType);

        // RemoteCallType.Command enum value.
        object commandEnumValue = Enum.Parse(mirrorRctType, "Command");

        // Register and capture the returned hash.
        // Signature: UInt16 RegisterDelegate(Type, string, RemoteCallType, RemoteCallDelegate, bool)
        object result = mirrorRegisterDelegate.Invoke(
            null,
            new object[] { playerInventoryType, CommandMethodName, commandEnumValue, remoteCallDelegate, false });

        registeredCommandHash = (ushort)result;

        if (registeredCommandHash == 0)
        {
            BirdieLog.Warning("[Birdie] Grant bridge: RegisterDelegate returned hash 0 — may indicate a collision.");
        }
    }

    private static int GetItemMaxUses(int itemTypeInt)
    {
        try
        {
            if (cachedAllItemsProperty == null ||
                cachedTryGetItemDataMethod == null ||
                cachedItemDataMaxUsesProperty == null ||
                cachedItemTypeEnumType == null)
            {
                return 1;
            }

            object allItems = cachedAllItemsProperty.GetValue(null, null);
            if (allItems == null)
            {
                return 1;
            }

            object itemTypeValue = Enum.ToObject(cachedItemTypeEnumType, itemTypeInt);
            object[] args = new object[] { itemTypeValue, null };
            bool found = (bool)cachedTryGetItemDataMethod.Invoke(allItems, args);
            if (!found || args[1] == null)
            {
                return 1;
            }

            return (int)cachedItemDataMaxUsesProperty.GetValue(args[1], null);
        }
        catch
        {
            return 1;
        }
    }
}

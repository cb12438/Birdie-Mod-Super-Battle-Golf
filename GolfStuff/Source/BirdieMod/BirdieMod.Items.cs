using System;
using System.Reflection;
using UnityEngine;

public partial class BirdieMod
{
    // Coffee is the only item type with a matching ItemDispenser in every scene.
    // All other items require host access or the BirdieGrantBridge fallback.
    private const int CoffeeItemTypeInt = 1;

    // Item type int values matching the game's enum (None=0, Coffee=1 ... FreezeBomb=12).
    private static readonly int[] SpawnableItemTypeInts = new int[]
    {
         1,  // Coffee
         2,  // DuelingPistol
         3,  // ElephantGun
         4,  // Airhorn
         5,  // SpringBoots
         6,  // GolfCart
         7,  // RocketLauncher
         8,  // Landmine
         9,  // Electromagnet
        10,  // OrbitalLaser
        11,  // RocketDriver
        12,  // FreezeBomb
    };

    // Human-readable labels shown in the menu, parallel to SpawnableItemTypeInts.
    private static readonly string[] SpawnableItemNames = new string[]
    {
        "Coffee",
        "Dueling Pistol",
        "Elephant Gun",
        "Airhorn",
        "Spring Boots",
        "Golf Cart",
        "Rocket Launcher",
        "Landmine",
        "Electromagnet",
        "Orbital Laser",
        "Rocket Driver",
        "Freeze Bomb",
    };

    // Keys that select each item while the menu is open. Index matches SpawnableItemTypeInts.
    private static readonly UnityEngine.InputSystem.Key[] SpawnableItemKeys = new UnityEngine.InputSystem.Key[]
    {
        UnityEngine.InputSystem.Key.Digit1,
        UnityEngine.InputSystem.Key.Digit2,
        UnityEngine.InputSystem.Key.Digit3,
        UnityEngine.InputSystem.Key.Digit4,
        UnityEngine.InputSystem.Key.Digit5,
        UnityEngine.InputSystem.Key.Digit6,
        UnityEngine.InputSystem.Key.Digit7,
        UnityEngine.InputSystem.Key.Digit8,
        UnityEngine.InputSystem.Key.Digit9,
        UnityEngine.InputSystem.Key.Digit0,
        UnityEngine.InputSystem.Key.Minus,
        UnityEngine.InputSystem.Key.Equals,
    };

    // Labels displayed next to each item in the menu, parallel to SpawnableItemKeys.
    private static readonly string[] SpawnableItemKeyLabels = new string[]
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=",
    };

    // ── reflection cache ────────────────────────────────────────────────────

    private bool itemSpawnerReflectionInitialized;

    // ── dispenser-path reflection cache ─────────────────────────────────────

    private bool dispenserReflectionInitialized;
    private Type cachedItemDispenserType;
    private FieldInfo cachedItemDispenserItemTypeField;
    private PropertyInfo cachedItemDispenserIsEnabledProperty;
    private MethodInfo cachedItemDispenserCmdDispenseMethod;
    private Type cachedPhysicalItemType;
    private FieldInfo cachedPhysicalItemItemTypeField;
    private MethodInfo cachedCmdGiveToPlayerMethod;

    // ── dispenser-path pending-pickup state ──────────────────────────────────

    private bool dispenserPickupPending;
    private int dispenserPickupItemTypeInt;
    private Component dispenserPickupLocalInventory;
    private float dispenserPickupDeadline;
    private const float dispenserPickupTimeout = 2.5f;

    // ── crate-path reflection cache ──────────────────────────────────────────

    private bool crateReflectionInitialized;
    // ── crate teleport state ─────────────────────────────────────────────────

    private bool pendingCrateTeleport;
    private float pendingCrateTeleportExpiry;
    private const float crateTeleportConfirmWindow = 3f;
    private bool crateReturnPending;
    private Vector3 crateReturnPosition;
    private Type cachedItemSpawnerType;
    private FieldInfo cachedItemSpawnerSettingsField;        // [SerializeField] private ItemSpawnerSettings settings
    private PropertyInfo cachedItemSpawnerNetHasItemBoxProp; // public NetworkhasItemBox { get; set; }
    private Type cachedItemSpawnerSettingsType;
    private MethodInfo cachedGetRandomItemForMethod;         // ItemSpawnerSettings.GetRandomItemFor(PlayerInfo) → ItemType
    private PropertyInfo cachedInventoryPlayerInfoProp;      // PlayerInventory.PlayerInfo → PlayerInfo

    // Mirror.NetworkServer.active — determines host vs. pure-client at call time.
    private PropertyInfo cachedNetworkServerActiveProperty;
    private bool cachedNetworkServerPropertyInit;

    // GameManager.LocalPlayerInventory  (static → PlayerInventory component)
    private PropertyInfo cachedLocalPlayerInventoryProperty;

    // GameManager.AllItems  (static → ItemCollection)
    private PropertyInfo cachedAllItemsProperty;

    // ItemCollection.TryGetItemData(ItemType, out ItemData)
    private MethodInfo cachedTryGetItemDataMethod;

    // ItemData.MaxUses  (int property)
    private PropertyInfo cachedItemDataMaxUsesProperty;

    // ItemType enum type
    private Type cachedItemTypeEnumType;

    // ── host-path cache ──────────────────────────────────────────────────────

    // PlayerInventory.ServerTryAddItem(ItemType, int)
    // Identical to the method called by both ItemSpawner and PhysicalItem server handlers.
    private MethodInfo cachedServerTryAddItemMethod;

    // ── initialization ───────────────────────────────────────────────────────

    private void InitializeItemSpawnerReflection()
    {
        if (itemSpawnerReflectionInitialized)
        {
            return;
        }

        itemSpawnerReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                // ── Mirror.NetworkServer ─────────────────────────────────────
                if (cachedNetworkServerActiveProperty == null)
                {
                    Type networkServerType = assembly.GetType("Mirror.NetworkServer");
                    if (networkServerType != null)
                    {
                        cachedNetworkServerActiveProperty = networkServerType.GetProperty(
                            "active",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                }

                // ── GameManager statics ──────────────────────────────────────
                if (cachedLocalPlayerInventoryProperty == null || cachedAllItemsProperty == null)
                {
                    Type gameManagerType = assembly.GetType("GameManager");
                    if (gameManagerType != null)
                    {
                        if (cachedLocalPlayerInventoryProperty == null)
                        {
                            cachedLocalPlayerInventoryProperty = gameManagerType.GetProperty(
                                "LocalPlayerInventory",
                                BindingFlags.Public | BindingFlags.Static);
                        }

                        if (cachedAllItemsProperty == null)
                        {
                            cachedAllItemsProperty = gameManagerType.GetProperty(
                                "AllItems",
                                BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                }

                // ── ItemType enum ────────────────────────────────────────────
                if (cachedItemTypeEnumType == null)
                {
                    cachedItemTypeEnumType = assembly.GetType("ItemType");
                }

                // ── ItemCollection.TryGetItemData ────────────────────────────
                if (cachedTryGetItemDataMethod == null)
                {
                    Type itemCollectionType = assembly.GetType("ItemCollection");
                    if (itemCollectionType != null)
                    {
                        cachedTryGetItemDataMethod = itemCollectionType.GetMethod(
                            "TryGetItemData",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                // ── ItemData.MaxUses ─────────────────────────────────────────
                if (cachedItemDataMaxUsesProperty == null)
                {
                    Type itemDataType = assembly.GetType("ItemData");
                    if (itemDataType != null)
                    {
                        cachedItemDataMaxUsesProperty = itemDataType.GetProperty(
                            "MaxUses",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                // ── PlayerInventory.ServerTryAddItem (host path) ─────────────
                if (cachedServerTryAddItemMethod == null)
                {
                    Type inventoryType = assembly.GetType("PlayerInventory");
                    if (inventoryType != null)
                    {
                        cachedServerTryAddItemMethod = inventoryType.GetMethod(
                            "ServerTryAddItem",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
        }
        catch
        {
        }
    }

    // ── dispenser-path initialization ────────────────────────────────────────

    private void InitializeDispenserReflection()
    {
        if (dispenserReflectionInitialized)
        {
            return;
        }

        dispenserReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                if (cachedItemDispenserType == null)
                {
                    Type t = assembly.GetType("ItemDispenser");
                    if (t != null)
                    {
                        cachedItemDispenserType = t;
                        cachedItemDispenserItemTypeField = t.GetField(
                            "itemType",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        cachedItemDispenserIsEnabledProperty = t.GetProperty(
                            "IsInteractionEnabled",
                            BindingFlags.Public | BindingFlags.Instance);
                        cachedItemDispenserCmdDispenseMethod = t.GetMethod(
                            "CmdDispenseItemFor",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (cachedPhysicalItemType == null)
                {
                    Type t = assembly.GetType("PhysicalItem");
                    if (t != null)
                    {
                        cachedPhysicalItemType = t;
                        cachedPhysicalItemItemTypeField = t.GetField(
                            "itemType",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        cachedCmdGiveToPlayerMethod = t.GetMethod(
                            "CmdGiveToPlayer",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (cachedItemDispenserType != null && cachedPhysicalItemType != null)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    // Returns the first ready ItemDispenser in the scene whose itemType matches,
    // or null if none exists or none is currently available (in cooldown).
    private Component FindMatchingDispenser(int itemTypeInt)
    {
        if (cachedItemDispenserType == null || cachedItemDispenserItemTypeField == null)
        {
            return null;
        }

        try
        {
            UnityEngine.Object[] dispensers = UnityEngine.Object.FindObjectsOfType(cachedItemDispenserType);
            for (int i = 0; i < dispensers.Length; i++)
            {
                Component dispenser = dispensers[i] as Component;
                if (dispenser == null)
                {
                    continue;
                }

                object typeField = cachedItemDispenserItemTypeField.GetValue(dispenser);
                if (typeField == null || (int)typeField != itemTypeInt)
                {
                    continue;
                }

                // Skip dispensers that are in cooldown (isInteractionEnabled == false).
                if (cachedItemDispenserIsEnabledProperty != null)
                {
                    object enabled = cachedItemDispenserIsEnabledProperty.GetValue(dispenser, null);
                    if (enabled != null && !(bool)enabled)
                    {
                        continue;
                    }
                }

                return dispenser;
            }
        }
        catch
        {
        }

        return null;
    }

    // Returns the first PhysicalItem in the scene whose itemType matches, or null.
    private Component FindPhysicalItemOfType(int itemTypeInt)
    {
        if (cachedPhysicalItemType == null || cachedPhysicalItemItemTypeField == null)
        {
            return null;
        }

        try
        {
            UnityEngine.Object[] items = UnityEngine.Object.FindObjectsOfType(cachedPhysicalItemType);
            for (int i = 0; i < items.Length; i++)
            {
                Component item = items[i] as Component;
                if (item == null)
                {
                    continue;
                }

                object typeField = cachedPhysicalItemItemTypeField.GetValue(item);
                if (typeField == null)
                {
                    continue;
                }

                if ((int)typeField == itemTypeInt)
                {
                    return item;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    // Called from OnUpdate. Polls for the PhysicalItem spawned by the dispenser
    // command and collects it once it appears in the scene.
    internal void PollDispenserPickup()
    {
        if (!dispenserPickupPending)
        {
            return;
        }

        if (Time.time > dispenserPickupDeadline)
        {
            BirdieLog.Warning("[Birdie] Item spawner (dispenser): timed out waiting for spawned item.");
            dispenserPickupPending = false;
            dispenserPickupLocalInventory = null;
            return;
        }

        Component physicalItem = FindPhysicalItemOfType(dispenserPickupItemTypeInt);
        if (physicalItem == null)
        {
            return;
        }

        dispenserPickupPending = false;

        if (cachedCmdGiveToPlayerMethod == null || dispenserPickupLocalInventory == null)
        {
            BirdieLog.Warning("[Birdie] Item spawner (dispenser): CmdGiveToPlayer reflection not ready.");
            dispenserPickupLocalInventory = null;
            return;
        }

        try
        {
            cachedCmdGiveToPlayerMethod.Invoke(physicalItem, new object[] { dispenserPickupLocalInventory, null });
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Item spawner (dispenser pickup): " + ex.Message);
        }

        dispenserPickupLocalInventory = null;
    }

    // ── crate pickup ─────────────────────────────────────────────────────────

    // Initializes reflection for ItemSpawner, ItemSpawnerSettings, and the
    // PlayerInventory.PlayerInfo property used by GetRandomItemFor.
    private void InitializeCrateReflection()
    {
        if (crateReflectionInitialized)
        {
            return;
        }

        crateReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                if (cachedItemSpawnerType == null)
                {
                    Type t = assembly.GetType("ItemSpawner");
                    if (t != null)
                    {
                        cachedItemSpawnerType = t;
                        cachedItemSpawnerSettingsField = t.GetField(
                            "settings",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        cachedItemSpawnerNetHasItemBoxProp = t.GetProperty(
                            "NetworkhasItemBox",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (cachedItemSpawnerSettingsType == null)
                {
                    Type t = assembly.GetType("ItemSpawnerSettings");
                    if (t != null)
                    {
                        cachedItemSpawnerSettingsType = t;
                        cachedGetRandomItemForMethod = t.GetMethod(
                            "GetRandomItemFor",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (cachedInventoryPlayerInfoProp == null)
                {
                    Type t = assembly.GetType("PlayerInventory");
                    if (t != null)
                    {
                        cachedInventoryPlayerInfoProp = t.GetProperty(
                            "PlayerInfo",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (cachedItemSpawnerType != null &&
                    cachedItemSpawnerSettingsType != null &&
                    cachedInventoryPlayerInfoProp != null)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    // Entry point called from the random_item_key handler.
    // Finds the nearest available item crate and collects it.
    internal void TryCollectFromCrate()
    {
        if (!itemSpawnerReflectionInitialized)
        {
            InitializeItemSpawnerReflection();
        }

        if (!crateReflectionInitialized)
        {
            InitializeCrateReflection();
        }

        if (cachedLocalPlayerInventoryProperty == null)
        {
            return;
        }

        Component localInventory = cachedLocalPlayerInventoryProperty.GetValue(null, null) as Component;
        if (localInventory == null)
        {
            BirdieLog.Warning("[Birdie] Crate: local player inventory not found.");
            return;
        }

        if (IsNetworkServerActive())
        {
            CollectFromCrateAsHost(localInventory);
        }
        else
        {
            CollectFromCrateAsClient(localInventory);
        }
    }

    // Host path: find the nearest ItemSpawner with an available box, get a
    // weighted-random item from its configured pool, call ServerTryAddItem,
    // then set NetworkhasItemBox = false to consume the crate.
    private void CollectFromCrateAsHost(Component localInventory)
    {
        if (cachedItemSpawnerType == null)
        {
            BirdieLog.Warning("[Birdie] Crate: ItemSpawner type not found.");
            return;
        }

        // Find the nearest crate that currently has an item box.
        UnityEngine.Object[] spawners = UnityEngine.Object.FindObjectsOfType(cachedItemSpawnerType);
        Component nearestSpawner = null;
        float nearestDistance = float.MaxValue;
        Vector3 originPosition = playerMovement != null
            ? playerMovement.transform.position
            : Vector3.zero;

        for (int i = 0; i < spawners.Length; i++)
        {
            Component s = spawners[i] as Component;
            if (s == null)
            {
                continue;
            }

            // Check NetworkhasItemBox.
            if (cachedItemSpawnerNetHasItemBoxProp != null)
            {
                object hasBox = cachedItemSpawnerNetHasItemBoxProp.GetValue(s, null);
                if (hasBox != null && !(bool)hasBox)
                {
                    continue;
                }
            }

            float dist = Vector3.Distance(s.transform.position, originPosition);
            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestSpawner = s;
            }
        }

        if (nearestSpawner == null)
        {
            BirdieLog.Warning("[Birdie] Crate: no item crate with an available box found.");
            return;
        }

        // Get the random item type from the crate's configured pool.
        int itemTypeInt = GetCrateRandomItemType(nearestSpawner, localInventory);
        if (itemTypeInt <= 0)
        {
            // Fallback: pick a random item from the mod's spawnable list.
            itemTypeInt = SpawnableItemTypeInts[UnityEngine.Random.Range(0, SpawnableItemTypeInts.Length)];
        }

        if (cachedServerTryAddItemMethod == null || cachedItemTypeEnumType == null)
        {
            BirdieLog.Warning("[Birdie] Crate: ServerTryAddItem reflection not ready.");
            return;
        }

        object itemTypeValue = Enum.ToObject(cachedItemTypeEnumType, itemTypeInt);
        int maxUses = GetItemMaxUses(itemTypeInt);
        if (maxUses <= 0)
        {
            maxUses = 1;
        }

        try
        {
            bool added = (bool)cachedServerTryAddItemMethod.Invoke(
                localInventory,
                new object[] { itemTypeValue, maxUses });

            if (!added)
            {
                BirdieLog.Warning("[Birdie] Crate: inventory full.");
                return;
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Crate (host grant): " + ex.Message);
            return;
        }

        // Consume the crate — triggers respawn timer and visual update on all clients.
        if (cachedItemSpawnerNetHasItemBoxProp != null)
        {
            try
            {
                cachedItemSpawnerNetHasItemBoxProp.SetValue(nearestSpawner, false);
            }
            catch
            {
            }
        }
    }

    // Calls ItemSpawnerSettings.GetRandomItemFor(playerInfo) on the spawner's
    // configured settings object. Returns 0 on any failure.
    private int GetCrateRandomItemType(Component spawner, Component localInventory)
    {
        try
        {
            if (cachedItemSpawnerSettingsField == null ||
                cachedGetRandomItemForMethod == null ||
                cachedInventoryPlayerInfoProp == null)
            {
                return 0;
            }

            object settings = cachedItemSpawnerSettingsField.GetValue(spawner);
            if (settings == null)
            {
                return 0;
            }

            object playerInfo = cachedInventoryPlayerInfoProp.GetValue(localInventory, null);
            if (playerInfo == null)
            {
                return 0;
            }

            object itemTypeValue = cachedGetRandomItemForMethod.Invoke(settings, new object[] { playerInfo });
            if (itemTypeValue == null)
            {
                return 0;
            }

            return Convert.ToInt32(itemTypeValue);
        }
        catch
        {
            return 0;
        }
    }

    // Client path:
    //   Tier 1 — BirdieGrantBridge if both sides run the mod (random item, no teleport).
    //   Tier 2 — Teleport approach: first keypress shows a confirmation warning;
    //            second keypress within 3 s teleports the player to the nearest
    //            available crate for one frame, then back. Server's OnTriggerEnter
    //            may or may not fire — entirely best-effort.
    //
    // ItemSpawner has no [Command]: its SphereCollider trigger is server-side only
    // (added in OnStartServer). The teleport works by moving the client's transform
    // so Mirror syncs the new position to the server, potentially triggering the
    // server-side overlap on the next physics tick.
    private void CollectFromCrateAsClient(Component localInventory)
    {
        // Tier 1 — BirdieGrantBridge (no teleport needed).
        if (BirdieGrantBridge.IsReady())
        {
            int itemTypeInt = SpawnableItemTypeInts[
                UnityEngine.Random.Range(0, SpawnableItemTypeInts.Length)];
            BirdieGrantBridge.RequestGrant(localInventory, itemTypeInt);
            return;
        }

        // Tier 2 — teleport approach.
        if (!pendingCrateTeleport)
        {
            // First keypress: arm the confirmation window.
            pendingCrateTeleport = true;
            pendingCrateTeleportExpiry = Time.time + crateTeleportConfirmWindow;
            MarkHudDirty();
            return;
        }

        // Second keypress within the window: execute the teleport.
        pendingCrateTeleport = false;
        MarkHudDirty();

        if (cachedItemSpawnerType == null || cachedItemSpawnerNetHasItemBoxProp == null)
        {
            BirdieLog.Warning("[Birdie] Crate teleport: ItemSpawner reflection not ready.");
            return;
        }

        if (playerMovement == null)
        {
            BirdieLog.Warning("[Birdie] Crate teleport: player not found.");
            return;
        }

        // Find the nearest available crate.
        UnityEngine.Object[] spawners = UnityEngine.Object.FindObjectsOfType(cachedItemSpawnerType);
        Component nearestSpawner = null;
        float nearestDistance = float.MaxValue;
        Vector3 originPosition = playerMovement.transform.position;

        for (int i = 0; i < spawners.Length; i++)
        {
            Component s = spawners[i] as Component;
            if (s == null)
            {
                continue;
            }

            object hasBox = cachedItemSpawnerNetHasItemBoxProp.GetValue(s, null);
            if (hasBox != null && !(bool)hasBox)
            {
                continue;
            }

            float dist = Vector3.Distance(s.transform.position, originPosition);
            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestSpawner = s;
            }
        }

        if (nearestSpawner == null)
        {
            BirdieLog.Warning("[Birdie] Crate teleport: no active crates in scene.");
            return;
        }

        // Teleport to the crate for one frame. PollCrateReturn will
        // move the player back on the very next OnUpdate call.
        crateReturnPosition = playerMovement.transform.position;
        playerMovement.transform.position = nearestSpawner.transform.position;
        crateReturnPending = true;
    }

    // Called every OnUpdate. Handles two things:
    //   1. Expires the confirmation window if the second keypress never came.
    //   2. Teleports the player back after a one-frame stay at the crate.
    internal void PollCrateReturn()
    {
        if (pendingCrateTeleport && Time.time > pendingCrateTeleportExpiry)
        {
            pendingCrateTeleport = false;
            MarkHudDirty();
        }

        if (crateReturnPending)
        {
            crateReturnPending = false;
            if (playerMovement != null)
            {
                playerMovement.transform.position = crateReturnPosition;
            }
        }
    }

    // ── public spawn entry point ─────────────────────────────────────────────

    private void SpawnItemClientSide(int itemTypeInt)
    {
        if (!itemSpawnerReflectionInitialized)
        {
            InitializeItemSpawnerReflection();
        }

        if (IsNetworkServerActive())
        {
            // ── HOST PATH ────────────────────────────────────────────────────
            // ServerTryAddItem directly — works for all item types.
            SpawnItemAsHost(itemTypeInt);
        }
        else
        {
            // ── CLIENT PATH ──────────────────────────────────────────────────
            // Coffee: ItemDispenser two-step (no mod required on host).
            // All items: BirdieGrantBridge if both sides run this mod.
            // Otherwise: host only.
            SpawnItemAsClient(itemTypeInt);
        }
    }

    // ── host path ────────────────────────────────────────────────────────────

    private void SpawnItemAsHost(int itemTypeInt)
    {
        try
        {
            if (cachedLocalPlayerInventoryProperty == null ||
                cachedServerTryAddItemMethod == null ||
                cachedItemTypeEnumType == null)
            {
                BirdieLog.Warning("[Birdie] Item spawner: host reflection cache incomplete.");
                return;
            }

            Component inventory = cachedLocalPlayerInventoryProperty.GetValue(null, null) as Component;
            if (inventory == null)
            {
                BirdieLog.Warning("[Birdie] Item spawner: local player inventory not found.");
                return;
            }

            object itemTypeValue = Enum.ToObject(cachedItemTypeEnumType, itemTypeInt);
            int maxUses = GetItemMaxUses(itemTypeInt);
            if (maxUses <= 0)
            {
                maxUses = 1;
            }

            // ServerTryAddItem handles HasSpaceForItem and slots[] assignment
            // internally, using the server-authoritative SyncList path.
            bool added = (bool)cachedServerTryAddItemMethod.Invoke(
                inventory,
                new object[] { itemTypeValue, maxUses });

            if (!added)
            {
                BirdieLog.Warning("[Birdie] Item spawner: inventory full or item invalid.");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Item spawner (host): " + ex.Message);
        }
    }

    // ── client path ──────────────────────────────────────────────────────────

    private void SpawnItemAsClient(int itemTypeInt)
    {
        try
        {
            if (cachedLocalPlayerInventoryProperty == null)
            {
                BirdieLog.Warning("[Birdie] Item spawner: LocalPlayerInventory reflection not ready.");
                return;
            }

            Component localInventory = cachedLocalPlayerInventoryProperty.GetValue(null, null) as Component;
            if (localInventory == null)
            {
                BirdieLog.Warning("[Birdie] Item spawner: local player inventory not found.");
                return;
            }

            // Coffee — ItemDispenser two-step (no mod required on host).
            // Only coffee has a guaranteed dispenser in every scene.
            if (itemTypeInt == CoffeeItemTypeInt)
            {
                if (!dispenserReflectionInitialized)
                {
                    InitializeDispenserReflection();
                }

                if (cachedItemDispenserCmdDispenseMethod != null)
                {
                    Component dispenser = FindMatchingDispenser(CoffeeItemTypeInt);
                    if (dispenser != null)
                    {
                        try
                        {
                            cachedItemDispenserCmdDispenseMethod.Invoke(dispenser, null);
                            dispenserPickupPending = true;
                            dispenserPickupItemTypeInt = CoffeeItemTypeInt;
                            dispenserPickupLocalInventory = localInventory;
                            dispenserPickupDeadline = Time.time + dispenserPickupTimeout;
                            return;
                        }
                        catch (Exception ex)
                        {
                            BirdieLog.Warning("[Birdie] Item spawner (dispenser): " + ex.Message);
                            // Fall through to bridge.
                        }
                    }
                }
            }

            // All items — BirdieGrantBridge if both sides run this mod.
            if (BirdieGrantBridge.IsReady())
            {
                BirdieGrantBridge.RequestGrant(localInventory, itemTypeInt);
                return;
            }

            // No client path available.
            BirdieLog.Warning("[Birdie] Item spawner: no client grant path available — host only.");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Item spawner (client): " + ex.Message);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    // Returns true when Mirror's NetworkServer is running (i.e. we are the host).
    private bool IsNetworkServerActive()
    {
        if (!cachedNetworkServerPropertyInit)
        {
            cachedNetworkServerPropertyInit = true;
            if (cachedNetworkServerActiveProperty == null)
            {
                // Property lookup may not have fired yet if reflection init ran
                // before Mirror had loaded — retry once.
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type t = assemblies[i].GetType("Mirror.NetworkServer");
                    if (t != null)
                    {
                        cachedNetworkServerActiveProperty = t.GetProperty(
                            "active", BindingFlags.Public | BindingFlags.Static);
                        break;
                    }
                }
            }
        }

        if (cachedNetworkServerActiveProperty == null)
        {
            return false;
        }

        try
        {
            return (bool)cachedNetworkServerActiveProperty.GetValue(null, null);
        }
        catch
        {
            return false;
        }
    }

    // Returns MaxUses for the given ItemType int from the game's ItemCollection,
    // falling back to 1 if reflection fails or the item is not found.
    private int GetItemMaxUses(int itemTypeInt)
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

            // TryGetItemData takes an 'out ItemData' second parameter; reflection
            // returns the out value in the same args slot after the call.
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

    // Converts an ItemType int value to the display name used in log messages.
    private string ItemIndexToName(int itemTypeInt)
    {
        int arrayIndex = itemTypeInt - 1;
        if (arrayIndex >= 0 && arrayIndex < SpawnableItemNames.Length)
        {
            return SpawnableItemNames[arrayIndex];
        }

        return "item #" + itemTypeInt;
    }
}

using System;
using System.Reflection;
using UnityEngine;

public partial class BirdieMod
{
    // ==================== NO WIND ====================
    // WindSettings lives on WindManager.windSettings (instance field), not on GameManager.

    private void InitializeWindExtrasReflection()
    {
        if (windExtrasReflectionInitialized)
        {
            return;
        }

        windExtrasReflectionInitialized = true;

        try
        {
            // Find WindManager type
            Type wmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                wmType = asm.GetType("WindManager");
                if (wmType != null)
                {
                    break;
                }
            }

            if (wmType == null)
            {
                BirdieLog.Warning("[Birdie] WindManager type not found");
                return;
            }

            // Find the WindManager instance in the scene
            UnityEngine.Object wmObj = UnityEngine.Object.FindObjectOfType(wmType);
            if (wmObj == null)
            {
                BirdieLog.Warning("[Birdie] WindManager not found in scene");
                return;
            }

            // Get the windSettings field on WindManager
            FieldInfo wsField = wmType.GetField(
                "windSettings",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (wsField == null)
            {
                BirdieLog.Warning("[Birdie] WindManager.windSettings field not found");
                return;
            }

            object ws = wsField.GetValue(wmObj);
            if (ws == null)
            {
                BirdieLog.Warning("[Birdie] WindManager.windSettings value is null");
                return;
            }

            // Get forceScale field on WindSettings
            cachedWindForceScaleField = ws.GetType().GetField(
                "forceScale",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (cachedWindForceScaleField != null && cachedWindForceScaleField.FieldType == typeof(float))
            {
                savedWindForceScale = (float)cachedWindForceScaleField.GetValue(ws);
                cachedWindSettingsInstance = ws;
            }
            else
            {
                BirdieLog.Warning("[Birdie] WindSettings.forceScale field not found");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Wind reflection error: " + ex.Message);
        }
    }

    private void ToggleNoWind()
    {
        if (!IsFeatureAllowed(3)) return;
        noWindEnabled = !noWindEnabled;
        InitializeWindExtrasReflection();

        BirdieLog.Msg(string.Format("[Birdie] No Wind {0} | ws={1} field={2}",
            noWindEnabled ? "ON" : "OFF",
            cachedWindSettingsInstance != null ? "OK" : "NULL",
            cachedWindForceScaleField   != null ? cachedWindForceScaleField.Name : "NULL"));

        if (noWindEnabled)
        {
            ApplyNoWindState();
        }
        else
        {
            RestoreWindState();
        }

        MarkHudDirty();
    }

    private void ApplyNoWindState()
    {
        if (cachedWindSettingsInstance == null || cachedWindForceScaleField == null)
        {
            return;
        }

        try
        {
            cachedWindForceScaleField.SetValue(cachedWindSettingsInstance, 0f);
        }
        catch
        {
        }
    }

    private void RestoreWindState()
    {
        if (cachedWindSettingsInstance == null || cachedWindForceScaleField == null)
        {
            return;
        }

        try
        {
            cachedWindForceScaleField.SetValue(cachedWindSettingsInstance, savedWindForceScale);
        }
        catch
        {
        }
    }

    // ==================== PERFECT SHOT ====================

    private void TogglePerfectShot()
    {
        if (!IsFeatureAllowed(4)) return;
        perfectShotEnabled = !perfectShotEnabled;
        BirdieLog.Msg(perfectShotEnabled ? "[Birdie] Perfect Shot ON" : "[Birdie] Perfect Shot OFF");
        MarkHudDirty();
    }

    // Called from OnLateUpdate while left mouse is held.
    // Forces the swing power backing field to the perfect zone (0.999).
    private static bool _perfectShotLogged;
    private void ApplyPerfectShotForcing()
    {
        if (!perfectShotEnabled || playerGolfer == null || !isLeftMousePressed)
        {
            return;
        }

        try
        {
            if (swingNormalizedPowerBackingField == null)
            {
                // Try canonical auto-property backing field name first.
                swingNormalizedPowerBackingField = playerGolfer.GetType().GetField(
                    "<SwingNormalizedPower>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                // Fallback: scan for any float field with "swing" + "power" in name.
                if (swingNormalizedPowerBackingField == null)
                {
                    foreach (FieldInfo f in playerGolfer.GetType().GetFields(
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (f.FieldType == typeof(float))
                        {
                            string lower = f.Name.ToLower();
                            if (lower.Contains("swing") && lower.Contains("power"))
                            {
                                swingNormalizedPowerBackingField = f;
                                break;
                            }
                        }
                    }
                }

                if (!_perfectShotLogged)
                {
                    _perfectShotLogged = true;
                    BirdieLog.Msg("[Birdie] Perfect Shot backing field: " +
                        (swingNormalizedPowerBackingField != null
                            ? swingNormalizedPowerBackingField.Name
                            : "NOT FOUND") +
                        " | type=" + playerGolfer.GetType().Name);

                    // Log all float fields for diagnosis.
                    foreach (FieldInfo f in playerGolfer.GetType().GetFields(
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (f.FieldType == typeof(float))
                        {
                            BirdieLog.Msg("[Birdie] float field: " + f.Name);
                        }
                    }
                }
            }

            if (swingNormalizedPowerBackingField != null)
            {
                swingNormalizedPowerBackingField.SetValue(playerGolfer, 0.999f);
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Perfect Shot error: " + ex.Message);
        }
    }

    // ==================== NO AIR DRAG ====================

    private void InitializeAirDragExtrasReflection()
    {
        if (airDragExtrasReflectionInitialized)
        {
            return;
        }

        airDragExtrasReflectionInitialized = true;

        try
        {
            object ballSettings = GetGolfBallSettingsObject();
            if (ballSettings == null)
            {
                BirdieLog.Warning("[Birdie] GolfBallSettings is null");
                return;
            }

            Type t = ballSettings.GetType();

            // GolfBallSettings uses auto-property backing fields: <LinearAirDragFactor>k__BackingField
            cachedLinearAirDragField = t.GetField(
                "<LinearAirDragFactor>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (cachedLinearAirDragField != null && cachedLinearAirDragField.FieldType == typeof(float))
            {
                savedLinearAirDrag = (float)cachedLinearAirDragField.GetValue(ballSettings);
            }
            else
            {
                BirdieLog.Warning("[Birdie] <LinearAirDragFactor>k__BackingField not found on " + t.Name);
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] AirDrag reflection error: " + ex.Message);
        }
    }

    private void ToggleNoAirDrag()
    {
        if (!IsFeatureAllowed(5)) return;
        noAirDragEnabled = !noAirDragEnabled;
        InitializeAirDragExtrasReflection();

        if (noAirDragEnabled)
        {
            ApplyNoAirDragState();
            BirdieLog.Msg(string.Format("[Birdie] No Air Drag ON (saved={0:F5})", savedLinearAirDrag));
        }
        else
        {
            RestoreAirDragState();
            BirdieLog.Msg("[Birdie] No Air Drag OFF");
        }

        MarkHudDirty();
    }

    private void ApplyNoAirDragState()
    {
        if (cachedLinearAirDragField == null)
        {
            return;
        }

        try
        {
            object ballSettings = GetGolfBallSettingsObject();
            if (ballSettings == null)
            {
                return;
            }

            cachedLinearAirDragField.SetValue(ballSettings, 0.00001f);
        }
        catch
        {
        }
    }

    private void RestoreAirDragState()
    {
        if (cachedLinearAirDragField == null)
        {
            return;
        }

        try
        {
            object ballSettings = GetGolfBallSettingsObject();
            if (ballSettings == null)
            {
                return;
            }

            cachedLinearAirDragField.SetValue(ballSettings, savedLinearAirDrag);
        }
        catch
        {
        }
    }

    // ==================== SPEED MULTIPLIER ====================

    private void InitializeMoveSpeedExtrasReflection()
    {
        if (moveSpeedExtrasReflectionInitialized)
        {
            return;
        }

        moveSpeedExtrasReflectionInitialized = true;

        try
        {
            Type gmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                gmType = asm.GetType("GameManager");
                if (gmType != null)
                {
                    break;
                }
            }

            if (gmType == null)
            {
                BirdieLog.Warning("[Birdie] GameManager type not found");
                return;
            }

            cachedPlayerMovSettingsProperty = gmType.GetProperty(
                "PlayerMovementSettings",
                BindingFlags.Public | BindingFlags.Static);

            if (cachedPlayerMovSettingsProperty == null)
            {
                BirdieLog.Warning("[Birdie] GameManager.PlayerMovementSettings property not found");
                return;
            }

            object pms = cachedPlayerMovSettingsProperty.GetValue(null, null);
            if (pms == null)
            {
                BirdieLog.Warning("[Birdie] PlayerMovementSettings value is null");
                return;
            }

            Type pmsType = pms.GetType();

            // All fields are auto-property backing fields: <FieldName>k__BackingField
            cachedDefaultMoveSpeedField = pmsType.GetField(
                "<DefaultMoveSpeed>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (cachedDefaultMoveSpeedField != null && cachedDefaultMoveSpeedField.FieldType == typeof(float))
            {
                savedDefaultMoveSpeed = (float)cachedDefaultMoveSpeedField.GetValue(pms);
            }
            else
            {
                BirdieLog.Warning("[Birdie] <DefaultMoveSpeed>k__BackingField not found on " + pmsType.Name);
            }

        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] MoveSpeed reflection error: " + ex.Message);
        }
    }

    private void ToggleSpeedMultiplier()
    {
        if (!IsFeatureAllowed(6)) return;
        speedMultiplierEnabled = !speedMultiplierEnabled;
        InitializeMoveSpeedExtrasReflection();

        if (speedMultiplierEnabled)
        {
            ApplySpeedMultiplierState();
            BirdieLog.Msg(string.Format("[Birdie] Speed Multiplier ON (x{0:F1}, base={1:F2})", speedMultiplierFactor, savedDefaultMoveSpeed));
        }
        else
        {
            RestoreSpeedState();
            BirdieLog.Msg("[Birdie] Speed Multiplier OFF");
        }

        MarkHudDirty();
    }

    private void ApplySpeedMultiplierState()
    {
        if (cachedPlayerMovSettingsProperty == null || cachedDefaultMoveSpeedField == null)
        {
            return;
        }

        try
        {
            object pms = cachedPlayerMovSettingsProperty.GetValue(null, null);
            if (pms == null)
            {
                return;
            }

            cachedDefaultMoveSpeedField.SetValue(pms, savedDefaultMoveSpeed * speedMultiplierFactor);
        }
        catch
        {
        }
    }

    private void RestoreSpeedState()
    {
        if (cachedPlayerMovSettingsProperty == null || cachedDefaultMoveSpeedField == null)
        {
            return;
        }

        try
        {
            object pms = cachedPlayerMovSettingsProperty.GetValue(null, null);
            if (pms == null)
            {
                return;
            }

            cachedDefaultMoveSpeedField.SetValue(pms, savedDefaultMoveSpeed);
        }
        catch
        {
        }
    }

    // ==================== INFINITE AMMO ====================
    // Targets PlayerInventory.localPlayerSlotOverrides (client-side ammo tracking dict).
    // Every LateUpdate, for each slot that holds a non-None item, we set the override
    // to {itemType, 99} so the game always sees 99 uses left.  We also maintain a backup
    // dict so we can restore an item even after the server empties the SyncList slot.

    private void InitializeAmmoInventoryReflection()
    {
        if (ammoInventoryReflectionReady)
        {
            return;
        }

        ammoInventoryReflectionReady = true; // prevent re-running

        try
        {
            Type gmType = null;
            Type piType = null;
            Type isType = null;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (gmType == null) gmType = asm.GetType("GameManager");
                if (piType == null) piType = asm.GetType("PlayerInventory");
                if (isType == null) isType = asm.GetType("InventorySlot");
                if (gmType != null && piType != null && isType != null)
                {
                    break;
                }
            }

            if (gmType == null || piType == null || isType == null)
            {
                BirdieLog.Warning("[Birdie] Ammo: required types not found (GM=" +
                    (gmType != null ? "ok" : "NULL") + " PI=" +
                    (piType != null ? "ok" : "NULL") + " IS=" +
                    (isType != null ? "ok" : "NULL") + ")");
                return;
            }

            cachedLocalPlayerInventoryProperty = gmType.GetProperty(
                "LocalPlayerInventory",
                BindingFlags.Public | BindingFlags.Static);

            cachedInventorySlotsSyncListField = piType.GetField(
                "slots",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            cachedInventoryOverridesDictField = piType.GetField(
                "localPlayerSlotOverrides",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            cachedInvSlotItemTypeField = isType.GetField(
                "itemType",
                BindingFlags.Public | BindingFlags.Instance);

            Type itemTypeEnumType = cachedInvSlotItemTypeField?.FieldType;
            if (itemTypeEnumType != null)
            {
                cachedInvSlotConstructor = isType.GetConstructor(
                    new Type[] { itemTypeEnumType, typeof(int) });
            }

            bool ok = cachedLocalPlayerInventoryProperty != null
                && cachedInventorySlotsSyncListField != null
                && cachedInventoryOverridesDictField != null
                && cachedInvSlotItemTypeField != null
                && cachedInvSlotConstructor != null;

            if (ok)
            {
                BirdieLog.Msg("[Birdie] Ammo inventory reflection ready");
            }
            else
            {
                BirdieLog.Warning("[Birdie] Ammo: one or more fields missing — " +
                    "inv=" + (cachedLocalPlayerInventoryProperty != null ? "ok" : "NULL") +
                    " slots=" + (cachedInventorySlotsSyncListField != null ? "ok" : "NULL") +
                    " overrides=" + (cachedInventoryOverridesDictField != null ? "ok" : "NULL") +
                    " itemType=" + (cachedInvSlotItemTypeField != null ? "ok" : "NULL") +
                    " ctor=" + (cachedInvSlotConstructor != null ? "ok" : "NULL"));
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Ammo inventory reflection error: " + ex.Message);
        }
    }

    private void ToggleInfiniteAmmo()
    {
        if (!IsFeatureAllowed(7)) return;
        infiniteAmmoEnabled = !infiniteAmmoEnabled;

        if (!infiniteAmmoEnabled)
        {
            ammoItemTypeBackup.Clear();
            BirdieInfiniteAmmoBridge.LocalPlayerInventory = null;
        }

        BirdieInfiniteAmmoBridge.EnsurePatchApplied();
        BirdieInfiniteAmmoBridge.IsActive = infiniteAmmoEnabled;

        BirdieLog.Msg(infiniteAmmoEnabled ? "[Birdie] Infinite Item Usage ON" : "[Birdie] Infinite Item Usage OFF");
        MarkHudDirty();
    }

    // Called every LateUpdate. Keeps BirdieInfiniteAmmoBridge.LocalPlayerInventory
    // current so the Harmony prefix can identify the local player's inventory and
    // skip DecrementUseFromSlotAt (preventing server-side ammo consumption).
    // The override-dict logic below remains as a belt-and-suspenders display fix.
    private void TickInfiniteAmmo()
    {
        if (!infiniteAmmoEnabled)
        {
            return;
        }

        if (!ammoInventoryReflectionReady)
        {
            InitializeAmmoInventoryReflection();
        }

        if (cachedLocalPlayerInventoryProperty == null
            || cachedInventorySlotsSyncListField == null
            || cachedInventoryOverridesDictField == null
            || cachedInvSlotItemTypeField == null
            || cachedInvSlotConstructor == null)
        {
            return;
        }

        try
        {
            object localInventory = cachedLocalPlayerInventoryProperty.GetValue(null, null);
            if (localInventory == null)
            {
                return;
            }

            BirdieInfiniteAmmoBridge.LocalPlayerInventory = localInventory;

            System.Collections.IList slotsList =
                cachedInventorySlotsSyncListField.GetValue(localInventory) as System.Collections.IList;
            System.Collections.IDictionary overridesDict =
                cachedInventoryOverridesDictField.GetValue(localInventory) as System.Collections.IDictionary;

            if (slotsList == null || overridesDict == null)
            {
                return;
            }

            for (int i = 0; i < slotsList.Count; i++)
            {
                // Read item type from the server-synced SyncList slot
                object slotItem = slotsList[i];
                object slotsItemType = slotItem != null
                    ? cachedInvSlotItemTypeField.GetValue(slotItem)
                    : null;

                // Read item type from the existing client-side override (if any)
                object existingOverride = overridesDict.Contains(i) ? overridesDict[i] : null;
                object overrideItemType = existingOverride != null
                    ? cachedInvSlotItemTypeField.GetValue(existingOverride)
                    : null;

                // Determine which item type to restore:
                // prefer override > SyncList > backup (in case server already emptied the slot)
                object itemTypeToUse = null;
                if (overrideItemType != null && (int)overrideItemType != 0)
                {
                    itemTypeToUse = overrideItemType;
                }
                else if (slotsItemType != null && (int)slotsItemType != 0)
                {
                    itemTypeToUse = slotsItemType;
                }
                else
                {
                    ammoItemTypeBackup.TryGetValue(i, out itemTypeToUse);
                }

                if (itemTypeToUse == null || (int)itemTypeToUse == 0)
                {
                    continue;
                }

                // Keep backup fresh
                ammoItemTypeBackup[i] = itemTypeToUse;

                // Set override to {itemType, 99} — game reads this via GetEffectiveSlot
                overridesDict[i] = cachedInvSlotConstructor.Invoke(
                    new object[] { itemTypeToUse, 99 });
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Infinite ammo tick error: " + ex.Message);
        }
    }

    // ==================== NO RECOIL ====================
    // Zeroes GameSettings.All.General.screenshakeFactor

    private void InitializeScreenshakeExtrasReflection()
    {
        if (screenshakeExtrasReflectionInitialized)
        {
            return;
        }

        screenshakeExtrasReflectionInitialized = true;

        try
        {
            Type gameSettingsType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                gameSettingsType = asm.GetType("GameSettings");
                if (gameSettingsType != null)
                {
                    break;
                }
            }

            if (gameSettingsType == null)
            {
                BirdieLog.Warning("[Birdie] GameSettings type not found");
                return;
            }

            PropertyInfo allProp = gameSettingsType.GetProperty(
                "All",
                BindingFlags.Public | BindingFlags.Static);

            if (allProp == null)
            {
                BirdieLog.Warning("[Birdie] GameSettings.All property not found");
                return;
            }

            object allSettings = allProp.GetValue(null, null);
            if (allSettings == null)
            {
                BirdieLog.Warning("[Birdie] GameSettings.All is null");
                return;
            }

            PropertyInfo generalProp = allSettings.GetType().GetProperty(
                "General",
                BindingFlags.Public | BindingFlags.Instance);

            if (generalProp == null)
            {
                BirdieLog.Warning("[Birdie] AllSettings.General property not found");
                return;
            }

            object generalSettings = generalProp.GetValue(allSettings, null);
            if (generalSettings == null)
            {
                BirdieLog.Warning("[Birdie] AllSettings.General is null");
                return;
            }

            // Field is lowercase: screenshakeFactor (not ScreenshakeFactor)
            FieldInfo shakeField = generalSettings.GetType().GetField(
                "screenshakeFactor",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (shakeField != null && shakeField.FieldType == typeof(float))
            {
                cachedScreenshakeOwner = generalSettings;
                cachedScreenshakeFactorField = shakeField;
                savedScreenshakeFactor = (float)shakeField.GetValue(generalSettings);
            }
            else
            {
                BirdieLog.Warning("[Birdie] screenshakeFactor field not found on GeneralSettings");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Screenshake reflection error: " + ex.Message);
        }
    }

    private void ToggleNoRecoil()
    {
        if (!IsFeatureAllowed(8)) return;
        noRecoilEnabled = !noRecoilEnabled;
        InitializeScreenshakeExtrasReflection();

        if (noRecoilEnabled)
        {
            ApplyNoRecoilState();
            BirdieLog.Msg(string.Format("[Birdie] No Recoil ON (saved shake={0:F3})", savedScreenshakeFactor));
        }
        else
        {
            RestoreRecoilState();
            BirdieLog.Msg("[Birdie] No Recoil OFF");
        }

        MarkHudDirty();
    }

    private void ApplyNoRecoilState()
    {
        if (cachedScreenshakeOwner == null || cachedScreenshakeFactorField == null)
        {
            return;
        }

        try
        {
            cachedScreenshakeFactorField.SetValue(cachedScreenshakeOwner, 0f);
        }
        catch
        {
        }
    }

    private void RestoreRecoilState()
    {
        if (cachedScreenshakeOwner == null || cachedScreenshakeFactorField == null)
        {
            return;
        }

        try
        {
            cachedScreenshakeFactorField.SetValue(cachedScreenshakeOwner, savedScreenshakeFactor);
        }
        catch
        {
        }
    }

    // ==================== NO KNOCKBACK ====================
    // Sets knockoutImmunityStatus.hasImmunity = true every LateUpdate on the local
    // PlayerMovement component.  This bypasses the SyncVar setter (no network noise)
    // while still making CanBeKnockedOutBy() return false every time it is checked.

    private void InitializeKnockbackImmunityReflection()
    {
        if (knockbackImmunityReflectionReady)
        {
            return;
        }

        knockbackImmunityReflectionReady = true; // prevent re-running

        try
        {
            Type pmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                pmType = asm.GetType("PlayerMovement");
                if (pmType != null)
                {
                    break;
                }
            }

            if (pmType == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback: PlayerMovement type not found");
                return;
            }

            // Private SyncVar backing field that holds the immunity struct
            cachedKnockoutImmunityStatusField = pmType.GetField(
                "knockoutImmunityStatus",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (cachedKnockoutImmunityStatusField == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback: knockoutImmunityStatus field not found");
                return;
            }

            // KnockOutImmunity.hasImmunity — the bool checked by CanBeKnockedOutBy
            Type koiType = cachedKnockoutImmunityStatusField.FieldType;
            cachedHasImmunityField = koiType.GetField(
                "hasImmunity",
                BindingFlags.Public | BindingFlags.Instance);

            if (cachedHasImmunityField == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback: hasImmunity field not found on " + koiType.Name);
                return;
            }

            BirdieLog.Msg("[Birdie] Knockback immunity reflection ready (struct=" + koiType.Name + ")");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Knockback immunity reflection error: " + ex.Message);
            knockbackImmunityReflectionReady = false;
        }
    }

    private void ToggleNoKnockback()
    {
        if (!IsFeatureAllowed(9)) return;
        noKnockbackEnabled = !noKnockbackEnabled;
        BirdieNoKnockbackBridge.EnsurePatchApplied();
        BirdieNoKnockbackBridge.IsActive = noKnockbackEnabled;
        BirdieLog.Msg(noKnockbackEnabled ? "[Birdie] No Knockback ON" : "[Birdie] No Knockback OFF");
        MarkHudDirty();
    }

    // ==================== LANDMINE IMMUNITY ====================

    private void ToggleLandmineImmunity()
    {
        if (!IsFeatureAllowed(10)) return;
        landmineImmunityEnabled = !landmineImmunityEnabled;
        BirdieLandmineImmunityBridge.EnsurePatchApplied();
        BirdieLandmineImmunityBridge.IsActive = landmineImmunityEnabled;
        BirdieLog.Msg(landmineImmunityEnabled ? "[Birdie] Landmine Immunity ON" : "[Birdie] Landmine Immunity OFF");
        MarkHudDirty();
    }

    // ==================== LOCK-ON ANY DISTANCE ====================

    private void InitializeLockOnReflection()
    {
        if (lockOnReflectionInitialized) return;
        lockOnReflectionInitialized = true;
        try
        {
            Type gmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                gmType = asm.GetType("GameManager");
                if (gmType != null) break;
            }
            if (gmType == null) { BirdieLog.Warning("[Birdie] Lock-On: GameManager not found"); return; }

            PropertyInfo golfSettingsProp = gmType.GetProperty("GolfSettings", BindingFlags.Public | BindingFlags.Static);
            if (golfSettingsProp == null) { BirdieLog.Warning("[Birdie] Lock-On: GolfSettings property not found"); return; }

            object golfSettings = golfSettingsProp.GetValue(null, null);
            if (golfSettings == null) { BirdieLog.Warning("[Birdie] Lock-On: GolfSettings is null"); return; }

            Type gsType = golfSettings.GetType();
            // Try auto-property backing field first
            cachedLockOnMaxDistanceField = gsType.GetField("<LockOnMaxDistance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cachedLockOnMaxDistanceField == null)
                cachedLockOnMaxDistanceField = gsType.GetField("LockOnMaxDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (cachedLockOnMaxDistanceField != null && cachedLockOnMaxDistanceField.FieldType == typeof(float))
            {
                savedLockOnMaxDistance = (float)cachedLockOnMaxDistanceField.GetValue(golfSettings);
                BirdieLog.Msg("[Birdie] Lock-On reflection ready, default dist=" + savedLockOnMaxDistance);
            }
            else
            {
                BirdieLog.Warning("[Birdie] Lock-On: LockOnMaxDistance field not found");
                cachedLockOnMaxDistanceField = null;
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Lock-On reflection error: " + ex.Message);
        }
    }

    private void ToggleLockOnAnyDistance()
    {
        if (!IsFeatureAllowed(11)) return;
        lockOnAnyDistanceEnabled = !lockOnAnyDistanceEnabled;
        InitializeLockOnReflection();

        if (lockOnAnyDistanceEnabled)
            ApplyLockOnAnyDistance();
        else
            RestoreLockOnDistance();

        BirdieLog.Msg(lockOnAnyDistanceEnabled ? "[Birdie] Lock-On Any Distance ON" : "[Birdie] Lock-On Any Distance OFF");
        MarkHudDirty();
    }

    private void ApplyLockOnAnyDistance()
    {
        if (cachedLockOnMaxDistanceField == null) return;
        try
        {
            Type gmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            { gmType = asm.GetType("GameManager"); if (gmType != null) break; }
            if (gmType == null) return;
            PropertyInfo golfSettingsProp = gmType.GetProperty("GolfSettings", BindingFlags.Public | BindingFlags.Static);
            if (golfSettingsProp == null) return;
            object gs = golfSettingsProp.GetValue(null, null);
            if (gs == null) return;
            cachedLockOnMaxDistanceField.SetValue(gs, 9999f);
        }
        catch { }
    }

    private void RestoreLockOnDistance()
    {
        if (cachedLockOnMaxDistanceField == null) return;
        try
        {
            Type gmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            { gmType = asm.GetType("GameManager"); if (gmType != null) break; }
            if (gmType == null) return;
            PropertyInfo golfSettingsProp = gmType.GetProperty("GolfSettings", BindingFlags.Public | BindingFlags.Static);
            if (golfSettingsProp == null) return;
            object gs = golfSettingsProp.GetValue(null, null);
            if (gs == null) return;
            cachedLockOnMaxDistanceField.SetValue(gs, savedLockOnMaxDistance);
        }
        catch { }
    }

    // Called every LateUpdate while noKnockbackEnabled.
    // Writes hasImmunity=true directly to the backing field (blocks knockout STATE),
    // and keeps BirdieNoKnockbackBridge.LocalPlayerHittable current so the Harmony
    // prefix on Hittable.HitWithItem can skip AddForceAtPosition entirely.
    private void TickNoKnockback()
    {
        bool needHittable = noKnockbackEnabled || landmineImmunityEnabled;

        if (!needHittable || playerMovement == null)
        {
            BirdieNoKnockbackBridge.LocalPlayerHittable = null;
            BirdieLandmineImmunityBridge.LocalPlayerHittable = null;
            return;
        }

        // Lazily resolve the Hittable component on the local player's hierarchy.
        if (localPlayerHittable == null)
        {
            // Walk up the parent chain first
            Transform t = playerMovement.transform;
            while (t != null && localPlayerHittable == null)
            {
                Component[] comps = t.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] != null && comps[i].GetType().Name == "Hittable")
                    {
                        localPlayerHittable = comps[i];
                        break;
                    }
                }
                t = t.parent;
            }

            // If not found in parents, check children
            if (localPlayerHittable == null)
            {
                Component[] comps = playerMovement.GetComponentsInChildren<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] != null && comps[i].GetType().Name == "Hittable")
                    {
                        localPlayerHittable = comps[i];
                        break;
                    }
                }
            }
        }

        BirdieNoKnockbackBridge.LocalPlayerHittable = noKnockbackEnabled ? localPlayerHittable : null;
        BirdieLandmineImmunityBridge.LocalPlayerHittable = landmineImmunityEnabled ? localPlayerHittable : null;

        if (!noKnockbackEnabled)
        {
            return;
        }

        if (!knockbackImmunityReflectionReady)
        {
            InitializeKnockbackImmunityReflection();
        }

        if (cachedKnockoutImmunityStatusField == null || cachedHasImmunityField == null)
        {
            return;
        }

        try
        {
            // GetValue boxes the KnockOutImmunity struct — we can modify it and write back
            object immunityStatus = cachedKnockoutImmunityStatusField.GetValue(playerMovement);
            if (immunityStatus == null)
            {
                return;
            }

            if (!(bool)cachedHasImmunityField.GetValue(immunityStatus))
            {
                cachedHasImmunityField.SetValue(immunityStatus, true);
                cachedKnockoutImmunityStatusField.SetValue(playerMovement, immunityStatus);
            }
        }
        catch
        {
        }
    }
}

// Harmony prefix on Hittable.HitWithItem (public) and
// Hittable.HitWithRocketLauncherBackBlast (public).
// Patching the public methods skips both the local execution (HitWithItemInternal)
// AND the Command to the server (CmdHitWithItem), providing complete protection.
// The prefix returns false (skip original) when active and the hit target is the
// local player, preventing the force from ever being applied.
internal static class BirdieNoKnockbackBridge
{
    internal static bool IsActive;
    internal static object LocalPlayerHittable;

    private static bool patchApplied;

    internal static void EnsurePatchApplied()
    {
        if (patchApplied)
        {
            return;
        }

        patchApplied = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Type hittableType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                hittableType = assemblies[i].GetType("Hittable");
                if (hittableType != null)
                {
                    break;
                }
            }

            if (hittableType == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback patch: Hittable type not found");
                patchApplied = false;
                return;
            }

            MethodInfo hitWithItem = hittableType.GetMethod(
                "HitWithItem",
                BindingFlags.Public | BindingFlags.Instance);

            MethodInfo hitWithRocket = hittableType.GetMethod(
                "HitWithRocketLauncherBackBlast",
                BindingFlags.Public | BindingFlags.Instance);

            if (hitWithItem == null && hitWithRocket == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback patch: neither Hittable target method found");
                patchApplied = false;
                return;
            }

            Type harmonyType = null;
            Type harmonyMethodType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (harmonyType == null)
                {
                    harmonyType = assemblies[i].GetType("HarmonyLib.Harmony");
                }

                if (harmonyMethodType == null)
                {
                    harmonyMethodType = assemblies[i].GetType("HarmonyLib.HarmonyMethod");
                }

                if (harmonyType != null && harmonyMethodType != null)
                {
                    break;
                }
            }

            if (harmonyType == null || harmonyMethodType == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback patch: HarmonyLib types not found");
                patchApplied = false;
                return;
            }

            object harmony = Activator.CreateInstance(harmonyType, "birdie.noknockback");

            MethodInfo prefixMethod = typeof(BirdieNoKnockbackBridge).GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefixMethod == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback patch: Prefix method not found");
                patchApplied = false;
                return;
            }

            object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefixMethod);

            MethodInfo patchMethod = null;
            MethodInfo[] candidates = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < candidates.Length; i++)
            {
                MethodInfo m = candidates[i];
                if (m.Name != "Patch" || m.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] prms = m.GetParameters();
                if (prms.Length >= 2 && prms[0].ParameterType.FullName == "System.Reflection.MethodBase")
                {
                    patchMethod = m;
                    break;
                }
            }

            if (patchMethod == null)
            {
                BirdieLog.Warning("[Birdie] No Knockback patch: Harmony.Patch overload not found");
                patchApplied = false;
                return;
            }

            ParameterInfo[] patchParams = patchMethod.GetParameters();

            if (hitWithItem != null)
            {
                object[] args = BuildArgs(patchParams, hitWithItem, harmonyPrefix);
                patchMethod.Invoke(harmony, args);
                BirdieLog.Msg("[Birdie] No Knockback patch: registered prefix on Hittable.HitWithItem");
            }

            if (hitWithRocket != null)
            {
                object[] args = BuildArgs(patchParams, hitWithRocket, harmonyPrefix);
                patchMethod.Invoke(harmony, args);
                BirdieLog.Msg("[Birdie] No Knockback patch: registered prefix on Hittable.HitWithRocketLauncherBackBlast");
            }
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] No Knockback patch error: " + ex.Message);
            patchApplied = false;
        }
    }

    private static object[] BuildArgs(ParameterInfo[] patchParams, MethodInfo original, object harmonyPrefix)
    {
        object[] args = new object[patchParams.Length];
        args[0] = original;
        for (int p = 1; p < patchParams.Length; p++)
        {
            string pname = patchParams[p].Name != null
                ? patchParams[p].Name.ToLowerInvariant()
                : string.Empty;
            if (pname == "prefix")
            {
                args[p] = harmonyPrefix;
            }
        }

        return args;
    }

    // Return false = skip the original method (and its AddForceAtPosition call).
    // Return true  = run the original as normal.
    private static bool Prefix(object __instance)
    {
        if (!IsActive || LocalPlayerHittable == null)
        {
            return true;
        }

        return !ReferenceEquals(__instance, LocalPlayerHittable);
    }
}

// Harmony prefix on PlayerInventory.DecrementUseFromSlotAt.
// Normally this method decrements the client override AND sends CmdDecrementUseFromSlotAt
// to the server, which then removes the item when uses reach zero. Skipping the
// entire method for the local player prevents the server from ever decrementing,
// so ammo is never consumed.
internal static class BirdieInfiniteAmmoBridge
{
    internal static bool IsActive;
    internal static object LocalPlayerInventory;

    private static bool patchApplied;

    internal static void EnsurePatchApplied()
    {
        if (patchApplied)
        {
            return;
        }

        patchApplied = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Type piType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                piType = assemblies[i].GetType("PlayerInventory");
                if (piType != null)
                {
                    break;
                }
            }

            if (piType == null)
            {
                BirdieLog.Warning("[Birdie] Infinite Ammo patch: PlayerInventory type not found");
                patchApplied = false;
                return;
            }

            MethodInfo decrementMethod = piType.GetMethod(
                "DecrementUseFromSlotAt",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (decrementMethod == null)
            {
                BirdieLog.Warning("[Birdie] Infinite Ammo patch: DecrementUseFromSlotAt not found");
                patchApplied = false;
                return;
            }

            Type harmonyType = null;
            Type harmonyMethodType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (harmonyType == null)
                {
                    harmonyType = assemblies[i].GetType("HarmonyLib.Harmony");
                }

                if (harmonyMethodType == null)
                {
                    harmonyMethodType = assemblies[i].GetType("HarmonyLib.HarmonyMethod");
                }

                if (harmonyType != null && harmonyMethodType != null)
                {
                    break;
                }
            }

            if (harmonyType == null || harmonyMethodType == null)
            {
                BirdieLog.Warning("[Birdie] Infinite Ammo patch: HarmonyLib types not found");
                patchApplied = false;
                return;
            }

            object harmony = Activator.CreateInstance(harmonyType, "birdie.infiniteammo");

            MethodInfo prefixMethod = typeof(BirdieInfiniteAmmoBridge).GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefixMethod == null)
            {
                BirdieLog.Warning("[Birdie] Infinite Ammo patch: Prefix method not found");
                patchApplied = false;
                return;
            }

            object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefixMethod);

            MethodInfo patchMethod = null;
            MethodInfo[] candidates = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < candidates.Length; i++)
            {
                MethodInfo m = candidates[i];
                if (m.Name != "Patch" || m.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] prms = m.GetParameters();
                if (prms.Length >= 2 && prms[0].ParameterType.FullName == "System.Reflection.MethodBase")
                {
                    patchMethod = m;
                    break;
                }
            }

            if (patchMethod == null)
            {
                BirdieLog.Warning("[Birdie] Infinite Ammo patch: Harmony.Patch overload not found");
                patchApplied = false;
                return;
            }

            ParameterInfo[] patchParams = patchMethod.GetParameters();
            object[] args = new object[patchParams.Length];
            args[0] = decrementMethod;
            for (int p = 1; p < patchParams.Length; p++)
            {
                string pname = patchParams[p].Name != null
                    ? patchParams[p].Name.ToLowerInvariant()
                    : string.Empty;
                if (pname == "prefix")
                {
                    args[p] = harmonyPrefix;
                }
            }

            patchMethod.Invoke(harmony, args);
            BirdieLog.Msg("[Birdie] Infinite Ammo patch: registered prefix on PlayerInventory.DecrementUseFromSlotAt");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Infinite Ammo patch error: " + ex.Message);
            patchApplied = false;
        }
    }

    // Return false = skip DecrementUseFromSlotAt entirely (no client decrement,
    // no CmdDecrementUseFromSlotAt sent to server).
    private static bool Prefix(object __instance)
    {
        if (!IsActive || LocalPlayerInventory == null)
        {
            return true;
        }

        return !ReferenceEquals(__instance, LocalPlayerInventory);
    }
}

// Harmony prefix on Hittable.HitWithItem (public).
// When active and the hit target is the local player and the itemType is Landmine,
// the prefix returns false to skip the entire method — preventing both local
// execution and the Command to the server.
internal static class BirdieLandmineImmunityBridge
{
    internal static bool IsActive;
    internal static object LocalPlayerHittable;
    internal static object LandmineItemTypeValue;

    private static bool patchApplied;

    internal static void EnsurePatchApplied()
    {
        if (patchApplied)
        {
            return;
        }

        patchApplied = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Type hittableType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                hittableType = assemblies[i].GetType("Hittable");
                if (hittableType != null)
                {
                    break;
                }
            }

            if (hittableType == null)
            {
                BirdieLog.Warning("[Birdie] Landmine Immunity patch: Hittable type not found");
                patchApplied = false;
                return;
            }

            MethodInfo hitWithItem = hittableType.GetMethod(
                "HitWithItemInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (hitWithItem == null)
            {
                BirdieLog.Warning("[Birdie] Landmine Immunity patch: HitWithItemInternal method not found");
                patchApplied = false;
                return;
            }

            // Cache the Landmine enum value for comparison at runtime
            Type itemTypeEnum = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                itemTypeEnum = assemblies[i].GetType("ItemType");
                if (itemTypeEnum != null) break;
            }
            if (itemTypeEnum != null)
            {
                try { LandmineItemTypeValue = Enum.Parse(itemTypeEnum, "Landmine"); } catch { }
            }

            Type harmonyType = null;
            Type harmonyMethodType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (harmonyType == null)
                {
                    harmonyType = assemblies[i].GetType("HarmonyLib.Harmony");
                }

                if (harmonyMethodType == null)
                {
                    harmonyMethodType = assemblies[i].GetType("HarmonyLib.HarmonyMethod");
                }

                if (harmonyType != null && harmonyMethodType != null)
                {
                    break;
                }
            }

            if (harmonyType == null || harmonyMethodType == null)
            {
                BirdieLog.Warning("[Birdie] Landmine Immunity patch: HarmonyLib types not found");
                patchApplied = false;
                return;
            }

            object harmony = Activator.CreateInstance(harmonyType, "birdie.landmineimmunity");

            MethodInfo prefixMethod = typeof(BirdieLandmineImmunityBridge).GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefixMethod == null)
            {
                BirdieLog.Warning("[Birdie] Landmine Immunity patch: Prefix method not found");
                patchApplied = false;
                return;
            }

            object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefixMethod);

            MethodInfo patchMethod = null;
            MethodInfo[] candidates = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < candidates.Length; i++)
            {
                MethodInfo m = candidates[i];
                if (m.Name != "Patch" || m.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] prms = m.GetParameters();
                if (prms.Length >= 2 && prms[0].ParameterType.FullName == "System.Reflection.MethodBase")
                {
                    patchMethod = m;
                    break;
                }
            }

            if (patchMethod == null)
            {
                BirdieLog.Warning("[Birdie] Landmine Immunity patch: Harmony.Patch overload not found");
                patchApplied = false;
                return;
            }

            ParameterInfo[] patchParams = patchMethod.GetParameters();
            object[] args = new object[patchParams.Length];
            args[0] = hitWithItem;
            for (int p = 1; p < patchParams.Length; p++)
            {
                string pname = patchParams[p].Name != null
                    ? patchParams[p].Name.ToLowerInvariant()
                    : string.Empty;
                if (pname == "prefix")
                {
                    args[p] = harmonyPrefix;
                }
            }

            patchMethod.Invoke(harmony, args);
            BirdieLog.Msg("[Birdie] Landmine Immunity patch: registered prefix on Hittable.HitWithItemInternal");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] Landmine Immunity patch error: " + ex.Message);
            patchApplied = false;
        }
    }

    // Return false = skip HitWithItem entirely when itemType is Landmine and
    // the target is the local player.
    // Return true  = run the original as normal.
    private static bool Prefix(object __instance, object itemType)
    {
        if (!IsActive || LocalPlayerHittable == null)
        {
            return true;
        }

        if (!ReferenceEquals(__instance, LocalPlayerHittable))
        {
            return true;
        }

        if (LandmineItemTypeValue == null || !Equals(itemType, LandmineItemTypeValue))
        {
            return true;
        }

        return false; // skip landmine hit on local player
    }
}

// ==================== EXPANDED ITEM SLOTS ====================
public partial class BirdieMod
{
    private void InitializeExpandedSlotsReflection()
    {
        if (expandedSlotsReflectionInitialized) return;
        expandedSlotsReflectionInitialized = true;

        try
        {
            Type hotkeysType = null;
            Type gmType = null;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (hotkeysType == null) hotkeysType = asm.GetType("Hotkeys");
                if (gmType == null) gmType = asm.GetType("GameManager");
                if (hotkeysType != null && gmType != null) break;
            }

            if (hotkeysType == null || gmType == null)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: Hotkeys or GameManager type not found");
                return;
            }

            UnityEngine.Object hotkeyObj = UnityEngine.Object.FindObjectOfType(hotkeysType);
            if (hotkeyObj == null)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: Hotkeys instance not in scene");
                return;
            }
            cachedExpandedHotkeysInstance = hotkeyObj;

            cachedHotkeyUisField = hotkeysType.GetField("hotkeyUis",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cachedHotkeyUisField == null)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: hotkeyUis field not found");
                return;
            }

            PropertyInfo piProp = gmType.GetProperty("PlayerInventorySettings",
                BindingFlags.Public | BindingFlags.Static);
            if (piProp == null)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: PlayerInventorySettings property not found");
                return;
            }

            cachedExpandedPlayerInvSettings = piProp.GetValue(null);
            if (cachedExpandedPlayerInvSettings == null)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: PlayerInventorySettings is null");
                return;
            }

            // Try auto-property backing field first, then plain field
            cachedMaxItemsBackingField = cachedExpandedPlayerInvSettings.GetType().GetField(
                "<MaxItems>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cachedMaxItemsBackingField == null)
            {
                cachedMaxItemsBackingField = cachedExpandedPlayerInvSettings.GetType().GetField(
                    "MaxItems",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (cachedMaxItemsBackingField == null)
                BirdieLog.Warning("[Birdie] ExpandedSlots: MaxItems field not found");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] ExpandedSlots init error: " + ex.Message);
        }
    }

    private void ToggleExpandedSlots()
    {
        if (!IsFeatureAllowed(12)) return;
        expandedSlotsEnabled = !expandedSlotsEnabled;
        if (expandedSlotsEnabled)
            ApplyExpandedSlots();
        else
            RestoreExpandedSlots();
        MarkHudDirty();
    }

    private void ApplyExpandedSlots()
    {
        InitializeExpandedSlotsReflection();

        if (cachedExpandedHotkeysInstance == null || cachedHotkeyUisField == null || cachedMaxItemsBackingField == null)
        {
            BirdieLog.Warning("[Birdie] ExpandedSlots: reflection not ready, cannot apply");
            expandedSlotsEnabled = false;
            return;
        }

        try
        {
            int currentMax = (int)cachedMaxItemsBackingField.GetValue(cachedExpandedPlayerInvSettings);
            if (currentMax >= ExpandedSlotsTarget)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: already at target or beyond (" + currentMax + ")");
                return;
            }
            savedOriginalMaxItems = currentMax;

            System.Array currentArr = (System.Array)cachedHotkeyUisField.GetValue(cachedExpandedHotkeysInstance);
            if (currentArr == null || currentArr.Length < 2)
            {
                BirdieLog.Warning("[Birdie] ExpandedSlots: hotkeyUis array is null or too small");
                expandedSlotsEnabled = false;
                return;
            }

            savedOriginalHotkeyUisArray = currentArr;

            // needed = golf-club slot (0) + ExpandedSlotsTarget item slots (1..ExpandedSlotsTarget)
            int needed = ExpandedSlotsTarget + 1;
            Type elemType = currentArr.GetType().GetElementType();
            System.Array newArr = System.Array.CreateInstance(elemType, needed);

            // Copy all existing elements (some may be null if game pre-allocated a larger array)
            for (int i = 0; i < System.Math.Min(currentArr.Length, needed); i++)
                newArr.SetValue(currentArr.GetValue(i), i);

            // Use slot-1 as the clone template (first real item slot)
            Component template = (Component)currentArr.GetValue(1);
            Transform slotParent = template.transform.parent;

            // Calculate step between slot 1 and slot 2
            RectTransform rt1 = template.GetComponent<RectTransform>();
            RectTransform rt2 = currentArr.Length > 2
                ? ((Component)currentArr.GetValue(2))?.GetComponent<RectTransform>() : null;
            Vector2 step = (rt1 != null && rt2 != null)
                ? (rt2.anchoredPosition - rt1.anchoredPosition)
                : new Vector2(rt1 != null ? rt1.sizeDelta.x + 4f : 80f, 0f);

            // Create UI for every slot index from currentMax+1 to ExpandedSlotsTarget.
            // This handles both the case where the array was pre-allocated (with nulls) and
            // where it was compact — we always create fresh UI for the new slots.
            for (int i = currentMax + 1; i <= ExpandedSlotsTarget; i++)
            {
                // If the array already had a valid non-null component here, keep it
                if (i < currentArr.Length)
                {
                    object existing = currentArr.GetValue(i);
                    if (existing != null && existing is Component existComp && existComp != null
                        && existComp.gameObject != null)
                    {
                        newArr.SetValue(existing, i);
                        continue;
                    }
                }

                GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, slotParent);
                clone.name = "HotkeyUi_Extra_" + i;

                RectTransform cloneRt = clone.GetComponent<RectTransform>();
                if (cloneRt != null && rt1 != null)
                {
                    // Position relative to the previous slot in newArr
                    RectTransform prevRt = null;
                    if (i > 1 && newArr.GetValue(i - 1) is Component prevComp && prevComp != null)
                        prevRt = prevComp.GetComponent<RectTransform>();
                    if (prevRt == null) prevRt = rt1;
                    cloneRt.anchoredPosition = prevRt.anchoredPosition + step;
                }

                clone.SetActive(true);
                Component cloneComp = clone.GetComponent(elemType) ?? (Component)clone.GetComponent<Component>();
                newArr.SetValue(cloneComp, i);
                spawnedExtraHotkeyUiObjects.Add(clone);
            }

            // Widen the parent RectTransform so the extra slots aren't clipped.
            savedSlotParentTransform = slotParent;
            RectTransform parentRt = slotParent.GetComponent<RectTransform>();
            if (parentRt != null)
            {
                savedSlotParentSizeDelta = parentRt.sizeDelta;
                float slotWidth = Mathf.Abs(step.x) > 1f ? Mathf.Abs(step.x) : (rt1 != null ? rt1.sizeDelta.x + 4f : 84f);
                parentRt.sizeDelta = new Vector2(slotWidth * needed + 20f, parentRt.sizeDelta.y);
            }
            // Disable any clip masks on the parent so extra slots aren't hidden
            UnityEngine.UI.Mask parentMask = slotParent.GetComponent<UnityEngine.UI.Mask>();
            savedSlotParentMaskEnabled = parentMask != null && parentMask.enabled;
            if (parentMask != null) parentMask.enabled = false;
            UnityEngine.UI.RectMask2D rectMask = slotParent.GetComponent<UnityEngine.UI.RectMask2D>();
            if (rectMask != null) rectMask.enabled = false;

            // Push the expanded array and new MaxItems BEFORE calling ForceRefresh
            cachedHotkeyUisField.SetValue(cachedExpandedHotkeysInstance, newArr);
            cachedMaxItemsBackingField.SetValue(cachedExpandedPlayerInvSettings, ExpandedSlotsTarget);

            // Ask the Hotkeys system to rebuild all slot UI (sets button-prompt sprites and labels)
            try
            {
                MethodInfo forceRefresh = cachedExpandedHotkeysInstance.GetType().GetMethod(
                    "ForceRefreshCurrentModeInternal",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                forceRefresh?.Invoke(cachedExpandedHotkeysInstance, null);
            }
            catch { }

            TryExpandServerInventorySlots();
            BirdieLog.Msg("[Birdie] ExpandedSlots: expanded to " + ExpandedSlotsTarget
                + " slots (orig array len=" + currentArr.Length + ", currentMax=" + currentMax + ")");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] ExpandedSlots apply error: " + ex.Message + "\n" + ex.StackTrace);
            expandedSlotsEnabled = false;
            RestoreExpandedSlots();
        }
    }

    private void TryExpandServerInventorySlotsPublic() => TryExpandServerInventorySlots();

    private Component GetLocalPlayerInventoryComponent(Type piType)
    {
        try
        {
            Type gmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                gmType = asm.GetType("GameManager");
                if (gmType != null) break;
            }
            if (gmType == null) return null;
            PropertyInfo p = gmType.GetProperty("LocalPlayerInventory", BindingFlags.Public | BindingFlags.Static);
            return p?.GetValue(null) as Component;
        }
        catch { return null; }
    }

    private void TryExpandServerInventorySlots()
    {
        try
        {
            // Only run on the server/host
            bool isServer = false;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type nsType = asm.GetType("Mirror.NetworkServer");
                if (nsType == null) continue;
                PropertyInfo activeProp = nsType.GetProperty("active", BindingFlags.Public | BindingFlags.Static);
                if (activeProp != null)
                    isServer = (bool)activeProp.GetValue(null);
                break;
            }
            if (!isServer) return;

            // Resolve PlayerInventory type and InventorySlot type
            Type piType = null;
            Type invSlotType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (piType == null) piType = asm.GetType("PlayerInventory");
                if (invSlotType == null) invSlotType = asm.GetType("InventorySlot");
                if (piType != null && invSlotType != null) break;
            }
            if (piType == null || invSlotType == null) return;

            object emptySlot = System.Activator.CreateInstance(invSlotType);

            // Expand inventories — all players or just local depending on setting
            UnityEngine.Object[] allInvs = expandedSlotsAllPlayers
                ? UnityEngine.Object.FindObjectsOfType(piType)
                : new UnityEngine.Object[] { GetLocalPlayerInventoryComponent(piType) };
            int expandedCount = 0;
            foreach (UnityEngine.Object invObj in allInvs)
            {
                Component inv = invObj as Component;
                if (inv == null) continue;

                FieldInfo slotsField = inv.GetType().GetField("slots",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (slotsField == null) continue;

                object slots = slotsField.GetValue(inv);
                if (slots == null) continue;

                PropertyInfo countProp = slots.GetType().GetProperty("Count");
                if (countProp == null) continue;
                int currentCount = (int)countProp.GetValue(slots);
                if (currentCount >= ExpandedSlotsTarget) continue;

                MethodInfo addMethod = slots.GetType().GetMethod("Add");
                if (addMethod == null) continue;

                int toAdd = ExpandedSlotsTarget - currentCount;
                for (int i = 0; i < toAdd; i++)
                    addMethod.Invoke(slots, new object[] { emptySlot });
                expandedCount++;
            }

            BirdieLog.Msg("[Birdie] ExpandedSlots: expanded " + expandedCount + " player inventories to " + ExpandedSlotsTarget + " slots");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] ExpandedSlots server slots error: " + ex.Message);
        }
    }

    private void RestoreExpandedSlots()
    {
        try
        {
            // Restore MaxItems first (so OnBUpdate doesn't over-index before we shrink the array)
            if (cachedMaxItemsBackingField != null && cachedExpandedPlayerInvSettings != null)
                cachedMaxItemsBackingField.SetValue(cachedExpandedPlayerInvSettings, savedOriginalMaxItems);

            // Restore original hotkeyUis array
            if (cachedHotkeyUisField != null && cachedExpandedHotkeysInstance != null && savedOriginalHotkeyUisArray != null)
                cachedHotkeyUisField.SetValue(cachedExpandedHotkeysInstance, savedOriginalHotkeyUisArray);

            savedOriginalHotkeyUisArray = null;

            // Destroy cloned GameObjects
            foreach (GameObject go in spawnedExtraHotkeyUiObjects)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            spawnedExtraHotkeyUiObjects.Clear();

            // Restore parent size and mask
            try
            {
                if (savedSlotParentTransform != null)
                {
                    RectTransform pRt = savedSlotParentTransform.GetComponent<RectTransform>();
                    if (pRt != null) pRt.sizeDelta = savedSlotParentSizeDelta;
                    UnityEngine.UI.Mask m = savedSlotParentTransform.GetComponent<UnityEngine.UI.Mask>();
                    if (m != null) m.enabled = savedSlotParentMaskEnabled;
                    UnityEngine.UI.RectMask2D rm = savedSlotParentTransform.GetComponent<UnityEngine.UI.RectMask2D>();
                    if (rm != null) rm.enabled = true;
                    savedSlotParentTransform = null;
                }
            }
            catch { }

            BirdieLog.Msg("[Birdie] ExpandedSlots: restored to " + savedOriginalMaxItems + " slots");
        }
        catch (Exception ex)
        {
            BirdieLog.Warning("[Birdie] ExpandedSlots restore error: " + ex.Message);
        }
    }
}

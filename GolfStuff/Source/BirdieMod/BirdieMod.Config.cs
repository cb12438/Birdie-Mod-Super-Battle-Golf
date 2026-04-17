using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class BirdieMod
{
    private void LoadOrCreateConfig()
    {
        ApplyDefaultConfig();

        try
        {
            string configDirectory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, BuildDefaultConfigText(), Encoding.ASCII);
            }
            else
            {
                LoadConfigFromFile(configPath);
            }
        }
        catch
        {
        }

        UpdateConfigLabels();
        MarkHudDirty();
        MarkTrailVisualSettingsDirty();
        nextImpactPreviewRenderTime = 0f;
    }

    private void ApplyDefaultConfig()
    {
        assistToggleKeyName = "F1";
        coffeeBoostKeyName = "F2";
        nearestBallModeKeyName = "F3";
        unlockAllCosmeticsKeyName = "F4";
        itemSpawnerKeyName = "F5";
        hudToggleKeyName = "H";
        randomItemKeyName = "G";
        iceToggleKeyName = "I";
        settingsKeyName = "F6";
        noWindKeyName = "W";
        perfectShotKeyName = "P";
        noAirDragKeyName = "D";
        speedMultiplierKeyName = "S";
        speedMultiplierFactor = 2.0f;
        infiniteAmmoKeyName = "A";
        noRecoilKeyName = "R";
        noKnockbackKeyName = "K";
        landmineImmunityKeyName = "M";
        lockOnAnyDistanceKeyName = "L";
        expandedSlotsKeyName = "U";
        hudShowBottomBar = true;
        hudShowBallDistance = true;
        hudShowIceIndicator = true;
        hudShowCenterTitle = true;
        hudShowPlayerInfo = true;
        tracersEnabled = true;
        creditsGrantAmount = 1000;
        actualTrailEnabled = true;
        predictedTrailEnabled = true;
        frozenTrailEnabled = true;
        impactPreviewEnabled = true;
        impactPreviewTargetFps = impactPreviewAutoTargetFps;
        impactPreviewTextureWidth = 640;
        impactPreviewTextureHeight = 360;
        actualTrailStartWidth = 0.22f;
        actualTrailEndWidth = 0.18f;
        predictedTrailStartWidth = 0.18f;
        predictedTrailEndWidth = 0.14f;
        frozenTrailStartWidth = 0.20f;
        frozenTrailEndWidth = 0.16f;
        actualTrailColor = new Color(1f, 0.58f, 0.20f, 1f);
        predictedTrailColor = new Color(0.36f, 0.95f, 0.46f, 0.95f);
        frozenTrailColor = new Color(0.36f, 0.74f, 1f, 0.92f);
    }

    private void LoadConfigFromFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            string line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = line.Substring(0, separatorIndex).Trim().ToLowerInvariant();
            string value = line.Substring(separatorIndex + 1).Trim();

            switch (key)
            {
                case "assist_toggle_key":
                    assistToggleKeyName = ParseKeyNameOrDefault(value, assistToggleKeyName);
                    break;
                case "coffee_boost_key":
                    coffeeBoostKeyName = ParseKeyNameOrDefault(value, coffeeBoostKeyName);
                    break;
                case "nearest_ball_mode_key":
                    nearestBallModeKeyName = ParseKeyNameOrDefault(value, nearestBallModeKeyName);
                    break;
                case "unlock_all_cosmetics_key":
                    unlockAllCosmeticsKeyName = ParseKeyNameOrDefault(value, unlockAllCosmeticsKeyName);
                    break;
                case "item_spawner_key":
                    itemSpawnerKeyName = ParseKeyNameOrDefault(value, itemSpawnerKeyName);
                    break;
                case "hud_toggle_key":
                    hudToggleKeyName = ParseKeyNameOrDefault(value, hudToggleKeyName);
                    break;
                case "random_item_key":
                    randomItemKeyName = ParseKeyNameOrDefault(value, randomItemKeyName);
                    break;
                case "ice_toggle_key":
                    iceToggleKeyName = ParseKeyNameOrDefault(value, iceToggleKeyName);
                    break;
                case "settings_key":
                    settingsKeyName = ParseKeyNameOrDefault(value, settingsKeyName);
                    break;
                case "no_wind_key":
                    noWindKeyName = ParseKeyNameOrDefault(value, noWindKeyName);
                    break;
                case "perfect_shot_key":
                    perfectShotKeyName = ParseKeyNameOrDefault(value, perfectShotKeyName);
                    break;
                case "no_air_drag_key":
                    noAirDragKeyName = ParseKeyNameOrDefault(value, noAirDragKeyName);
                    break;
                case "speed_multiplier_key":
                    speedMultiplierKeyName = ParseKeyNameOrDefault(value, speedMultiplierKeyName);
                    break;
                case "speed_multiplier_factor":
                    speedMultiplierFactor = ParseFloatOrDefault(value, speedMultiplierFactor, 0.1f, 20f);
                    break;
                case "infinite_ammo_key":
                    infiniteAmmoKeyName = ParseKeyNameOrDefault(value, infiniteAmmoKeyName);
                    break;
                case "no_recoil_key":
                    noRecoilKeyName = ParseKeyNameOrDefault(value, noRecoilKeyName);
                    break;
                case "no_knockback_key":
                    noKnockbackKeyName = ParseKeyNameOrDefault(value, noKnockbackKeyName);
                    break;
                case "landmine_immunity_key":
                    landmineImmunityKeyName = ParseKeyNameOrDefault(value, landmineImmunityKeyName);
                    break;
                case "lock_on_any_distance_key":
                    lockOnAnyDistanceKeyName = ParseKeyNameOrDefault(value, lockOnAnyDistanceKeyName);
                    break;
                case "expanded_slots_key":
                    expandedSlotsKeyName = ParseKeyNameOrDefault(value, expandedSlotsKeyName);
                    break;
                case "hud_show_bottom_bar":
                    hudShowBottomBar = ParseBoolOrDefault(value, hudShowBottomBar);
                    break;
                case "hud_show_ball_distance":
                    hudShowBallDistance = ParseBoolOrDefault(value, hudShowBallDistance);
                    break;
                case "hud_show_ice_indicator":
                    hudShowIceIndicator = ParseBoolOrDefault(value, hudShowIceIndicator);
                    break;
                case "hud_show_center_title":
                    hudShowCenterTitle = ParseBoolOrDefault(value, hudShowCenterTitle);
                    break;
                case "hud_show_player_info":
                    hudShowPlayerInfo = ParseBoolOrDefault(value, hudShowPlayerInfo);
                    break;
                case "tracers_enabled":
                    tracersEnabled = ParseBoolOrDefault(value, tracersEnabled);
                    break;
                case "credits_grant_amount":
                    creditsGrantAmount = ParseIntOrDefault(value, creditsGrantAmount, 0, 999999);
                    break;
                case "actual_trail_enabled":
                    actualTrailEnabled = ParseBoolOrDefault(value, actualTrailEnabled);
                    break;
                case "predicted_trail_enabled":
                    predictedTrailEnabled = ParseBoolOrDefault(value, predictedTrailEnabled);
                    break;
                case "frozen_trail_enabled":
                    frozenTrailEnabled = ParseBoolOrDefault(value, frozenTrailEnabled);
                    break;
                case "impact_preview_enabled":
                    impactPreviewEnabled = ParseBoolOrDefault(value, impactPreviewEnabled);
                    break;
                case "impact_preview_fps":
                    impactPreviewTargetFps = ParseFloatOrDefault(value, impactPreviewTargetFps, 0f, 360f);
                    break;
                case "impact_preview_width":
                    impactPreviewTextureWidth = ParseIntOrDefault(value, impactPreviewTextureWidth, 320, 3840);
                    break;
                case "impact_preview_height":
                    impactPreviewTextureHeight = ParseIntOrDefault(value, impactPreviewTextureHeight, 180, 2160);
                    break;
                case "actual_trail_start_width":
                    actualTrailStartWidth = ParseFloatOrDefault(value, actualTrailStartWidth, 0.005f, 1f);
                    break;
                case "actual_trail_end_width":
                    actualTrailEndWidth = ParseFloatOrDefault(value, actualTrailEndWidth, 0.005f, 1f);
                    break;
                case "predicted_trail_start_width":
                    predictedTrailStartWidth = ParseFloatOrDefault(value, predictedTrailStartWidth, 0.005f, 1f);
                    break;
                case "predicted_trail_end_width":
                    predictedTrailEndWidth = ParseFloatOrDefault(value, predictedTrailEndWidth, 0.005f, 1f);
                    break;
                case "frozen_trail_start_width":
                    frozenTrailStartWidth = ParseFloatOrDefault(value, frozenTrailStartWidth, 0.005f, 1f);
                    break;
                case "frozen_trail_end_width":
                    frozenTrailEndWidth = ParseFloatOrDefault(value, frozenTrailEndWidth, 0.005f, 1f);
                    break;
                case "actual_trail_color":
                    actualTrailColor = ParseColorOrDefault(value, actualTrailColor);
                    break;
                case "predicted_trail_color":
                    predictedTrailColor = ParseColorOrDefault(value, predictedTrailColor);
                    break;
                case "frozen_trail_color":
                    frozenTrailColor = ParseColorOrDefault(value, frozenTrailColor);
                    break;
            }
        }
    }

    private string BuildDefaultConfigText()
    {
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("# Birdie Mod config");
        builder.AppendLine("# Restart the game after editing this file for trail/visual settings.");
        builder.AppendLine("# Keybind and HUD changes can also be made in-game via the settings panel (F6).");
        builder.AppendLine();
        builder.AppendLine("assist_toggle_key=F1");
        builder.AppendLine("coffee_boost_key=F2");
        builder.AppendLine("nearest_ball_mode_key=F3");
        builder.AppendLine("unlock_all_cosmetics_key=F4");
        builder.AppendLine("item_spawner_key=F5");
        builder.AppendLine("hud_toggle_key=H");
        builder.AppendLine("random_item_key=G");
        builder.AppendLine("ice_toggle_key=I");
        builder.AppendLine("settings_key=F6");
        builder.AppendLine();
        builder.AppendLine("no_wind_key=W");
        builder.AppendLine("perfect_shot_key=P");
        builder.AppendLine("no_air_drag_key=D");
        builder.AppendLine("speed_multiplier_key=S");
        builder.AppendLine("speed_multiplier_factor=2.0");
        builder.AppendLine("infinite_ammo_key=A");
        builder.AppendLine("no_recoil_key=R");
        builder.AppendLine("no_knockback_key=K");
        builder.AppendLine("landmine_immunity_key=M");
        builder.AppendLine("lock_on_any_distance_key=L");
        builder.AppendLine("expanded_slots_key=U");
        builder.AppendLine();
        builder.AppendLine("hud_show_bottom_bar=true");
        builder.AppendLine("hud_show_ball_distance=true");
        builder.AppendLine("hud_show_ice_indicator=true");
        builder.AppendLine("hud_show_center_title=true");
        builder.AppendLine("hud_show_player_info=true");
        builder.AppendLine("tracers_enabled=true");
        builder.AppendLine();
        builder.AppendLine("credits_grant_amount=1000");
        builder.AppendLine();
        builder.AppendLine("actual_trail_enabled=true");
        builder.AppendLine("actual_trail_start_width=0.22");
        builder.AppendLine("actual_trail_end_width=0.18");
        builder.AppendLine("actual_trail_color=#FF9433");
        builder.AppendLine();
        builder.AppendLine("predicted_trail_enabled=true");
        builder.AppendLine("predicted_trail_start_width=0.18");
        builder.AppendLine("predicted_trail_end_width=0.14");
        builder.AppendLine("predicted_trail_color=#39F26E");
        builder.AppendLine();
        builder.AppendLine("frozen_trail_enabled=true");
        builder.AppendLine("frozen_trail_start_width=0.20");
        builder.AppendLine("frozen_trail_end_width=0.16");
        builder.AppendLine("frozen_trail_color=#53ACFF");
        builder.AppendLine();
        builder.AppendLine("impact_preview_enabled=true");
        builder.AppendLine("impact_preview_fps=60");
        builder.AppendLine("impact_preview_width=640");
        builder.AppendLine("impact_preview_height=360");
        return builder.ToString();
    }

    private string ParseKeyNameOrDefault(string value, string fallbackValue)
    {
        string normalized = value == null ? "" : value.Trim();
        return string.IsNullOrEmpty(normalized) ? fallbackValue : normalized;
    }

    private bool ParseBoolOrDefault(string value, bool fallbackValue)
    {
        bool parsedBool;
        return bool.TryParse(value, out parsedBool) ? parsedBool : fallbackValue;
    }

    private float ParseFloatOrDefault(string value, float fallbackValue, float minValue, float maxValue)
    {
        float parsedFloat;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedFloat))
        {
            return fallbackValue;
        }

        return Mathf.Clamp(parsedFloat, minValue, maxValue);
    }

    private int ParseIntOrDefault(string value, int fallbackValue, int minValue, int maxValue)
    {
        int parsedInt;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt))
        {
            return fallbackValue;
        }

        return Mathf.Clamp(parsedInt, minValue, maxValue);
    }

    private Color ParseColorOrDefault(string value, Color fallbackValue)
    {
        Color parsedColor;
        if (ColorUtility.TryParseHtmlString(value, out parsedColor))
        {
            return parsedColor;
        }

        string[] parts = value.Split(',');
        if (parts.Length >= 3 && parts.Length <= 4)
        {
            float[] values = new float[4] { fallbackValue.r, fallbackValue.g, fallbackValue.b, fallbackValue.a };
            for (int i = 0; i < parts.Length; i++)
            {
                float parsedValue;
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
                {
                    return fallbackValue;
                }

                values[i] = parsedValue > 1f ? Mathf.Clamp01(parsedValue / 255f) : Mathf.Clamp01(parsedValue);
            }

            return new Color(values[0], values[1], values[2], values[3]);
        }

        return fallbackValue;
    }

    private void UpdateConfigLabels()
    {
        assistToggleKey = ParseConfiguredKey(assistToggleKeyName, Key.F1);
        coffeeBoostKey = ParseConfiguredKey(coffeeBoostKeyName, Key.F2);
        nearestBallModeKey = ParseConfiguredKey(nearestBallModeKeyName, Key.F3);
        unlockAllCosmeticsKey = ParseConfiguredKey(unlockAllCosmeticsKeyName, Key.F4);
        itemSpawnerKey = ParseConfiguredKey(itemSpawnerKeyName, Key.F5);
        hudToggleKey = ParseConfiguredKey(hudToggleKeyName, Key.H);
        randomItemKey = ParseConfiguredKey(randomItemKeyName, Key.G);
        iceToggleKey = ParseConfiguredKey(iceToggleKeyName, Key.I);
        settingsKey = ParseConfiguredKey(settingsKeyName, Key.F6);
        noWindKey = ParseConfiguredKey(noWindKeyName, Key.W);
        perfectShotKey = ParseConfiguredKey(perfectShotKeyName, Key.P);
        noAirDragKey = ParseConfiguredKey(noAirDragKeyName, Key.D);
        speedMultiplierKey = ParseConfiguredKey(speedMultiplierKeyName, Key.S);
        infiniteAmmoKey = ParseConfiguredKey(infiniteAmmoKeyName, Key.A);
        noRecoilKey = ParseConfiguredKey(noRecoilKeyName, Key.R);
        noKnockbackKey = ParseConfiguredKey(noKnockbackKeyName, Key.K);
        landmineImmunityKey = ParseConfiguredKey(landmineImmunityKeyName, Key.M);
        lockOnAnyDistanceKey = ParseConfiguredKey(lockOnAnyDistanceKeyName, Key.L);
        expandedSlotsKey = ParseConfiguredKey(expandedSlotsKeyName, Key.U);
        assistToggleKeyLabel = FormatKeyLabel(assistToggleKeyName);
        coffeeBoostKeyLabel = FormatKeyLabel(coffeeBoostKeyName);
        nearestBallModeKeyLabel = FormatKeyLabel(nearestBallModeKeyName);
        unlockAllCosmeticsKeyLabel = FormatKeyLabel(unlockAllCosmeticsKeyName);
        itemSpawnerKeyLabel = FormatKeyLabel(itemSpawnerKeyName);
        hudToggleKeyLabel = FormatKeyLabel(hudToggleKeyName);
        randomItemKeyLabel = FormatKeyLabel(randomItemKeyName);
        iceToggleKeyLabel = FormatKeyLabel(iceToggleKeyName);
        settingsKeyLabel = FormatKeyLabel(settingsKeyName);
        noWindKeyLabel = FormatKeyLabel(noWindKeyName);
        perfectShotKeyLabel = FormatKeyLabel(perfectShotKeyName);
        noAirDragKeyLabel = FormatKeyLabel(noAirDragKeyName);
        speedMultiplierKeyLabel = FormatKeyLabel(speedMultiplierKeyName);
        infiniteAmmoKeyLabel = FormatKeyLabel(infiniteAmmoKeyName);
        noRecoilKeyLabel = FormatKeyLabel(noRecoilKeyName);
        noKnockbackKeyLabel = FormatKeyLabel(noKnockbackKeyName);
        landmineImmunityKeyLabel = FormatKeyLabel(landmineImmunityKeyName);
        lockOnAnyDistanceKeyLabel = FormatKeyLabel(lockOnAnyDistanceKeyName);
        expandedSlotsKeyLabel = FormatKeyLabel(expandedSlotsKeyName);
    }

    // Writes all current config values to disk immediately.
    // Called after in-game keybind changes or HUD toggle changes.
    private void SaveCurrentConfig()
    {
        try
        {
            string configDirectory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            File.WriteAllText(configPath, BuildCurrentConfigText(), Encoding.ASCII);
        }
        catch
        {
        }
    }

    private string BuildCurrentConfigText()
    {
        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine("# Birdie Mod config");
        builder.AppendLine("# Restart the game after editing this file for trail/visual settings.");
        builder.AppendLine("# Keybind and HUD changes can also be made in-game via the settings panel.");
        builder.AppendLine();
        builder.AppendLine("assist_toggle_key=" + assistToggleKeyName);
        builder.AppendLine("coffee_boost_key=" + coffeeBoostKeyName);
        builder.AppendLine("nearest_ball_mode_key=" + nearestBallModeKeyName);
        builder.AppendLine("unlock_all_cosmetics_key=" + unlockAllCosmeticsKeyName);
        builder.AppendLine("item_spawner_key=" + itemSpawnerKeyName);
        builder.AppendLine("hud_toggle_key=" + hudToggleKeyName);
        builder.AppendLine("random_item_key=" + randomItemKeyName);
        builder.AppendLine("ice_toggle_key=" + iceToggleKeyName);
        builder.AppendLine("settings_key=" + settingsKeyName);
        builder.AppendLine();
        builder.AppendLine("no_wind_key=" + noWindKeyName);
        builder.AppendLine("perfect_shot_key=" + perfectShotKeyName);
        builder.AppendLine("no_air_drag_key=" + noAirDragKeyName);
        builder.AppendLine("speed_multiplier_key=" + speedMultiplierKeyName);
        builder.AppendLine("speed_multiplier_factor=" + speedMultiplierFactor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("infinite_ammo_key=" + infiniteAmmoKeyName);
        builder.AppendLine("no_recoil_key=" + noRecoilKeyName);
        builder.AppendLine("no_knockback_key=" + noKnockbackKeyName);
        builder.AppendLine("landmine_immunity_key=" + landmineImmunityKeyName);
        builder.AppendLine("lock_on_any_distance_key=" + lockOnAnyDistanceKeyName);
        builder.AppendLine("expanded_slots_key=" + expandedSlotsKeyName);
        builder.AppendLine();
        builder.AppendLine("hud_show_bottom_bar=" + hudShowBottomBar.ToString().ToLowerInvariant());
        builder.AppendLine("hud_show_ball_distance=" + hudShowBallDistance.ToString().ToLowerInvariant());
        builder.AppendLine("hud_show_ice_indicator=" + hudShowIceIndicator.ToString().ToLowerInvariant());
        builder.AppendLine("hud_show_center_title=" + hudShowCenterTitle.ToString().ToLowerInvariant());
        builder.AppendLine("hud_show_player_info=" + hudShowPlayerInfo.ToString().ToLowerInvariant());
        builder.AppendLine("tracers_enabled=" + tracersEnabled.ToString().ToLowerInvariant());
        builder.AppendLine();
        builder.AppendLine("credits_grant_amount=" + creditsGrantAmount);
        builder.AppendLine();
        builder.AppendLine("actual_trail_enabled=" + actualTrailEnabled.ToString().ToLowerInvariant());
        builder.AppendLine("actual_trail_start_width=" + actualTrailStartWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("actual_trail_end_width=" + actualTrailEndWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("actual_trail_color=#" + ColorUtility.ToHtmlStringRGB(actualTrailColor));
        builder.AppendLine();
        builder.AppendLine("predicted_trail_enabled=" + predictedTrailEnabled.ToString().ToLowerInvariant());
        builder.AppendLine("predicted_trail_start_width=" + predictedTrailStartWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("predicted_trail_end_width=" + predictedTrailEndWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("predicted_trail_color=#" + ColorUtility.ToHtmlStringRGB(predictedTrailColor));
        builder.AppendLine();
        builder.AppendLine("frozen_trail_enabled=" + frozenTrailEnabled.ToString().ToLowerInvariant());
        builder.AppendLine("frozen_trail_start_width=" + frozenTrailStartWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("frozen_trail_end_width=" + frozenTrailEndWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("frozen_trail_color=#" + ColorUtility.ToHtmlStringRGB(frozenTrailColor));
        builder.AppendLine();
        builder.AppendLine("impact_preview_enabled=" + impactPreviewEnabled.ToString().ToLowerInvariant());
        builder.AppendLine("impact_preview_fps=" + ((int)impactPreviewTargetFps).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("impact_preview_width=" + impactPreviewTextureWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("impact_preview_height=" + impactPreviewTextureHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private Key ParseConfiguredKey(string configuredKeyName, Key fallbackKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKeyName))
        {
            return fallbackKey;
        }

        Key parsedKey;
        return Enum.TryParse(configuredKeyName.Trim(), true, out parsedKey) && parsedKey != Key.None
            ? parsedKey
            : fallbackKey;
    }

    private string FormatKeyLabel(string configuredKeyName)
    {
        if (string.IsNullOrWhiteSpace(configuredKeyName))
        {
            return "?";
        }

        string keyName = configuredKeyName.Trim();
        Key parsedKey;
        if (Enum.TryParse(keyName, true, out parsedKey))
        {
            keyName = parsedKey.ToString();
        }

        if (keyName.StartsWith("Digit", StringComparison.OrdinalIgnoreCase))
        {
            return keyName.Substring("Digit".Length);
        }

        return keyName.ToUpperInvariant();
    }

    private bool WasConfiguredKeyPressed(Key configuredKey)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || configuredKey == Key.None)
        {
            return false;
        }

        try
        {
            return keyboard[configuredKey] != null && keyboard[configuredKey].wasPressedThisFrame;
        }
        catch
        {
            return false;
        }
    }

    private Color BrightenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a);
    }

    private Color DarkenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a);
    }

    private Gradient CreateTrailGradient(Color baseColor, float startFactor, float midFactor, float endFactor, float startAlpha, float midAlpha, float endAlpha)
    {
        Color startColor = BrightenColor(baseColor, startFactor);
        Color midColor = BrightenColor(baseColor, midFactor);
        Color endColor = DarkenColor(baseColor, endFactor);

        Gradient gradient = new Gradient();
        gradient.colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(startColor, 0f),
            new GradientColorKey(midColor, 0.55f),
            new GradientColorKey(endColor, 1f)
        };
        gradient.alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(Mathf.Clamp01(startAlpha), 0f),
            new GradientAlphaKey(Mathf.Clamp01(midAlpha), 0.6f),
            new GradientAlphaKey(Mathf.Clamp01(endAlpha), 1f)
        };
        return gradient;
    }

    private void ApplyTrailVisualSettings()
    {
        ApplyTrailVisualSettings(
            shotPathLine,
            shotPathMaterial,
            actualTrailEnabled,
            actualTrailStartWidth,
            actualTrailEndWidth,
            actualTrailColor,
            1.20f,
            1.00f,
            0.78f,
            0.96f,
            0.80f,
            0.62f);

        ApplyTrailVisualSettings(
            predictedPathLine,
            predictedPathMaterial,
            predictedTrailEnabled,
            predictedTrailStartWidth,
            predictedTrailEndWidth,
            predictedTrailColor,
            1.15f,
            1.00f,
            0.82f,
            0.94f,
            0.78f,
            0.55f);

        ApplyTrailVisualSettings(
            frozenPredictedPathLine,
            frozenPredictedPathMaterial,
            frozenTrailEnabled,
            frozenTrailStartWidth,
            frozenTrailEndWidth,
            frozenTrailColor,
            1.18f,
            1.00f,
            0.80f,
            0.92f,
            0.76f,
            0.52f);
    }

    private void ApplyTrailVisualSettings(
        LineRenderer lineRenderer,
        Material material,
        bool enabled,
        float startWidth,
        float endWidth,
        Color baseColor,
        float startFactor,
        float midFactor,
        float endFactor,
        float startAlpha,
        float midAlpha,
        float endAlpha)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.enabled = enabled;
        lineRenderer.colorGradient = CreateTrailGradient(baseColor, startFactor, midFactor, endFactor, startAlpha, midAlpha, endAlpha);

        if (!enabled)
        {
            lineRenderer.positionCount = 0;
        }

        if (material != null)
        {
            material.color = baseColor;
        }
    }
}

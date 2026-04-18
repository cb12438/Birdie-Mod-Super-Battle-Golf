using UnityEngine;

// IMGUI-based settings panel for BirdieMod.
//
// Uses Unity's immediate-mode GUI (OnGUI), which is always available in any
// MelonLoader mod regardless of whether the game uses UI Toolkit or UGUI.
//
// Method names are identical to the RosettaUI version so Runtime.cs and
// Settings.cs do not need changes:
//   F6 → ToggleRosettaSettings()
//   Escape inside panel → CloseRosettaSettings()
//
// No UIDocument, no PanelSettings, no external library needed at runtime.

public partial class BirdieMod
{
    // ── State ────────────────────────────────────────────────────────────────

    private Rect _imRect;
    private bool _imRectInit;
    private Vector2 _imFeaturesScroll;
    private Vector2 _imKeysScroll;

    // ── Styles (lazy init on first OnGUI call) ────────────────────────────────

    private bool        _imStylesReady;
    private GUIStyle    _imWindowStyle;
    private GUIStyle    _imTitleStyle;
    private GUIStyle    _imTabActive;
    private GUIStyle    _imTabIdle;
    private GUIStyle    _imSectionLabel;
    private GUIStyle    _imToggleOn;
    private GUIStyle    _imToggleOff;
    private GUIStyle    _imActionLabel;
    private GUIStyle    _imKeyBadge;
    private GUIStyle    _imRebindActive;
    private GUIStyle    _imButton;
    private GUIStyle    _imCloseBtn;
    private GUIStyle    _imInfoLabel;
    private GUIStyle    _imDescLabel;
    private Texture2D   _imTxDark;
    private Texture2D   _imTxBlue;
    private Texture2D   _imTxGrey;
    private Texture2D   _imTxDarker;
    private Texture2D   _imTxRed;
    private Texture2D   _imTxGreen;
    private Texture2D   _imTxSeparator;

    private static Texture2D MakeTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private void InitImStyles()
    {
        if (_imStylesReady) return;
        _imStylesReady = true;

        _imTxDark   = MakeTex(new Color(0.10f, 0.10f, 0.13f, 0.97f));
        _imTxBlue   = MakeTex(new Color(0.22f, 0.50f, 0.90f, 1.00f));
        _imTxGrey   = MakeTex(new Color(0.20f, 0.20f, 0.25f, 1.00f));
        _imTxDarker = MakeTex(new Color(0.07f, 0.07f, 0.09f, 1.00f));
        _imTxRed       = MakeTex(new Color(0.60f, 0.12f, 0.12f, 1.00f));
        _imTxGreen     = MakeTex(new Color(0.16f, 0.45f, 0.20f, 1.00f));
        _imTxSeparator = MakeTex(new Color(1f, 1f, 1f, 0.06f));

        _imWindowStyle = new GUIStyle(GUI.skin.window);
        _imWindowStyle.normal.background   = _imTxDark;
        _imWindowStyle.onNormal.background = _imTxDark;
        _imWindowStyle.border   = new RectOffset(4, 4, 4, 4);
        _imWindowStyle.padding  = new RectOffset(0, 0, 0, 0);
        _imWindowStyle.normal.textColor = Color.clear;   // hide default title

        _imTitleStyle = new GUIStyle(GUI.skin.label);
        _imTitleStyle.fontSize  = 14;
        _imTitleStyle.fontStyle = FontStyle.Bold;
        _imTitleStyle.normal.textColor = Color.white;
        _imTitleStyle.alignment = TextAnchor.MiddleLeft;

        _imTabActive = new GUIStyle(GUI.skin.button);
        _imTabActive.normal.background   = _imTxBlue;
        _imTabActive.hover.background    = _imTxBlue;
        _imTabActive.active.background   = _imTxBlue;
        _imTabActive.normal.textColor    = Color.white;
        _imTabActive.fontStyle           = FontStyle.Bold;
        _imTabActive.fontSize            = 11;

        _imTabIdle = new GUIStyle(GUI.skin.button);
        _imTabIdle.normal.background  = _imTxDarker;
        _imTabIdle.hover.background   = _imTxGrey;
        _imTabIdle.active.background  = _imTxGrey;
        _imTabIdle.normal.textColor   = new Color(0.70f, 0.70f, 0.75f);
        _imTabIdle.fontSize           = 11;

        _imSectionLabel = new GUIStyle(GUI.skin.label);
        _imSectionLabel.fontSize  = 10;
        _imSectionLabel.fontStyle = FontStyle.Bold;
        _imSectionLabel.normal.textColor = new Color(0.55f, 0.55f, 0.62f);

        _imActionLabel = new GUIStyle(GUI.skin.label);
        _imActionLabel.fontSize = 13;
        _imActionLabel.normal.textColor = Color.white;
        _imActionLabel.alignment = TextAnchor.MiddleLeft;

        _imToggleOn = new GUIStyle(GUI.skin.button);
        _imToggleOn.normal.background = _imTxBlue;
        _imToggleOn.hover.background  = _imTxBlue;
        _imToggleOn.normal.textColor  = Color.white;
        _imToggleOn.fontSize          = 11;
        _imToggleOn.fontStyle         = FontStyle.Bold;

        _imToggleOff = new GUIStyle(GUI.skin.button);
        _imToggleOff.normal.background = _imTxGrey;
        _imToggleOff.hover.background  = _imTxGrey;
        _imToggleOff.normal.textColor  = new Color(0.55f, 0.55f, 0.60f);
        _imToggleOff.fontSize          = 11;

        _imKeyBadge = new GUIStyle(GUI.skin.button);
        _imKeyBadge.normal.background = _imTxBlue;
        _imKeyBadge.hover.background  = _imTxGrey;
        _imKeyBadge.normal.textColor  = Color.white;
        _imKeyBadge.fontSize          = 11;

        _imRebindActive = new GUIStyle(_imKeyBadge);
        _imRebindActive.normal.background = _imTxGreen;
        _imRebindActive.hover.background  = _imTxGreen;

        _imButton = new GUIStyle(GUI.skin.button);
        _imButton.normal.background = _imTxGrey;
        _imButton.hover.background  = _imTxBlue;
        _imButton.normal.textColor  = Color.white;
        _imButton.fontSize          = 13;

        _imCloseBtn = new GUIStyle(GUI.skin.button);
        _imCloseBtn.normal.background = _imTxRed;
        _imCloseBtn.hover.background  = _imTxRed;
        _imCloseBtn.normal.textColor  = new Color(1f, 0.5f, 0.5f);
        _imCloseBtn.fontStyle         = FontStyle.Bold;
        _imCloseBtn.fontSize          = 14;

        _imInfoLabel = new GUIStyle(GUI.skin.label);
        _imInfoLabel.fontSize  = 12;
        _imInfoLabel.wordWrap  = true;
        _imInfoLabel.normal.textColor = new Color(0.65f, 0.65f, 0.72f);

        _imDescLabel = new GUIStyle(GUI.skin.label);
        _imDescLabel.fontSize  = 10;
        _imDescLabel.wordWrap  = false;
        _imDescLabel.normal.textColor = new Color(0.50f, 0.50f, 0.58f);
    }

    // ── OnGUI (called from entry shim) ───────────────────────────────────────

    internal void BirdieOnGUI()
    {
        if (!settingsPanelOpen) return;

        InitImStyles();

        if (!_imRectInit)
        {
            _imRect = new Rect(
                Screen.width  * 0.5f - 320f,
                Screen.height * 0.5f - 220f,
                640f, 440f);
            _imRectInit = true;
        }

        _imRect = GUI.Window(9901, _imRect, DrawImWindow, string.Empty, _imWindowStyle);
    }

    // ── Window contents ──────────────────────────────────────────────────────

    private void DrawImWindow(int id)
    {
        float w = _imRect.width;
        float h = _imRect.height;
        float sideW = 70f;
        float titleH = 36f;

        // ── Title bar ──────────────────────────────────────────────────────
        GUI.DrawTexture(new Rect(0, 0, w, titleH), _imTxDarker);
        GUI.Label(new Rect(10f, 8f, w - 50f, titleH - 8f), "Birdie Settings", _imTitleStyle);

        if (GUI.Button(new Rect(w - 32f, 6f, 26f, 24f), "✕", _imCloseBtn))
            CloseRosettaSettings();

        // thin separator below title
        GUI.DrawTexture(new Rect(0, titleH, w, 1f), _imTxSeparator);

        // ── Sidebar ────────────────────────────────────────────────────────
        GUI.DrawTexture(new Rect(0, titleH + 1f, sideW, h - titleH - 1f), _imTxDarker);
        GUI.DrawTexture(new Rect(sideW, titleH + 1f, 1f, h - titleH - 1f), _imTxSeparator);

        string[] tabLabels = { "Features", "Keys", "HUD", "$", "Items", "Net", "Weather" };
        float tabH = 44f;
        for (int i = 0; i < tabLabels.Length; i++)
        {
            GUIStyle s = (settingsTabIndex == i) ? _imTabActive : _imTabIdle;
            if (GUI.Button(new Rect(2f, titleH + 4f + i * (tabH + 2f), sideW - 4f, tabH), tabLabels[i], s))
                settingsTabIndex = i;
        }

        // ── Content area ───────────────────────────────────────────────────
        float cx = sideW + 8f;
        float cy = titleH + 6f;
        float cw = w - sideW - 16f;
        float ch = h - cy - 6f;

        GUILayout.BeginArea(new Rect(cx, cy, cw, ch));
        switch (settingsTabIndex)
        {
            case 0: DrawImFeatures(cw); break;
            case 1: DrawImKeys(cw);     break;
            case 2: DrawImHud(cw);      break;
            case 3: DrawImCredits(cw);  break;
            case 4: DrawImItems(cw);    break;
            case 5: DrawImNetwork(cw);  break;
            case 6: DrawImWeather(cw);  break;
        }
        GUILayout.EndArea();

        GUI.DragWindow(new Rect(0, 0, w, titleH));
    }

    // ── Features tab ─────────────────────────────────────────────────────────

    private void DrawImFeatures(float w)
    {
        _imFeaturesScroll = GUILayout.BeginScrollView(_imFeaturesScroll);

        ImSectionHeader("CORE");
        if (IsFeatureAllowed(0))
            ImToggleRowWithDesc("Ice Immunity [" + iceToggleKeyLabel + "]",  iceImmunityEnabled,  "Slide on ice without slipping",                    () => ToggleIceImmunity());
        if (IsFeatureAllowed(1))
            ImToggleRowWithDesc("Shot Tracer",                                  tracersEnabled,       "Shows the ball's actual flight path",              () => { tracersEnabled = !tracersEnabled; SaveCurrentConfig(); MarkTrailVisualSettingsDirty(); MarkHudDirty(); });
        if (IsFeatureAllowed(14))
            ImToggleRowWithDesc("Assist [" + assistToggleKeyLabel + "]",        assistEnabled,        "Auto-aims and releases at the perfect moment",     () => ToggleAssist());
        if (IsFeatureAllowed(2))
            ImToggleRowWithDesc("Impact Preview",                               impactPreviewEnabled, "Preview exactly where your shot will land",        () => { impactPreviewEnabled = !impactPreviewEnabled; SaveCurrentConfig(); });

        GUILayout.Space(6f);
        ImSectionHeader("EXTRAS");
        if (IsFeatureAllowed(3))
            ImToggleRowWithDesc("No Wind [" + noWindKeyLabel + "]",                  noWindEnabled,            "Removes wind deflection from your ball",                                          () => ToggleNoWind());
        if (IsFeatureAllowed(4))
            ImToggleRowWithDesc("Perfect Shot [" + perfectShotKeyLabel + "]",        perfectShotEnabled,       "Forces your swing into the perfect power zone",                                   () => TogglePerfectShot());
        if (IsFeatureAllowed(5))
            ImToggleRowWithDesc("No Air Drag [" + noAirDragKeyLabel + "]",           noAirDragEnabled,         "Removes air resistance — ball flies further",                                     () => ToggleNoAirDrag());
        if (IsFeatureAllowed(6))
            ImToggleRowWithDesc("Speed Boost [" + speedMultiplierKeyLabel + "]",     speedMultiplierEnabled,   "Multiplies your movement speed",                                                  () => ToggleSpeedMultiplier());
        if (IsFeatureAllowed(7))
            ImToggleRowWithDesc("Inf. Item Usage [" + infiniteAmmoKeyLabel + "]",    infiniteAmmoEnabled,      "Weapons and items never run out",                                                 () => ToggleInfiniteAmmo());
        if (IsFeatureAllowed(8))
            ImToggleRowWithDesc("No Recoil [" + noRecoilKeyLabel + "]",              noRecoilEnabled,          "Removes screen shake when firing weapons",                                        () => ToggleNoRecoil());
        if (IsFeatureAllowed(9))
            ImToggleRowWithDesc("No Knockback [" + noKnockbackKeyLabel + "]",        noKnockbackEnabled,       "Prevents weapons from flinging you back",                                         () => ToggleNoKnockback());
        if (IsFeatureAllowed(10))
            ImToggleRowWithDesc("Landmine Immunity [" + landmineImmunityKeyLabel + "]",  landmineImmunityEnabled,   "Walk over landmines without being hit",                                () => ToggleLandmineImmunity());
        if (IsFeatureAllowed(11))
            ImToggleRowWithDesc("Lock-On Any Dist. [" + lockOnAnyDistanceKeyLabel + "]", lockOnAnyDistanceEnabled,  "Lock-on targets golf balls at any range",                               () => ToggleLockOnAnyDistance());
        if (IsFeatureAllowed(12))
        {
            ImToggleRowWithDesc("Expanded Slots [" + expandedSlotsKeyLabel + "]", expandedSlotsEnabled, "Expand hotbar to 8 total slots / keys 1-8", () => ToggleExpandedSlots());
            if (expandedSlotsEnabled && IsNetworkHost())
                ImToggleRow("  Apply to all players", expandedSlotsAllPlayers, () => { expandedSlotsAllPlayers = !expandedSlotsAllPlayers; SaveCurrentConfig(); if (expandedSlotsEnabled) TryExpandServerInventorySlotsPublic(); });
        }

        if (IsFeatureAllowed(6))
        {
            GUILayout.Space(6f);
            ImSectionHeader("SPEED FACTOR");
            GUILayout.BeginHorizontal();
            GUILayout.Label("x" + speedMultiplierFactor.ToString("F1"), _imActionLabel, GUILayout.Width(36f));
            float newFactor = GUILayout.HorizontalSlider(speedMultiplierFactor, 0.5f, 10f);
            if (System.Math.Abs(newFactor - speedMultiplierFactor) > 0.01f)
            {
                speedMultiplierFactor = Mathf.Round(newFactor * 10f) / 10f;
                if (speedMultiplierEnabled) ApplySpeedMultiplierState();
                SaveCurrentConfig();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    // ── Keys tab ──────────────────────────────────────────────────────────────

    private void DrawImKeys(float w)
    {
        // Status line
        GUIStyle statusStyle = new GUIStyle(_imInfoLabel);
        statusStyle.normal.textColor = keybindRebindMode
            ? new Color(0.4f, 0.9f, 0.5f)
            : new Color(0.55f, 0.55f, 0.62f);
        GUILayout.Label(keybindRebindMode
            ? "Press any key to bind: " + SettingsKeybindDisplayNames[keybindRebindIndex]
            : "Click a key badge to rebind",
            statusStyle);

        GUILayout.Space(4f);
        _imKeysScroll = GUILayout.BeginScrollView(_imKeysScroll);

        for (int i = 0; i < SettingsKeybindDisplayNames.Length; i++)
        {
            int captured = i;
            GUILayout.BeginHorizontal();
            GUILayout.Label(SettingsKeybindDisplayNames[i], _imActionLabel);
            bool isRebinding = keybindRebindMode && keybindRebindIndex == i;
            GUIStyle badge = isRebinding ? _imRebindActive : _imKeyBadge;
            string badgeText = isRebinding ? "..." : FormatKeyLabel(GetKeybindNameByIndex(i));
            if (GUILayout.Button(badgeText, badge, GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                StartKeybindRebindForIndex(captured);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        GUILayout.EndScrollView();
    }

    // ── HUD tab ───────────────────────────────────────────────────────────────

    private void DrawImHud(float w)
    {
        ImSectionHeader("HUD VISIBILITY");
        ImToggleRow("Bottom keybind bar",    hudShowBottomBar,    () => { hudShowBottomBar    = !hudShowBottomBar;    SaveCurrentConfig(); MarkHudDirty(); });
        ImToggleRow("Ball distance to hole", hudShowBallDistance, () => { hudShowBallDistance = !hudShowBallDistance; SaveCurrentConfig(); MarkHudDirty(); });
        ImToggleRow("Ice immunity indicator",hudShowIceIndicator, () => { hudShowIceIndicator = !hudShowIceIndicator; SaveCurrentConfig(); MarkHudDirty(); });
        ImToggleRow("Center title",          hudShowCenterTitle,  () => { hudShowCenterTitle  = !hudShowCenterTitle;  SaveCurrentConfig(); MarkHudDirty(); });
        ImToggleRow("Player info (left)",    hudShowPlayerInfo,   () => { hudShowPlayerInfo   = !hudShowPlayerInfo;   SaveCurrentConfig(); MarkHudDirty(); });
        ImToggleRow("Shot tracers",          tracersEnabled,      () => { tracersEnabled = !tracersEnabled; SaveCurrentConfig(); MarkTrailVisualSettingsDirty(); MarkHudDirty(); });
    }

    // ── Credits tab ───────────────────────────────────────────────────────────

    private void DrawImCredits(float w)
    {
        GUILayout.Label("Grant credits to yourself\n(client-local, no server sync)", _imInfoLabel);
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Amount:", _imActionLabel, GUILayout.Width(70f));
        GUILayout.Label(creditsGrantAmount.ToString(), _imActionLabel, GUILayout.Width(60f));
        GUILayout.EndHorizontal();

        int newAmount = Mathf.RoundToInt(GUILayout.HorizontalSlider(creditsGrantAmount, 0f, 50000f));
        if (newAmount != creditsGrantAmount)
        {
            creditsGrantAmount = newAmount;
            SaveCurrentConfig();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Grant " + creditsGrantAmount + " Credits", _imButton, GUILayout.Height(34f)))
            GrantCredits(creditsGrantAmount);

#if BEPINEX
        GUILayout.Space(12f);
        GUIStyle discordStyle = new GUIStyle(_imButton);
        discordStyle.normal.textColor = new Color(0.56f, 0.62f, 0.90f, 1f);
        if (GUILayout.Button("Join Discord", discordStyle, GUILayout.Height(30f)))
            UnityEngine.Application.OpenURL("https://discord.gg/wbhNEHFGMq");
#endif
    }

    // ── Items tab ─────────────────────────────────────────────────────────────

    private Vector2 _imItemsScroll;

    private void DrawImItems(float w)
    {
        bool hostReady = BirdieGrantBridge.IsReady();
        GUIStyle statusStyle = new GUIStyle(_imInfoLabel);
        statusStyle.normal.textColor = hostReady
            ? new Color(0.30f, 0.85f, 0.45f)
            : new Color(0.80f, 0.35f, 0.35f);
        GUILayout.Label(hostReady
            ? "Host has BirdieMod — items will spawn"
            : "Host does not have BirdieMod — items require host",
            statusStyle);

        GUILayout.Space(6f);
        _imItemsScroll = GUILayout.BeginScrollView(_imItemsScroll);

        float btnW = (w - 12f) * 0.5f;
        for (int i = 0; i < SpawnableItemNames.Length; i++)
        {
            if (i % 2 == 0) GUILayout.BeginHorizontal();
            int captured = SpawnableItemTypeInts[i];
            string lbl = SpawnableItemKeyLabels[i] + "  " + SpawnableItemNames[i];
            if (GUILayout.Button(lbl, _imButton, GUILayout.Width(btnW), GUILayout.Height(30f)))
            {
                SpawnItemClientSide(captured);
            }
            if (i % 2 == 1 || i == SpawnableItemNames.Length - 1) GUILayout.EndHorizontal();
            if (i % 2 == 1) GUILayout.Space(2f);
        }

        GUILayout.EndScrollView();
    }

    // ── Network tab ──────────────────────────────────────────────────────────

    private Vector2 _imNetScroll;

    private void DrawImNetwork(float w)
    {
        _imNetScroll = GUILayout.BeginScrollView(_imNetScroll);

        bool isHost = IsNetworkHost();

        if (isHost)
        {
            ImSectionHeader("HOST CONTROLS");
            ImToggleRow("Host Controls", hostControlsActive, () => SetHostControlsActive(!hostControlsActive));

            if (hostControlsActive)
            {
                GUILayout.Space(4f);
                ImSectionHeader("ALLOWED FEATURES");
                ImHostFeatureToggle("Ice Immunity",        0);
                ImHostFeatureToggle("Shot Tracer",         1);
                ImHostFeatureToggle("Impact Preview",      2);
                ImHostFeatureToggle("No Wind",             3);
                ImHostFeatureToggle("Perfect Shot",        4);
                ImHostFeatureToggle("No Air Drag",         5);
                ImHostFeatureToggle("Speed Multiplier",    6);
                ImHostFeatureToggle("Infinite Item Usage", 7);
                ImHostFeatureToggle("No Recoil",           8);
                ImHostFeatureToggle("No Knockback",        9);
                ImHostFeatureToggle("Landmine Immunity",   10);
                ImHostFeatureToggle("Lock-On Any Dist.",   11);
                ImHostFeatureToggle("Expanded Slots",      12);
                ImHostFeatureToggle("Coffee Boost",        13);
                ImHostFeatureToggle("Assist",              14);
                ImHostFeatureToggle("Weather",             15);
            }
        }
        else
        {
            if (BirdieHostBridge.IsUnderHostControl)
            {
                GUIStyle bannerStyle = new GUIStyle(_imInfoLabel);
                bannerStyle.normal.textColor = new Color(1f, 0.75f, 0.20f, 1f);
                GUILayout.Label("Host Controls Active", bannerStyle);
                GUILayout.Space(4f);
                GUILayout.Label("Some features may be restricted by the lobby host.", _imInfoLabel);
            }
            else
            {
                GUILayout.Label("No host control active. All features available.", _imInfoLabel);
            }
        }

        GUILayout.EndScrollView();
    }

    // ── Weather tab (host only) ──────────────────────────────────────────────

    private void DrawImWeather(float w)
    {
        _imNetScroll = GUILayout.BeginScrollView(_imNetScroll);

        if (!IsNetworkHost())
        {
            GUILayout.Label("Weather controls are available to the lobby host only.", _imInfoLabel);
            GUILayout.EndScrollView();
            return;
        }

        ImSectionHeader("WEATHER EVENT");

        string[] weatherNames = {
            "None (Clear)", "Rain - Light", "Rain - Medium", "Rain - Heavy",
            "Wind Gusts - Light", "Wind Gusts - Medium", "Wind Gusts - Heavy",
            "Thunderstorm", "Tornado"
        };

        for (int i = 0; i < weatherNames.Length; i++)
        {
            bool isSelected = (_hostSelectedWeather == (byte)i);
            GUIStyle s = isSelected ? _imToggleOn : _imToggleOff;
            if (GUILayout.Button(weatherNames[i], s, GUILayout.Height(28f)))
            {
                _hostSelectedWeather = (byte)i;
            }
        }

        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_hostWeatherRunning ? "Stop Weather" : "Start Weather", _imButton, GUILayout.Height(34f)))
        {
            if (_hostWeatherRunning)
            {
                _hostWeatherRunning = false;
                BirdieWeatherBridge.BroadcastWeather(0, true);
                ApplyWeather(0);
            }
            else
            {
                _hostWeatherRunning = true;
                bool weatherAllowed = (hostAllowedFeatureMask & (1UL << 15)) != 0;
                BirdieWeatherBridge.BroadcastWeather(_hostSelectedWeather, weatherAllowed);
                ApplyWeather(_hostSelectedWeather);
            }
        }
        GUILayout.EndHorizontal();

        if (_hostWeatherRunning)
        {
            GUIStyle activeStyle = new GUIStyle(_imInfoLabel);
            activeStyle.normal.textColor = new Color(0.30f, 0.85f, 0.45f, 1f);
            GUILayout.Label("Weather active: " + ((_hostSelectedWeather < 9) ? new string[]{ "None","Rain (Light)","Rain (Medium)","Rain (Heavy)","Wind Gusts (Light)","Wind Gusts (Medium)","Wind Gusts (Heavy)","Thunderstorm","Tornado" }[_hostSelectedWeather] : "Unknown"), activeStyle);
        }

        GUILayout.Space(10f);
        ImSectionHeader("AUTO WEATHER");

        GUILayout.BeginHorizontal();
        GUIStyle autoStyle = autoWeatherEnabled ? _imToggleOn : _imToggleOff;
        if (GUILayout.Button(autoWeatherEnabled ? "Auto Weather  ON" : "Auto Weather  OFF", autoStyle, GUILayout.Height(28f)))
            autoWeatherEnabled = !autoWeatherEnabled;
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Spawn Chance", _imInfoLabel, GUILayout.Width(110f));
        autoWeatherChance = Mathf.RoundToInt(GUILayout.HorizontalSlider(autoWeatherChance, 0f, 100f, GUILayout.Width(w - 180f)));
        GUILayout.Label(autoWeatherChance + "%", _imInfoLabel, GUILayout.Width(40f));
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label("Per-Type Spawn Weight (0 = disabled):", _imInfoLabel);
        string[] autoNames = { "Rain - Light", "Rain - Medium", "Rain - Heavy",
                               "Wind - Light", "Wind - Medium", "Wind - Heavy",
                               "Thunderstorm", "Tornado" };
        for (int i = 0; i < autoNames.Length; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(autoNames[i], _imInfoLabel, GUILayout.Width(115f));
            autoWeatherChances[i] = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(autoWeatherChances[i], 0f, 100f, GUILayout.Width(w - 195f)));
            GUILayout.Label(autoWeatherChances[i] + "%", _imInfoLabel, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10f);
        ImSectionHeader("NOTES");
        GUILayout.Label("Wind changes affect all players. Rain drag and visual effects apply to Birdie clients only.", _imInfoLabel);

        GUILayout.EndScrollView();
    }

    private void ImHostFeatureToggle(string label, int bit)
    {
        bool enabled = (hostAllowedFeatureMask & (1UL << bit)) != 0;
        ImToggleRow(label, enabled, () => SetHostFeatureBit(bit, !enabled));
    }

    private bool IsNetworkHost()
    {
        try
        {
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type nsType = asm.GetType("Mirror.NetworkServer");
                if (nsType == null) continue;
                System.Reflection.PropertyInfo p = nsType.GetProperty("active",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (p != null) return (bool)p.GetValue(null);
                break;
            }
        }
        catch { }
        return false;
    }

    // ── Shared row helpers ────────────────────────────────────────────────────

    private void ImSectionHeader(string text)
    {
        GUILayout.Label(text, _imSectionLabel);
    }

    private void ImToggleRow(string label, bool value, System.Action onToggle)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _imActionLabel);
        GUIStyle s = value ? _imToggleOn : _imToggleOff;
        if (GUILayout.Button(value ? "ON" : "OFF", s, GUILayout.Width(48f), GUILayout.Height(24f)))
            onToggle();
        GUILayout.EndHorizontal();
        GUILayout.Space(1f);
    }

    private void ImToggleRowWithDesc(string label, bool value, string desc, System.Action onToggle)
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label(label, _imActionLabel);
        GUILayout.Label(desc, _imDescLabel);
        GUILayout.EndVertical();
        GUIStyle s = value ? _imToggleOn : _imToggleOff;
        if (GUILayout.Button(value ? "ON" : "OFF", s, GUILayout.Width(48f), GUILayout.Height(34f)))
            onToggle();
        GUILayout.EndHorizontal();
        GUILayout.Space(2f);
    }

    // ── Open / close ──────────────────────────────────────────────────────────

    private void OpenRosettaSettings()
    {
        settingsPanelOpen = true;
        keybindRebindMode = false;
        keybindRebindIndex = -1;
        ApplyCursorUnlockForSettings(true);
        ApplyGameInputSuppressionForSettings(true);
        MarkHudDirty();
    }

    private void CloseRosettaSettings()
    {
        settingsPanelOpen = false;
        keybindRebindMode = false;
        keybindRebindIndex = -1;
        ApplyCursorUnlockForSettings(false);
        ApplyGameInputSuppressionForSettings(false);
        MarkHudDirty();
    }

    private void ToggleRosettaSettings()
    {
        if (settingsPanelOpen)
            CloseRosettaSettings();
        else
            OpenRosettaSettings();
    }

    // ── No-ops kept for compiler compatibility ────────────────────────────────

    private void EnsureRosettaRoot() { }
}

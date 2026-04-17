using MelonLoader;
using System;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public partial class BirdieMod
{
    // Parallel arrays for the Keybinds tab in the settings panel.
    // Index must match GetKeybindNameByIndex / SetKeybindNameByIndex switch cases.
    private static readonly string[] SettingsKeybindDisplayNames = new string[]
    {
        "Assist Toggle",
        "Coffee Boost",
        "Nearest Ball Mode",
        "Unlock Cosmetics",
        "Item Spawner",
        "HUD Toggle",
        "Random Item (Crate)",
        "Ice Toggle",
        "Settings Panel",
        "Landmine Immunity",
        "Lock-On Any Dist.",
        "No Wind",
        "Perfect Shot",
        "No Air Drag",
        "Speed Multiplier",
        "Infinite Item Usage",
        "No Recoil",
        "No Knockback",
        "Expanded Slots",
    };

    // Keys used to select rows within the settings panel tabs.
    private static readonly Key[] SettingsSelectionKeys = new Key[]
    {
        Key.Digit1,
        Key.Digit2,
        Key.Digit3,
        Key.Digit4,
        Key.Digit5,
        Key.Digit6,
        Key.Digit7,
        Key.Digit8,
        Key.Digit9,
        Key.Digit0,
    };

    // ── Rounded sprite helpers ───────────────────────────────────────────────

    private static Sprite GetRoundedSprite(ref Sprite cache, int texSize, int radius)
    {
        if (cache != null) return cache;
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pix = new Color32[texSize * texSize];
        for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
                pix[y * texSize + x] = InsideRoundedRect(x, y, texSize, texSize, radius)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
        tex.SetPixels32(pix);
        tex.Apply();
        int b = radius;
        cache = Sprite.Create(tex,
            new Rect(0, 0, texSize, texSize),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(b, b, b, b));
        return cache;
    }

    private static bool InsideRoundedRect(int px, int py, int w, int h, int r)
    {
        int lx = r, rx = w - r - 1, ly = r, ry = h - r - 1;
        int cx = (px < lx) ? lx : (px > rx ? rx : px);
        int cy = (py < ly) ? ly : (py > ry ? ry : py);
        int dx = px - cx, dy = py - cy;
        return dx * dx + dy * dy <= r * r;
    }

    private static void SetRounded(Image img, Sprite sprite, Color color)
    {
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = color;
    }

    // ── Panel open / close animations ───────────────────────────────────────

    private System.Collections.IEnumerator AnimatePanelOpen()
    {
        CanvasGroup cg = settingsPanelObject != null
            ? settingsPanelObject.GetComponent<CanvasGroup>()
            : null;
        if (cg == null) yield break;
        float dur = 0.18f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            cg.alpha = p;
            float s = Mathf.Lerp(0.92f, 1f, p);
            settingsPanelObject.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        cg.alpha = 1f;
        settingsPanelObject.transform.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator AnimatePanelClose()
    {
        CanvasGroup cg = settingsPanelObject != null
            ? settingsPanelObject.GetComponent<CanvasGroup>()
            : null;
        if (cg == null)
        {
            if (settingsPanelObject != null) settingsPanelObject.SetActive(false);
            yield break;
        }
        float dur = 0.12f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            cg.alpha = 1f - p;
            float s = Mathf.Lerp(1f, 0.95f, p);
            settingsPanelObject.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        cg.alpha = 0f;
        settingsPanelObject.SetActive(false);
        settingsPanelObject.transform.localScale = Vector3.one;
    }

    // ── Toggle pill slide animation ──────────────────────────────────────────

    private System.Collections.IEnumerator AnimatePillSlide(
        RectTransform knob,
        Image pillBg,
        float targetX,
        Color targetColor)
    {
        float dur = 0.14f, t = 0f;
        float startX = knob.anchoredPosition.x;
        Color startColor = pillBg.color;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, p), 0f);
            if (pillBg != null) pillBg.color = Color.Lerp(startColor, targetColor, p);
            yield return null;
        }
        if (knob != null) knob.anchoredPosition = new Vector2(targetX, 0f);
        if (pillBg != null) pillBg.color = targetColor;
    }

    // ── Settings panel open / close ──────────────────────────────────────────

    private void OpenSettingsMenu()
    {
        settingsPanelOpen = true;
        keybindRebindMode = false;
        keybindRebindIndex = -1;
        ApplyCursorUnlockForSettings(true);
        ApplyGameInputSuppressionForSettings(true);
        SetSettingsV2Tab(settingsTabIndex);
        UpdateSettingsPanelVisibility();
        // Start panel open animation
        if (settingsPanelObject != null)
        {
            CanvasGroup cg = settingsPanelObject.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                settingsPanelObject.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            }
            MelonCoroutines.Start(AnimatePanelOpen());
        }
        MarkHudDirty();
    }

    private void CloseSettingsMenu()
    {
        settingsPanelOpen = false;
        keybindRebindMode = false;
        keybindRebindIndex = -1;
        ApplyCursorUnlockForSettings(false);
        ApplyGameInputSuppressionForSettings(false);
        // Coroutine deactivates panel after animation; do not call UpdateSettingsPanelVisibility() here
        MelonCoroutines.Start(AnimatePanelClose());
        MarkHudDirty();
    }

    // ── Cursor / input suppression via reflection ────────────────────────────

    private void InitializeSettingsInputReflection()
    {
        if (settingsInputReflectionInitialized)
        {
            return;
        }

        settingsInputReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];

                if (cachedCursorSetForceUnlockedMethod == null)
                {
                    Type cursorMgr = asm.GetType("CursorManager");
                    if (cursorMgr != null)
                    {
                        cachedCursorSetForceUnlockedMethod = cursorMgr.GetMethod(
                            "SetCursorForceUnlocked",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                }

                if (cachedInputManagerEnableModeMethod == null || cachedInputManagerDisableModeMethod == null)
                {
                    Type inputMgr = asm.GetType("InputManager");
                    if (inputMgr != null)
                    {
                        if (cachedInputManagerEnableModeMethod == null)
                        {
                            cachedInputManagerEnableModeMethod = inputMgr.GetMethod(
                                "EnableMode",
                                BindingFlags.Public | BindingFlags.Static);
                        }

                        if (cachedInputManagerDisableModeMethod == null)
                        {
                            cachedInputManagerDisableModeMethod = inputMgr.GetMethod(
                                "DisableMode",
                                BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                }

                if (cachedInputModePausedValue == null)
                {
                    Type inputModeEnum = asm.GetType("InputMode");
                    if (inputModeEnum != null && inputModeEnum.IsEnum)
                    {
                        try
                        {
                            cachedInputModePausedValue = Enum.Parse(inputModeEnum, "Paused");
                        }
                        catch
                        {
                        }
                    }
                }

                bool allFound = cachedCursorSetForceUnlockedMethod != null
                    && cachedInputManagerEnableModeMethod != null
                    && cachedInputManagerDisableModeMethod != null
                    && cachedInputModePausedValue != null;

                if (allFound)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private void ApplyCursorUnlockForSettings(bool unlock)
    {
        if (!settingsInputReflectionInitialized)
        {
            InitializeSettingsInputReflection();
        }

        if (cachedCursorSetForceUnlockedMethod == null)
        {
            return;
        }

        try
        {
            cachedCursorSetForceUnlockedMethod.Invoke(null, new object[] { unlock });
        }
        catch
        {
        }
    }

    private void ApplyGameInputSuppressionForSettings(bool suppress)
    {
        if (!settingsInputReflectionInitialized)
        {
            InitializeSettingsInputReflection();
        }

        if (cachedInputModePausedValue == null)
        {
            return;
        }

        try
        {
            if (suppress)
            {
                if (cachedInputManagerEnableModeMethod != null)
                {
                    cachedInputManagerEnableModeMethod.Invoke(null, new object[] { cachedInputModePausedValue });
                }
            }
            else
            {
                if (cachedInputManagerDisableModeMethod != null)
                {
                    cachedInputManagerDisableModeMethod.Invoke(null, new object[] { cachedInputModePausedValue });
                }
            }
        }
        catch
        {
        }
    }

    // ── Settings panel UI construction ───────────────────────────────────────

    private void CreateSettingsPanelUi(Transform parent)
    {
        if (settingsPanelObject != null)
        {
            return;
        }

        // Root panel — 640x440, centered
        settingsPanelObject = new GameObject("BirdieSettingsPanel");
        settingsPanelObject.transform.SetParent(parent, false);

        RectTransform rootRect = settingsPanelObject.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(640f, 440f);

        // CanvasGroup for fade animation
        settingsPanelObject.AddComponent<CanvasGroup>();

        Image rootBg = settingsPanelObject.AddComponent<Image>();
        SetRounded(rootBg, GetRoundedSprite(ref s_roundedLg, 64, 12),
            new Color(0.10f, 0.10f, 0.13f, 0.96f));

        // Sidebar — 60px wide, left-anchored
        GameObject sidebar = CreateUiObject("Sidebar", settingsPanelObject.transform);
        RectTransform sidebarRect = sidebar.GetComponent<RectTransform>();
        sidebarRect.anchorMin = new Vector2(0f, 0f);
        sidebarRect.anchorMax = new Vector2(0f, 1f);
        sidebarRect.pivot = new Vector2(0f, 0.5f);
        sidebarRect.offsetMin = Vector2.zero;
        sidebarRect.offsetMax = new Vector2(60f, 0f);

        Image sidebarBg = sidebar.AddComponent<Image>();
        SetRounded(sidebarBg, GetRoundedSprite(ref s_roundedLg, 64, 12),
            new Color(0.07f, 0.07f, 0.09f, 1f));

        // Right-edge separator for sidebar
        GameObject sep = CreateUiObject("SidebarSep", settingsPanelObject.transform);
        RectTransform sepRect = sep.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0f, 0f);
        sepRect.anchorMax = new Vector2(0f, 1f);
        sepRect.pivot = new Vector2(0f, 0.5f);
        sepRect.offsetMin = new Vector2(59f, 4f);
        sepRect.offsetMax = new Vector2(60f, -4f);
        Image sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(1f, 1f, 1f, 0.06f);

        // Main area — fills right portion
        GameObject mainArea = CreateUiObject("MainArea", settingsPanelObject.transform);
        RectTransform mainRect = mainArea.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0f, 0f);
        mainRect.anchorMax = new Vector2(1f, 1f);
        mainRect.offsetMin = new Vector2(60f, 0f);
        mainRect.offsetMax = Vector2.zero;

        // TitleBar — 36px tall at top of MainArea
        GameObject titleBar = CreateUiObject("TitleBar", mainArea.transform);
        RectTransform titleBarRect = titleBar.GetComponent<RectTransform>();
        titleBarRect.anchorMin = new Vector2(0f, 1f);
        titleBarRect.anchorMax = new Vector2(1f, 1f);
        titleBarRect.pivot = new Vector2(0.5f, 1f);
        titleBarRect.offsetMin = new Vector2(0f, -36f);
        titleBarRect.offsetMax = Vector2.zero;

        Image titleBarBg = titleBar.AddComponent<Image>();
        titleBarBg.color = new Color(0.08f, 0.08f, 0.10f, 1f);

        // Title bar bottom separator
        GameObject titleSep = CreateUiObject("TitleSep", titleBar.transform);
        RectTransform tsRect = titleSep.GetComponent<RectTransform>();
        tsRect.anchorMin = new Vector2(0f, 0f);
        tsRect.anchorMax = new Vector2(1f, 0f);
        tsRect.pivot = new Vector2(0.5f, 0f);
        tsRect.offsetMin = new Vector2(0f, 0f);
        tsRect.offsetMax = new Vector2(0f, 1f);
        Image tsImg = titleSep.AddComponent<Image>();
        tsImg.color = new Color(1f, 1f, 1f, 0.06f);

        // Title label
        GameObject titleLabelObj = CreateUiObject("TitleLabel", titleBar.transform);
        RectTransform tlRect = titleLabelObj.GetComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.offsetMin = new Vector2(10f, 0f);
        tlRect.offsetMax = Vector2.zero;
        TextMeshProUGUI titleLabel = titleLabelObj.AddComponent<TextMeshProUGUI>();
        titleLabel.text = "Birdie Settings";
        titleLabel.fontSize = 15f;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.color = Color.white;
        titleLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // ContentArea — fills below TitleBar
        GameObject contentArea = CreateUiObject("ContentArea", mainArea.transform);
        RectTransform contentRect = contentArea.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = new Vector2(0f, -36f);

        // Build tab panels
        settingsV2TabPanels = new GameObject[5];
        settingsV2TabPanels[0] = BuildFeaturesTab(contentArea.transform);
        settingsV2TabPanels[1] = BuildKeybindsTab(contentArea.transform);
        settingsV2TabPanels[2] = BuildHudTab(contentArea.transform);
        settingsV2TabPanels[3] = BuildCreditsTab(contentArea.transform);
        settingsV2TabPanels[4] = BuildItemsTab(contentArea.transform);

        // Build sidebar nav buttons
        settingsV2NavButtons = new Button[5];
        string[] navLabels = new string[] { "Features", "Keys", "HUD", "$", "Items" };

        for (int i = 0; i < 5; i++)
        {
            int capturedIndex = i;
            GameObject navBtn = CreateSidebarNavButton(
                sidebar.transform,
                navLabels[i],
                i,
                out settingsV2NavButtons[i]);

            RectTransform nbRect = navBtn.GetComponent<RectTransform>();
            nbRect.anchorMin = new Vector2(0f, 1f);
            nbRect.anchorMax = new Vector2(1f, 1f);
            nbRect.pivot = new Vector2(0.5f, 1f);
            float topOffset = -(4f + i * 56f);
            nbRect.offsetMin = new Vector2(2f, topOffset - 52f);
            nbRect.offsetMax = new Vector2(-2f, topOffset);

            settingsV2NavButtons[i].onClick.AddListener(new UnityAction(() =>
            {
                SetSettingsV2Tab(capturedIndex);
            }));
        }

        // Close button at bottom of sidebar
        GameObject closeBtn = CreateUiObject("CloseBtn", sidebar.transform);
        RectTransform closeBtnRect = closeBtn.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0f, 0f);
        closeBtnRect.anchorMax = new Vector2(1f, 0f);
        closeBtnRect.pivot = new Vector2(0.5f, 0f);
        closeBtnRect.offsetMin = new Vector2(2f, 4f);
        closeBtnRect.offsetMax = new Vector2(-2f, 44f);

        Image closeBtnImg = closeBtn.AddComponent<Image>();
        SetRounded(closeBtnImg, GetRoundedSprite(ref s_roundedMd, 48, 8),
            new Color(0.55f, 0.12f, 0.12f, 0.90f));

        Button closeBtnComp = closeBtn.AddComponent<Button>();
        ColorBlock closeCb = closeBtnComp.colors;
        closeCb.normalColor = Color.white;
        closeCb.highlightedColor = new Color(1.1f, 0.85f, 0.85f, 1f);
        closeCb.pressedColor = new Color(0.85f, 0.7f, 0.7f, 1f);
        closeCb.fadeDuration = 0.08f;
        closeBtnComp.colors = closeCb;
        closeBtnComp.targetGraphic = closeBtnImg;
        closeBtnComp.onClick.AddListener(new UnityAction(CloseSettingsMenu));

        GameObject closeLabelObj = CreateUiObject("CloseLbl", closeBtn.transform);
        RectTransform clRect = closeLabelObj.GetComponent<RectTransform>();
        clRect.anchorMin = Vector2.zero;
        clRect.anchorMax = Vector2.one;
        clRect.offsetMin = Vector2.zero;
        clRect.offsetMax = Vector2.zero;
        TextMeshProUGUI closeLabel = closeLabelObj.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "\u2715";
        closeLabel.fontSize = 16f;
        closeLabel.color = new Color(1f, 0.4f, 0.4f, 1f);
        closeLabel.alignment = TextAlignmentOptions.Center;

        settingsPanelObject.SetActive(false);
        SetSettingsV2Tab(settingsTabIndex);
    }

    // ── Helper: create a bare RectTransform GameObject ───────────────────────

    private GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    // ── Sidebar nav button factory ───────────────────────────────────────────

    private GameObject CreateSidebarNavButton(Transform parent, string labelText, int index, out Button buttonOut)
    {
        GameObject btn = CreateUiObject("NavBtn_" + index, parent);

        Image btnImg = btn.AddComponent<Image>();
        btnImg.color = Color.clear;

        Button btnComp = btn.AddComponent<Button>();
        ColorBlock cb = btnComp.colors;
        cb.normalColor = Color.clear;
        cb.highlightedColor = new Color(0.22f, 0.47f, 0.85f, 0.15f);
        cb.pressedColor = new Color(0.22f, 0.47f, 0.85f, 0.30f);
        cb.fadeDuration = 0.08f;
        btnComp.colors = cb;
        btnComp.targetGraphic = btnImg;

        // Active indicator bar (left edge, 4px wide)
        GameObject activeBar = CreateUiObject("ActiveBar", btn.transform);
        RectTransform abRect = activeBar.GetComponent<RectTransform>();
        abRect.anchorMin = new Vector2(0f, 0f);
        abRect.anchorMax = new Vector2(0f, 1f);
        abRect.pivot = new Vector2(0f, 0.5f);
        abRect.offsetMin = Vector2.zero;
        abRect.offsetMax = new Vector2(4f, 0f);
        Image abImg = activeBar.AddComponent<Image>();
        abImg.color = new Color(0.22f, 0.47f, 0.85f, 1f);
        activeBar.SetActive(false);

        // Label
        GameObject labelObj = CreateUiObject("NavLbl", btn.transform);
        RectTransform lRect = labelObj.GetComponent<RectTransform>();
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = Vector2.one;
        lRect.offsetMin = Vector2.zero;
        lRect.offsetMax = Vector2.zero;
        TextMeshProUGUI lbl = labelObj.AddComponent<TextMeshProUGUI>();
        lbl.text = labelText;
        lbl.fontSize = 11f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.enableWordWrapping = false;

        buttonOut = btnComp;
        return btn;
    }

    // ── Tab switching ────────────────────────────────────────────────────────

    private void SetSettingsV2Tab(int tabIndex)
    {
        settingsTabIndex = tabIndex;

        if (settingsV2TabPanels != null)
        {
            for (int i = 0; i < settingsV2TabPanels.Length; i++)
            {
                if (settingsV2TabPanels[i] != null)
                {
                    settingsV2TabPanels[i].SetActive(i == tabIndex);
                }
            }
        }

        RefreshSidebarButtonStates();
    }

    private void RefreshSidebarButtonStates()
    {
        if (settingsV2NavButtons == null)
        {
            return;
        }

        for (int i = 0; i < settingsV2NavButtons.Length; i++)
        {
            if (settingsV2NavButtons[i] == null)
            {
                continue;
            }

            bool active = i == settingsTabIndex;

            // Set background color on the button's image
            Image btnImg = settingsV2NavButtons[i].GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = active ? new Color(0.22f, 0.50f, 0.90f, 0.22f) : new Color(1f, 1f, 1f, 0f);
            }

            // Toggle the active indicator bar (first child)
            Transform activeBar = settingsV2NavButtons[i].transform.Find("ActiveBar");
            if (activeBar != null)
            {
                activeBar.gameObject.SetActive(active);
            }

            // Make active label text brighter
            TextMeshProUGUI lbl = settingsV2NavButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.color = active ? new Color(0.55f, 0.80f, 1f, 1f) : new Color(0.75f, 0.75f, 0.80f, 1f);
        }
    }

    // ── Tab builders ─────────────────────────────────────────────────────────

    private GameObject BuildFeaturesTab(Transform contentParent)
    {
        GameObject tab = CreateUiObject("Tab_Features", contentParent);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = Vector2.zero;
        tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;

        // Scroll container
        float yOffset = -10f;
        float rowHeight = 60f;
        float spacing = 8f;

        // Card: Toggles
        GameObject card = CreateCard(tab.transform, "Feature Toggles");
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);
        cardRect.offsetMin = new Vector2(10f, yOffset - (rowHeight * 13f + 50f));
        cardRect.offsetMax = new Vector2(-10f, yOffset);

        Transform cardContent = card.transform.Find("Content");
        if (cardContent == null) cardContent = card.transform;

        Image unused0, unused1, unused2, unused3;

        CreatePillToggle(cardContent, "Ice Immunity", iceImmunityEnabled, () =>
        {
            ToggleIceImmunity();
        }, out unused0, () => iceImmunityEnabled);
        PositionPillRow(cardContent, 0, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Slide on ice like normal [I]");

        CreatePillToggle(cardContent, "Shot Tracer", tracersEnabled, () =>
        {
            tracersEnabled = !tracersEnabled;
            SaveCurrentConfig();
            MarkTrailVisualSettingsDirty();
            MarkHudDirty();
        }, out unused1, () => tracersEnabled);
        PositionPillRow(cardContent, 1, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Shows ball's actual flight path");

        CreatePillToggle(cardContent, "Assist", assistEnabled, () =>
        {
            ToggleAssist();
        }, out unused2, () => assistEnabled);
        PositionPillRow(cardContent, 2, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Auto-aims and releases at perfect moment [F]");

        CreatePillToggle(cardContent, "Impact Preview", impactPreviewEnabled, () =>
        {
            impactPreviewEnabled = !impactPreviewEnabled;
            SaveCurrentConfig();
        }, out unused3, () => impactPreviewEnabled);
        PositionPillRow(cardContent, 3, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Preview where your shot will land");

        Image unused4, unused5, unused6, unused7, unused8, unused9, unused10;

        CreatePillToggle(cardContent, "No Wind", noWindEnabled, () =>
        {
            ToggleNoWind();
        }, out unused4, () => noWindEnabled);
        PositionPillRow(cardContent, 4, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Removes wind from your ball [F7]");

        CreatePillToggle(cardContent, "Perfect Shot", perfectShotEnabled, () =>
        {
            TogglePerfectShot();
        }, out unused5, () => perfectShotEnabled);
        PositionPillRow(cardContent, 5, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Forces swing into perfect zone [F8]");

        CreatePillToggle(cardContent, "No Air Drag", noAirDragEnabled, () =>
        {
            ToggleNoAirDrag();
        }, out unused6, () => noAirDragEnabled);
        PositionPillRow(cardContent, 6, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Removes air resistance from ball [F9]");

        CreatePillToggle(cardContent, "Speed Boost", speedMultiplierEnabled, () =>
        {
            ToggleSpeedMultiplier();
        }, out unused7, () => speedMultiplierEnabled);
        PositionPillRow(cardContent, 7, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "2x movement speed [F10]");

        CreatePillToggle(cardContent, "Infinite Item Usage", infiniteAmmoEnabled, () =>
        {
            ToggleInfiniteAmmo();
        }, out unused8, () => infiniteAmmoEnabled);
        PositionPillRow(cardContent, 8, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Weapons never run out of ammo [F11]");

        CreatePillToggle(cardContent, "No Recoil", noRecoilEnabled, () =>
        {
            ToggleNoRecoil();
        }, out unused9, () => noRecoilEnabled);
        PositionPillRow(cardContent, 9, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Removes screen shake on fire [F12]");

        CreatePillToggle(cardContent, "No Knockback", noKnockbackEnabled, () =>
        {
            ToggleNoKnockback();
        }, out unused10, () => noKnockbackEnabled);
        PositionPillRow(cardContent, 10, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Prevents force from weapons hitting you [N]");

        Image unusedLandmine, unusedLockOn;
        CreatePillToggle(cardContent, "Landmine Immunity", landmineImmunityEnabled, () =>
        {
            ToggleLandmineImmunity();
        }, out unusedLandmine, () => landmineImmunityEnabled);
        PositionPillRow(cardContent, 11, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Walk over landmines safely [M]");

        CreatePillToggle(cardContent, "Lock-On Any Dist.", lockOnAnyDistanceEnabled, () =>
        {
            ToggleLockOnAnyDistance();
        }, out unusedLockOn, () => lockOnAnyDistanceEnabled);
        PositionPillRow(cardContent, 12, rowHeight, spacing);
        AddPillRowSubtitle(cardContent, "Lock-on works at any range [L]");

        return tab;
    }

    private void AddPillRowSubtitle(Transform cardContent, string subtitleText)
    {
        int childCount = cardContent.childCount;
        if (childCount == 0) return;
        Transform row = cardContent.GetChild(childCount - 1);

        GameObject subObj = CreateUiObject("Subtitle", row.transform);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(0.75f, 0.45f);
        subRect.offsetMin = new Vector2(8f, 0f);
        subRect.offsetMax = new Vector2(0f, 0f);
        TextMeshProUGUI subTmp = subObj.AddComponent<TextMeshProUGUI>();
        subTmp.text = subtitleText;
        subTmp.fontSize = 9f;
        subTmp.color = new Color(0.60f, 0.60f, 0.68f, 1f);
        subTmp.alignment = TextAlignmentOptions.BottomLeft;
    }

    private void PositionPillRow(Transform parent, int rowIndex, float rowHeight, float spacing)
    {
        int childCount = parent.childCount;
        if (childCount == 0) return;
        Transform row = parent.GetChild(childCount - 1);
        RectTransform rr = row.GetComponent<RectTransform>();
        if (rr == null) return;
        float topY = -(rowIndex * (rowHeight + spacing) + 32f);
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.offsetMin = new Vector2(10f, topY - rowHeight);
        rr.offsetMax = new Vector2(-10f, topY);
    }

    private GameObject CreateCard(Transform parent, string title)
    {
        GameObject card = CreateUiObject("Card_" + title, parent);
        Image cardImg = card.AddComponent<Image>();
        // Card bg — brighter for contrast
        SetRounded(cardImg, GetRoundedSprite(ref s_roundedMd, 48, 8), new Color(0.17f, 0.17f, 0.21f, 1f));

        // Top accent bar (1px height, blue tinted)
        GameObject cardAccent = CreateUiObject("CardAccent", card.transform);
        RectTransform accentRect = cardAccent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(8f, -2f);
        accentRect.offsetMax = new Vector2(-8f, 0f);
        Image accentImg = cardAccent.AddComponent<Image>();
        accentImg.color = new Color(0.22f, 0.50f, 0.90f, 0.30f);

        GameObject titleObj = CreateUiObject("CardTitle", card.transform);
        RectTransform tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 1f);
        tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.offsetMin = new Vector2(10f, -28f);
        tRect.offsetMax = new Vector2(-10f, 0f);
        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = title.ToUpper();
        titleTmp.fontSize = 10f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.55f, 0.55f, 0.62f, 1f);
        titleTmp.characterSpacing = 1.5f;
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Content area inside card
        GameObject content = CreateUiObject("Content", card.transform);
        RectTransform cRect = content.GetComponent<RectTransform>();
        cRect.anchorMin = Vector2.zero;
        cRect.anchorMax = Vector2.one;
        cRect.offsetMin = new Vector2(0f, 0f);
        cRect.offsetMax = new Vector2(0f, -28f);

        return card;
    }

    private void CreatePillToggle(
        Transform parent,
        string label,
        bool initialValue,
        System.Action onToggle,
        out Image pillBgOut,
        System.Func<bool> valueGetter)
    {
        GameObject row = CreateUiObject("PillRow_" + label, parent);
        row.AddComponent<RectTransform>();

        // Label
        GameObject labelObj = CreateUiObject("PillLabel", row.transform);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-60f, 0f);
        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 13f;
        labelTmp.color = Color.white;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Pill container
        GameObject pillContainer = CreateUiObject("Pill", row.transform);
        RectTransform pillContainerRect = pillContainer.GetComponent<RectTransform>();
        pillContainerRect.anchorMin = new Vector2(1f, 0.5f);
        pillContainerRect.anchorMax = new Vector2(1f, 0.5f);
        pillContainerRect.pivot = new Vector2(1f, 0.5f);
        pillContainerRect.sizeDelta = new Vector2(40f, 22f);
        pillContainerRect.anchoredPosition = new Vector2(-8f, 0f);

        Image pillBg = pillContainer.AddComponent<Image>();
        SetRounded(pillBg, GetRoundedSprite(ref s_pillSprite, 44, 22),
            initialValue ? new Color(0.22f, 0.50f, 0.90f, 1f) : new Color(0.28f, 0.28f, 0.33f, 1f));
        pillBgOut = pillBg;

        // Knob
        GameObject knob = CreateUiObject("Knob", pillContainer.transform);
        RectTransform knobRect = knob.GetComponent<RectTransform>();
        knobRect.anchorMin = new Vector2(0f, 0.5f);
        knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0.5f, 0.5f);
        knobRect.sizeDelta = new Vector2(18f, 18f);
        knobRect.anchoredPosition = initialValue ? new Vector2(29f, 0f) : new Vector2(11f, 0f);

        Image knobImg = knob.AddComponent<Image>();
        SetRounded(knobImg, GetRoundedSprite(ref s_circleSprite, 36, 18), Color.white);

        // Button on pill
        Button pillBtn = pillContainer.AddComponent<Button>();
        ColorBlock pillCb = pillBtn.colors;
        pillCb.normalColor = Color.white;
        pillCb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        pillCb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        pillCb.fadeDuration = 0.08f;
        pillBtn.colors = pillCb;
        pillBtn.targetGraphic = pillBg;

        Image capturedPillBg = pillBg;
        RectTransform capturedKnobRect = knobRect;
        System.Func<bool> capturedGetter = valueGetter;
        System.Action capturedToggle = onToggle;

        pillBtn.onClick.AddListener(new UnityAction(() =>
        {
            capturedToggle();
            bool newVal = capturedGetter();
            float targetX = newVal ? 29f : 11f;
            Color targetColor = newVal
                ? new Color(0.22f, 0.50f, 0.90f, 1f)
                : new Color(0.28f, 0.28f, 0.33f, 1f);
            MelonCoroutines.Start(AnimatePillSlide(capturedKnobRect, capturedPillBg, targetX, targetColor));
        }));
    }

    private GameObject BuildKeybindsTab(Transform contentParent)
    {
        GameObject tab = CreateUiObject("Tab_Keybinds", contentParent);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = Vector2.zero;
        tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;

        // Status label at top
        GameObject statusObj = CreateUiObject("RebindStatus", tab.transform);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.offsetMin = new Vector2(10f, -32f);
        statusRect.offsetMax = new Vector2(-10f, -4f);

        settingsKeybindRebindStatusLabel = statusObj.AddComponent<TextMeshProUGUI>();
        settingsKeybindRebindStatusLabel.text = "Click a binding to rebind";
        settingsKeybindRebindStatusLabel.fontSize = 12f;
        settingsKeybindRebindStatusLabel.color = new Color(0.65f, 0.65f, 0.72f, 1f);
        settingsKeybindRebindStatusLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // Keybind rows
        settingsKeybindRowButtons = new Button[SettingsKeybindDisplayNames.Length];
        float rowHeight = 34f;
        float spacing = 4f;

        for (int i = 0; i < SettingsKeybindDisplayNames.Length; i++)
        {
            int capturedIndex = i;
            string actionName = SettingsKeybindDisplayNames[i];

            GameObject row = CreateUiObject("KeybindRow_" + i, tab.transform);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            float topY = -(36f + i * (rowHeight + spacing));
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(10f, topY - rowHeight);
            rowRect.offsetMax = new Vector2(-10f, topY);

            Image rowImg = row.AddComponent<Image>();
            SetRounded(rowImg, GetRoundedSprite(ref s_roundedSm, 32, 6),
                new Color(0.15f, 0.15f, 0.18f, 1f));

            Button rowBtn = row.AddComponent<Button>();
            ColorBlock cb = rowBtn.colors;
            cb.normalColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            cb.highlightedColor = new Color(0.20f, 0.22f, 0.28f, 1f);
            cb.pressedColor = new Color(0.22f, 0.47f, 0.85f, 0.25f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            rowBtn.colors = cb;
            rowBtn.targetGraphic = rowImg;
            rowBtn.onClick.AddListener(new UnityAction(() =>
            {
                StartKeybindRebindForIndex(capturedIndex);
            }));

            settingsKeybindRowButtons[i] = rowBtn;

            // Action label
            GameObject actionLabelObj = CreateUiObject("ActionLabel", row.transform);
            RectTransform alRect = actionLabelObj.GetComponent<RectTransform>();
            alRect.anchorMin = new Vector2(0f, 0f);
            alRect.anchorMax = new Vector2(1f, 1f);
            alRect.offsetMin = new Vector2(10f, 0f);
            alRect.offsetMax = new Vector2(-90f, 0f);
            TextMeshProUGUI actionLabel = actionLabelObj.AddComponent<TextMeshProUGUI>();
            actionLabel.text = actionName;
            actionLabel.fontSize = 13f;
            actionLabel.color = Color.white;
            actionLabel.alignment = TextAlignmentOptions.MidlineLeft;

            // Key badge
            GameObject badgeObj = CreateUiObject("KeyBadge", row.transform);
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 0.5f);
            badgeRect.anchorMax = new Vector2(1f, 0.5f);
            badgeRect.pivot = new Vector2(1f, 0.5f);
            badgeRect.sizeDelta = new Vector2(80f, 24f);
            badgeRect.anchoredPosition = new Vector2(-6f, 0f);

            Image badgeImg = badgeObj.AddComponent<Image>();
            SetRounded(badgeImg, GetRoundedSprite(ref s_roundedSm, 32, 6),
                new Color(0.22f, 0.47f, 0.85f, 0.85f));

            GameObject badgeLabelObj = CreateUiObject("BadgeLabel", badgeObj.transform);
            RectTransform blRect = badgeLabelObj.GetComponent<RectTransform>();
            blRect.anchorMin = Vector2.zero;
            blRect.anchorMax = Vector2.one;
            blRect.offsetMin = Vector2.zero;
            blRect.offsetMax = Vector2.zero;
            TextMeshProUGUI badgeLabel = badgeLabelObj.AddComponent<TextMeshProUGUI>();
            badgeLabel.text = FormatKeyLabel(GetKeybindNameByIndex(i));
            badgeLabel.fontSize = 11f;
            badgeLabel.color = Color.white;
            badgeLabel.alignment = TextAlignmentOptions.Center;

            // Row separator (between rows, not after last)
            if (i < SettingsKeybindDisplayNames.Length - 1)
            {
                GameObject rowSep = CreateUiObject("RowSep", tab.transform);
                RectTransform rSepRect = rowSep.GetComponent<RectTransform>();
                float sepTopY = -(36f + i * (rowHeight + spacing));
                rSepRect.anchorMin = new Vector2(0f, 1f);
                rSepRect.anchorMax = new Vector2(1f, 1f);
                rSepRect.pivot = new Vector2(0.5f, 1f);
                rSepRect.offsetMin = new Vector2(10f, sepTopY - rowHeight - 1f);
                rSepRect.offsetMax = new Vector2(-10f, sepTopY - rowHeight);
                Image rSepImg = rowSep.AddComponent<Image>();
                rSepImg.color = new Color(1f, 1f, 1f, 0.04f);
            }
        }

        return tab;
    }

    private GameObject BuildHudTab(Transform contentParent)
    {
        GameObject tab = CreateUiObject("Tab_HUD", contentParent);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = Vector2.zero;
        tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;

        GameObject card = CreateCard(tab.transform, "HUD Visibility");
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);
        cardRect.offsetMin = new Vector2(10f, -10f - (6 * 52f + 36f));
        cardRect.offsetMax = new Vector2(-10f, -10f);

        Transform cardContent = card.transform.Find("Content");
        if (cardContent == null) cardContent = card.transform;

        settingsHudTogglePillBgs = new Image[6];
        float rowHeight = 44f;
        float spacing = 4f;

        string[] hudLabels = new string[]
        {
            "Bottom keybind bar",
            "Ball distance to hole",
            "Ice immunity indicator",
            "Center title",
            "Player info (left)",
            "Shot tracers",
        };

        System.Func<bool>[] hudGetters = new System.Func<bool>[]
        {
            () => hudShowBottomBar,
            () => hudShowBallDistance,
            () => hudShowIceIndicator,
            () => hudShowCenterTitle,
            () => hudShowPlayerInfo,
            () => tracersEnabled,
        };

        System.Action[] hudToggles = new System.Action[]
        {
            () => { hudShowBottomBar = !hudShowBottomBar; SaveCurrentConfig(); MarkHudDirty(); },
            () => { hudShowBallDistance = !hudShowBallDistance; SaveCurrentConfig(); MarkHudDirty(); },
            () => { hudShowIceIndicator = !hudShowIceIndicator; SaveCurrentConfig(); MarkHudDirty(); },
            () => { hudShowCenterTitle = !hudShowCenterTitle; SaveCurrentConfig(); MarkHudDirty(); },
            () => { hudShowPlayerInfo = !hudShowPlayerInfo; SaveCurrentConfig(); MarkHudDirty(); },
            () => { tracersEnabled = !tracersEnabled; SaveCurrentConfig(); MarkTrailVisualSettingsDirty(); MarkHudDirty(); },
        };

        bool[] hudInitials = new bool[]
        {
            hudShowBottomBar,
            hudShowBallDistance,
            hudShowIceIndicator,
            hudShowCenterTitle,
            hudShowPlayerInfo,
            tracersEnabled,
        };

        for (int i = 0; i < 6; i++)
        {
            Image pillBg;
            CreatePillToggle(cardContent, hudLabels[i], hudInitials[i], hudToggles[i], out pillBg, hudGetters[i]);
            settingsHudTogglePillBgs[i] = pillBg;
            PositionPillRow(cardContent, i, rowHeight, spacing);
        }

        return tab;
    }

    private GameObject BuildCreditsTab(Transform contentParent)
    {
        GameObject tab = CreateUiObject("Tab_Credits", contentParent);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = Vector2.zero;
        tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;

        // Info label
        GameObject infoObj = CreateUiObject("InfoLabel", tab.transform);
        RectTransform infoRect = infoObj.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 1f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.pivot = new Vector2(0.5f, 1f);
        infoRect.offsetMin = new Vector2(10f, -60f);
        infoRect.offsetMax = new Vector2(-10f, -10f);
        TextMeshProUGUI infoLabel = infoObj.AddComponent<TextMeshProUGUI>();
        infoLabel.text = "Grant credits to yourself (client-local, no server sync)";
        infoLabel.fontSize = 13f;
        infoLabel.color = new Color(0.65f, 0.65f, 0.72f, 1f);
        infoLabel.alignment = TextAlignmentOptions.MidlineLeft;
        infoLabel.textWrappingMode = TextWrappingModes.Normal;

        // Amount label
        GameObject amountLabelObj = CreateUiObject("AmountLabel", tab.transform);
        RectTransform amtLabelRect = amountLabelObj.GetComponent<RectTransform>();
        amtLabelRect.anchorMin = new Vector2(0f, 1f);
        amtLabelRect.anchorMax = new Vector2(0f, 1f);
        amtLabelRect.pivot = new Vector2(0f, 1f);
        amtLabelRect.offsetMin = new Vector2(10f, -100f);
        amtLabelRect.offsetMax = new Vector2(110f, -70f);
        TextMeshProUGUI amtLabel = amountLabelObj.AddComponent<TextMeshProUGUI>();
        amtLabel.text = "Amount:";
        amtLabel.fontSize = 13f;
        amtLabel.color = Color.white;
        amtLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // Input field container
        GameObject inputContainer = CreateUiObject("InputContainer", tab.transform);
        RectTransform inputContRect = inputContainer.GetComponent<RectTransform>();
        inputContRect.anchorMin = new Vector2(0f, 1f);
        inputContRect.anchorMax = new Vector2(0f, 1f);
        inputContRect.pivot = new Vector2(0f, 1f);
        inputContRect.offsetMin = new Vector2(110f, -105f);
        inputContRect.offsetMax = new Vector2(260f, -72f);

        Image inputBg = inputContainer.AddComponent<Image>();
        SetRounded(inputBg, GetRoundedSprite(ref s_roundedSm, 32, 6),
            new Color(0.12f, 0.12f, 0.16f, 1f));

        // TMP_InputField
        GameObject inputFieldObj = CreateUiObject("CreditsInput", inputContainer.transform);
        RectTransform inputFieldRect = inputFieldObj.GetComponent<RectTransform>();
        inputFieldRect.anchorMin = Vector2.zero;
        inputFieldRect.anchorMax = Vector2.one;
        inputFieldRect.offsetMin = new Vector2(4f, 2f);
        inputFieldRect.offsetMax = new Vector2(-4f, -2f);

        // TMP_InputField requires a text area and text component child
        GameObject textArea = CreateUiObject("Text Area", inputFieldObj.transform);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = Vector2.zero;
        textAreaRect.offsetMax = Vector2.zero;

        GameObject inputTextObj = CreateUiObject("Text", textArea.transform);
        RectTransform inputTextRect = inputTextObj.GetComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI inputTextTmp = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputTextTmp.fontSize = 13f;
        inputTextTmp.color = Color.white;
        inputTextTmp.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField inputField = inputFieldObj.AddComponent<TMP_InputField>();
        inputField.textComponent = inputTextTmp;
        inputField.textViewport = textAreaRect;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.text = creditsGrantAmount.ToString();

        settingsCreditsInputField = inputField;

        inputField.onValueChanged.AddListener(new UnityAction<string>((val) =>
        {
            int parsed;
            if (int.TryParse(val, out parsed))
            {
                creditsGrantAmount = Mathf.Max(0, parsed);
                SaveCurrentConfig();
            }
        }));

        // Grant button
        GameObject grantBtnObj = CreateUiObject("GrantBtn", tab.transform);
        RectTransform grantBtnRect = grantBtnObj.GetComponent<RectTransform>();
        grantBtnRect.anchorMin = new Vector2(0f, 1f);
        grantBtnRect.anchorMax = new Vector2(0f, 1f);
        grantBtnRect.pivot = new Vector2(0f, 1f);
        grantBtnRect.offsetMin = new Vector2(10f, -148f);
        grantBtnRect.offsetMax = new Vector2(150f, -115f);

        Image grantBtnImg = grantBtnObj.AddComponent<Image>();
        SetRounded(grantBtnImg, GetRoundedSprite(ref s_roundedMd, 48, 8),
            new Color(0.22f, 0.50f, 0.90f, 1f));

        Button grantBtn = grantBtnObj.AddComponent<Button>();
        ColorBlock grantCb = grantBtn.colors;
        grantCb.normalColor = Color.white;
        grantCb.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        grantCb.pressedColor = new Color(0.75f, 0.85f, 1f, 1f);
        grantCb.fadeDuration = 0.08f;
        grantBtn.colors = grantCb;
        grantBtn.targetGraphic = grantBtnImg;
        grantBtn.onClick.AddListener(new UnityAction(() =>
        {
            GrantCredits(creditsGrantAmount);
        }));

        GameObject grantLabelObj = CreateUiObject("GrantLabel", grantBtnObj.transform);
        RectTransform glRect = grantLabelObj.GetComponent<RectTransform>();
        glRect.anchorMin = Vector2.zero;
        glRect.anchorMax = Vector2.one;
        glRect.offsetMin = Vector2.zero;
        glRect.offsetMax = Vector2.zero;
        TextMeshProUGUI grantLabel = grantLabelObj.AddComponent<TextMeshProUGUI>();
        grantLabel.text = "Grant Credits";
        grantLabel.fontSize = 13f;
        grantLabel.color = Color.white;
        grantLabel.alignment = TextAlignmentOptions.Center;

        // Discord button
        GameObject discordBtn = CreateUiObject("DiscordBtn", tab.transform);
        RectTransform dbRect = discordBtn.GetComponent<RectTransform>();
        dbRect.anchorMin = new Vector2(0.1f, 0f);
        dbRect.anchorMax = new Vector2(0.9f, 0f);
        dbRect.pivot = new Vector2(0.5f, 0f);
        dbRect.offsetMin = new Vector2(0f, 12f);
        dbRect.offsetMax = new Vector2(0f, 52f);

        Image dbImg = discordBtn.AddComponent<Image>();
        dbImg.color = new Color(0.34f, 0.40f, 0.87f, 1f);

        Button dbBtn = discordBtn.AddComponent<Button>();
        ColorBlock dbCb = dbBtn.colors;
        dbCb.normalColor = Color.white;
        dbCb.highlightedColor = new Color(0.9f, 0.9f, 1f, 1f);
        dbCb.pressedColor = new Color(0.75f, 0.78f, 1f, 1f);
        dbCb.fadeDuration = 0.08f;
        dbBtn.colors = dbCb;
        dbBtn.targetGraphic = dbImg;
        dbBtn.onClick.AddListener(new UnityAction(() =>
        {
            Application.OpenURL("https://discord.gg/EaCRS6TBH9");
        }));

        TextMeshProUGUI dbTmp = CreateUiObject("DiscordLabel", discordBtn.transform).AddComponent<TextMeshProUGUI>();
        RectTransform dbLblRect = dbTmp.gameObject.GetComponent<RectTransform>();
        dbLblRect.anchorMin = Vector2.zero;
        dbLblRect.anchorMax = Vector2.one;
        dbLblRect.offsetMin = Vector2.zero;
        dbLblRect.offsetMax = Vector2.zero;
        dbTmp.text = "Join our Discord";
        dbTmp.fontSize = 13f;
        dbTmp.fontStyle = FontStyles.Bold;
        dbTmp.color = Color.white;
        dbTmp.alignment = TextAlignmentOptions.Center;

        return tab;
    }

    private GameObject BuildItemsTab(Transform contentParent)
    {
        GameObject tab = CreateUiObject("Tab_Items", contentParent);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = Vector2.zero;
        tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;

        // Host status indicator at top
        GameObject hostStatusObj = CreateUiObject("HostStatus", tab.transform);
        RectTransform hsRect = hostStatusObj.GetComponent<RectTransform>();
        hsRect.anchorMin = new Vector2(0f, 1f);
        hsRect.anchorMax = new Vector2(1f, 1f);
        hsRect.pivot = new Vector2(0.5f, 1f);
        hsRect.offsetMin = new Vector2(10f, -42f);
        hsRect.offsetMax = new Vector2(-10f, -8f);
        Image hsBg = hostStatusObj.AddComponent<Image>();
        bool hostReady = BirdieGrantBridge.IsReady();
        hsBg.color = hostReady
            ? new Color(0.10f, 0.38f, 0.18f, 0.90f)
            : new Color(0.35f, 0.20f, 0.08f, 0.90f);
        TextMeshProUGUI hsTmp = CreateUiObject("HostStatusText", hostStatusObj.transform)
            .AddComponent<TextMeshProUGUI>();
        hsTmp.gameObject.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        hsTmp.gameObject.GetComponent<RectTransform>().anchorMax = Vector2.one;
        hsTmp.gameObject.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 0f);
        hsTmp.gameObject.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, 0f);
        hsTmp.text = hostReady
            ? "Host has BirdieMod  — items can be granted"
            : "Host does not have BirdieMod — items may be limited";
        hsTmp.fontSize = 11f;
        hsTmp.color = hostReady ? new Color(0.50f, 1f, 0.60f, 1f) : new Color(1f, 0.75f, 0.40f, 1f);
        hsTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Item grid — 2 columns of buttons
        string[] itemNames = new string[]
        {
            "Coffee", "Dueling Pistol", "Elephant Gun", "Airhorn",
            "Spring Boots", "Golf Cart", "Rocket Launcher", "Landmine",
            "Electromagnet", "Orbital Laser", "Rocket Driver", "Freeze Bomb"
        };

        float startY = -54f;
        float btnH = 44f;
        float gapY = 6f;
        int cols = 2;

        for (int i = 0; i < itemNames.Length && i < SpawnableItemTypeInts.Length; i++)
        {
            int col = i % cols;
            int row2 = i / cols;
            int capturedTypeInt = SpawnableItemTypeInts[i];

            GameObject btnObj = CreateUiObject("ItemBtn_" + itemNames[i], tab.transform);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            float anchorXMin = col == 0 ? 0.02f : 0.52f;
            float anchorXMax = col == 0 ? 0.48f : 0.98f;
            float topY = startY - row2 * (btnH + gapY);
            btnRect.anchorMin = new Vector2(anchorXMin, 1f);
            btnRect.anchorMax = new Vector2(anchorXMax, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.offsetMin = new Vector2(0f, topY - btnH);
            btnRect.offsetMax = new Vector2(0f, topY);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.20f, 0.22f, 0.27f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.92f, 1f, 1f);
            cb.pressedColor = new Color(0.70f, 0.80f, 1f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(new UnityAction(() =>
            {
                SpawnItemClientSide(capturedTypeInt);
            }));

            TextMeshProUGUI btnTmp = CreateUiObject("BtnLabel", btnObj.transform).AddComponent<TextMeshProUGUI>();
            RectTransform lblRect = btnTmp.gameObject.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = new Vector2(6f, 0f);
            lblRect.offsetMax = new Vector2(-6f, 0f);
            btnTmp.text = itemNames[i];
            btnTmp.fontSize = 12f;
            btnTmp.color = Color.white;
            btnTmp.alignment = TextAlignmentOptions.MidlineLeft;
            btnTmp.fontStyle = FontStyles.Bold;
        }

        return tab;
    }

    // ── Keybind rebind helpers ───────────────────────────────────────────────

    private void StartKeybindRebindForIndex(int index)
    {
        keybindRebindMode = true;
        keybindRebindIndex = index;

        if (settingsKeybindRebindStatusLabel != null)
        {
            settingsKeybindRebindStatusLabel.text =
                "Press any key to set: " + SettingsKeybindDisplayNames[index];
        }

        if (settingsKeybindRowButtons == null)
        {
            return;
        }

        for (int i = 0; i < settingsKeybindRowButtons.Length; i++)
        {
            if (settingsKeybindRowButtons[i] == null)
            {
                continue;
            }

            ColorBlock cb = settingsKeybindRowButtons[i].colors;
            cb.normalColor = i == index
                ? new Color(0.22f, 0.47f, 0.85f, 0.30f)
                : new Color(0.15f, 0.15f, 0.18f, 1f);
            settingsKeybindRowButtons[i].colors = cb;
        }
    }

    private void RefreshKeybindRowLabels()
    {
        if (settingsKeybindRowButtons == null)
        {
            return;
        }

        for (int i = 0; i < settingsKeybindRowButtons.Length; i++)
        {
            if (settingsKeybindRowButtons[i] == null)
            {
                continue;
            }

            Transform badgeLabel = settingsKeybindRowButtons[i].transform.Find("KeyBadge/BadgeLabel");
            if (badgeLabel != null)
            {
                TextMeshProUGUI tmp = badgeLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = FormatKeyLabel(GetKeybindNameByIndex(i));
                }
            }
        }
    }

    private void RefreshKeybindRowColors()
    {
        if (settingsKeybindRowButtons == null)
        {
            return;
        }

        for (int i = 0; i < settingsKeybindRowButtons.Length; i++)
        {
            if (settingsKeybindRowButtons[i] == null)
            {
                continue;
            }

            ColorBlock cb = settingsKeybindRowButtons[i].colors;
            cb.normalColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            settingsKeybindRowButtons[i].colors = cb;
        }
    }

    // ── Settings panel input (keyboard only: Escape + rebind capture) ────────

    private void HandleSettingsPanelInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard[Key.Escape] != null && keyboard[Key.Escape].wasPressedThisFrame)
        {
            if (keybindRebindMode)
            {
                keybindRebindMode = false;
                keybindRebindIndex = -1;

                if (settingsKeybindRebindStatusLabel != null)
                {
                    settingsKeybindRebindStatusLabel.text = "Click a binding to rebind";
                }

                RefreshKeybindRowColors();
            }
            else
            {
                CloseRosettaSettings();
            }

            return;
        }

        if (keybindRebindMode)
        {
            Key pressedKey = DetectAnyKeyPressed(keyboard);
            if (pressedKey != Key.None)
            {
                FinishKeybindRebind(pressedKey);
            }
        }
    }

    // ── Panel visibility ─────────────────────────────────────────────────────

    private void UpdateSettingsPanelVisibility()
    {
        if (settingsPanelObject != null)
        {
            settingsPanelObject.SetActive(settingsPanelOpen);
        }
    }

    // ── No-op kept for Runtime.cs compatibility ──────────────────────────────

    private void RefreshSettingsPanelText() { }

    // ── Detect any key pressed ───────────────────────────────────────────────

    // Iterates through all Key enum values and returns the first one that was
    // pressed this frame. Skips modifier keys that are likely held alongside
    // the target key so they don't accidentally become the binding.
    private Key DetectAnyKeyPressed(Keyboard keyboard)
    {
        Array keyValues = Enum.GetValues(typeof(Key));
        for (int i = 0; i < keyValues.Length; i++)
        {
            Key k = (Key)keyValues.GetValue(i);
            if (k == Key.None ||
                k == Key.LeftShift  || k == Key.RightShift ||
                k == Key.LeftCtrl   || k == Key.RightCtrl  ||
                k == Key.LeftAlt    || k == Key.RightAlt   ||
                k == Key.LeftMeta   || k == Key.RightMeta)
            {
                continue;
            }

            try
            {
                KeyControl control = keyboard[k];
                if (control != null && control.wasPressedThisFrame)
                {
                    return k;
                }
            }
            catch
            {
            }
        }

        return Key.None;
    }

    private void FinishKeybindRebind(Key newKey)
    {
        int index = keybindRebindIndex;
        keybindRebindMode = false;
        keybindRebindIndex = -1;

        if (index < 0 || index >= SettingsKeybindDisplayNames.Length)
        {
            RefreshKeybindRowLabels();
            RefreshKeybindRowColors();

            if (settingsKeybindRebindStatusLabel != null)
            {
                settingsKeybindRebindStatusLabel.text = "Click a binding to rebind";
            }

            return;
        }

        string newKeyName = newKey.ToString();
        SetKeybindNameByIndex(index, newKeyName);
        UpdateConfigLabels();
        SaveCurrentConfig();
        RefreshItemMenuText();
        MarkHudDirty();

        RefreshKeybindRowLabels();
        RefreshKeybindRowColors();

        if (settingsKeybindRebindStatusLabel != null)
        {
            settingsKeybindRebindStatusLabel.text = "Click a binding to rebind";
        }
    }

    // ── Keybind get / set by index ───────────────────────────────────────────

    private string GetKeybindNameByIndex(int index)
    {
        switch (index)
        {
            case 0: return assistToggleKeyName;
            case 1: return coffeeBoostKeyName;
            case 2: return nearestBallModeKeyName;
            case 3: return unlockAllCosmeticsKeyName;
            case 4: return itemSpawnerKeyName;
            case 5: return hudToggleKeyName;
            case 6: return randomItemKeyName;
            case 7: return iceToggleKeyName;
            case 8: return settingsKeyName;
            case 9: return landmineImmunityKeyName;
            case 10: return lockOnAnyDistanceKeyName;
            case 11: return noWindKeyName;
            case 12: return perfectShotKeyName;
            case 13: return noAirDragKeyName;
            case 14: return speedMultiplierKeyName;
            case 15: return infiniteAmmoKeyName;
            case 16: return noRecoilKeyName;
            case 17: return noKnockbackKeyName;
            case 18: return expandedSlotsKeyName;
            default: return "?";
        }
    }

    private void SetKeybindNameByIndex(int index, string keyName)
    {
        switch (index)
        {
            case 0: assistToggleKeyName = keyName; break;
            case 1: coffeeBoostKeyName = keyName; break;
            case 2: nearestBallModeKeyName = keyName; break;
            case 3: unlockAllCosmeticsKeyName = keyName; break;
            case 4: itemSpawnerKeyName = keyName; break;
            case 5: hudToggleKeyName = keyName; break;
            case 6: randomItemKeyName = keyName; break;
            case 7: iceToggleKeyName = keyName; break;
            case 8: settingsKeyName = keyName; break;
            case 9: landmineImmunityKeyName = keyName; break;
            case 10: lockOnAnyDistanceKeyName = keyName; break;
            case 11: noWindKeyName = keyName; break;
            case 12: perfectShotKeyName = keyName; break;
            case 13: noAirDragKeyName = keyName; break;
            case 14: speedMultiplierKeyName = keyName; break;
            case 15: infiniteAmmoKeyName = keyName; break;
            case 16: noRecoilKeyName = keyName; break;
            case 17: noKnockbackKeyName = keyName; break;
            case 18: expandedSlotsKeyName = keyName; break;
        }
    }

    // ── Ice physics ──────────────────────────────────────────────────────────

    private void InitializeIceReflection()
    {
        if (iceReflectionInitialized)
        {
            return;
        }

        iceReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                if (cachedHorizontalDragField == null)
                {
                    Type t = assembly.GetType("PlayerMovement");
                    if (t != null)
                    {
                        cachedHorizontalDragField = t.GetField(
                            "horizontalDrag",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                }

                if (cachedPlayerMovementSettingsProperty == null)
                {
                    Type gmType = assembly.GetType("GameManager");
                    if (gmType != null)
                    {
                        cachedPlayerMovementSettingsProperty = gmType.GetProperty(
                            "PlayerMovementSettings",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                }

                if (cachedHorizontalDragField != null && cachedPlayerMovementSettingsProperty != null)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private void ToggleIceImmunity()
    {
        iceImmunityEnabled = !iceImmunityEnabled;

        if (iceImmunityEnabled)
        {
            if (!iceReflectionInitialized)
            {
                InitializeIceReflection();
            }

            TryCacheNormalDrag();

            // Wire the static bridge that the Harmony postfix reads from.
            BirdieIcePatchBridge.HorizontalDragField = cachedHorizontalDragField;
            BirdieIcePatchBridge.NormalDragValue = normalHorizontalDragValue;

            // Apply the Harmony postfix patch (no-op on subsequent calls).
            BirdieIcePatchBridge.EnsurePatchApplied();
            BirdieIcePatchBridge.IsActive = true;

            MelonLogger.Msg(string.Format("[Birdie] Ice immunity ON  (normal drag = {0:F3})", normalHorizontalDragValue));
        }
        else
        {
            BirdieIcePatchBridge.IsActive = false;
            MelonLogger.Msg("[Birdie] Ice immunity OFF");
        }

        MarkHudDirty();
    }

    // Reads GroundedHorizontalDrag from PlayerMovementSettings to use as the
    // reference drag value when ice immunity is active. Falls back to 10.0
    // if reflection fails (a safe default for normal terrain).
    private void TryCacheNormalDrag()
    {
        try
        {
            if (cachedPlayerMovementSettingsProperty == null)
            {
                return;
            }

            object settings = cachedPlayerMovementSettingsProperty.GetValue(null, null);
            if (settings == null)
            {
                return;
            }

            PropertyInfo groundedDragProp = settings.GetType().GetProperty(
                "GroundedHorizontalDrag",
                BindingFlags.Public | BindingFlags.Instance);

            if (groundedDragProp != null)
            {
                normalHorizontalDragValue = (float)groundedDragProp.GetValue(settings, null);
            }
        }
        catch
        {
        }
    }

    // ── Credits grant ────────────────────────────────────────────────────────

    private void InitializeCreditsReflection()
    {
        if (creditsReflectionInitialized)
        {
            return;
        }

        creditsReflectionInitialized = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type t = assemblies[i].GetType("CosmeticsUnlocksManager");
                if (t != null)
                {
                    // public static void RewardCredits(int amount, bool checkCheats = true)
                    cachedRewardCreditsMethod = t.GetMethod(
                        "RewardCredits",
                        BindingFlags.Public | BindingFlags.Static);

                    if (cachedRewardCreditsMethod != null)
                    {
                        break;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void GrantCredits(int amount)
    {
        if (!creditsReflectionInitialized)
        {
            InitializeCreditsReflection();
        }

        if (cachedRewardCreditsMethod == null)
        {
            MelonLogger.Warning("[Birdie] Credits: RewardCredits not found — game version may differ.");
            return;
        }

        if (amount <= 0)
        {
            MelonLogger.Warning("[Birdie] Credits: amount must be greater than zero.");
            return;
        }

        try
        {
            // checkCheats=false bypasses MatchSetupRules.IsCheatsEnabled() check.
            cachedRewardCreditsMethod.Invoke(null, new object[] { amount, false });
            MelonLogger.Msg("[Birdie] Credits: granted " + amount + " credits.");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Birdie] Credits grant: " + ex.Message);
        }
    }
}

// ── Ice physics Harmony bridge ────────────────────────────────────────────────
//
// Harmony postfix on PlayerMovement.UpdatePhysicsParameters().
// That method runs inside FixedUpdate() and sets horizontalDrag to a low ice
// value before ApplyMovement() and ApplyHorizontalDrag() consume it in the
// same call. A per-frame OnUpdate() override is always too late.
//
// EnsurePatchApplied() is called the first time ice immunity is toggled on.
// Subsequent toggles only flip IsActive — the patch itself stays registered.
internal static class BirdieIcePatchBridge
{
    internal static bool IsActive;
    internal static FieldInfo HorizontalDragField;
    internal static float NormalDragValue = 10f;

    private static bool patchApplied;
    private static float lastLogTime = -999f;

    // One-time Harmony patch registration. Idempotent.
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

            // Find PlayerMovement.UpdatePhysicsParameters (private instance method).
            Type playerMovementType = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                playerMovementType = assemblies[i].GetType("PlayerMovement");
                if (playerMovementType != null)
                {
                    break;
                }
            }

            if (playerMovementType == null)
            {
                MelonLogger.Warning("[Birdie] Ice patch: PlayerMovement type not found");
                patchApplied = false;
                return;
            }

            MethodInfo original = playerMovementType.GetMethod(
                "UpdatePhysicsParameters",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (original == null)
            {
                MelonLogger.Warning("[Birdie] Ice patch: PlayerMovement.UpdatePhysicsParameters not found");
                patchApplied = false;
                return;
            }

            // Locate HarmonyLib.Harmony and HarmonyLib.HarmonyMethod at runtime.
            // MelonLoader bundles HarmonyLib so these types are always present
            // once the game has started, even though we don't reference the DLL
            // at compile time.
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

            if (harmonyType == null)
            {
                MelonLogger.Warning("[Birdie] Ice patch: HarmonyLib.Harmony not found at runtime");
                patchApplied = false;
                return;
            }

            if (harmonyMethodType == null)
            {
                MelonLogger.Warning("[Birdie] Ice patch: HarmonyLib.HarmonyMethod not found at runtime");
                patchApplied = false;
                return;
            }

            // Create Harmony("birdie.icephysics").
            object harmony = Activator.CreateInstance(harmonyType, "birdie.icephysics");

            // Get our static postfix method.
            MethodInfo postfixMethod = typeof(BirdieIcePatchBridge).GetMethod(
                "Postfix",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (postfixMethod == null)
            {
                MelonLogger.Warning("[Birdie] Ice patch: Postfix method not found on BirdieIcePatchBridge");
                patchApplied = false;
                return;
            }

            // Wrap in HarmonyMethod.
            object harmonyPostfix = Activator.CreateInstance(harmonyMethodType, postfixMethod);

            // Find harmony.Patch(MethodBase original, ...) — locate it by name and
            // first-parameter type regardless of how many optional params there are.
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
                MelonLogger.Warning("[Birdie] Ice patch: Harmony.Patch(MethodBase,...) overload not found");
                patchApplied = false;
                return;
            }

            // Build the argument array — set the 'postfix' parameter by name,
            // leave all others (prefix, transpiler, finalizer, ilmanipulator) null.
            ParameterInfo[] patchParams = patchMethod.GetParameters();
            object[] patchArgs = new object[patchParams.Length];
            patchArgs[0] = original;
            for (int p = 1; p < patchParams.Length; p++)
            {
                string pname = patchParams[p].Name != null
                    ? patchParams[p].Name.ToLowerInvariant()
                    : string.Empty;
                if (pname == "postfix")
                {
                    patchArgs[p] = harmonyPostfix;
                }
                // All other optional HarmonyMethod params stay null.
            }

            patchMethod.Invoke(harmony, patchArgs);
            MelonLogger.Msg("[Birdie] Ice patch: Harmony postfix registered on PlayerMovement.UpdatePhysicsParameters");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Birdie] Ice patch error: " + ex.Message);
            patchApplied = false; // allow retry on next toggle
        }
    }

    // Called by Harmony immediately after PlayerMovement.UpdatePhysicsParameters()
    // completes. At that point horizontalDrag may have been set to the ice-terrain
    // override value. We restore it to the normal grounded value so that the
    // subsequent ApplyMovement() and ApplyHorizontalDrag() calls in the same
    // FixedUpdate() use the correct drag and the player slides normally.
    private static void Postfix(object __instance)
    {
        if (!IsActive || HorizontalDragField == null)
        {
            return;
        }

        try
        {
            float current = (float)HorizontalDragField.GetValue(__instance);
            if (current < NormalDragValue * 0.9f)
            {
                HorizontalDragField.SetValue(__instance, NormalDragValue);

                // Rate-limit the console log to once per second to avoid spam.
                float now = UnityEngine.Time.time;
                if (now - lastLogTime >= 1f)
                {
                    lastLogTime = now;
                    MelonLogger.Msg(string.Format(
                        "[Birdie] Ice immunity: horizontalDrag {0:F3} -> {1:F3}",
                        current,
                        NormalDragValue));
                }
            }
        }
        catch
        {
        }
    }
}

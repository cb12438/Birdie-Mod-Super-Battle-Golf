using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class BirdieMod
{
    private Component playerMovement;
    private Component playerGolfer;
    private Component golfBall;

    private readonly Dictionary<string, PropertyInfo> playerGolferProperties = new Dictionary<string, PropertyInfo>(8);
    private readonly Dictionary<string, FieldInfo> playerGolferFields = new Dictionary<string, FieldInfo>(4);

    private MethodInfo addSpeedBoostMethod;
    private FieldInfo swingNormalizedPowerBackingField;

    private bool playerFound;
    private bool assistEnabled;
    private bool isLeftMousePressed;
    private bool isRightMousePressed;
    private bool autoReleaseTriggeredThisCharge;
    private bool autoChargeSequenceStarted;
    private int lastAutoSwingReleaseFrame = -1;
    private float nextTryStartChargingTime;
    private readonly float tryStartChargingInterval = 0.05f;
    private float idealSwingPower;
    private float idealSwingPitch;
    private Vector3 flagPosition = Vector3.zero;
    private Vector3 holePosition = Vector3.zero;
    private Vector3 currentAimTargetPosition = Vector3.zero;
    private Vector3 currentSwingOriginPosition = Vector3.zero;
    private Vector3 aimTargetOffsetLocal = Vector3.zero;
    private readonly Vector3 swingOriginLocalOffset = new Vector3(0.86f, 0.05f, -0.12f);

    private string cachedLocalPlayerDisplayName = "";
    private float nextDisplayNameRefreshTime;
    private readonly float displayNameRefreshInterval = 0.5f;

    private int cachedAllGameObjectsFrame = -1;
    private GameObject[] cachedAllGameObjects;
    private int cachedAllComponentsFrame = -1;
    private Component[] cachedAllComponents;
    private float nextPlayerSearchTime;
    private float nextHoleSearchTime;
    private float nextIdealSwingCalculationTime;
    private float nextBallResolveTime;
    private readonly float playerSearchInterval = 1f;
    private readonly float holeSearchInterval = 0.5f;
    private readonly float idealSwingCalculationInterval = 0.25f;
    private readonly float ballResolveInterval = 0.2f;
    private readonly float puttDistanceThreshold = 12f;

    private bool hadResolvedPlayerContext;
    private bool hadResolvedBallContext;
    private string lastBallResolveSource = "missing";
    private Component initializedPlayerGolfer;

    private GameObject hudCanvas;
    private TextMeshProUGUI leftHudText;
    private TextMeshProUGUI centerHudText;
    private TextMeshProUGUI rightHudText;
    private TextMeshProUGUI bottomHudText;

    private bool isAimModeActive;
    private bool wasAimRequestedLastFrame;
    private bool reflectionCacheInitialized;
    private MethodInfo cachedTryGetMethod;
    private MethodInfo cachedOrbitSetYawMethod;
    private MethodInfo cachedOrbitSetPitchMethod;
    private MethodInfo cachedOrbitForceUpdateMethod;
    private MethodInfo cachedEnterSwingAimCameraMethod;
    private MethodInfo cachedExitSwingAimCameraMethod;
    private MethodInfo cachedReachOrbitSteadyStateMethod;
    private Component initializedYawPlayerMovement;
    private PropertyInfo cachedPlayerMovementYawProperty;
    private FieldInfo cachedPlayerMovementYawField;
    private Component initializedYawPlayerGolfer;
    private PropertyInfo cachedPlayerGolferYawProperty;
    private FieldInfo cachedPlayerGolferYawField;
    private bool cameraAimSmoothingInitialized;
    private float smoothedOrbitYaw;
    private float smoothedOrbitPitch;
    private float orbitYawVelocity;
    private float orbitPitchVelocity;
    private readonly float orbitAimSmoothTime = 0.02f;
    private readonly float orbitAimMaxSpeed = 2160f;
    private readonly object[] cachedOrbitModuleQueryArgs = new object[1];
    private readonly object[] cachedOrbitYawArgs = new object[1];
    private readonly object[] cachedOrbitPitchArgs = new object[1];

    private bool swingMathReflectionInitialized;
    private PropertyInfo cachedGolfSettingsProperty;
    private object cachedGolfSettingsObject;
    private PropertyInfo cachedGolfBallSettingsProperty;
    private object cachedGolfBallSettingsObject;
    private MethodInfo cachedBMathEaseInMethod;
    private MethodInfo cachedUpdateSwingNormalizedPowerMethod;
    private bool matchSetupRulesReflectionInitialized;
    private Type cachedMatchSetupRuleEnumType;
    private MethodInfo cachedMatchSetupGetValueMethod;
    private object cachedMatchSetupSwingPowerRuleValue;
    private PropertyInfo cachedLocalPlayerAsGolferProperty;
    private bool localGolferResolverInitialized;
    private MethodInfo cachedTryStartChargingSwingMethod;
    private MethodInfo cachedSetIsChargingSwingMethod;
    private MethodInfo cachedReleaseSwingChargeMethod;
    private float lastObservedSwingPower;

    private readonly float[] launchModelPowers = new float[] { 0.10f, 0.50f, 1.00f, 1.15f };
    private readonly float[] launchModelSpeeds = new float[] { 17.000f, 85.000f, 170.000f, 195.500f };
    private readonly float launchModelReferenceSrvMul = 2.00f;
    private bool golfBallVelocityReflectionInitialized;
    private Type cachedGolfBallTypeForVelocity;
    private PropertyInfo cachedGolfBallRigidbodyProperty;
    private bool rigidbodyVelocityReflectionInitialized;
    private PropertyInfo cachedRigidbodyLinearVelocityProperty;
    private readonly float trajectoryGravity = 9.81f;
    private bool windReflectionInitialized;
    private PropertyInfo cachedWindManagerWindProperty;
    private float windFactor = 1.0f;
    private float crossWindFactor = 1.0f;
    private bool swingHittableReflectionInitialized;
    private Component cachedGolfBallForHittable;
    private float cachedMaxPowerSwingHitSpeed = 85f;
    private float cachedMaxPowerPuttHitSpeed = 15f;
    private Vector3 windAimOffset = Vector3.zero;

    private GameObject shotPathObject;
    private LineRenderer shotPathLine;
    private Material shotPathMaterial;
    private GameObject predictedPathObject;
    private LineRenderer predictedPathLine;
    private Material predictedPathMaterial;
    private GameObject frozenPredictedPathObject;
    private LineRenderer frozenPredictedPathLine;
    private Material frozenPredictedPathMaterial;
    private readonly List<Vector3> shotPathPoints = new List<Vector3>(768);
    private readonly List<Vector3> predictedPathPoints = new List<Vector3>(384);
    private readonly List<Vector3> frozenPredictedPathPoints = new List<Vector3>(384);
    private bool predictedImpactPreviewValid;
    private Vector3 predictedImpactPreviewPoint = Vector3.zero;
    private Vector3 predictedImpactPreviewApproachDirection = Vector3.forward;
    private bool frozenImpactPreviewValid;
    private Vector3 frozenImpactPreviewPoint = Vector3.zero;
    private Vector3 frozenImpactPreviewApproachDirection = Vector3.forward;
    private bool lockLivePredictedPath;
    private bool observedBallMotionSinceLastShot;
    private bool isRecordingShotPath;
    private float predictedTrajectoryHideStartTime;
    private float nextPredictedPathRefreshTime;
    private Vector3 lastShotPathBallPosition = Vector3.zero;
    private float lastShotPathMoveTime;
    private readonly float predictedUnlockSpeedThreshold = 0.12f;
    private readonly float shotPathHeightOffset = 0.14f;
    private readonly float shotPathMoveThreshold = 0.004f;
    private readonly float shotPathPointSpacing = 0.012f;
    private readonly int shotPathMaxPoints = 3072;
    private readonly float shotPathStationaryDelay = 0.65f;
    private readonly int predictedPathMaxSteps = 360;
    private readonly float predictedPathMaxTime = 7.2f;
    private readonly float predictedPathPointSpacing = 0.30f;
    private readonly float predictedPathRefreshInterval = 0.05f;
    private readonly float predictedTrajectoryUnlockFallbackDelay = 0.75f;
    private bool predictedPathCacheValid;
    private Component cachedPredictedPathBall;
    private Vector3 cachedPredictedShotOrigin = Vector3.zero;
    private Vector3 cachedPredictedAimTargetPosition = Vector3.zero;
    private float cachedPredictedSwingPower;
    private float cachedPredictedSwingPitch;
    private readonly float predictedPathRebuildDistanceEpsilon = 0.015f;
    private readonly float predictedPathRebuildPowerEpsilon = 0.0025f;
    private readonly float predictedPathRebuildPitchEpsilon = 0.05f;

    private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "BirdieMod.cfg");
    private string assistToggleKeyName = "F";
    private string coffeeBoostKeyName = "F2";
    private string nearestBallModeKeyName = "F3";
    private string unlockAllCosmeticsKeyName = "F4";
    private string itemSpawnerKeyName = "F5";
    private string hudToggleKeyName = "H";
    private string randomItemKeyName = "G";
    private string assistToggleKeyLabel = "F";
    private string coffeeBoostKeyLabel = "F2";
    private string nearestBallModeKeyLabel = "F3";
    private string unlockAllCosmeticsKeyLabel = "F4";
    private string itemSpawnerKeyLabel = "F5";
    private string hudToggleKeyLabel = "H";
    private string randomItemKeyLabel = "G";
    private Key assistToggleKey = Key.F;
    private Key coffeeBoostKey = Key.F2;
    private Key nearestBallModeKey = Key.F3;
    private Key unlockAllCosmeticsKey = Key.F4;
    private Key itemSpawnerKey = Key.F5;
    private Key hudToggleKey = Key.H;
    private Key randomItemKey = Key.G;
    private bool hudVisible = true;
    private bool itemMenuOpen;
    private GameObject itemMenuPanelObject;
    private TextMeshProUGUI itemMenuText;
    private bool actualTrailEnabled = true;
    private bool predictedTrailEnabled = true;
    private bool frozenTrailEnabled = true;
    private bool impactPreviewEnabled = true;
    private float impactPreviewTargetFps;
    private int impactPreviewTextureWidth = 640;
    private int impactPreviewTextureHeight = 360;
    private float actualTrailStartWidth = 0.22f;
    private float actualTrailEndWidth = 0.18f;
    private float predictedTrailStartWidth = 0.18f;
    private float predictedTrailEndWidth = 0.14f;
    private float frozenTrailStartWidth = 0.20f;
    private float frozenTrailEndWidth = 0.16f;
    private Color actualTrailColor = new Color(1f, 0.58f, 0.20f, 1f);
    private Color predictedTrailColor = new Color(0.36f, 0.95f, 0.46f, 0.95f);
    private Color frozenTrailColor = new Color(0.36f, 0.74f, 1f, 0.92f);
    private bool visualsInitialized;
    private readonly float visualsInitializationDelay = 2.5f;
    private bool trailVisualSettingsDirty = true;
    private bool actualTrailLineDirty = true;
    private bool predictedTrailLineDirty = true;
    private bool frozenTrailLineDirty = true;
    private bool hudDirty = true;
    private float nextHudRefreshTime;
    private readonly float hudRefreshInterval = 0.1f;
    private string cachedLeftHudText = "";
    private string cachedCenterHudText = "";
    private string cachedRightHudText = "";
    private string cachedBottomHudText = "";
    private bool nearestAnyBallModeEnabled;
    private float nextNearestAnyBallResolveTime;
    private readonly float nearestAnyBallResolveInterval = 0.1f;
    private readonly List<Component> cachedGolfBalls = new List<Component>(64);
    private float nextGolfBallCacheRefreshTime;
    private readonly float golfBallCacheRefreshInterval = 0.75f;
    private readonly float emptyGolfBallCacheRefreshInterval = 2f;
    private float nextImpactPreviewRenderTime;
    private Camera cachedImpactPreviewReferenceCamera;
    private float nextImpactPreviewReferenceCameraRefreshTime;
    private readonly float impactPreviewReferenceCameraRefreshInterval = 1f;
    private readonly float impactPreviewAutoTargetFps = 60f;
    private readonly RaycastHit[] impactPreviewRaycastHits = new RaycastHit[24];
    private readonly RaycastHit[] impactPreviewGroundProbeHits = new RaycastHit[24];
    private readonly object[] cachedSpeedBoostArgs = new object[1];
    private readonly object[] cachedEaseInArgs = new object[1];
    private readonly object[] cachedMatchSetupGetValueArgs = new object[1];
    private readonly object[] cachedChargingStateArgs = new object[1];
    private readonly object[] cachedUpdateSwingPowerArgs = new object[] { true, false };

    // ── Ice physics toggle ────────────────────────────────────────────────────
    private string iceToggleKeyName = "I";
    private string iceToggleKeyLabel = "I";
    private Key iceToggleKey = Key.I;
    private bool iceImmunityEnabled;
    private bool iceReflectionInitialized;
    private FieldInfo cachedHorizontalDragField;
    private PropertyInfo cachedPlayerMovementSettingsProperty;
    private float normalHorizontalDragValue = 10f;

    // ── Settings panel ────────────────────────────────────────────────────────
    private string settingsKeyName = "F6";
    private string settingsKeyLabel = "F6";
    private Key settingsKey = Key.F6;
    private bool settingsPanelOpen;
    private int settingsTabIndex;
    private bool keybindRebindMode;
    private int keybindRebindIndex = -1;
    private GameObject settingsPanelObject;

    // ── Settings panel v2 visual cache ───────────────────────────────────────
    private static UnityEngine.Sprite s_roundedLg = null;   // radius-12 rounded rect (panel, sidebar)
    private static UnityEngine.Sprite s_roundedMd = null;   // radius-8 rounded rect (cards, close btn)
    private static UnityEngine.Sprite s_roundedSm = null;   // radius-6 rounded rect (rows, badges)
    private static UnityEngine.Sprite s_pillSprite = null;  // pill shape for toggles
    private static UnityEngine.Sprite s_circleSprite = null; // circle for knob

    // ── Settings panel v2 (uGUI) ──────────────────────────────────────────────
    private UnityEngine.UI.Button[] settingsV2NavButtons = new UnityEngine.UI.Button[5];
    private GameObject[] settingsV2TabPanels = new GameObject[5];
    private UnityEngine.UI.Button[] settingsKeybindRowButtons;
    private UnityEngine.UI.Image[] settingsHudTogglePillBgs;
    private TMPro.TMP_InputField settingsCreditsInputField;
    private TMPro.TextMeshProUGUI settingsKeybindRebindStatusLabel;

    // ── Settings cursor + input suppression reflection ────────────────────────
    private bool settingsInputReflectionInitialized;
    private System.Reflection.MethodInfo cachedCursorSetForceUnlockedMethod;
    private System.Reflection.MethodInfo cachedInputManagerEnableModeMethod;
    private System.Reflection.MethodInfo cachedInputManagerDisableModeMethod;
    private object cachedInputModePausedValue;

    // ── Credits grant ─────────────────────────────────────────────────────────
    private int creditsGrantAmount = 1000;
    private bool creditsReflectionInitialized;
    private MethodInfo cachedRewardCreditsMethod;

    // ── Per-element HUD visibility ────────────────────────────────────────────
    private bool hudShowBottomBar = true;
    private bool hudShowBallDistance = true;
    private bool hudShowIceIndicator = true;
    private bool hudShowCenterTitle = true;
    private bool hudShowPlayerInfo = true;
    private bool tracersEnabled = true;

    // ── No Wind ──────────────────────────────────────────────────────────────
    private string noWindKeyName = "F7";
    private string noWindKeyLabel = "F7";
    private Key noWindKey = Key.F7;
    private bool noWindEnabled;
    private bool windExtrasReflectionInitialized;
    private object cachedWindSettingsInstance;
    private System.Reflection.FieldInfo cachedWindForceScaleField;
    private float savedWindForceScale = 1f;

    // ── Perfect Shot ─────────────────────────────────────────────────────────
    private string perfectShotKeyName = "F8";
    private string perfectShotKeyLabel = "F8";
    private Key perfectShotKey = Key.F8;
    private bool perfectShotEnabled;

    // ── No Air Drag ──────────────────────────────────────────────────────────
    private string noAirDragKeyName = "F9";
    private string noAirDragKeyLabel = "F9";
    private Key noAirDragKey = Key.F9;
    private bool noAirDragEnabled;
    private bool airDragExtrasReflectionInitialized;
    private System.Reflection.FieldInfo cachedLinearAirDragField;
    private float savedLinearAirDrag = 0.01f;

    // ── Speed Multiplier ─────────────────────────────────────────────────────
    private string speedMultiplierKeyName = "F10";
    private string speedMultiplierKeyLabel = "F10";
    private Key speedMultiplierKey = Key.F10;
    private bool speedMultiplierEnabled;
    private float speedMultiplierFactor = 2.0f;
    private bool moveSpeedExtrasReflectionInitialized;
    private System.Reflection.PropertyInfo cachedPlayerMovSettingsProperty;
    private System.Reflection.FieldInfo cachedDefaultMoveSpeedField;
    private float savedDefaultMoveSpeed = 7f;

    // ── Infinite Ammo ────────────────────────────────────────────────────────
    private string infiniteAmmoKeyName = "F11";
    private string infiniteAmmoKeyLabel = "F11";
    private Key infiniteAmmoKey = Key.F11;
    private bool infiniteAmmoEnabled;
    private bool ammoInventoryReflectionReady;
    private System.Reflection.FieldInfo cachedInventorySlotsSyncListField;
    private System.Reflection.FieldInfo cachedInventoryOverridesDictField;
    private System.Reflection.FieldInfo cachedInvSlotItemTypeField;
    private System.Reflection.ConstructorInfo cachedInvSlotConstructor;
    private readonly Dictionary<int, object> ammoItemTypeBackup = new Dictionary<int, object>();

    // ── No Recoil ────────────────────────────────────────────────────────────
    private string noRecoilKeyName = "F12";
    private string noRecoilKeyLabel = "F12";
    private Key noRecoilKey = Key.F12;
    private bool noRecoilEnabled;
    private bool screenshakeExtrasReflectionInitialized;
    private object cachedScreenshakeOwner;
    private System.Reflection.FieldInfo cachedScreenshakeFactorField;
    private float savedScreenshakeFactor = 1f;

    // ── No Knockback ─────────────────────────────────────────────────────────
    private string noKnockbackKeyName = "N";
    private string noKnockbackKeyLabel = "N";
    private Key noKnockbackKey = Key.N;
    private bool noKnockbackEnabled;
    private bool knockbackImmunityReflectionReady;
    private System.Reflection.FieldInfo cachedKnockoutImmunityStatusField;
    private System.Reflection.FieldInfo cachedHasImmunityField;
    private Component localPlayerHittable;

    // ── Landmine Immunity ─────────────────────────────────────────────────────
    private string landmineImmunityKeyName = "M";
    private string landmineImmunityKeyLabel = "M";
    private Key landmineImmunityKey = Key.M;
    private bool landmineImmunityEnabled;

    // ── Lock-On Any Distance ──────────────────────────────────────────────────
    private string lockOnAnyDistanceKeyName = "L";
    private string lockOnAnyDistanceKeyLabel = "L";
    private Key lockOnAnyDistanceKey = Key.L;
    private bool lockOnAnyDistanceEnabled;
    private bool lockOnReflectionInitialized;
    private System.Reflection.FieldInfo cachedLockOnMaxDistanceField;
    private float savedLockOnMaxDistance = 60f;

    // ── Expanded Item Slots ───────────────────────────────────────────────────
    private string expandedSlotsKeyName = "U";
    private string expandedSlotsKeyLabel = "U";
    private Key expandedSlotsKey = Key.U;
    private bool expandedSlotsEnabled;
    private bool expandedSlotsAllPlayers = true;
    private const int ExpandedSlotsTarget = 7;
    private bool expandedSlotsReflectionInitialized;
    private object cachedExpandedHotkeysInstance;
    private System.Reflection.FieldInfo cachedHotkeyUisField;
    private object cachedExpandedPlayerInvSettings;
    private System.Reflection.FieldInfo cachedMaxItemsBackingField;
    private int savedOriginalMaxItems = 3;
    private System.Array savedOriginalHotkeyUisArray;
    private System.Collections.Generic.List<GameObject> spawnedExtraHotkeyUiObjects = new System.Collections.Generic.List<GameObject>();
    private Vector2 savedSlotParentSizeDelta;
    private bool savedSlotParentMaskEnabled;
    private Transform savedSlotParentTransform;

    // ── Host Controls ─────────────────────────────────────────────────────────────
    private bool hostControlsActive;
    private ulong hostAllowedFeatureMask = 0xFFFF; // bits 0-15 all on by default

    // ── Weather System ────────────────────────────────────────────────────────────
    private byte _hostSelectedWeather = 0;   // weather type the host has queued (0 = none)
    private bool _hostWeatherRunning = false;

    private bool IsFeatureAllowed(int featureBit)
    {
        if (!BirdieHostBridge.IsUnderHostControl) return true;
        if (featureBit < 0) return true;
        return (BirdieHostBridge.ReceivedFeatureMask & (1UL << featureBit)) != 0;
    }

    private void SetHostControlsActive(bool active)
    {
        hostControlsActive = active;
        BirdieHostBridge.BroadcastToClients(active, active ? hostAllowedFeatureMask : ulong.MaxValue);
        MarkHudDirty();
    }

    private void SetHostFeatureBit(int bit, bool enabled)
    {
        if (enabled)
            hostAllowedFeatureMask |= (1UL << bit);
        else
            hostAllowedFeatureMask &= ~(1UL << bit);
        if (hostControlsActive)
            BirdieHostBridge.BroadcastToClients(true, hostAllowedFeatureMask);
    }
}

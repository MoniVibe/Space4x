using System;
using System.Collections.Generic;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Effects;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Power;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Temporal;
using Space4x.Fleetcrawl;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Transforms;
using UTime = UnityEngine.Time;

namespace Space4X.UI
{
    /// <summary>
    /// In-run HUD kernel for UI/UX iteration and agent-driven validation.
    /// </summary>
    [DefaultExecutionOrder(-9290)]
    [DisallowMultipleComponent]
    public sealed class Space4XInRunHudOverlay : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string ForceRunActiveEnv = "SPACE4X_UIUX_FORCE_RUN_ACTIVE";
        private const string RuntimeThemeResourcePath = "UI/Space4XRuntimeTheme";
        private const float LowResourceThreshold = 0.25f;
        private const int KernelMinimapContactWindowSize = 6;
        private const float KernelFallbackContactMaxDistance = 1750f;
        private const int KernelFallbackScanLimit = 512;
        private const float HudPanelDefaultX = 24f;
        private const float HudPanelDefaultY = 300f;
        private const int KernelInventorySummaryMaxChars = 420;
        private const int KernelProductionSummaryMaxChars = 420;
        private const int KernelNotificationWindowSize = 4;
        private const int KernelNotificationTextMaxChars = 180;
        private const int KernelProductionQueueWindowSize = 6;
        private const int NotificationHistoryCapacity = 64;
        private const float MinimapPanelSize = 172f;
        private const float MinimapViewportSize = 148f;
        private const float MinimapOverlaySignatureThreshold = 0.05f;
        private const float StatusBarHeight = 7f;
        private const float SpeedWidgetWidth = 260f;
        private const float SpeedWidgetBottomOffset = 12f;
        private const float ForcesHoloWidth = 292f;
        private const float ForcesHoloBottomOffset = 64f;
        private const float RootPanelDefaultWidth = 286f;
        private const float RootPanelDefaultHeight = 176f;
        private const float MinimapDefaultTop = 20f;
        private const float MinimapDefaultRight = 20f;
        private const float MinimapDefaultHeight = 192f;
        private const float InventoryDefaultTop = 182f;
        private const float InventoryDefaultRight = 246f;
        private const float InventoryDefaultWidth = 428f;
        private const float InventoryDefaultHeight = 288f;
        private const float ProductionDefaultTop = 182f;
        private const float ProductionDefaultRight = 486f;
        private const float ProductionDefaultWidth = 342f;
        private const float ProductionDefaultHeight = 244f;
        private const float TargetDefaultTop = 312f;
        private const float TargetDefaultRight = 246f;
        private const float TargetDefaultWidth = 300f;
        private const float TargetDefaultHeight = 76f;
        private const float NotificationDefaultTop = 182f;
        private const float NotificationDefaultRight = 14f;
        private const float NotificationDefaultWidth = 220f;
        private const float NotificationDefaultHeight = 124f;
        private const float PowerRoutingDefaultTop = 310f;
        private const float PowerRoutingDefaultRight = 20f;
        private const float PowerRoutingDefaultWidth = 236f;
        private const float PowerRoutingDefaultHeight = 178f;
        private const float ShipControlDefaultTop = 306f;
        private const float ShipControlDefaultRight = 546f;
        private const float ShipControlDefaultWidth = 320f;
        private const float ShipControlDefaultHeight = 216f;
        private const float UtilityControlsDefaultTop = 220f;
        private const float UtilityControlsDefaultRight = 20f;
        private const float UtilityControlsDefaultWidth = 292f;
        private const float UtilityControlsDefaultHeight = 40f;
        private const float TimeControlsDefaultTop = 266f;
        private const float TimeControlsDefaultRight = 20f;
        private const float TimeControlsDefaultWidth = 172f;
        private const float TimeControlsDefaultHeight = 36f;
        private const float SpeedWidgetDefaultHeight = 62f;
        private const float ForcesHoloDefaultHeight = 70f;
        private const float LayoutMinWindowWidth = 96f;
        private const float LayoutMinWindowHeight = 60f;
        private const float LayoutMinScale = 0.7f;
        private const float LayoutMaxScale = 1.8f;
        private const float LayoutScaleStep = 0.1f;
        private const float LayoutWindowScaleStep = 0.05f;
        private const string LayoutPrefsKey = "space4x.ui.hud.layout.v2";
        private const int LayoutSchemaVersion = 4;
        private const string PowerRoutingPrefsKey = "space4x.ui.hud.power_routing.v1";
        private const int PowerRoutingPrefsSchemaVersion = 1;
        private const int PowerRoutingProfileCount = 3;
        private const float PowerRoutingMinPercent = 10f;
        private const float PowerRoutingMaxPercent = 250f;
        private const float PowerRoutingDefaultPercent = 100f;
        private const float PowerRoutingStepPercent = 5f;
        private const float ProductionPowerScaleMin = 0.50f;
        private const float ProductionPowerScaleMax = 2.00f;
        private const float ProductionPowerScaleStep = 0.10f;
        private const float HudDeficitFadeStartMw = 5f;
        private const float HudDeficitFadeFullMw = 150f;
        private const float HudDeficitFadeMinAlpha = 0.28f;

        [SerializeField] private bool showHudByDefault = true;
        [SerializeField] private bool enableInSmokeScene = true;
        [SerializeField] private float refreshRateHz = 10f;
        [SerializeField] private int maxNotifications = 8;
        [SerializeField] private float minimapCameraHeight = 280f;
        [SerializeField] private float minimapCameraOrthoSize = 180f;
        [SerializeField] private float minimapCameraFollowSmoothing = 12f;
        [SerializeField] private Vector2Int minimapRenderResolution = new Vector2Int(256, 256);
        [SerializeField] private LayerMask minimapCullingMask = ~0;
        [SerializeField] private Color minimapCameraClearColor = new Color(0.02f, 0.05f, 0.07f, 1f);
        [SerializeField] private Key toggleHudKey = Key.Backquote;
        [SerializeField] private Key toggleInventoryKey = Key.I;
        [SerializeField] private Key toggleMinimapKey = Key.M;
        [SerializeField] private Key cycleMinimapOverlayKey = Key.O;
        [SerializeField] private Key toggleNotificationsKey = Key.N;
        [SerializeField] private Key toggleForcesHoloKey = Key.F7;
        [SerializeField] private Key toggleLayoutEditModeKey = Key.F8;
        [SerializeField] private Key increaseUiScaleKey = Key.RightBracket;
        [SerializeField] private Key decreaseUiScaleKey = Key.LeftBracket;
        [SerializeField] private Key resetLayoutKey = Key.F9;
        private Key _runtimeToggleHudKey;
        private Key _runtimeToggleInventoryKey;
        private Key _runtimeToggleMinimapKey;
        private Key _runtimeCycleMinimapOverlayKey;
        private Key _runtimeToggleNotificationsKey;
        private Key _runtimeToggleForcesHoloKey;
        private Key _runtimeToggleLayoutEditModeKey;
        private Key _runtimeIncreaseUiScaleKey;
        private Key _runtimeDecreaseUiScaleKey;
        private Key _runtimeResetLayoutKey;
        private bool _runtimeHudFadeOnPowerDeficit;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _minimapPanel;
        private Image _minimapViewportImage;
        private VisualElement _minimapOverlayLayer;
        private VisualElement _minimapContactList;
        private VisualElement _inventoryPanel;
        private VisualElement _inventoryList;
        private ScrollView _inventorySegmentsScroll;
        private ScrollView _inventoryCargoScroll;
        private VisualElement _productionPanel;
        private VisualElement _productionList;
        private VisualElement _targetPanel;
        private VisualElement _notificationPanel;
        private ScrollView _notificationScroll;
        private VisualElement _notificationList;
        private Label _inventoryHeaderLabel;
        private Label _inventoryHintLabel;
        private Label _targetPrimaryLabel;
        private Label _targetSecondaryLabel;
        private Label _healthLabel;
        private Label _shieldsLabel;
        private Label _armorLabel;
        private Label _suppliesLabel;
        private Label _fuelLabel;
        private Label _foodLabel;
        private Label _crewLabel;
        private Label _ammoLabel;
        private Label _powerLabel;
        private Label _timeLabel;
        private Label _speedWidgetSpeedLabel;
        private Label _speedWidgetDrawLabel;
        private Label _forcesHeadingLabel;
        private Label _forcesVelocityLabel;
        private Label _forcesPushLabel;
        private Label _minimapSummaryLabel;
        private Label _minimapOverlayModeLabel;
        private VisualElement _healthBarFill;
        private VisualElement _shieldsBarFill;
        private VisualElement _armorBarFill;
        private VisualElement _suppliesBarFill;
        private VisualElement _foodBarFill;
        private VisualElement _waterBarFill;
        private VisualElement _crewBarFill;
        private VisualElement _ammoBarFill;
        private VisualElement _fuelReserveBarFill;
        private VisualElement _targetHullBarFill;
        private Button _toggleMinimapButton;
        private Button _cycleMinimapOverlayButton;
        private Button _toggleInventoryButton;
        private Button _toggleProductionButton;
        private Button _toggleShipControlButton;
        private Button _toggleNotificationsButton;
        private Button _togglePauseButton;
        private Button _timeHalfButton;
        private Button _timeNormalButton;
        private Button _timeFastButton;
        private Button _shipControlMapButton;
        private Button _shipControlInventoryButton;
        private Button _shipControlProductionButton;
        private Button _shipControlFeedButton;
        private Button _shipControlForcesButton;
        private VisualElement _utilityControlsPanel;
        private VisualElement _timeControlsPanel;
        private VisualElement _speedWidgetPanel;
        private VisualElement _shipControlPanel;
        private ScrollView _shipControlStatusScroll;
        private ScrollView _shipControlThermalScroll;
        private VisualElement _shipControlStatusList;
        private VisualElement _shipControlThermalList;
        private VisualElement _powerRoutingPanel;
        private Label _fuelReserveLabel;
        private VisualElement _forcesHoloPanel;
        private Label _targetHullLabel;
        private Label _shipControlSummaryLabel;
        private Label _powerRoutingProfileLabel;
        private Label _powerRoutingSummaryLabel;
        private Slider _powerRoutingEnginesSlider;
        private Slider _powerRoutingWeaponsSlider;
        private Slider _powerRoutingShieldsSlider;
        private Slider _powerRoutingReactorSlider;
        private Slider _powerRoutingSensorsSlider;
        private Label _powerRoutingEnginesValueLabel;
        private Label _powerRoutingWeaponsValueLabel;
        private Label _powerRoutingShieldsValueLabel;
        private Label _powerRoutingReactorValueLabel;
        private Label _powerRoutingSensorsValueLabel;
        private readonly Button[] _powerRoutingProfileButtons = new Button[PowerRoutingProfileCount];

        private readonly List<string> _notifications = new List<string>(12);
        private readonly List<Space4XMinimapContactKernelSnapshot> _minimapContactsScratch = new List<Space4XMinimapContactKernelSnapshot>(24);
        private readonly List<MinimapOverlayModeButtonRuntime> _minimapOverlayButtons = new List<MinimapOverlayModeButtonRuntime>(8);
        private readonly Dictionary<string, Space4XInventoryModuleKernelSnapshot> _inventoryModuleLookup = new Dictionary<string, Space4XInventoryModuleKernelSnapshot>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _inventoryOrganIntegrityOverrides = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, InventoryPopupRuntime> _inventoryPopups = new Dictionary<string, InventoryPopupRuntime>(StringComparer.Ordinal);
        private readonly List<string> _inventoryPopupRemovalScratch = new List<string>(8);
        private readonly List<string> _inventoryOrganOverrideRemovalScratch = new List<string>(16);
        private readonly Dictionary<string, int> _productionShiftByFacility = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _productionPowerScaleByFacility = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _productionRecipeIndexByFacility = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _productionLimbIndexByFacility = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _productionBaseRequiredCrewByFacility = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _productionBaseRequiredPowerByFacility = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<Space4XProductionFacilityKernelSnapshot> _productionFacilitiesScratch = new List<Space4XProductionFacilityKernelSnapshot>(16);
        private Space4XMainMenuOverlay _menuOverlay;
        private Space4XPlayerFlagshipController _flagshipController;
        private Space4XShipPresetCatalog _shipPresetCatalog;
        private Space4XFacilityCatalog _facilityCatalog;
        private PlayerInput _playerInput;
        private World _world;
        private EntityQuery _tickQuery;
        private bool _tickQueryValid;
        private EntityQuery _moduleCatalogQuery;
        private bool _moduleCatalogQueryValid;
        private EntityQuery _playerFlagshipQuery;
        private bool _playerFlagshipQueryValid;
        private UnityEngine.Camera _minimapCamera;
        private RenderTexture _minimapRenderTexture;
        private bool _hasFlagshipPose;
        private float3 _latestFlagshipPosition;
        private quaternion _latestFlagshipRotation;
        private Vector2 _hudPanelPosition = new Vector2(HudPanelDefaultX, HudPanelDefaultY);
        private EntityQuery _minimapFallbackQuery;
        private bool _minimapFallbackQueryValid;
        private float _nextRefreshAt;
        private float _nextLowFuelWarningAt;
        private float _nextLowFoodWarningAt;
        private float _nextLowSuppliesWarningAt;
        private bool _minimapVisible = true;
        private bool _inventoryVisible = false;
        private bool _productionVisible = false;
        private bool _notificationsVisible = true;
        private bool _forcesHoloVisible;
        private bool _shipControlVisible = true;
        private MinimapOverlayMode _minimapOverlayMode = MinimapOverlayMode.All;
        private string _activeContextNotification = string.Empty;
        private string _inventorySelectedModuleRef = string.Empty;
        private string _inventoryHoveredModuleRef = string.Empty;
        private Vector2 _inventorySegmentsScrollOffset = Vector2.zero;
        private Vector2 _inventoryCargoScrollOffset = Vector2.zero;
        private int _productionSelectedFacilityIndex;
        private string _productionSelectedFacilityRef = string.Empty;
        private bool _hudEnabled;
        private bool _layoutEditMode;
        private bool _layoutInitialized;
        private float _globalUiScale = 1f;
        private Space4XInRunHudKernelSnapshot _latestSnapshot;
        private bool _hasForceVelocitySample;
        private float3 _lastForceSampleVelocity;
        private float _lastForceSampleTime;
        private static Font _runtimeFont;
        private static ThemeStyleSheet _runtimeTheme;
        private static float _runtimeFontScale = 1f;
        private readonly Dictionary<string, WindowLayoutRuntime> _windowLayouts = new Dictionary<string, WindowLayoutRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, WindowLayoutSavedState> _savedWindowLayouts = new Dictionary<string, WindowLayoutSavedState>(StringComparer.Ordinal);
        private bool _migrateLegacyTopRightPads;
        private readonly PowerRoutingProfileSavedState[] _powerRoutingProfiles = new PowerRoutingProfileSavedState[PowerRoutingProfileCount];
        private int _powerRoutingActiveProfile;
        private bool _powerRoutingUiSuppress;

        private enum KernelMinimapRelation : byte
        {
            Self = 0,
            Ally = 1,
            Neutral = 2,
            Hostile = 3,
            Unknown = 4
        }

        private enum MinimapOverlayMode : byte
        {
            All = 0,
            Hostile = 1,
            Ally = 2,
            Neutral = 3,
            Em = 4,
            Thermal = 5,
            Gravitic = 6,
            Psi = 7
        }

        private enum RelationLookupSource : byte
        {
            None = 0,
            DiplomaticStatus = 1,
            FactionRelation = 2
        }

        private enum PowerRoutingDomain : byte
        {
            Engines = 0,
            Weapons = 1,
            Shields = 2,
            Reactor = 3,
            Sensors = 4
        }

        [Serializable]
        private sealed class HudLayoutSaveBlob
        {
            public int version = LayoutSchemaVersion;
            public float global_scale = 1f;
            public WindowLayoutSavedState[] windows = Array.Empty<WindowLayoutSavedState>();
        }

        [Serializable]
        private sealed class WindowLayoutSavedState
        {
            public string id = string.Empty;
            public float x;
            public float y;
            public float width;
            public float height;
            public float scale = 1f;
        }

        [Serializable]
        private sealed class PowerRoutingPrefsBlob
        {
            public int version = PowerRoutingPrefsSchemaVersion;
            public int active_profile;
            public PowerRoutingProfileSavedState[] profiles = Array.Empty<PowerRoutingProfileSavedState>();
        }

        [Serializable]
        private sealed class PowerRoutingProfileSavedState
        {
            public float engines = PowerRoutingDefaultPercent;
            public float weapons = PowerRoutingDefaultPercent;
            public float shields = PowerRoutingDefaultPercent;
            public float reactor = PowerRoutingDefaultPercent;
            public float sensors = PowerRoutingDefaultPercent;
        }

        private sealed class WindowLayoutRuntime
        {
            public string Id = string.Empty;
            public VisualElement Element;
            public VisualElement ResizeGrip;
            public Vector2 Position;
            public Vector2 Size;
            public float Scale = 1f;
            public float MinWidth = LayoutMinWindowWidth;
            public float MinHeight = LayoutMinWindowHeight;
            public bool ResizeWidth = true;
            public bool ResizeHeight = true;
            public bool LockAspect;
            public float AspectRatio = 1f;
            public bool DragActive;
            public bool ResizeActive;
            public int PointerId = -1;
            public Vector2 DragStartPointer;
            public Vector2 DragStartPosition;
            public Vector2 ResizeStartPointer;
            public Vector2 ResizeStartSize;
        }

        private sealed class InventoryPopupRuntime
        {
            public string ModuleRef = string.Empty;
            public VisualElement Panel;
            public VisualElement DragHandle;
            public Label TitleLabel;
            public VisualElement Content;
            public bool DragActive;
            public int PointerId = -1;
            public Vector2 DragStartPointer;
            public Vector2 DragStartPosition;
            public Vector2 Position;
        }

        private sealed class MinimapOverlayModeButtonRuntime
        {
            public MinimapOverlayMode Mode;
            public Button Button;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
                return;

            if (FindAnyObjectByType<Space4XInRunHudOverlay>() != null)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var go = new GameObject("Space4X In-Run HUD Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XInRunHudOverlay>();
        }

        private void OnEnable()
        {
            Space4XScenarioAuthority.NormalizeLegacyScenarioOverlayEnvironment();
            _hudEnabled = showHudByDefault;
            _layoutEditMode = false;
            _layoutInitialized = false;
            _nextRefreshAt = 0f;
            _nextLowFuelWarningAt = 0f;
            _nextLowFoodWarningAt = 0f;
            _nextLowSuppliesWarningAt = 0f;
            _hasFlagshipPose = false;
            _latestFlagshipPosition = float3.zero;
            _latestFlagshipRotation = quaternion.identity;
            _forcesHoloVisible = false;
            _shipControlVisible = true;
            _hasForceVelocitySample = false;
            _lastForceSampleVelocity = float3.zero;
            _lastForceSampleTime = 0f;
            LoadLayoutPreferences();
            LoadPowerRoutingPreferences();
            ApplyUserSettings(Space4XUserSettingsStore.LoadOrDefault());
            EnsureUiDocument();
            BuildUi();
            AddNotification("HUD kernel online.");
            AddNotification($"Keys: {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleHudKey)} HUD, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleInventoryKey)} inventory, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleMinimapKey)} minimap, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeCycleMinimapOverlayKey)} minimap overlay cycle, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleNotificationsKey)} notifications, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleForcesHoloKey)} forces holo, B manual aim toggle, LMB hold manual fire, MMB fighter target cycle, Inventory MMB inspect, Shift+drag multi-target (Ctrl append).");
            AddNotification($"Layout: {Space4XUserSettingsStore.FormatKeyLabel(_runtimeToggleLayoutEditModeKey)} edit mode, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeDecreaseUiScaleKey)}/{Space4XUserSettingsStore.FormatKeyLabel(_runtimeIncreaseUiScaleKey)} UI scale, Ctrl+Wheel window scale, {Space4XUserSettingsStore.FormatKeyLabel(_runtimeResetLayoutKey)} reset.");
        }

        private void OnDisable()
        {
            ReleaseAllWindowCaptures();
            CloseAllInventoryPopups();
            SaveLayoutPreferences();
            SavePowerRoutingPreferences();
            DisposeQuery(ref _tickQuery, ref _tickQueryValid);
            DisposeQuery(ref _moduleCatalogQuery, ref _moduleCatalogQueryValid);
            DisposeQuery(ref _playerFlagshipQuery, ref _playerFlagshipQueryValid);
            DisposeQuery(ref _minimapFallbackQuery, ref _minimapFallbackQueryValid);
            CleanupMinimapRenderResources();
        }

        public void ApplyUserSettings(Space4XUserSettingsData settings)
        {
            var normalized = Space4XUserSettingsStore.Normalize(settings ?? Space4XUserSettingsStore.CreateDefaults());
            _runtimeToggleHudKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_hud, toggleHudKey);
            _runtimeToggleInventoryKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_inventory, toggleInventoryKey);
            _runtimeToggleMinimapKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_minimap, toggleMinimapKey);
            _runtimeCycleMinimapOverlayKey = cycleMinimapOverlayKey;
            _runtimeToggleNotificationsKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_notifications, toggleNotificationsKey);
            _runtimeToggleForcesHoloKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_forces_holo, toggleForcesHoloKey);
            _runtimeToggleLayoutEditModeKey = Space4XUserSettingsStore.ResolveKey(normalized.key_toggle_layout_edit, toggleLayoutEditModeKey);
            _runtimeIncreaseUiScaleKey = Space4XUserSettingsStore.ResolveKey(normalized.key_ui_scale_increase, increaseUiScaleKey);
            _runtimeDecreaseUiScaleKey = Space4XUserSettingsStore.ResolveKey(normalized.key_ui_scale_decrease, decreaseUiScaleKey);
            _runtimeResetLayoutKey = Space4XUserSettingsStore.ResolveKey(normalized.key_layout_reset, resetLayoutKey);
            _runtimeHudFadeOnPowerDeficit = normalized.hud_fade_on_power_deficit != 0;
            _globalUiScale = Mathf.Clamp(normalized.hud_scale, LayoutMinScale, LayoutMaxScale);
            var previousFontScale = _runtimeFontScale;
            _runtimeFontScale = Mathf.Clamp(normalized.font_scale, Space4XUserSettingsStore.FontScaleMin, Space4XUserSettingsStore.FontScaleMax);
            if (_layoutInitialized)
            {
                ApplyAllWindowLayouts(clampToViewport: true);
            }

            if (_root != null && !Mathf.Approximately(previousFontScale, _runtimeFontScale))
            {
                RescaleHudFontSizes(previousFontScale, _runtimeFontScale);
            }
        }

        private void Update()
        {
            if (_root == null)
            {
                EnsureUiDocument();
                BuildUi();
                if (_root == null)
                {
                    return;
                }
            }

            var keyboard = Keyboard.current;
            if (WasPressedThisFrame(keyboard, _runtimeToggleHudKey))
            {
                _hudEnabled = !_hudEnabled;
                AddNotification(_hudEnabled ? "HUD shown." : "HUD hidden.");
            }

            if (WasPressedThisFrame(keyboard, _runtimeToggleInventoryKey))
            {
                TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleInventory);
            }

            if (WasPressedThisFrame(keyboard, _runtimeToggleMinimapKey))
            {
                TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleMinimap);
            }

            if (WasPressedThisFrame(keyboard, _runtimeCycleMinimapOverlayKey))
            {
                CycleMinimapOverlayMode(1, notify: true);
            }

            if (WasPressedThisFrame(keyboard, _runtimeToggleNotificationsKey))
            {
                TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleNotifications);
            }

            if (WasPressedThisFrame(keyboard, _runtimeToggleForcesHoloKey))
            {
                TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleForcesHolo);
            }

            if (WasPressedThisFrame(keyboard, _runtimeToggleLayoutEditModeKey))
            {
                _layoutEditMode = !_layoutEditMode;
                if (!_layoutEditMode)
                {
                    ReleaseAllWindowCaptures();
                }

                AddNotification(_layoutEditMode
                    ? "Layout edit mode enabled: drag windows, drag grips to resize."
                    : "Layout edit mode disabled.");
            }

            if (WasPressedThisFrame(keyboard, _runtimeIncreaseUiScaleKey))
            {
                SetGlobalUiScale(_globalUiScale + LayoutScaleStep);
            }

            if (WasPressedThisFrame(keyboard, _runtimeDecreaseUiScaleKey))
            {
                SetGlobalUiScale(_globalUiScale - LayoutScaleStep);
            }

            if (WasPressedThisFrame(keyboard, _runtimeResetLayoutKey))
            {
                ResetWindowLayouts();
                AddNotification("HUD layout reset.");
            }

            if (WasMovementInputPressedThisFrame(keyboard))
            {
                ClearHudFocus();
            }

            if (!_hudEnabled)
            {
                if (_root != null)
                {
                    _root.style.display = DisplayStyle.None;
                }

                return;
            }

            if (UTime.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt = UTime.unscaledTime + 1f / Mathf.Max(1f, refreshRateHz);
            RefreshSnapshot();
            ApplySnapshotToUi(_latestSnapshot);
        }

        public bool TryExecuteKernelCommand(Space4XInRunHudKernelCommand command)
        {
            switch (command)
            {
                case Space4XInRunHudKernelCommand.ToggleMinimap:
                    _minimapVisible = !_minimapVisible;
                    AddNotification(_minimapVisible ? "Minimap shown." : "Minimap hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.ToggleNotifications:
                    _notificationsVisible = !_notificationsVisible;
                    AddNotification(_notificationsVisible ? "Notifications shown." : "Notifications hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.ToggleInventory:
                    _inventoryVisible = !_inventoryVisible;
                    if (!_inventoryVisible)
                    {
                        CloseAllInventoryPopups();
                    }
                    AddNotification(_inventoryVisible ? "Inventory shown." : "Inventory hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.ToggleProduction:
                    _productionVisible = !_productionVisible;
                    AddNotification(_productionVisible ? "Production shown." : "Production hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.ToggleForcesHolo:
                    _forcesHoloVisible = !_forcesHoloVisible;
                    AddNotification(_forcesHoloVisible ? "Forces holo shown." : "Forces holo hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.ToggleShipControl:
                    _shipControlVisible = !_shipControlVisible;
                    AddNotification(_shipControlVisible ? "Ship control shown." : "Ship control hidden.");
                    return true;
                case Space4XInRunHudKernelCommand.TogglePause:
                    return TryTogglePause();
                case Space4XInRunHudKernelCommand.SetTimeHalf:
                    return TrySetTimeSpeed(0.5f);
                case Space4XInRunHudKernelCommand.SetTimeNormal:
                    return TrySetTimeSpeed(1f);
                case Space4XInRunHudKernelCommand.SetTimeFast:
                    return TrySetTimeSpeed(2f);
                default:
                    return false;
            }
        }

        public Space4XInRunHudKernelSnapshot CaptureKernelSnapshot()
        {
            RefreshSnapshot();
            return _latestSnapshot;
        }

        private void EnsureUiDocument()
        {
            if (_document != null)
                return;

            _document = gameObject.GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettings.themeStyleSheet = ResolveRuntimeTheme();
                _document.panelSettings = _panelSettings;
            }
            else if (_document.panelSettings.themeStyleSheet == null)
            {
                _document.panelSettings.themeStyleSheet = ResolveRuntimeTheme();
            }
        }

        private void BuildUi()
        {
            if (_document == null)
            {
                return;
            }

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                return;
            }

            _root.Clear();
            _root.name = Space4XInRunHudElementIds.Root;
            _root.style.flexGrow = 1f;
            _root.style.alignItems = Align.Stretch;
            _root.style.justifyContent = Justify.FlexStart;
            _root.focusable = false;
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                _root.style.unityFont = runtimeFont;
            }

            _root.UnregisterCallback<KeyDownEvent>(OnHudRootKeyDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<KeyDownEvent>(OnHudRootKeyDown, TrickleDown.TrickleDown);
            _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged, TrickleDown.NoTrickleDown);
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged, TrickleDown.NoTrickleDown);
            _windowLayouts.Clear();

            _panel = new VisualElement
            {
                name = Space4XInRunHudElementIds.RootPanel
            };
            _panel.style.position = Position.Absolute;
            _panel.style.width = RootPanelDefaultWidth;
            _panel.style.paddingLeft = 10f;
            _panel.style.paddingRight = 10f;
            _panel.style.paddingTop = 8f;
            _panel.style.paddingBottom = 10f;
            _panel.style.overflow = Overflow.Hidden;
            _panel.style.backgroundColor = new Color(0.06f, 0.10f, 0.14f, 0.82f);
            _panel.style.borderTopLeftRadius = 8f;
            _panel.style.borderTopRightRadius = 8f;
            _panel.style.borderBottomLeftRadius = 8f;
            _panel.style.borderBottomRightRadius = 8f;
            _panel.style.borderTopWidth = 1f;
            _panel.style.borderRightWidth = 1f;
            _panel.style.borderBottomWidth = 1f;
            _panel.style.borderLeftWidth = 1f;
            var borderColor = new Color(0.32f, 0.56f, 0.71f, 0.9f);
            _panel.style.borderTopColor = borderColor;
            _panel.style.borderRightColor = borderColor;
            _panel.style.borderBottomColor = borderColor;
            _panel.style.borderLeftColor = borderColor;
            _panel.focusable = false;
            _root.Add(_panel);
            ApplyHudPanelPosition();

            var header = CreateHeaderLabel("Status");
            header.style.fontSize = Fsi(12);
            header.style.marginBottom = 4f;
            _panel.Add(header);

            var statColumn = new VisualElement { name = Space4XInRunHudElementIds.StatColumn };
            statColumn.style.flexDirection = FlexDirection.Column;
            statColumn.style.marginTop = 2f;
            statColumn.style.flexShrink = 0f;
            _panel.Add(statColumn);

            var integrityGroup = CreateBarGroup(string.Empty);
            integrityGroup.Add(CreateMetricBar("HULL", Space4XInRunHudElementIds.HealthLabel, new Color(0.78f, 0.20f, 0.24f, 0.95f), out _healthBarFill, out _healthLabel));
            integrityGroup.Add(CreateMetricBar("SHLD", Space4XInRunHudElementIds.ShieldsLabel, new Color(0.27f, 0.71f, 0.91f, 0.95f), out _shieldsBarFill, out _shieldsLabel));
            integrityGroup.Add(CreateMetricBar("ARMR", Space4XInRunHudElementIds.ArmorLabel, new Color(0.88f, 0.61f, 0.27f, 0.95f), out _armorBarFill, out _armorLabel));
            statColumn.Add(integrityGroup);

            var sustainGroup = CreateBarGroup(string.Empty);
            sustainGroup.Add(CreateMetricBar("SUPP", Space4XInRunHudElementIds.SuppliesLabel, new Color(0.93f, 0.93f, 0.93f, 0.95f), out _suppliesBarFill, out _suppliesLabel));
            sustainGroup.Add(CreateMetricBar("FOOD", Space4XInRunHudElementIds.FoodLabel, new Color(0.53f, 0.84f, 0.53f, 0.95f), out _foodBarFill, out _foodLabel));
            sustainGroup.Add(CreateMetricBar("WATR", Space4XInRunHudElementIds.FuelLabel, new Color(0.24f, 0.56f, 0.90f, 0.95f), out _waterBarFill, out _fuelLabel));
            statColumn.Add(sustainGroup);

            var logisticGroup = CreateBarGroup(string.Empty);
            logisticGroup.Add(CreateMetricBar("CREW", Space4XInRunHudElementIds.CrewLabel, new Color(0.59f, 0.62f, 0.66f, 0.95f), out _crewBarFill, out _crewLabel));
            logisticGroup.Add(CreateMetricBar("AMMO", Space4XInRunHudElementIds.AmmoLabel, new Color(0.92f, 0.82f, 0.31f, 0.95f), out _ammoBarFill, out _ammoLabel));
            logisticGroup.Add(CreateMetricBar("FUEL", Space4XInRunHudElementIds.FuelReserveLabel, new Color(0.84f, 0.22f, 0.22f, 0.95f), out _fuelReserveBarFill, out _fuelReserveLabel));
            statColumn.Add(logisticGroup);

            var telemetryRow = new VisualElement();
            telemetryRow.style.flexDirection = FlexDirection.Column;
            telemetryRow.style.marginTop = 4f;
            telemetryRow.style.flexShrink = 0f;
            _panel.Add(telemetryRow);

            _powerLabel = CreateMetricLabel(Space4XInRunHudElementIds.PowerLabel, "PWR n/a");
            _powerLabel.style.fontSize = Fsi(8);
            _powerLabel.style.marginTop = 0f;
            _powerLabel.style.minHeight = 11f;
            _timeLabel = CreateMetricLabel(Space4XInRunHudElementIds.TimeLabel, "Time n/a");
            _timeLabel.style.fontSize = Fsi(8);
            _timeLabel.style.marginTop = 0f;
            _timeLabel.style.minHeight = 11f;
            telemetryRow.Add(_powerLabel);
            telemetryRow.Add(_timeLabel);

            _utilityControlsPanel = CreateControlsPad(Space4XInRunHudElementIds.UtilityControlsPanel);
            _utilityControlsPanel.style.position = Position.Absolute;
            _utilityControlsPanel.style.right = UtilityControlsDefaultRight;
            _utilityControlsPanel.style.top = UtilityControlsDefaultTop;
            _utilityControlsPanel.style.width = UtilityControlsDefaultWidth;
            _root.Add(_utilityControlsPanel);

            _timeControlsPanel = CreateControlsPad(Space4XInRunHudElementIds.TimeControlsPanel);
            _timeControlsPanel.style.position = Position.Absolute;
            _timeControlsPanel.style.right = TimeControlsDefaultRight;
            _timeControlsPanel.style.top = TimeControlsDefaultTop;
            _timeControlsPanel.style.width = TimeControlsDefaultWidth;
            _root.Add(_timeControlsPanel);

            _toggleMinimapButton = CreateControlButton(Space4XInRunHudElementIds.ToggleMinimapButton, "Map");
            _toggleMinimapButton.tooltip = "Toggle minimap";
            _toggleMinimapButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleMinimap);
            _utilityControlsPanel.Add(_toggleMinimapButton);

            _cycleMinimapOverlayButton = CreateControlButton(Space4XInRunHudElementIds.CycleMinimapOverlayButton, "Ovl");
            _cycleMinimapOverlayButton.tooltip = "Cycle minimap overlay mode";
            _cycleMinimapOverlayButton.clicked += () => CycleMinimapOverlayMode(1, notify: true);
            _utilityControlsPanel.Add(_cycleMinimapOverlayButton);

            _toggleInventoryButton = CreateControlButton(Space4XInRunHudElementIds.ToggleInventoryButton, "Inv");
            _toggleInventoryButton.tooltip = "Toggle inventory";
            _toggleInventoryButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleInventory);
            _utilityControlsPanel.Add(_toggleInventoryButton);

            _toggleProductionButton = CreateControlButton(Space4XInRunHudElementIds.ToggleProductionButton, "Prod");
            _toggleProductionButton.tooltip = "Toggle production";
            _toggleProductionButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleProduction);
            _utilityControlsPanel.Add(_toggleProductionButton);

            _toggleShipControlButton = CreateControlButton(Space4XInRunHudElementIds.ToggleShipControlButton, "Ctrl");
            _toggleShipControlButton.tooltip = "Toggle ship control panel";
            _toggleShipControlButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleShipControl);
            _utilityControlsPanel.Add(_toggleShipControlButton);

            _toggleNotificationsButton = CreateControlButton(Space4XInRunHudElementIds.ToggleNotificationsButton, "Feed");
            _toggleNotificationsButton.tooltip = "Toggle feed";
            _toggleNotificationsButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleNotifications);
            _utilityControlsPanel.Add(_toggleNotificationsButton);

            _togglePauseButton = CreateControlButton(Space4XInRunHudElementIds.TogglePauseButton, "||");
            _togglePauseButton.tooltip = "Pause / Resume";
            _togglePauseButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.TogglePause);
            _utilityControlsPanel.Add(_togglePauseButton);

            _timeHalfButton = CreateControlButton(Space4XInRunHudElementIds.TimeHalfButton, "0.5");
            _timeHalfButton.tooltip = "Set time to 0.5x";
            _timeHalfButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.SetTimeHalf);
            _timeControlsPanel.Add(_timeHalfButton);

            _timeNormalButton = CreateControlButton(Space4XInRunHudElementIds.TimeNormalButton, "1x");
            _timeNormalButton.tooltip = "Set time to 1x";
            _timeNormalButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.SetTimeNormal);
            _timeControlsPanel.Add(_timeNormalButton);

            _timeFastButton = CreateControlButton(Space4XInRunHudElementIds.TimeFastButton, "2x");
            _timeFastButton.tooltip = "Set time to 2x";
            _timeFastButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.SetTimeFast);
            _timeControlsPanel.Add(_timeFastButton);

            _minimapPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.MinimapPanel
            };
            _minimapPanel.style.position = Position.Absolute;
            _minimapPanel.style.top = 20f;
            _minimapPanel.style.right = 20f;
            _minimapPanel.style.width = MinimapPanelSize;
            _minimapPanel.style.alignItems = Align.Center;
            _minimapPanel.style.paddingTop = 2f;
            var minimapCircle = new VisualElement();
            minimapCircle.style.width = MinimapViewportSize;
            minimapCircle.style.height = MinimapViewportSize;
            minimapCircle.style.borderTopLeftRadius = MinimapViewportSize * 0.5f;
            minimapCircle.style.borderTopRightRadius = MinimapViewportSize * 0.5f;
            minimapCircle.style.borderBottomLeftRadius = MinimapViewportSize * 0.5f;
            minimapCircle.style.borderBottomRightRadius = MinimapViewportSize * 0.5f;
            minimapCircle.style.overflow = Overflow.Hidden;
            minimapCircle.style.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
            minimapCircle.style.borderTopWidth = 2f;
            minimapCircle.style.borderRightWidth = 2f;
            minimapCircle.style.borderBottomWidth = 2f;
            minimapCircle.style.borderLeftWidth = 2f;
            var minimapRingColor = new Color(0.28f, 0.52f, 0.66f, 0.95f);
            minimapCircle.style.borderTopColor = minimapRingColor;
            minimapCircle.style.borderRightColor = minimapRingColor;
            minimapCircle.style.borderBottomColor = minimapRingColor;
            minimapCircle.style.borderLeftColor = minimapRingColor;
            _minimapPanel.Add(minimapCircle);

            _minimapViewportImage = new Image
            {
                name = Space4XInRunHudElementIds.MinimapViewport
            };
            _minimapViewportImage.scaleMode = ScaleMode.ScaleAndCrop;
            _minimapViewportImage.image = _minimapRenderTexture;
            _minimapViewportImage.style.flexGrow = 1f;
            _minimapViewportImage.style.width = Length.Percent(100f);
            _minimapViewportImage.style.height = Length.Percent(100f);
            minimapCircle.Add(_minimapViewportImage);

            _minimapOverlayLayer = new VisualElement
            {
                name = "space4x.ui.hud.minimap.overlay_layer"
            };
            _minimapOverlayLayer.style.position = Position.Absolute;
            _minimapOverlayLayer.style.left = 0f;
            _minimapOverlayLayer.style.right = 0f;
            _minimapOverlayLayer.style.top = 0f;
            _minimapOverlayLayer.style.bottom = 0f;
            _minimapOverlayLayer.pickingMode = PickingMode.Ignore;
            minimapCircle.Add(_minimapOverlayLayer);

            _minimapSummaryLabel = new Label("Minimap contacts: 0")
            {
                name = Space4XInRunHudElementIds.MinimapSummaryLabel
            };
            _minimapSummaryLabel.style.marginTop = 4f;
            _minimapSummaryLabel.style.fontSize = Fsi(10);
            _minimapSummaryLabel.style.color = new Color(0.82f, 0.90f, 0.95f, 1f);
            _minimapSummaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _minimapPanel.Add(_minimapSummaryLabel);

            _minimapOverlayModeLabel = new Label("Overlay: All")
            {
                name = Space4XInRunHudElementIds.MinimapOverlayModeLabel
            };
            _minimapOverlayModeLabel.style.marginTop = 1f;
            _minimapOverlayModeLabel.style.fontSize = Fsi(9);
            _minimapOverlayModeLabel.style.color = new Color(0.68f, 0.86f, 0.94f, 1f);
            _minimapOverlayModeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _minimapPanel.Add(_minimapOverlayModeLabel);

            var minimapOverlayButtonsRow = new VisualElement();
            minimapOverlayButtonsRow.style.flexDirection = FlexDirection.Row;
            minimapOverlayButtonsRow.style.flexWrap = Wrap.Wrap;
            minimapOverlayButtonsRow.style.justifyContent = Justify.Center;
            minimapOverlayButtonsRow.style.alignItems = Align.Center;
            minimapOverlayButtonsRow.style.width = MinimapPanelSize;
            minimapOverlayButtonsRow.style.marginTop = 1f;
            _minimapPanel.Add(minimapOverlayButtonsRow);
            _minimapOverlayButtons.Clear();
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "ALL", MinimapOverlayMode.All, "Show all overlays");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "H", MinimapOverlayMode.Hostile, "Show hostile contacts");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "A", MinimapOverlayMode.Ally, "Show allied contacts");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "N", MinimapOverlayMode.Neutral, "Show neutral contacts");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "E", MinimapOverlayMode.Em, "Show EM overlay");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "T", MinimapOverlayMode.Thermal, "Show thermal overlay");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "G", MinimapOverlayMode.Gravitic, "Show gravitic overlay");
            AddMinimapOverlayModeButton(minimapOverlayButtonsRow, "P", MinimapOverlayMode.Psi, "Show psi overlay");

            _minimapContactList = new VisualElement
            {
                name = Space4XInRunHudElementIds.MinimapContactList
            };
            _minimapContactList.style.flexDirection = FlexDirection.Column;
            _minimapContactList.style.marginTop = 2f;
            _minimapContactList.style.width = MinimapPanelSize;
            _minimapContactList.style.display = DisplayStyle.None;
            _minimapPanel.Add(_minimapContactList);
            _root.Add(_minimapPanel);

            _inventoryPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.InventoryPanel
            };
            _inventoryPanel.style.position = Position.Absolute;
            _inventoryPanel.style.top = InventoryDefaultTop;
            _inventoryPanel.style.right = InventoryDefaultRight;
            _inventoryPanel.style.width = InventoryDefaultWidth;
            _inventoryPanel.style.minHeight = InventoryDefaultHeight;
            _inventoryPanel.style.paddingTop = 6f;
            _inventoryPanel.style.paddingBottom = 6f;
            _inventoryPanel.style.paddingLeft = 7f;
            _inventoryPanel.style.paddingRight = 7f;
            _inventoryPanel.style.flexDirection = FlexDirection.Column;
            _inventoryPanel.style.overflow = Overflow.Hidden;
            _inventoryPanel.style.backgroundColor = new Color(0.09f, 0.14f, 0.19f, 0.90f);
            _inventoryPanel.style.borderTopWidth = 1f;
            _inventoryPanel.style.borderRightWidth = 1f;
            _inventoryPanel.style.borderBottomWidth = 1f;
            _inventoryPanel.style.borderLeftWidth = 1f;
            var inventoryBorder = new Color(0.26f, 0.40f, 0.52f, 0.9f);
            _inventoryPanel.style.borderTopColor = inventoryBorder;
            _inventoryPanel.style.borderRightColor = inventoryBorder;
            _inventoryPanel.style.borderBottomColor = inventoryBorder;
            _inventoryPanel.style.borderLeftColor = inventoryBorder;
            _root.Add(_inventoryPanel);

            _inventoryHeaderLabel = new Label("Inventory / Loadout");
            _inventoryHeaderLabel.style.fontSize = Fsi(12);
            _inventoryHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inventoryHeaderLabel.style.color = new Color(0.88f, 0.93f, 0.97f, 1f);
            _inventoryHeaderLabel.style.marginBottom = 1f;
            _inventoryPanel.Add(_inventoryHeaderLabel);

            _inventoryHintLabel = new Label("LMB select module. MMB inspect module window.");
            _inventoryHintLabel.name = Space4XInRunHudElementIds.InventoryHintLabel;
            _inventoryHintLabel.style.fontSize = Fsi(9);
            _inventoryHintLabel.style.color = new Color(0.70f, 0.82f, 0.90f, 1f);
            _inventoryHintLabel.style.marginBottom = 3f;
            _inventoryPanel.Add(_inventoryHintLabel);

            _inventoryList = new VisualElement
            {
                name = Space4XInRunHudElementIds.InventoryList
            };
            _inventoryList.style.flexDirection = FlexDirection.Column;
            _inventoryList.style.flexGrow = 1f;
            _inventoryList.style.overflow = Overflow.Hidden;
            _inventoryPanel.Add(_inventoryList);

            _productionPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.ProductionPanel
            };
            _productionPanel.style.position = Position.Absolute;
            _productionPanel.style.top = ProductionDefaultTop;
            _productionPanel.style.right = ProductionDefaultRight;
            _productionPanel.style.width = ProductionDefaultWidth;
            _productionPanel.style.minHeight = ProductionDefaultHeight;
            _productionPanel.style.paddingTop = 6f;
            _productionPanel.style.paddingBottom = 6f;
            _productionPanel.style.paddingLeft = 7f;
            _productionPanel.style.paddingRight = 7f;
            _productionPanel.style.flexDirection = FlexDirection.Column;
            _productionPanel.style.overflow = Overflow.Hidden;
            _productionPanel.style.backgroundColor = new Color(0.08f, 0.13f, 0.18f, 0.90f);
            _productionPanel.style.borderTopWidth = 1f;
            _productionPanel.style.borderRightWidth = 1f;
            _productionPanel.style.borderBottomWidth = 1f;
            _productionPanel.style.borderLeftWidth = 1f;
            var productionBorder = new Color(0.28f, 0.42f, 0.54f, 0.9f);
            _productionPanel.style.borderTopColor = productionBorder;
            _productionPanel.style.borderRightColor = productionBorder;
            _productionPanel.style.borderBottomColor = productionBorder;
            _productionPanel.style.borderLeftColor = productionBorder;
            _root.Add(_productionPanel);

            var productionHeader = new Label("Production");
            productionHeader.style.fontSize = Fsi(12);
            productionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            productionHeader.style.color = new Color(0.88f, 0.93f, 0.97f, 1f);
            productionHeader.style.marginBottom = 2f;
            _productionPanel.Add(productionHeader);

            var productionHint = new Label("Facilities / recipes / shifts / power / limbs");
            productionHint.style.fontSize = Fsi(9);
            productionHint.style.color = new Color(0.70f, 0.82f, 0.90f, 1f);
            productionHint.style.marginBottom = 3f;
            _productionPanel.Add(productionHint);

            _productionList = new VisualElement
            {
                name = Space4XInRunHudElementIds.ProductionList
            };
            _productionList.style.flexDirection = FlexDirection.Column;
            _productionList.style.flexGrow = 1f;
            _productionList.style.overflow = Overflow.Hidden;
            _productionPanel.Add(_productionList);

            _targetPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.TargetPanel
            };
            _targetPanel.style.position = Position.Absolute;
            _targetPanel.style.top = TargetDefaultTop;
            _targetPanel.style.right = TargetDefaultRight;
            _targetPanel.style.width = TargetDefaultWidth;
            _targetPanel.style.minHeight = TargetDefaultHeight;
            _targetPanel.style.paddingTop = 4f;
            _targetPanel.style.paddingBottom = 4f;
            _targetPanel.style.paddingLeft = 6f;
            _targetPanel.style.paddingRight = 6f;
            _targetPanel.style.backgroundColor = new Color(0.07f, 0.12f, 0.17f, 0.9f);
            _targetPanel.style.borderTopWidth = 1f;
            _targetPanel.style.borderRightWidth = 1f;
            _targetPanel.style.borderBottomWidth = 1f;
            _targetPanel.style.borderLeftWidth = 1f;
            var targetBorder = new Color(0.32f, 0.50f, 0.67f, 0.92f);
            _targetPanel.style.borderTopColor = targetBorder;
            _targetPanel.style.borderRightColor = targetBorder;
            _targetPanel.style.borderBottomColor = targetBorder;
            _targetPanel.style.borderLeftColor = targetBorder;
            _root.Add(_targetPanel);

            var targetHeader = new Label("Target");
            targetHeader.style.fontSize = Fsi(11);
            targetHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            targetHeader.style.color = new Color(0.88f, 0.93f, 0.97f, 1f);
            _targetPanel.Add(targetHeader);

            _targetPrimaryLabel = CreateMetricLabel(Space4XInRunHudElementIds.TargetPrimaryLabel, "No target selected.");
            _targetPrimaryLabel.style.fontSize = Fsi(10);
            _targetPrimaryLabel.style.marginTop = 1f;
            _targetPrimaryLabel.style.minHeight = 12f;
            _targetPanel.Add(_targetPrimaryLabel);

            _targetSecondaryLabel = CreateMetricLabel(Space4XInRunHudElementIds.TargetSecondaryLabel, "Mode 1/2: LMB target | Shift+drag multi");
            _targetSecondaryLabel.style.fontSize = Fsi(9);
            _targetSecondaryLabel.style.marginTop = 0f;
            _targetSecondaryLabel.style.minHeight = 12f;
            _targetSecondaryLabel.style.color = new Color(0.74f, 0.84f, 0.91f, 1f);
            _targetPanel.Add(_targetSecondaryLabel);

            var targetHullBar = CreateMetricBar(
                "HULL",
                Space4XInRunHudElementIds.TargetHullLabel,
                new Color(0.82f, 0.28f, 0.32f, 0.96f),
                out _targetHullBarFill,
                out _targetHullLabel);
            targetHullBar.style.marginTop = 2f;
            _targetPanel.Add(targetHullBar);

            _notificationPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.NotificationPanel
            };
            _notificationPanel.style.position = Position.Absolute;
            _notificationPanel.style.right = 14f;
            _notificationPanel.style.top = 182f;
            _notificationPanel.style.width = 220f;
            _notificationPanel.style.height = 164f;
            _notificationPanel.style.paddingTop = 4f;
            _notificationPanel.style.paddingBottom = 4f;
            _notificationPanel.style.paddingLeft = 4f;
            _notificationPanel.style.paddingRight = 4f;
            _notificationPanel.style.overflow = Overflow.Hidden;
            _notificationPanel.style.backgroundColor = new Color(0.06f, 0.10f, 0.14f, 0.72f);
            _notificationPanel.style.borderTopWidth = 1f;
            _notificationPanel.style.borderRightWidth = 1f;
            _notificationPanel.style.borderBottomWidth = 1f;
            _notificationPanel.style.borderLeftWidth = 1f;
            var notificationBorder = new Color(0.24f, 0.36f, 0.45f, 0.9f);
            _notificationPanel.style.borderTopColor = notificationBorder;
            _notificationPanel.style.borderRightColor = notificationBorder;
            _notificationPanel.style.borderBottomColor = notificationBorder;
            _notificationPanel.style.borderLeftColor = notificationBorder;
            _root.Add(_notificationPanel);

            var feedHeader = new Label("Feed");
            feedHeader.style.fontSize = Fsi(12);
            feedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            feedHeader.style.color = new Color(0.88f, 0.93f, 0.97f, 1f);
            feedHeader.style.marginBottom = 2f;
            _notificationPanel.Add(feedHeader);

            _notificationScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "space4x.ui.hud.scroll.notifications"
            };
            _notificationScroll.style.flexGrow = 1f;
            _notificationScroll.style.marginTop = 2f;
            _notificationScroll.style.height = 128f;
            _notificationScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _notificationScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _notificationScroll.contentViewport.style.overflow = Overflow.Hidden;
            _notificationScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            _notificationPanel.Add(_notificationScroll);

            _notificationList = new VisualElement
            {
                name = Space4XInRunHudElementIds.NotificationList
            };
            _notificationList.style.flexDirection = FlexDirection.Column;
            _notificationList.style.overflow = Overflow.Hidden;
            _notificationList.style.marginTop = 1f;
            _notificationScroll.Add(_notificationList);

            _shipControlPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.ShipControlPanel
            };
            _shipControlPanel.style.position = Position.Absolute;
            _shipControlPanel.style.right = ShipControlDefaultRight;
            _shipControlPanel.style.top = ShipControlDefaultTop;
            _shipControlPanel.style.width = ShipControlDefaultWidth;
            _shipControlPanel.style.height = ShipControlDefaultHeight;
            _shipControlPanel.style.paddingTop = 5f;
            _shipControlPanel.style.paddingBottom = 5f;
            _shipControlPanel.style.paddingLeft = 6f;
            _shipControlPanel.style.paddingRight = 6f;
            _shipControlPanel.style.backgroundColor = new Color(0.05f, 0.10f, 0.14f, 0.90f);
            _shipControlPanel.style.borderTopWidth = 1f;
            _shipControlPanel.style.borderRightWidth = 1f;
            _shipControlPanel.style.borderBottomWidth = 1f;
            _shipControlPanel.style.borderLeftWidth = 1f;
            var shipControlBorder = new Color(0.30f, 0.49f, 0.63f, 0.92f);
            _shipControlPanel.style.borderTopColor = shipControlBorder;
            _shipControlPanel.style.borderRightColor = shipControlBorder;
            _shipControlPanel.style.borderBottomColor = shipControlBorder;
            _shipControlPanel.style.borderLeftColor = shipControlBorder;
            _shipControlPanel.style.borderTopLeftRadius = 7f;
            _shipControlPanel.style.borderTopRightRadius = 7f;
            _shipControlPanel.style.borderBottomLeftRadius = 7f;
            _shipControlPanel.style.borderBottomRightRadius = 7f;
            _shipControlPanel.style.flexDirection = FlexDirection.Column;
            _root.Add(_shipControlPanel);

            var shipControlHeaderRow = new VisualElement();
            shipControlHeaderRow.style.flexDirection = FlexDirection.Row;
            shipControlHeaderRow.style.alignItems = Align.Center;
            shipControlHeaderRow.style.justifyContent = Justify.SpaceBetween;
            shipControlHeaderRow.style.marginBottom = 2f;
            _shipControlPanel.Add(shipControlHeaderRow);

            var shipControlTitle = new Label("Ship Control")
            {
                focusable = false
            };
            shipControlTitle.style.fontSize = Fsi(10);
            shipControlTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            shipControlTitle.style.color = new Color(0.87f, 0.95f, 0.99f, 1f);
            shipControlHeaderRow.Add(shipControlTitle);

            var shipControlHideButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.hide", "X");
            shipControlHideButton.style.width = 22f;
            shipControlHideButton.style.minWidth = 22f;
            shipControlHideButton.style.height = 18f;
            shipControlHideButton.style.marginRight = 0f;
            shipControlHideButton.tooltip = "Hide ship control panel";
            shipControlHideButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleShipControl);
            shipControlHeaderRow.Add(shipControlHideButton);

            var shipControlActionRow = new VisualElement();
            shipControlActionRow.style.flexDirection = FlexDirection.Row;
            shipControlActionRow.style.alignItems = Align.Center;
            shipControlActionRow.style.marginBottom = 2f;
            _shipControlPanel.Add(shipControlActionRow);

            _shipControlMapButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.btn.map", "Map");
            _shipControlMapButton.style.width = 32f;
            _shipControlMapButton.style.minWidth = 32f;
            _shipControlMapButton.style.height = 20f;
            _shipControlMapButton.style.fontSize = Fsi(8);
            _shipControlMapButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleMinimap);
            shipControlActionRow.Add(_shipControlMapButton);

            _shipControlInventoryButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.btn.inv", "Inv");
            _shipControlInventoryButton.style.width = 32f;
            _shipControlInventoryButton.style.minWidth = 32f;
            _shipControlInventoryButton.style.height = 20f;
            _shipControlInventoryButton.style.fontSize = Fsi(8);
            _shipControlInventoryButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleInventory);
            shipControlActionRow.Add(_shipControlInventoryButton);

            _shipControlProductionButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.btn.prod", "Prod");
            _shipControlProductionButton.style.width = 34f;
            _shipControlProductionButton.style.minWidth = 34f;
            _shipControlProductionButton.style.height = 20f;
            _shipControlProductionButton.style.fontSize = Fsi(8);
            _shipControlProductionButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleProduction);
            shipControlActionRow.Add(_shipControlProductionButton);

            _shipControlFeedButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.btn.feed", "Feed");
            _shipControlFeedButton.style.width = 34f;
            _shipControlFeedButton.style.minWidth = 34f;
            _shipControlFeedButton.style.height = 20f;
            _shipControlFeedButton.style.fontSize = Fsi(8);
            _shipControlFeedButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleNotifications);
            shipControlActionRow.Add(_shipControlFeedButton);

            _shipControlForcesButton = CreateControlButton($"{Space4XInRunHudElementIds.ShipControlPanel}.btn.forces", "F7");
            _shipControlForcesButton.style.width = 30f;
            _shipControlForcesButton.style.minWidth = 30f;
            _shipControlForcesButton.style.height = 20f;
            _shipControlForcesButton.style.fontSize = Fsi(8);
            _shipControlForcesButton.style.marginRight = 0f;
            _shipControlForcesButton.clicked += () => TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleForcesHolo);
            shipControlActionRow.Add(_shipControlForcesButton);

            _shipControlSummaryLabel = CreateMetricLabel(Space4XInRunHudElementIds.ShipControlSummaryLabel, "status unavailable");
            _shipControlSummaryLabel.style.fontSize = Fsi(8);
            _shipControlSummaryLabel.style.marginTop = 0f;
            _shipControlSummaryLabel.style.marginBottom = 2f;
            _shipControlSummaryLabel.style.color = new Color(0.76f, 0.87f, 0.95f, 0.98f);
            _shipControlSummaryLabel.style.minHeight = 10f;
            _shipControlPanel.Add(_shipControlSummaryLabel);

            var shipControlStatusHeader = new Label("Statuses");
            shipControlStatusHeader.style.fontSize = Fsi(9);
            shipControlStatusHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            shipControlStatusHeader.style.color = new Color(0.84f, 0.93f, 0.98f, 1f);
            shipControlStatusHeader.style.marginBottom = 1f;
            _shipControlPanel.Add(shipControlStatusHeader);

            _shipControlStatusScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = $"{Space4XInRunHudElementIds.ShipControlStatusList}.scroll"
            };
            _shipControlStatusScroll.style.flexGrow = 1f;
            _shipControlStatusScroll.style.minHeight = 56f;
            _shipControlStatusScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _shipControlStatusScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _shipControlPanel.Add(_shipControlStatusScroll);

            _shipControlStatusList = new VisualElement
            {
                name = Space4XInRunHudElementIds.ShipControlStatusList
            };
            _shipControlStatusList.style.flexDirection = FlexDirection.Column;
            _shipControlStatusList.style.overflow = Overflow.Hidden;
            _shipControlStatusScroll.Add(_shipControlStatusList);

            var shipControlThermalHeader = new Label("Module Thermals");
            shipControlThermalHeader.style.fontSize = Fsi(9);
            shipControlThermalHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            shipControlThermalHeader.style.color = new Color(0.84f, 0.93f, 0.98f, 1f);
            shipControlThermalHeader.style.marginTop = 2f;
            shipControlThermalHeader.style.marginBottom = 1f;
            _shipControlPanel.Add(shipControlThermalHeader);

            _shipControlThermalScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = $"{Space4XInRunHudElementIds.ShipControlThermalList}.scroll"
            };
            _shipControlThermalScroll.style.flexGrow = 1f;
            _shipControlThermalScroll.style.minHeight = 64f;
            _shipControlThermalScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _shipControlThermalScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _shipControlPanel.Add(_shipControlThermalScroll);

            _shipControlThermalList = new VisualElement
            {
                name = Space4XInRunHudElementIds.ShipControlThermalList
            };
            _shipControlThermalList.style.flexDirection = FlexDirection.Column;
            _shipControlThermalList.style.overflow = Overflow.Hidden;
            _shipControlThermalScroll.Add(_shipControlThermalList);

            _speedWidgetPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.SpeedWidgetPanel
            };
            _speedWidgetPanel.style.position = Position.Absolute;
            _speedWidgetPanel.style.left = Length.Percent(50f);
            _speedWidgetPanel.style.marginLeft = -SpeedWidgetWidth * 0.5f;
            _speedWidgetPanel.style.bottom = SpeedWidgetBottomOffset;
            _speedWidgetPanel.style.width = SpeedWidgetWidth;
            _speedWidgetPanel.style.paddingTop = 4f;
            _speedWidgetPanel.style.paddingBottom = 4f;
            _speedWidgetPanel.style.paddingLeft = 8f;
            _speedWidgetPanel.style.paddingRight = 8f;
            _speedWidgetPanel.style.alignItems = Align.Center;
            _speedWidgetPanel.style.backgroundColor = new Color(0.06f, 0.10f, 0.14f, 0.84f);
            _speedWidgetPanel.style.borderTopWidth = 1f;
            _speedWidgetPanel.style.borderRightWidth = 1f;
            _speedWidgetPanel.style.borderBottomWidth = 1f;
            _speedWidgetPanel.style.borderLeftWidth = 1f;
            var speedWidgetBorder = new Color(0.28f, 0.47f, 0.62f, 0.92f);
            _speedWidgetPanel.style.borderTopColor = speedWidgetBorder;
            _speedWidgetPanel.style.borderRightColor = speedWidgetBorder;
            _speedWidgetPanel.style.borderBottomColor = speedWidgetBorder;
            _speedWidgetPanel.style.borderLeftColor = speedWidgetBorder;
            _speedWidgetPanel.style.borderTopLeftRadius = 8f;
            _speedWidgetPanel.style.borderTopRightRadius = 8f;
            _speedWidgetPanel.style.borderBottomLeftRadius = 8f;
            _speedWidgetPanel.style.borderBottomRightRadius = 8f;
            _root.Add(_speedWidgetPanel);

            var speedHeader = new Label("Speed / Engine")
            {
                focusable = false
            };
            speedHeader.style.fontSize = Fsi(10);
            speedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            speedHeader.style.color = new Color(0.84f, 0.92f, 0.98f, 1f);
            speedHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speedWidgetPanel.Add(speedHeader);

            _speedWidgetSpeedLabel = CreateMetricLabel(Space4XInRunHudElementIds.SpeedWidgetSpeedLabel, "SPD 0.0 u/s");
            _speedWidgetSpeedLabel.style.fontSize = Fsi(12);
            _speedWidgetSpeedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _speedWidgetSpeedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speedWidgetSpeedLabel.style.color = new Color(0.93f, 0.97f, 1f, 1f);
            _speedWidgetPanel.Add(_speedWidgetSpeedLabel);

            _speedWidgetDrawLabel = CreateMetricLabel(Space4XInRunHudElementIds.SpeedWidgetDrawLabel, "Draw n/a");
            _speedWidgetDrawLabel.style.fontSize = Fsi(10);
            _speedWidgetDrawLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speedWidgetDrawLabel.style.color = new Color(0.76f, 0.86f, 0.93f, 1f);
            _speedWidgetPanel.Add(_speedWidgetDrawLabel);

            _powerRoutingPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.PowerRoutingPanel
            };
            _powerRoutingPanel.style.position = Position.Absolute;
            _powerRoutingPanel.style.right = PowerRoutingDefaultRight;
            _powerRoutingPanel.style.top = PowerRoutingDefaultTop;
            _powerRoutingPanel.style.width = PowerRoutingDefaultWidth;
            _powerRoutingPanel.style.height = PowerRoutingDefaultHeight;
            _powerRoutingPanel.style.paddingTop = 4f;
            _powerRoutingPanel.style.paddingBottom = 5f;
            _powerRoutingPanel.style.paddingLeft = 6f;
            _powerRoutingPanel.style.paddingRight = 6f;
            _powerRoutingPanel.style.backgroundColor = new Color(0.05f, 0.10f, 0.14f, 0.86f);
            _powerRoutingPanel.style.borderTopWidth = 1f;
            _powerRoutingPanel.style.borderRightWidth = 1f;
            _powerRoutingPanel.style.borderBottomWidth = 1f;
            _powerRoutingPanel.style.borderLeftWidth = 1f;
            var powerRoutingBorder = new Color(0.29f, 0.49f, 0.63f, 0.92f);
            _powerRoutingPanel.style.borderTopColor = powerRoutingBorder;
            _powerRoutingPanel.style.borderRightColor = powerRoutingBorder;
            _powerRoutingPanel.style.borderBottomColor = powerRoutingBorder;
            _powerRoutingPanel.style.borderLeftColor = powerRoutingBorder;
            _powerRoutingPanel.style.borderTopLeftRadius = 7f;
            _powerRoutingPanel.style.borderTopRightRadius = 7f;
            _powerRoutingPanel.style.borderBottomLeftRadius = 7f;
            _powerRoutingPanel.style.borderBottomRightRadius = 7f;
            _powerRoutingPanel.style.flexDirection = FlexDirection.Column;
            _root.Add(_powerRoutingPanel);

            var powerRoutingTitle = new Label("Power Routing")
            {
                focusable = false
            };
            powerRoutingTitle.style.fontSize = Fsi(10);
            powerRoutingTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            powerRoutingTitle.style.color = new Color(0.86f, 0.94f, 0.99f, 1f);
            powerRoutingTitle.style.marginBottom = 1f;
            _powerRoutingPanel.Add(powerRoutingTitle);

            var profileRow = new VisualElement();
            profileRow.style.flexDirection = FlexDirection.Row;
            profileRow.style.alignItems = Align.Center;
            profileRow.style.marginBottom = 2f;
            _powerRoutingPanel.Add(profileRow);

            _powerRoutingProfileLabel = new Label("P1")
            {
                focusable = false
            };
            _powerRoutingProfileLabel.style.fontSize = Fsi(8);
            _powerRoutingProfileLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _powerRoutingProfileLabel.style.color = new Color(0.80f, 0.90f, 0.97f, 1f);
            _powerRoutingProfileLabel.style.minWidth = 22f;
            profileRow.Add(_powerRoutingProfileLabel);

            for (var profileIndex = 0; profileIndex < PowerRoutingProfileCount; profileIndex++)
            {
                var captureIndex = profileIndex;
                var profileButton = CreateControlButton($"{Space4XInRunHudElementIds.PowerRoutingPanel}.profile_{profileIndex + 1}", $"P{profileIndex + 1}");
                profileButton.style.width = 28f;
                profileButton.style.minWidth = 28f;
                profileButton.style.marginRight = 3f;
                profileButton.clicked += () => SetActivePowerRoutingProfile(captureIndex, emitNotification: true);
                _powerRoutingProfileButtons[profileIndex] = profileButton;
                profileRow.Add(profileButton);
            }

            var profileResetButton = CreateControlButton($"{Space4XInRunHudElementIds.PowerRoutingPanel}.profile_reset", "R");
            profileResetButton.style.width = 24f;
            profileResetButton.style.minWidth = 24f;
            profileResetButton.tooltip = "Reset active profile to 100% all domains.";
            profileResetButton.clicked += ResetActivePowerRoutingProfileToDefaults;
            profileRow.Add(profileResetButton);

            _powerRoutingPanel.Add(CreatePowerRoutingSliderRow("ENG", out _powerRoutingEnginesSlider, out _powerRoutingEnginesValueLabel, OnPowerRoutingEnginesChanged));
            _powerRoutingPanel.Add(CreatePowerRoutingSliderRow("WPN", out _powerRoutingWeaponsSlider, out _powerRoutingWeaponsValueLabel, OnPowerRoutingWeaponsChanged));
            _powerRoutingPanel.Add(CreatePowerRoutingSliderRow("SHD", out _powerRoutingShieldsSlider, out _powerRoutingShieldsValueLabel, OnPowerRoutingShieldsChanged));
            _powerRoutingPanel.Add(CreatePowerRoutingSliderRow("RCT", out _powerRoutingReactorSlider, out _powerRoutingReactorValueLabel, OnPowerRoutingReactorChanged));
            _powerRoutingPanel.Add(CreatePowerRoutingSliderRow("SNS", out _powerRoutingSensorsSlider, out _powerRoutingSensorsValueLabel, OnPowerRoutingSensorsChanged));

            _powerRoutingSummaryLabel = CreateMetricLabel($"{Space4XInRunHudElementIds.PowerRoutingPanel}.summary", "jam 0.00%/tick sig x1.00");
            _powerRoutingSummaryLabel.style.fontSize = Fsi(7);
            _powerRoutingSummaryLabel.style.marginTop = 2f;
            _powerRoutingSummaryLabel.style.color = new Color(0.71f, 0.84f, 0.92f, 1f);
            _powerRoutingSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _powerRoutingSummaryLabel.style.minHeight = 10f;
            _powerRoutingPanel.Add(_powerRoutingSummaryLabel);

            _forcesHoloPanel = new VisualElement
            {
                name = Space4XInRunHudElementIds.ForcesHoloPanel
            };
            _forcesHoloPanel.style.position = Position.Absolute;
            _forcesHoloPanel.style.left = Length.Percent(50f);
            _forcesHoloPanel.style.marginLeft = -ForcesHoloWidth * 0.5f;
            _forcesHoloPanel.style.bottom = ForcesHoloBottomOffset;
            _forcesHoloPanel.style.width = ForcesHoloWidth;
            _forcesHoloPanel.style.paddingTop = 4f;
            _forcesHoloPanel.style.paddingBottom = 6f;
            _forcesHoloPanel.style.paddingLeft = 8f;
            _forcesHoloPanel.style.paddingRight = 8f;
            _forcesHoloPanel.style.backgroundColor = new Color(0.03f, 0.10f, 0.13f, 0.92f);
            _forcesHoloPanel.style.borderTopWidth = 1f;
            _forcesHoloPanel.style.borderRightWidth = 1f;
            _forcesHoloPanel.style.borderBottomWidth = 1f;
            _forcesHoloPanel.style.borderLeftWidth = 1f;
            var forcesBorder = new Color(0.22f, 0.73f, 0.86f, 0.96f);
            _forcesHoloPanel.style.borderTopColor = forcesBorder;
            _forcesHoloPanel.style.borderRightColor = forcesBorder;
            _forcesHoloPanel.style.borderBottomColor = forcesBorder;
            _forcesHoloPanel.style.borderLeftColor = forcesBorder;
            _forcesHoloPanel.style.borderTopLeftRadius = 8f;
            _forcesHoloPanel.style.borderTopRightRadius = 8f;
            _forcesHoloPanel.style.borderBottomLeftRadius = 8f;
            _forcesHoloPanel.style.borderBottomRightRadius = 8f;
            _forcesHoloPanel.style.display = DisplayStyle.None;
            _root.Add(_forcesHoloPanel);

            var forcesTitle = new Label("Forces Holo  [F7]");
            forcesTitle.style.fontSize = Fsi(10);
            forcesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            forcesTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            forcesTitle.style.color = new Color(0.74f, 0.96f, 0.98f, 1f);
            _forcesHoloPanel.Add(forcesTitle);

            _forcesHeadingLabel = CreateMetricLabel(Space4XInRunHudElementIds.ForcesHeadingLabel, "HDG 0 deg");
            _forcesHeadingLabel.style.fontSize = Fsi(11);
            _forcesHeadingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _forcesHeadingLabel.style.color = new Color(0.90f, 0.97f, 1f, 1f);
            _forcesHoloPanel.Add(_forcesHeadingLabel);

            _forcesVelocityLabel = CreateMetricLabel(Space4XInRunHudElementIds.ForcesVelocityLabel, "VEL F+0.0 L+0.0 V+0.0");
            _forcesVelocityLabel.style.fontSize = Fsi(10);
            _forcesVelocityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _forcesVelocityLabel.style.color = new Color(0.83f, 0.92f, 0.97f, 1f);
            _forcesHoloPanel.Add(_forcesVelocityLabel);

            _forcesPushLabel = CreateMetricLabel(Space4XInRunHudElementIds.ForcesPushLabel, "PUSH F+0.0 L+0.0 V+0.0 |a|0.0");
            _forcesPushLabel.style.fontSize = Fsi(10);
            _forcesPushLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _forcesPushLabel.style.color = new Color(0.70f, 0.86f, 0.93f, 1f);
            _forcesHoloPanel.Add(_forcesPushLabel);

            RegisterLayoutWindow(Space4XInRunHudElementIds.RootPanel, _panel, 160f, 128f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.MinimapPanel, _minimapPanel, 148f, 148f, true, true);
            RegisterLayoutWindow(Space4XInRunHudElementIds.UtilityControlsPanel, _utilityControlsPanel, 140f, 32f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.TimeControlsPanel, _timeControlsPanel, 116f, 30f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.InventoryPanel, _inventoryPanel, 320f, 180f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.ProductionPanel, _productionPanel, 284f, 160f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.TargetPanel, _targetPanel, 220f, 72f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.NotificationPanel, _notificationPanel, 168f, 96f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.SpeedWidgetPanel, _speedWidgetPanel, 196f, 56f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.ShipControlPanel, _shipControlPanel, 220f, 132f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.PowerRoutingPanel, _powerRoutingPanel, 208f, 140f, false, false);
            RegisterLayoutWindow(Space4XInRunHudElementIds.ForcesHoloPanel, _forcesHoloPanel, 216f, 52f, false, false);

            ApplyPowerRoutingProfileToControls();
            UpdatePowerRoutingProfileButtonStyles();
            UpdateMinimapOverlayModeUi();
            ScheduleLayoutInitialization();
        }

        private void RefreshSnapshot()
        {
            var snapshot = new Space4XInRunHudKernelSnapshot
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                frame = Time.frameCount,
                scene_name = SceneManager.GetActiveScene().name,
                power_deficit_tags = "no_flagship",
                minimap_relation_counts = "self=0|ally=0|neutral=0|hostile=0|unknown=0",
                minimap_contacts = Array.Empty<Space4XMinimapContactKernelSnapshot>(),
                target_entity_ref = string.Empty,
                target_callsign = string.Empty,
                target_relation = string.Empty,
                target_relation_stance = string.Empty,
                target_relation_source = string.Empty,
                target_color_token = "unknown_gray",
                inventory_summary = string.Empty,
                inventory_ship_preset_id = string.Empty,
                inventory_ship_label = string.Empty,
                inventory_ship_archetype = string.Empty,
                inventory_highlight_module_ref = string.Empty,
                inventory_segments = Array.Empty<Space4XInventorySegmentKernelSnapshot>(),
                inventory_cargo_lines = Array.Empty<string>(),
                inventory_available_modules = Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>(),
                production_summary = string.Empty,
                production_selected_entity_ref = string.Empty,
                production_selected_recipe_id = string.Empty,
                production_selected_limb_id = string.Empty,
                production_facilities = Array.Empty<Space4XProductionFacilityKernelSnapshot>(),
                notifications_latest = string.Empty,
                notifications_window = string.Empty,
                minimap_visible = BoolToInt(_minimapVisible),
                inventory_visible = BoolToInt(_inventoryVisible),
                production_visible = BoolToInt(_productionVisible),
                notifications_visible = BoolToInt(_notificationsVisible),
                notifications_count = _notifications.Count,
                ship_control_visible = BoolToInt(_shipControlVisible),
                ship_control_summary = string.Empty,
                ship_control_status_count = 0,
                ship_control_thermal_count = 0,
                ship_control_statuses = Array.Empty<Space4XShipStatusEffectKernelSnapshot>(),
                ship_control_modules = Array.Empty<Space4XShipModuleThermalKernelSnapshot>(),
                minimap_widget_3d = 1,
                minimap_camera_ready = BoolToInt(_minimapCamera != null && _minimapRenderTexture != null),
                minimap_overlay_mode = ResolveMinimapOverlayModeToken(_minimapOverlayMode),
                minimap_overlay_visible_count = 0,
                hud_panel_x = _hudPanelPosition.x,
                hud_panel_y = _hudPanelPosition.y,
                layout_edit_mode = BoolToInt(_layoutEditMode),
                ui_global_scale = _globalUiScale,
                engine_power_estimated = 1,
                engine_fuel_estimated = 1,
                forces_holo_visible = BoolToInt(_forcesHoloVisible),
                accel_estimated = 1,
                power_routing_profile = _powerRoutingActiveProfile + 1,
                power_route_engines_pct = PowerRoutingDefaultPercent,
                power_route_weapons_pct = PowerRoutingDefaultPercent,
                power_route_shields_pct = PowerRoutingDefaultPercent,
                power_route_reactor_pct = PowerRoutingDefaultPercent,
                power_route_sensors_pct = PowerRoutingDefaultPercent,
                power_route_total_pct = PowerRoutingDefaultPercent * 5f,
                power_route_engines_factor = 1f,
                power_route_weapons_factor = 1f,
                power_route_shields_factor = 1f,
                power_route_reactor_factor = 1f,
                power_route_sensors_factor = 1f,
                power_route_reactor_signature_factor = 1f,
                power_route_jam_risk_per_tick = 0f,
                power_route_overclock_pct = 0f,
                power_route_underclock_pct = 0f,
                hud_deficit_fade_enabled = BoolToInt(_runtimeHudFadeOnPowerDeficit),
                hud_deficit_fade_alpha = 1f,
                production_selected_shift = 1,
                production_selected_power_scale = 1f
            };
            ApplyPowerRoutingProfileToSnapshot(GetActivePowerRoutingProfile(), ref snapshot);

            var runActive = ResolveRunActive();
            snapshot.run_active = BoolToInt(runActive);
            snapshot.hud_visible = BoolToInt(_hudEnabled && runActive);

            EnsureInputReferences();
            snapshot.action_map = _playerInput != null && _playerInput.currentActionMap != null
                ? _playerInput.currentActionMap.name
                : string.Empty;

            var focused = ResolveFocusedElement(_root);
            snapshot.focused_element_name = focused is VisualElement focusedElement
                ? focusedElement.name ?? string.Empty
                : string.Empty;
            snapshot.focused_element_type = focused != null ? focused.GetType().Name : string.Empty;

            if (TryResolveFlagshipEntity(out var flagship, out var entityManager))
            {
                snapshot.flagship_bound = 1;
                _hasFlagshipPose = TryResolveMinimapAnchorPose(entityManager, flagship, out _latestFlagshipPosition, out _latestFlagshipRotation);
                ApplyPowerRoutingProfileToRuntimeState(entityManager, flagship);

                PopulateVitals(entityManager, flagship, ref snapshot);
                PopulatePowerMetrics(entityManager, flagship, ref snapshot);
                PopulatePowerRoutingMetrics(entityManager, flagship, ref snapshot);
                PopulateEngineTelemetry(entityManager, flagship, ref snapshot);
                PopulateForceTelemetry(entityManager, flagship, ref snapshot);
                PopulateMinimapMetrics(entityManager, flagship, ref snapshot);
                snapshot.minimap_overlay_visible_count = CountVisibleMinimapOverlayContacts(_minimapContactsScratch);
                PopulateTargetMetrics(entityManager, flagship, ref snapshot);
                PopulateInventoryMetrics(entityManager, flagship, ref snapshot);
                PopulateProductionMetrics(entityManager, flagship, ref snapshot);
                PopulateShipControlMetrics(entityManager, flagship, ref snapshot);
            }
            else
            {
                snapshot.flagship_bound = 0;
                _hasForceVelocitySample = false;
                if (TryResolveEntityManager(out var fallbackManager))
                {
                    _hasFlagshipPose = TryResolveMinimapAnchorPose(fallbackManager, Entity.Null, out _latestFlagshipPosition, out _latestFlagshipRotation);
                }
                else
                {
                    _hasFlagshipPose = false;
                    _latestFlagshipPosition = float3.zero;
                    _latestFlagshipRotation = quaternion.identity;
                }

                snapshot.minimap_contacts_tracked = 0;
                snapshot.minimap_overlay_visible_count = 0;
                snapshot.inventory_line_count = 0;
                snapshot.inventory_summary = "flagship_unbound";
                snapshot.inventory_segment_count = 0;
                snapshot.inventory_module_count = 0;
                snapshot.inventory_popup_count = _inventoryPopups.Count;
                snapshot.inventory_highlight_module_ref = string.Empty;
                snapshot.inventory_segments = Array.Empty<Space4XInventorySegmentKernelSnapshot>();
                snapshot.inventory_cargo_lines = Array.Empty<string>();
                snapshot.inventory_available_module_count = 0;
                snapshot.inventory_available_modules = Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>();
                snapshot.production_summary = "flagship_unbound";
                snapshot.production_facility_count = 0;
                snapshot.production_selected_index = -1;
                snapshot.production_selected_entity_ref = string.Empty;
                snapshot.production_selected_recipe_id = string.Empty;
                snapshot.production_selected_limb_id = string.Empty;
                snapshot.production_selected_shift = 1;
                snapshot.production_selected_power_scale = 1f;
                snapshot.production_selected_queue_count = 0;
                snapshot.production_facilities = Array.Empty<Space4XProductionFacilityKernelSnapshot>();
                snapshot.ship_control_summary = "flagship_unbound";
                snapshot.ship_control_status_count = 0;
                snapshot.ship_control_thermal_count = 0;
                snapshot.ship_control_statuses = Array.Empty<Space4XShipStatusEffectKernelSnapshot>();
                snapshot.ship_control_modules = Array.Empty<Space4XShipModuleThermalKernelSnapshot>();
            }

            PopulateTimeMetrics(ref snapshot);
            EmitResourceWarnings(in snapshot);
            PopulateNotificationWindow(ref snapshot);
            PopulateLayoutSnapshot(ref snapshot);
            snapshot.hud_deficit_fade_enabled = BoolToInt(_runtimeHudFadeOnPowerDeficit);
            snapshot.hud_deficit_fade_alpha = ComputeHudDeficitFadeAlpha(in snapshot);
            _latestSnapshot = snapshot;
        }

        private void ApplySnapshotToUi(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_root == null || _panel == null)
                return;

            _root.style.display = snapshot.hud_visible == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (snapshot.hud_visible == 0)
            {
                _root.style.opacity = 1f;
                return;
            }

            _root.style.opacity = math.clamp(snapshot.hud_deficit_fade_alpha, HudDeficitFadeMinAlpha, 1f);

            if (!_layoutInitialized && _root.resolvedStyle.width > 1f && _root.resolvedStyle.height > 1f)
            {
                InitializeWindowLayoutsFromVisualTree();
            }

            ApplyAllWindowLayouts(clampToViewport: true);
            UpdateMetricBar(_healthBarFill, _healthLabel, "HULL", snapshot.health_current, snapshot.health_max, snapshot.health_ratio);
            UpdateMetricBar(_shieldsBarFill, _shieldsLabel, "SHLD", snapshot.shields_current, snapshot.shields_max, snapshot.shields_ratio);
            var armorCurrent = math.max(0f, snapshot.armor_rating);
            var armorMax = math.max(1f, armorCurrent);
            UpdateMetricBar(_armorBarFill, _armorLabel, "ARMR", armorCurrent, armorMax, armorCurrent <= 0.001f ? 0f : armorCurrent / armorMax);
            UpdateMetricBar(_suppliesBarFill, _suppliesLabel, "SUPP", snapshot.supplies_current, snapshot.supplies_max, snapshot.supplies_ratio);
            UpdateMetricBar(_foodBarFill, _foodLabel, "FOOD", snapshot.food_current, snapshot.food_max, snapshot.food_ratio);
            UpdateMetricBar(_waterBarFill, _fuelLabel, "WATR", snapshot.fuel_current, snapshot.fuel_max, snapshot.fuel_ratio);
            UpdateMetricBar(_crewBarFill, _crewLabel, "CREW", snapshot.crew_current, snapshot.crew_max, snapshot.crew_ratio);
            UpdateMetricBar(_ammoBarFill, _ammoLabel, "AMMO", snapshot.ammo_current, snapshot.ammo_max, snapshot.ammo_ratio);
            UpdateMetricBar(_fuelReserveBarFill, _fuelReserveLabel, "FUEL", snapshot.fuel_current, snapshot.fuel_max, snapshot.fuel_ratio);
            _powerLabel.text = $"PWR {snapshot.power_draw_mw:0}/{snapshot.power_capacity_mw:0} MW  free {snapshot.power_available_mw:0}";
            _timeLabel.text = $"tick {snapshot.tick}  t {snapshot.world_seconds:0.0}s  x{snapshot.time_speed_multiplier:0.##}";
            if (_speedWidgetSpeedLabel != null)
            {
                _speedWidgetSpeedLabel.text = $"SPD {snapshot.speed_current_ups:0.0} u/s";
            }

            if (_speedWidgetDrawLabel != null)
            {
                var powerPrefix = snapshot.engine_power_estimated == 1 ? "~" : string.Empty;
                var fuelPrefix = snapshot.engine_fuel_estimated == 1 ? "~" : string.Empty;
                _speedWidgetDrawLabel.text =
                    $"ENG {powerPrefix}{snapshot.engine_power_draw_mw:0.0} MW  FUEL {fuelPrefix}{snapshot.engine_fuel_draw_per_tick:0.00}/tick";
            }

            if (_powerRoutingProfileLabel != null)
            {
                _powerRoutingProfileLabel.text = $"P{math.max(1, snapshot.power_routing_profile)}";
            }

            UpdatePowerRoutingValueLabels(in snapshot);

            if (_powerRoutingSummaryLabel != null)
            {
                _powerRoutingSummaryLabel.text =
                    $"jam {(snapshot.power_route_jam_risk_per_tick * 100f):0.00}%/tick  sig x{snapshot.power_route_reactor_signature_factor:0.00}";
            }

            if (_forcesHeadingLabel != null)
            {
                _forcesHeadingLabel.text = $"HDG {snapshot.heading_deg:000.0} deg";
            }

            if (_forcesVelocityLabel != null)
            {
                _forcesVelocityLabel.text =
                    $"VEL F{snapshot.velocity_forward_ups:+0.0;-0.0;+0.0} L{snapshot.velocity_lateral_ups:+0.0;-0.0;+0.0} V{snapshot.velocity_vertical_ups:+0.0;-0.0;+0.0}";
            }

            if (_forcesPushLabel != null)
            {
                var pushPrefix = snapshot.accel_estimated == 1 ? "~" : string.Empty;
                _forcesPushLabel.text =
                    $"PUSH {pushPrefix}F{snapshot.accel_forward_ups2:+0.0;-0.0;+0.0} L{snapshot.accel_lateral_ups2:+0.0;-0.0;+0.0} V{snapshot.accel_vertical_ups2:+0.0;-0.0;+0.0} |a|{snapshot.accel_total_ups2:0.0}";
            }

            if (_minimapSummaryLabel != null)
            {
                _minimapSummaryLabel.text =
                    $"A:{snapshot.minimap_ally_count}  N:{snapshot.minimap_neutral_count}  H:{snapshot.minimap_hostile_count}  U:{snapshot.minimap_unknown_count}  vis:{snapshot.minimap_overlay_visible_count}";
            }

            if (_targetPrimaryLabel != null)
            {
                var multiSuffix = snapshot.target_multi_count > 1
                    ? $"  +{snapshot.target_multi_count - 1}"
                    : string.Empty;
                var targetIdentity = !string.IsNullOrWhiteSpace(snapshot.target_callsign)
                    ? $"{snapshot.target_callsign} ({snapshot.target_entity_ref})"
                    : snapshot.target_entity_ref;
                _targetPrimaryLabel.text = snapshot.target_selected == 1
                    ? $"{snapshot.target_relation.ToUpperInvariant()}  {targetIdentity}{multiSuffix}"
                    : "No target selected.";
                _targetPrimaryLabel.style.color = ResolveRelationColor(snapshot.target_color_token);
            }

            if (_targetSecondaryLabel != null)
            {
                _targetSecondaryLabel.text = snapshot.target_selected == 1
                    ? $"d {snapshot.target_distance:0.0}m  brg {snapshot.target_bearing_deg:0}  v {snapshot.target_speed:0.0}  c {snapshot.target_closing_speed:+0.0;-0.0;+0.0}"
                    : "Mode 1/2: LMB target | Shift+drag multi (Ctrl append)";
            }

            UpdateMetricBar(
                _targetHullBarFill,
                _targetHullLabel,
                "HULL",
                snapshot.target_hull_current,
                snapshot.target_hull_max,
                snapshot.target_hull_ratio);

            _minimapPanel.style.display = _minimapVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _inventoryPanel.style.display = _inventoryVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_inventoryVisible)
            {
                CloseAllInventoryPopups();
            }
            if (_productionPanel != null)
            {
                _productionPanel.style.display = _productionVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _targetPanel.style.display = DisplayStyle.Flex;
            _notificationPanel.style.display = _notificationsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_shipControlPanel != null)
            {
                _shipControlPanel.style.display = _shipControlVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_forcesHoloPanel != null)
            {
                _forcesHoloPanel.style.display = _forcesHoloVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_minimapContactList != null)
            {
                _minimapContactList.style.display = _inventoryVisible && _minimapVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateMinimapOverlayModeUi();
            UpdateMinimapViewport(in snapshot);
            UpdateMinimapOverlay(in snapshot);
            RebuildMinimapContactList(in snapshot);
            RebuildInventoryList(in snapshot);
            RebuildProductionList(in snapshot);
            RebuildNotificationList();
            RebuildShipControlPanel(in snapshot);
            UpdateShipControlButtonStates();
        }

        public Vector2 GetHudPanelPosition()
        {
            return _hudPanelPosition;
        }

        public bool TrySetKernelElementPosition(string elementId, Vector2 absolutePosition)
        {
            if (string.IsNullOrWhiteSpace(elementId) || !_windowLayouts.TryGetValue(elementId, out var window))
            {
                return false;
            }

            window.Position = absolutePosition;
            ApplyWindowLayout(window, clampToViewport: true);
            SaveLayoutPreferences();
            return true;
        }

        private void OnHudRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null || !IsMovementOrUiNavigationKey(evt.keyCode))
            {
                return;
            }

            ClearHudFocus();
            evt.StopImmediatePropagation();
        }

        private static bool WasMovementInputPressedThisFrame(Keyboard keyboard)
        {
            return WasPressedThisFrame(keyboard, Key.W) ||
                   WasPressedThisFrame(keyboard, Key.A) ||
                   WasPressedThisFrame(keyboard, Key.S) ||
                   WasPressedThisFrame(keyboard, Key.D) ||
                   WasPressedThisFrame(keyboard, Key.UpArrow) ||
                   WasPressedThisFrame(keyboard, Key.DownArrow) ||
                   WasPressedThisFrame(keyboard, Key.LeftArrow) ||
                   WasPressedThisFrame(keyboard, Key.RightArrow);
        }

        private static bool IsMovementOrUiNavigationKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.W ||
                   keyCode == KeyCode.A ||
                   keyCode == KeyCode.S ||
                   keyCode == KeyCode.D ||
                   keyCode == KeyCode.UpArrow ||
                   keyCode == KeyCode.DownArrow ||
                   keyCode == KeyCode.LeftArrow ||
                   keyCode == KeyCode.RightArrow ||
                   keyCode == KeyCode.Tab ||
                   keyCode == KeyCode.Return ||
                   keyCode == KeyCode.KeypadEnter ||
                   keyCode == KeyCode.Space;
        }

        private void ClearHudFocus()
        {
            var focusController = _root?.focusController;
            if (focusController?.focusedElement is Focusable focusable)
            {
                focusable.Blur();
            }
        }

        private void RegisterLayoutWindow(string id, VisualElement element, float minWidth, float minHeight, bool lockAspect, bool squareAspect)
        {
            if (string.IsNullOrWhiteSpace(id) || element == null)
            {
                return;
            }

            var window = new WindowLayoutRuntime
            {
                Id = id,
                Element = element,
                MinWidth = math.max(LayoutMinWindowWidth, minWidth),
                MinHeight = math.max(LayoutMinWindowHeight, minHeight),
                LockAspect = lockAspect,
                AspectRatio = squareAspect ? 1f : 0f
            };

            _windowLayouts[id] = window;
            element.userData = id;
            element.style.position = Position.Absolute;
            element.style.right = StyleKeyword.Auto;
            element.style.bottom = StyleKeyword.Auto;
            AttachLayoutInteractions(window);
            AddResizeGrip(window);
        }

        private void AttachLayoutInteractions(WindowLayoutRuntime window)
        {
            if (window?.Element == null)
            {
                return;
            }

            window.Element.RegisterCallback<PointerDownEvent>(evt => OnWindowPointerDown(window, evt), TrickleDown.TrickleDown);
            window.Element.RegisterCallback<PointerMoveEvent>(evt => OnWindowPointerMove(window, evt), TrickleDown.TrickleDown);
            window.Element.RegisterCallback<PointerUpEvent>(evt => OnWindowPointerUp(window, evt), TrickleDown.TrickleDown);
            window.Element.RegisterCallback<PointerCaptureOutEvent>(evt => OnWindowPointerCaptureOut(window, evt), TrickleDown.TrickleDown);
            window.Element.RegisterCallback<WheelEvent>(evt => OnWindowWheel(window, evt), TrickleDown.TrickleDown);
        }

        private void AddResizeGrip(WindowLayoutRuntime window)
        {
            if (window?.Element == null)
            {
                return;
            }

            var grip = new VisualElement
            {
                name = $"{window.Id}.resize_grip"
            };
            grip.style.position = Position.Absolute;
            grip.style.width = 12f;
            grip.style.height = 12f;
            grip.style.right = 2f;
            grip.style.bottom = 2f;
            grip.style.backgroundColor = new Color(0.36f, 0.60f, 0.76f, 0.88f);
            grip.style.borderTopWidth = 1f;
            grip.style.borderRightWidth = 1f;
            grip.style.borderBottomWidth = 1f;
            grip.style.borderLeftWidth = 1f;
            grip.style.borderTopColor = new Color(0.54f, 0.76f, 0.91f, 1f);
            grip.style.borderRightColor = new Color(0.54f, 0.76f, 0.91f, 1f);
            grip.style.borderBottomColor = new Color(0.54f, 0.76f, 0.91f, 1f);
            grip.style.borderLeftColor = new Color(0.54f, 0.76f, 0.91f, 1f);
            grip.style.display = DisplayStyle.None;
            grip.pickingMode = PickingMode.Position;
            window.Element.Add(grip);
            window.ResizeGrip = grip;

            grip.RegisterCallback<PointerDownEvent>(evt => OnWindowResizePointerDown(window, evt), TrickleDown.NoTrickleDown);
            grip.RegisterCallback<PointerMoveEvent>(evt => OnWindowResizePointerMove(window, evt), TrickleDown.NoTrickleDown);
            grip.RegisterCallback<PointerUpEvent>(evt => OnWindowResizePointerUp(window, evt), TrickleDown.NoTrickleDown);
            grip.RegisterCallback<PointerCaptureOutEvent>(evt => OnWindowResizePointerCaptureOut(window, evt), TrickleDown.NoTrickleDown);
        }

        private void OnWindowPointerDown(WindowLayoutRuntime window, PointerDownEvent evt)
        {
            if (window?.Element == null || !_layoutEditMode || evt == null || evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            if (window.ResizeGrip != null && evt.target is VisualElement targetElement)
            {
                if (targetElement == window.ResizeGrip)
                {
                    return;
                }
            }

            window.DragActive = true;
            window.ResizeActive = false;
            window.PointerId = evt.pointerId;
            window.DragStartPointer = new Vector2(evt.position.x, evt.position.y);
            window.DragStartPosition = window.Position;
            window.Element.CapturePointer(evt.pointerId);
            window.Element.BringToFront();
            evt.StopImmediatePropagation();
        }

        private void OnWindowPointerMove(WindowLayoutRuntime window, PointerMoveEvent evt)
        {
            if (window == null || !window.DragActive || evt == null || evt.pointerId != window.PointerId)
            {
                return;
            }

            var pointerPosition = new Vector2(evt.position.x, evt.position.y);
            var delta = pointerPosition - window.DragStartPointer;
            window.Position = window.DragStartPosition + delta;
            ApplyWindowLayout(window, clampToViewport: true);
            evt.StopImmediatePropagation();
        }

        private void OnWindowPointerUp(WindowLayoutRuntime window, PointerUpEvent evt)
        {
            if (window == null || !window.DragActive || evt == null || evt.pointerId != window.PointerId)
            {
                return;
            }

            ReleaseWindowPointerCapture(window);
            SaveLayoutPreferences();
            evt.StopImmediatePropagation();
        }

        private void OnWindowPointerCaptureOut(WindowLayoutRuntime window, PointerCaptureOutEvent evt)
        {
            if (window != null && window.DragActive && evt != null && evt.pointerId == window.PointerId)
            {
                window.DragActive = false;
                window.PointerId = -1;
            }
        }

        private void OnWindowResizePointerDown(WindowLayoutRuntime window, PointerDownEvent evt)
        {
            if (window?.ResizeGrip == null || !_layoutEditMode || evt == null || evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            window.ResizeActive = true;
            window.DragActive = false;
            window.PointerId = evt.pointerId;
            window.ResizeStartPointer = new Vector2(evt.position.x, evt.position.y);
            window.ResizeStartSize = window.Size;
            if (window.LockAspect || window.AspectRatio <= 0f)
            {
                window.AspectRatio = math.max(0.05f, window.Size.x / math.max(1f, window.Size.y));
            }

            window.ResizeGrip.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnWindowResizePointerMove(WindowLayoutRuntime window, PointerMoveEvent evt)
        {
            if (window == null || !window.ResizeActive || evt == null || evt.pointerId != window.PointerId)
            {
                return;
            }

            var pointerPosition = new Vector2(evt.position.x, evt.position.y);
            var delta = pointerPosition - window.ResizeStartPointer;
            var width = window.ResizeWidth ? window.ResizeStartSize.x + delta.x : window.ResizeStartSize.x;
            var height = window.ResizeHeight ? window.ResizeStartSize.y + delta.y : window.ResizeStartSize.y;

            width = math.max(window.MinWidth, width);
            height = math.max(window.MinHeight, height);

            if (window.LockAspect)
            {
                var ratio = math.max(0.05f, window.AspectRatio);
                height = width / ratio;
                if (height < window.MinHeight)
                {
                    height = window.MinHeight;
                    width = height * ratio;
                }
            }

            window.Size = new Vector2(width, height);
            ApplyWindowLayout(window, clampToViewport: true);
            evt.StopImmediatePropagation();
        }

        private void OnWindowResizePointerUp(WindowLayoutRuntime window, PointerUpEvent evt)
        {
            if (window == null || !window.ResizeActive || evt == null || evt.pointerId != window.PointerId)
            {
                return;
            }

            ReleaseWindowPointerCapture(window);
            SaveLayoutPreferences();
            evt.StopImmediatePropagation();
        }

        private void OnWindowResizePointerCaptureOut(WindowLayoutRuntime window, PointerCaptureOutEvent evt)
        {
            if (window != null && window.ResizeActive && evt != null && evt.pointerId == window.PointerId)
            {
                window.ResizeActive = false;
                window.PointerId = -1;
            }
        }

        private void OnWindowWheel(WindowLayoutRuntime window, WheelEvent evt)
        {
            if (window == null || evt == null || !_layoutEditMode)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var ctrlHeld = keyboard != null &&
                           (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            if (!ctrlHeld)
            {
                return;
            }

            var direction = evt.delta.y < 0f ? 1f : -1f;
            var nextScale = math.clamp(window.Scale + direction * LayoutWindowScaleStep, LayoutMinScale, LayoutMaxScale);
            if (math.abs(nextScale - window.Scale) < 0.0001f)
            {
                return;
            }

            window.Scale = nextScale;
            ApplyWindowLayout(window, clampToViewport: true);
            SaveLayoutPreferences();
            evt.StopImmediatePropagation();
        }

        private void SetHudPanelPosition(Vector2 absolutePosition, bool clampToViewport)
        {
            if (_windowLayouts.TryGetValue(Space4XInRunHudElementIds.RootPanel, out var rootWindow))
            {
                rootWindow.Position = absolutePosition;
                ApplyWindowLayout(rootWindow, clampToViewport);
                return;
            }

            _hudPanelPosition = absolutePosition;
            ApplyHudPanelPosition();
        }

        private void ApplyHudPanelPosition()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.left = _hudPanelPosition.x;
            _panel.style.top = _hudPanelPosition.y;
            _panel.style.right = StyleKeyword.Auto;
            _panel.style.bottom = StyleKeyword.Auto;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (_root == null)
            {
                return;
            }

            if (!_layoutInitialized)
            {
                InitializeWindowLayoutsFromVisualTree();
            }
            else
            {
                ApplyAllWindowLayouts(clampToViewport: true);
            }
        }

        private void InitializeWindowLayoutsFromVisualTree()
        {
            if (_root == null)
            {
                return;
            }

            var rootWidth = _root.resolvedStyle.width;
            var rootHeight = _root.resolvedStyle.height;
            if (rootWidth <= 1f || rootHeight <= 1f)
            {
                return;
            }

            foreach (var pair in _windowLayouts)
            {
                var window = pair.Value;
                if (window?.Element == null)
                {
                    continue;
                }

                if (_savedWindowLayouts.TryGetValue(window.Id, out var saved))
                {
                    if (_migrateLegacyTopRightPads && IsCompactWindowForMigration(window.Id))
                    {
                        ApplyDefaultWindowLayout(window, rootWidth, rootHeight);
                    }
                    else
                    {
                        window.Position = new Vector2(saved.x, saved.y);
                        var width = saved.width > 0f ? saved.width : window.Size.x;
                        var height = saved.height > 0f ? saved.height : window.Size.y;
                        NormalizeCompactWindowBounds(window, saved, rootWidth, rootHeight, ref width, ref height);

                        window.Size = new Vector2(math.max(window.MinWidth, width), math.max(window.MinHeight, height));
                        window.Scale = 1f;
                        if (saved.scale > 0f)
                        {
                            window.Scale = math.clamp(saved.scale, LayoutMinScale, LayoutMaxScale);
                        }
                    }
                }
                else
                {
                    ApplyDefaultWindowLayout(window, rootWidth, rootHeight);
                }

                ApplyWindowLayout(window, clampToViewport: true);
            }

            _layoutInitialized = true;
            SetGlobalUiScale(_globalUiScale, emitNotification: false, persist: false);
        }

        private void ApplyDefaultWindowLayout(WindowLayoutRuntime window, float rootWidth, float rootHeight)
        {
            var worldRect = window.Element.worldBound;
            var hasMeasuredSize = worldRect.width > 1f && worldRect.height > 1f;
            var measuredWidth = hasMeasuredSize ? worldRect.width : 0f;
            var measuredHeight = hasMeasuredSize ? worldRect.height : 0f;

            var width = math.max(window.MinWidth, measuredWidth);
            var height = math.max(window.MinHeight, measuredHeight);
            var position = window.Position;

            switch (window.Id)
            {
                case Space4XInRunHudElementIds.RootPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : RootPanelDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : RootPanelDefaultHeight);
                    position = new Vector2(HudPanelDefaultX, HudPanelDefaultY);
                    break;
                case Space4XInRunHudElementIds.MinimapPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : MinimapPanelSize);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : MinimapDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - MinimapDefaultRight - width), MinimapDefaultTop);
                    break;
                case Space4XInRunHudElementIds.UtilityControlsPanel:
                    width = math.max(window.MinWidth, UtilityControlsDefaultWidth);
                    height = math.max(window.MinHeight, UtilityControlsDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - UtilityControlsDefaultRight - width), UtilityControlsDefaultTop);
                    break;
                case Space4XInRunHudElementIds.TimeControlsPanel:
                    width = math.max(window.MinWidth, TimeControlsDefaultWidth);
                    height = math.max(window.MinHeight, TimeControlsDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - TimeControlsDefaultRight - width), TimeControlsDefaultTop);
                    break;
                case Space4XInRunHudElementIds.InventoryPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : InventoryDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : InventoryDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - InventoryDefaultRight - width), InventoryDefaultTop);
                    break;
                case Space4XInRunHudElementIds.ProductionPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : ProductionDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : ProductionDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - ProductionDefaultRight - width), ProductionDefaultTop);
                    break;
                case Space4XInRunHudElementIds.TargetPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : TargetDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : TargetDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - TargetDefaultRight - width), TargetDefaultTop);
                    break;
                case Space4XInRunHudElementIds.NotificationPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : NotificationDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : NotificationDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - NotificationDefaultRight - width), NotificationDefaultTop);
                    break;
                case Space4XInRunHudElementIds.ShipControlPanel:
                    width = math.max(window.MinWidth, measuredWidth > 0f ? measuredWidth : ShipControlDefaultWidth);
                    height = math.max(window.MinHeight, measuredHeight > 0f ? measuredHeight : ShipControlDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - ShipControlDefaultRight - width), ShipControlDefaultTop);
                    break;
                case Space4XInRunHudElementIds.SpeedWidgetPanel:
                    width = math.max(window.MinWidth, SpeedWidgetWidth);
                    height = math.max(window.MinHeight, SpeedWidgetDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, (rootWidth - width) * 0.5f), Mathf.Max(0f, rootHeight - SpeedWidgetBottomOffset - height));
                    break;
                case Space4XInRunHudElementIds.PowerRoutingPanel:
                    width = math.max(window.MinWidth, PowerRoutingDefaultWidth);
                    height = math.max(window.MinHeight, PowerRoutingDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, rootWidth - PowerRoutingDefaultRight - width), PowerRoutingDefaultTop);
                    break;
                case Space4XInRunHudElementIds.ForcesHoloPanel:
                    width = math.max(window.MinWidth, ForcesHoloWidth);
                    height = math.max(window.MinHeight, ForcesHoloDefaultHeight);
                    position = new Vector2(Mathf.Max(0f, (rootWidth - width) * 0.5f), Mathf.Max(0f, rootHeight - ForcesHoloBottomOffset - height));
                    break;
                default:
                    if (hasMeasuredSize)
                    {
                        var worldPosition = new Vector2(worldRect.xMin, worldRect.yMin);
                        position = _root.WorldToLocal(worldPosition);
                    }

                    break;
            }

            window.Position = position;
            window.Size = new Vector2(width, height);
            window.Scale = 1f;
        }

        private static bool IsTopRightPad(string id)
        {
            return string.Equals(id, Space4XInRunHudElementIds.UtilityControlsPanel, StringComparison.Ordinal) ||
                   string.Equals(id, Space4XInRunHudElementIds.TimeControlsPanel, StringComparison.Ordinal);
        }

        private static bool IsCompactWindowForMigration(string id)
        {
            return IsTopRightPad(id) ||
                   string.Equals(id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal);
        }

        private static bool IsCompactWindow(string id)
        {
            return IsTopRightPad(id) ||
                    string.Equals(id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal) ||
                    string.Equals(id, Space4XInRunHudElementIds.PowerRoutingPanel, StringComparison.Ordinal) ||
                    string.Equals(id, Space4XInRunHudElementIds.ForcesHoloPanel, StringComparison.Ordinal);
        }

        private void NormalizeCompactWindowBounds(
            WindowLayoutRuntime window,
            WindowLayoutSavedState saved,
            float rootWidth,
            float rootHeight,
            ref float width,
            ref float height)
        {
            if (window == null || !IsCompactWindow(window.Id))
            {
                return;
            }

            var defaultWidth = ResolveDefaultLayoutWidth(window.Id, window.MinWidth);
            var defaultHeight = ResolveDefaultLayoutHeight(window.Id, window.MinHeight);
            if (IsInvalidDimension(width))
            {
                width = defaultWidth;
            }

            if (IsInvalidDimension(height))
            {
                height = defaultHeight;
            }

            var widthMultiplier = IsTopRightPad(window.Id) ? 2.0f : 2.35f;
            var heightMultiplier = IsTopRightPad(window.Id) ? 2.0f : 2.2f;
            var maxWidth = math.max(window.MinWidth, math.min(rootWidth * 0.55f, defaultWidth * widthMultiplier));
            var maxHeight = math.max(window.MinHeight, math.min(rootHeight * 0.35f, defaultHeight * heightMultiplier));
            width = math.clamp(width, window.MinWidth, maxWidth);
            height = math.clamp(height, window.MinHeight, maxHeight);

            if (rootWidth - width > 24f && saved.x <= 2f)
            {
                if (IsTopRightPad(window.Id))
                {
                    window.Position = new Vector2(math.max(0f, rootWidth - 20f - width), saved.y);
                }
                else if (string.Equals(window.Id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal) ||
                         string.Equals(window.Id, Space4XInRunHudElementIds.ForcesHoloPanel, StringComparison.Ordinal))
                {
                    window.Position = new Vector2(math.max(0f, (rootWidth - width) * 0.5f), saved.y);
                }
            }
        }

        private static float ResolveDefaultLayoutWidth(string id, float fallback)
        {
            if (string.Equals(id, Space4XInRunHudElementIds.UtilityControlsPanel, StringComparison.Ordinal))
            {
                return UtilityControlsDefaultWidth;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.TimeControlsPanel, StringComparison.Ordinal))
            {
                return TimeControlsDefaultWidth;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal))
            {
                return SpeedWidgetWidth;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.PowerRoutingPanel, StringComparison.Ordinal))
            {
                return PowerRoutingDefaultWidth;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.ForcesHoloPanel, StringComparison.Ordinal))
            {
                return ForcesHoloWidth;
            }

            return fallback;
        }

        private static float ResolveDefaultLayoutHeight(string id, float fallback)
        {
            if (string.Equals(id, Space4XInRunHudElementIds.UtilityControlsPanel, StringComparison.Ordinal))
            {
                return UtilityControlsDefaultHeight;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.TimeControlsPanel, StringComparison.Ordinal))
            {
                return TimeControlsDefaultHeight;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal))
            {
                return SpeedWidgetDefaultHeight;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.PowerRoutingPanel, StringComparison.Ordinal))
            {
                return PowerRoutingDefaultHeight;
            }

            if (string.Equals(id, Space4XInRunHudElementIds.ForcesHoloPanel, StringComparison.Ordinal))
            {
                return ForcesHoloDefaultHeight;
            }

            return fallback;
        }

        private static bool IsInvalidDimension(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f;
        }

        private static int Fsi(int value)
        {
            return math.max(1, (int)math.round(value * math.max(Space4XUserSettingsStore.FontScaleMin, _runtimeFontScale)));
        }

        private void RescaleHudFontSizes(float previousScale, float nextScale)
        {
            if (_root == null)
            {
                return;
            }

            var oldScale = math.max(Space4XUserSettingsStore.FontScaleMin, previousScale);
            var newScale = math.max(Space4XUserSettingsStore.FontScaleMin, nextScale);
            var ratio = newScale / oldScale;
            if (math.abs(ratio - 1f) <= 0.0001f)
            {
                return;
            }

            var stack = new Stack<VisualElement>(128);
            stack.Push(_root);
            while (stack.Count > 0)
            {
                var element = stack.Pop();
                if (element != null)
                {
                    var current = element.resolvedStyle.fontSize;
                    if (!float.IsNaN(current) && !float.IsInfinity(current) && current > 0.01f)
                    {
                        element.style.fontSize = math.max(1f, current * ratio);
                    }

                    var childCount = element.childCount;
                    for (var i = 0; i < childCount; i++)
                    {
                        stack.Push(element[i]);
                    }
                }
            }
        }

        private void ScheduleLayoutInitialization()
        {
            _layoutInitialized = false;
            if (_root == null)
            {
                return;
            }

            _root.schedule.Execute(() =>
            {
                if (!_layoutInitialized)
                {
                    InitializeWindowLayoutsFromVisualTree();
                }
            });
        }

        private void ApplyAllWindowLayouts(bool clampToViewport)
        {
            foreach (var pair in _windowLayouts)
            {
                ApplyWindowLayout(pair.Value, clampToViewport);
            }
        }

        private void ApplyWindowLayout(WindowLayoutRuntime window, bool clampToViewport)
        {
            if (window?.Element == null)
            {
                return;
            }

            var effectiveScale = math.clamp(_globalUiScale * math.max(LayoutMinScale, window.Scale), LayoutMinScale, LayoutMaxScale * 2f);
            var width = math.max(window.MinWidth, window.Size.x);
            var height = math.max(window.MinHeight, window.Size.y);
            var position = window.Position;
            var rootWidth = 0f;
            var rootHeight = 0f;
            var hasRootSize = false;

            if (_root != null)
            {
                rootWidth = _root.resolvedStyle.width;
                rootHeight = _root.resolvedStyle.height;
                hasRootSize = rootWidth > 1f && rootHeight > 1f;
            }

            if (hasRootSize && IsCompactWindow(window.Id))
            {
                var defaultWidth = ResolveDefaultLayoutWidth(window.Id, window.MinWidth);
                var defaultHeight = ResolveDefaultLayoutHeight(window.Id, window.MinHeight);
                var widthMultiplier = IsTopRightPad(window.Id) ? 2.0f : 2.35f;
                var heightMultiplier = IsTopRightPad(window.Id) ? 2.0f : 2.2f;
                var maxWidth = math.max(window.MinWidth, math.min(rootWidth * 0.55f, defaultWidth * widthMultiplier));
                var maxHeight = math.max(window.MinHeight, math.min(rootHeight * 0.35f, defaultHeight * heightMultiplier));
                width = math.clamp(width, window.MinWidth, maxWidth);
                height = math.clamp(height, window.MinHeight, maxHeight);
            }

            if (clampToViewport && _root != null)
            {
                if (rootWidth > 0f)
                {
                    position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, rootWidth - width * effectiveScale));
                }

                if (rootHeight > 0f)
                {
                    position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, rootHeight - height * effectiveScale));
                }
            }

            window.Position = position;
            window.Size = new Vector2(width, height);
            window.Element.style.position = Position.Absolute;
            window.Element.style.left = position.x;
            window.Element.style.top = position.y;
            window.Element.style.right = StyleKeyword.Auto;
            window.Element.style.bottom = StyleKeyword.Auto;
            window.Element.style.marginLeft = 0f;
            window.Element.style.marginTop = 0f;
            window.Element.style.width = width;
            window.Element.style.height = height;
            window.Element.transform.scale = new Vector3(effectiveScale, effectiveScale, 1f);
            window.Element.transform.position = Vector3.zero;

            if (window.ResizeGrip != null)
            {
                window.ResizeGrip.style.display = _layoutEditMode ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (string.Equals(window.Id, Space4XInRunHudElementIds.RootPanel, StringComparison.Ordinal))
            {
                _hudPanelPosition = position;
            }
        }

        private void ReleaseWindowPointerCapture(WindowLayoutRuntime window)
        {
            if (window == null)
            {
                return;
            }

            var captureElement = window.ResizeActive && window.ResizeGrip != null ? window.ResizeGrip : window.Element;
            if (captureElement != null && window.PointerId >= 0 && captureElement.HasPointerCapture(window.PointerId))
            {
                captureElement.ReleasePointer(window.PointerId);
            }

            window.DragActive = false;
            window.ResizeActive = false;
            window.PointerId = -1;
        }

        private void ReleaseAllWindowCaptures()
        {
            foreach (var pair in _windowLayouts)
            {
                ReleaseWindowPointerCapture(pair.Value);
            }
        }

        private void SetGlobalUiScale(float scale, bool emitNotification = true, bool persist = true)
        {
            var clamped = math.clamp(scale, LayoutMinScale, LayoutMaxScale);
            if (math.abs(_globalUiScale - clamped) < 0.0001f)
            {
                return;
            }

            _globalUiScale = clamped;
            ApplyAllWindowLayouts(clampToViewport: true);
            if (persist)
            {
                SaveLayoutPreferences();
                PersistHudScaleSetting(_globalUiScale);
            }

            if (emitNotification)
            {
                AddNotification($"UI scale {_globalUiScale:0.00}x");
            }
        }

        private void ResetWindowLayouts()
        {
            _savedWindowLayouts.Clear();
            _globalUiScale = 1f;
            InitializeWindowLayoutsFromVisualTree();
            SaveLayoutPreferences();
            PersistHudScaleSetting(_globalUiScale);
        }

        private static void PersistHudScaleSetting(float scale)
        {
            var settings = Space4XUserSettingsStore.LoadOrDefault();
            settings.hud_scale = math.clamp(scale, LayoutMinScale, LayoutMaxScale);
            Space4XUserSettingsStore.Save(settings);
        }

        private void PopulateLayoutSnapshot(ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.layout_edit_mode = BoolToInt(_layoutEditMode);
            snapshot.ui_global_scale = _globalUiScale;
            PopulateWindowSnapshot(Space4XInRunHudElementIds.RootPanel, out snapshot.status_panel_x, out snapshot.status_panel_y, out snapshot.status_panel_w, out snapshot.status_panel_h, out snapshot.status_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.MinimapPanel, out snapshot.minimap_panel_x, out snapshot.minimap_panel_y, out snapshot.minimap_panel_w, out snapshot.minimap_panel_h, out snapshot.minimap_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.TargetPanel, out snapshot.target_panel_x, out snapshot.target_panel_y, out snapshot.target_panel_w, out snapshot.target_panel_h, out snapshot.target_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.InventoryPanel, out snapshot.inventory_panel_x, out snapshot.inventory_panel_y, out snapshot.inventory_panel_w, out snapshot.inventory_panel_h, out snapshot.inventory_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.NotificationPanel, out snapshot.notification_panel_x, out snapshot.notification_panel_y, out snapshot.notification_panel_w, out snapshot.notification_panel_h, out snapshot.notification_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.SpeedWidgetPanel, out snapshot.speed_panel_x, out snapshot.speed_panel_y, out snapshot.speed_panel_w, out snapshot.speed_panel_h, out snapshot.speed_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.ShipControlPanel, out snapshot.ship_control_panel_x, out snapshot.ship_control_panel_y, out snapshot.ship_control_panel_w, out snapshot.ship_control_panel_h, out snapshot.ship_control_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.ForcesHoloPanel, out snapshot.forces_panel_x, out snapshot.forces_panel_y, out snapshot.forces_panel_w, out snapshot.forces_panel_h, out snapshot.forces_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.PowerRoutingPanel, out snapshot.power_routing_panel_x, out snapshot.power_routing_panel_y, out snapshot.power_routing_panel_w, out snapshot.power_routing_panel_h, out snapshot.power_routing_panel_scale);
            PopulateWindowSnapshot(Space4XInRunHudElementIds.ProductionPanel, out snapshot.production_panel_x, out snapshot.production_panel_y, out snapshot.production_panel_w, out snapshot.production_panel_h, out snapshot.production_panel_scale);
            snapshot.hud_panel_x = snapshot.status_panel_x;
            snapshot.hud_panel_y = snapshot.status_panel_y;
        }

        private void PopulateWindowSnapshot(
            string id,
            out float x,
            out float y,
            out float width,
            out float height,
            out float scale)
        {
            if (_windowLayouts.TryGetValue(id, out var window) && window != null)
            {
                x = window.Position.x;
                y = window.Position.y;
                width = window.Size.x;
                height = window.Size.y;
                scale = window.Scale;
                return;
            }

            x = 0f;
            y = 0f;
            width = 0f;
            height = 0f;
            scale = 1f;
        }

        private void LoadLayoutPreferences()
        {
            _savedWindowLayouts.Clear();
            _globalUiScale = 1f;
            _migrateLegacyTopRightPads = false;

            if (!PlayerPrefs.HasKey(LayoutPrefsKey))
            {
                return;
            }

            var json = PlayerPrefs.GetString(LayoutPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            HudLayoutSaveBlob blob;
            try
            {
                blob = JsonUtility.FromJson<HudLayoutSaveBlob>(json);
            }
            catch
            {
                return;
            }

            if (blob == null)
            {
                return;
            }

            _migrateLegacyTopRightPads = blob.version < LayoutSchemaVersion;
            _globalUiScale = math.clamp(blob.global_scale <= 0f ? 1f : blob.global_scale, LayoutMinScale, LayoutMaxScale);
            var windows = blob.windows ?? Array.Empty<WindowLayoutSavedState>();
            if (IsLikelyCollapsedLayout(windows))
            {
                PlayerPrefs.DeleteKey(LayoutPrefsKey);
                PlayerPrefs.Save();
                return;
            }

            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window == null || string.IsNullOrWhiteSpace(window.id))
                {
                    continue;
                }

                _savedWindowLayouts[window.id] = window;
            }
        }

        private static bool IsLikelyCollapsedLayout(WindowLayoutSavedState[] windows)
        {
            if (windows == null || windows.Length == 0)
            {
                return false;
            }

            var tracked = 0;
            var nearOrigin = 0;
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window == null || string.IsNullOrWhiteSpace(window.id))
                {
                    continue;
                }

                if (!string.Equals(window.id, Space4XInRunHudElementIds.RootPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.MinimapPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.UtilityControlsPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.TimeControlsPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.TargetPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.InventoryPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.ProductionPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.ShipControlPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.NotificationPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.SpeedWidgetPanel, StringComparison.Ordinal) &&
                    !string.Equals(window.id, Space4XInRunHudElementIds.ForcesHoloPanel, StringComparison.Ordinal))
                {
                    continue;
                }

                tracked++;
                if (math.abs(window.x) <= 2f && math.abs(window.y) <= 2f)
                {
                    nearOrigin++;
                }
            }

            return tracked >= 4 && nearOrigin >= 4;
        }

        private void SaveLayoutPreferences()
        {
            var entries = new List<WindowLayoutSavedState>(_windowLayouts.Count);
            foreach (var pair in _windowLayouts)
            {
                var window = pair.Value;
                if (window == null)
                {
                    continue;
                }

                entries.Add(new WindowLayoutSavedState
                {
                    id = window.Id ?? string.Empty,
                    x = window.Position.x,
                    y = window.Position.y,
                    width = window.Size.x,
                    height = window.Size.y,
                    scale = window.Scale
                });
            }

            var blob = new HudLayoutSaveBlob
            {
                version = LayoutSchemaVersion,
                global_scale = _globalUiScale,
                windows = entries.ToArray()
            };

            var json = JsonUtility.ToJson(blob);
            PlayerPrefs.SetString(LayoutPrefsKey, json);
            PlayerPrefs.Save();
        }

        private void UpdateMinimapViewport(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_minimapViewportImage == null)
            {
                return;
            }

            var viewportVisible = snapshot.hud_visible == 1 && _minimapVisible;
            _minimapViewportImage.style.display = viewportVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!viewportVisible)
            {
                return;
            }

            EnsureMinimapRenderResources();
            if (_minimapCamera == null || _minimapRenderTexture == null)
            {
                return;
            }

            var anchorPosition = _hasFlagshipPose ? _latestFlagshipPosition : float3.zero;
            var anchorRotation = _hasFlagshipPose ? _latestFlagshipRotation : quaternion.identity;
            var desired = new Vector3(anchorPosition.x, anchorPosition.y + minimapCameraHeight, anchorPosition.z);
            var current = _minimapCamera.transform.position;
            var follow = 1f - Mathf.Exp(-Mathf.Max(0.01f, minimapCameraFollowSmoothing) * UTime.unscaledDeltaTime);
            _minimapCamera.transform.position = Vector3.Lerp(current, desired, follow);

            var forward = math.mul(anchorRotation, new float3(0f, 0f, 1f));
            if (math.lengthsq(forward) < 0.0001f)
            {
                forward = new float3(0f, 0f, 1f);
            }

            var yaw = math.degrees(math.atan2(forward.x, forward.z));
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            _minimapCamera.orthographicSize = Mathf.Max(8f, minimapCameraOrthoSize);
            _minimapCamera.cullingMask = minimapCullingMask.value;
            _minimapCamera.backgroundColor = minimapCameraClearColor;
            _minimapCamera.Render();
        }

        private void UpdateMinimapOverlay(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_minimapOverlayLayer == null)
            {
                return;
            }

            var visible = snapshot.hud_visible == 1 && _minimapVisible;
            _minimapOverlayLayer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _minimapOverlayLayer.Clear();
            if (!visible)
            {
                return;
            }

            var contacts = _minimapContactsScratch;
            if (contacts == null || contacts.Count == 0)
            {
                return;
            }

            var overlayWidth = _minimapOverlayLayer.resolvedStyle.width;
            var overlayHeight = _minimapOverlayLayer.resolvedStyle.height;
            if (overlayWidth <= 1f || overlayHeight <= 1f)
            {
                overlayWidth = MinimapViewportSize;
                overlayHeight = MinimapViewportSize;
            }

            var radius = math.max(8f, math.min(overlayWidth, overlayHeight) * 0.5f - 2f);
            var center = new float2(overlayWidth * 0.5f, overlayHeight * 0.5f);
            var extent = math.max(8f, minimapCameraOrthoSize);
            var inverseRotation = _hasFlagshipPose ? math.inverse(_latestFlagshipRotation) : quaternion.identity;

            for (var i = 0; i < contacts.Count; i++)
            {
                var contact = contacts[i];
                if (!ShouldShowContactForCurrentOverlay(contact))
                {
                    continue;
                }

                var relative = new float3(contact.relative_x, 0f, contact.relative_z);
                var local = math.mul(inverseRotation, relative);
                var normalized = new float2(local.x / extent, local.z / extent);

                var length = math.length(normalized);
                if (length > 1f)
                {
                    normalized /= length;
                }

                var position = center + new float2(normalized.x * radius, -normalized.y * radius);
                var size = Mathf.Lerp(3f, 7f, Mathf.Clamp01(contact.threat_level));
                var marker = new VisualElement();
                marker.style.position = Position.Absolute;
                marker.style.left = position.x - size * 0.5f;
                marker.style.top = position.y - size * 0.5f;
                marker.style.width = size;
                marker.style.height = size;
                marker.style.borderTopLeftRadius = size * 0.5f;
                marker.style.borderTopRightRadius = size * 0.5f;
                marker.style.borderBottomLeftRadius = size * 0.5f;
                marker.style.borderBottomRightRadius = size * 0.5f;
                marker.style.backgroundColor = ResolveOverlayMarkerColor(contact);
                marker.style.borderTopWidth = 1f;
                marker.style.borderRightWidth = 1f;
                marker.style.borderBottomWidth = 1f;
                marker.style.borderLeftWidth = 1f;
                marker.style.borderTopColor = new Color(0.03f, 0.05f, 0.06f, 0.85f);
                marker.style.borderRightColor = new Color(0.03f, 0.05f, 0.06f, 0.85f);
                marker.style.borderBottomColor = new Color(0.03f, 0.05f, 0.06f, 0.85f);
                marker.style.borderLeftColor = new Color(0.03f, 0.05f, 0.06f, 0.85f);
                marker.tooltip = BuildMinimapMarkerTooltip(contact);
                marker.focusable = false;
                marker.pickingMode = PickingMode.Ignore;
                _minimapOverlayLayer.Add(marker);
            }
        }

        private void AddMinimapOverlayModeButton(VisualElement parent, string text, MinimapOverlayMode mode, string tooltip)
        {
            var button = new Button
            {
                text = text
            };
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                button.style.unityFont = runtimeFont;
            }

            button.style.width = 22f;
            button.style.height = 16f;
            button.style.minWidth = 22f;
            button.style.marginRight = 2f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 1f;
            button.style.paddingRight = 1f;
            button.style.fontSize = Fsi(8);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = new Color(0.86f, 0.93f, 0.98f, 1f);
            button.style.backgroundColor = new Color(0.16f, 0.23f, 0.30f, 0.95f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.30f, 0.46f, 0.58f, 0.9f);
            button.style.borderRightColor = new Color(0.30f, 0.46f, 0.58f, 0.9f);
            button.style.borderBottomColor = new Color(0.30f, 0.46f, 0.58f, 0.9f);
            button.style.borderLeftColor = new Color(0.30f, 0.46f, 0.58f, 0.9f);
            button.tooltip = tooltip;
            button.focusable = false;
            button.tabIndex = -1;
            button.clicked += () => SetMinimapOverlayMode(mode, notify: true);
            parent.Add(button);

            _minimapOverlayButtons.Add(new MinimapOverlayModeButtonRuntime
            {
                Mode = mode,
                Button = button
            });
        }

        private void CycleMinimapOverlayMode(int step, bool notify)
        {
            const int modeCount = 8;
            var current = (int)_minimapOverlayMode;
            var next = (current + step) % modeCount;
            if (next < 0)
            {
                next += modeCount;
            }

            SetMinimapOverlayMode((MinimapOverlayMode)next, notify);
        }

        private void SetMinimapOverlayMode(MinimapOverlayMode mode, bool notify)
        {
            if (_minimapOverlayMode == mode)
            {
                if (!notify)
                {
                    UpdateMinimapOverlayModeUi();
                }
                return;
            }

            _minimapOverlayMode = mode;
            if (notify)
            {
                AddNotification($"Minimap overlay: {ResolveMinimapOverlayModeDisplayName(_minimapOverlayMode)}.");
            }

            UpdateMinimapOverlayModeUi();
        }

        private void UpdateMinimapOverlayModeUi()
        {
            if (_minimapOverlayModeLabel != null)
            {
                _minimapOverlayModeLabel.text = $"Overlay: {ResolveMinimapOverlayModeDisplayName(_minimapOverlayMode)}";
            }

            if (_cycleMinimapOverlayButton != null)
            {
                _cycleMinimapOverlayButton.text = $"O:{ResolveMinimapOverlayModeShortToken(_minimapOverlayMode)}";
            }

            for (var i = 0; i < _minimapOverlayButtons.Count; i++)
            {
                var entry = _minimapOverlayButtons[i];
                if (entry?.Button == null)
                {
                    continue;
                }

                var active = entry.Mode == _minimapOverlayMode;
                entry.Button.style.backgroundColor = active
                    ? new Color(0.29f, 0.47f, 0.60f, 1f)
                    : new Color(0.16f, 0.23f, 0.30f, 0.95f);
                entry.Button.style.color = active
                    ? new Color(0.96f, 0.99f, 1f, 1f)
                    : new Color(0.84f, 0.92f, 0.97f, 1f);
            }
        }

        private int CountVisibleMinimapOverlayContacts(List<Space4XMinimapContactKernelSnapshot> contacts)
        {
            if (contacts == null || contacts.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < contacts.Count; i++)
            {
                if (ShouldShowContactForCurrentOverlay(contacts[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool ShouldShowContactForCurrentOverlay(Space4XMinimapContactKernelSnapshot contact)
        {
            if (contact == null)
            {
                return false;
            }

            return _minimapOverlayMode switch
            {
                MinimapOverlayMode.All => true,
                MinimapOverlayMode.Hostile => string.Equals(contact.relation, "hostile", System.StringComparison.Ordinal),
                MinimapOverlayMode.Ally => string.Equals(contact.relation, "ally", System.StringComparison.Ordinal) || string.Equals(contact.relation, "self", System.StringComparison.Ordinal),
                MinimapOverlayMode.Neutral => string.Equals(contact.relation, "neutral", System.StringComparison.Ordinal),
                MinimapOverlayMode.Em => contact.em_signature > MinimapOverlaySignatureThreshold,
                MinimapOverlayMode.Thermal => contact.thermal_signature > MinimapOverlaySignatureThreshold,
                MinimapOverlayMode.Gravitic => contact.gravitic_signature > MinimapOverlaySignatureThreshold,
                MinimapOverlayMode.Psi => contact.psi_signature > MinimapOverlaySignatureThreshold,
                _ => true
            };
        }

        private static string ResolveMinimapOverlayModeDisplayName(MinimapOverlayMode mode)
        {
            return mode switch
            {
                MinimapOverlayMode.All => "All",
                MinimapOverlayMode.Hostile => "Hostile",
                MinimapOverlayMode.Ally => "Ally/Self",
                MinimapOverlayMode.Neutral => "Neutral",
                MinimapOverlayMode.Em => "EM",
                MinimapOverlayMode.Thermal => "Thermal",
                MinimapOverlayMode.Gravitic => "Gravitic",
                MinimapOverlayMode.Psi => "Psi",
                _ => "All"
            };
        }

        private static string ResolveMinimapOverlayModeShortToken(MinimapOverlayMode mode)
        {
            return mode switch
            {
                MinimapOverlayMode.All => "ALL",
                MinimapOverlayMode.Hostile => "H",
                MinimapOverlayMode.Ally => "A",
                MinimapOverlayMode.Neutral => "N",
                MinimapOverlayMode.Em => "E",
                MinimapOverlayMode.Thermal => "T",
                MinimapOverlayMode.Gravitic => "G",
                MinimapOverlayMode.Psi => "P",
                _ => "ALL"
            };
        }

        private static string ResolveMinimapOverlayModeToken(MinimapOverlayMode mode)
        {
            return mode switch
            {
                MinimapOverlayMode.All => "all",
                MinimapOverlayMode.Hostile => "hostile",
                MinimapOverlayMode.Ally => "ally",
                MinimapOverlayMode.Neutral => "neutral",
                MinimapOverlayMode.Em => "em",
                MinimapOverlayMode.Thermal => "thermal",
                MinimapOverlayMode.Gravitic => "gravitic",
                MinimapOverlayMode.Psi => "psi",
                _ => "all"
            };
        }

        private Color ResolveOverlayMarkerColor(Space4XMinimapContactKernelSnapshot contact)
        {
            return _minimapOverlayMode switch
            {
                MinimapOverlayMode.Em => Color.Lerp(new Color(0.14f, 0.35f, 0.48f, 0.85f), new Color(0.42f, 0.86f, 1f, 1f), Mathf.Clamp01(contact.em_signature)),
                MinimapOverlayMode.Thermal => Color.Lerp(new Color(0.34f, 0.20f, 0.09f, 0.85f), new Color(0.98f, 0.50f, 0.25f, 1f), Mathf.Clamp01(contact.thermal_signature)),
                MinimapOverlayMode.Gravitic => Color.Lerp(new Color(0.14f, 0.26f, 0.16f, 0.85f), new Color(0.56f, 0.89f, 0.56f, 1f), Mathf.Clamp01(contact.gravitic_signature)),
                MinimapOverlayMode.Psi => Color.Lerp(new Color(0.30f, 0.14f, 0.30f, 0.85f), new Color(0.96f, 0.50f, 0.88f, 1f), Mathf.Clamp01(contact.psi_signature)),
                _ => ResolveRelationColor(contact.color_token)
            };
        }

        private static string BuildMinimapMarkerTooltip(Space4XMinimapContactKernelSnapshot contact)
        {
            return $"{contact.relation} d={contact.distance:0.0}m em={contact.em_signature:0.00} th={contact.thermal_signature:0.00} g={contact.gravitic_signature:0.00} p={contact.psi_signature:0.00}";
        }

        private void EnsureMinimapRenderResources()
        {
            var width = Mathf.Clamp(minimapRenderResolution.x, 64, 1024);
            var height = Mathf.Clamp(minimapRenderResolution.y, 64, 1024);
            if (_minimapRenderTexture == null || !_minimapRenderTexture.IsCreated() || _minimapRenderTexture.width != width || _minimapRenderTexture.height != height)
            {
                if (_minimapRenderTexture != null)
                {
                    _minimapRenderTexture.Release();
                    Destroy(_minimapRenderTexture);
                }

                _minimapRenderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "Space4XMinimapRT",
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _minimapRenderTexture.Create();
            }

            if (_minimapCamera == null)
            {
                var cameraGo = new GameObject("Space4X Minimap Camera");
                cameraGo.hideFlags = HideFlags.DontSave;
                DontDestroyOnLoad(cameraGo);
                _minimapCamera = cameraGo.AddComponent<UnityEngine.Camera>();
                _minimapCamera.enabled = false;
                _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
                _minimapCamera.allowMSAA = false;
                _minimapCamera.allowHDR = false;
                _minimapCamera.orthographic = true;
                _minimapCamera.nearClipPlane = 0.3f;
                _minimapCamera.farClipPlane = 6000f;
                _minimapCamera.depth = -90f;
            }

            _minimapCamera.targetTexture = _minimapRenderTexture;
            if (_minimapViewportImage != null && _minimapViewportImage.image != _minimapRenderTexture)
            {
                _minimapViewportImage.image = _minimapRenderTexture;
            }
        }

        private void CleanupMinimapRenderResources()
        {
            if (_minimapViewportImage != null)
            {
                _minimapViewportImage.image = null;
            }

            if (_minimapCamera != null)
            {
                _minimapCamera.targetTexture = null;
                Destroy(_minimapCamera.gameObject);
                _minimapCamera = null;
            }

            if (_minimapRenderTexture != null)
            {
                _minimapRenderTexture.Release();
                Destroy(_minimapRenderTexture);
                _minimapRenderTexture = null;
            }
        }

        private bool ResolveRunActive()
        {
            if (IsTruthy(System.Environment.GetEnvironmentVariable(ForceRunActiveEnv)))
            {
                return true;
            }

            if (_menuOverlay == null)
            {
                _menuOverlay = FindAnyObjectByType<Space4XMainMenuOverlay>();
            }

            if (_menuOverlay != null)
            {
                var snapshot = _menuOverlay.CaptureKernelSnapshot();
                if (snapshot.run_active == 1)
                    return true;

                if (snapshot.menu_visible == 1)
                    return false;
            }

            if (HasPlayableFlagshipContext())
            {
                return true;
            }

            if (!enableInSmokeScene)
                return false;

            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal);
        }

        private bool HasPlayableFlagshipContext()
        {
            if (_flagshipController == null)
            {
                _flagshipController = FindAnyObjectByType<Space4XPlayerFlagshipController>();
            }

            if (_flagshipController != null &&
                _flagshipController.TryGetControlledFlagship(out var controlled) &&
                controlled != Entity.Null)
            {
                return true;
            }

            if (!TryResolveEntityManager(out var entityManager))
            {
                return false;
            }

            if (!_playerFlagshipQueryValid)
            {
                _playerFlagshipQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagshipTag>());
                _playerFlagshipQueryValid = true;
            }

            return !_playerFlagshipQuery.IsEmptyIgnoreFilter;
        }

        private void EnsureInputReferences()
        {
            if (_playerInput == null)
            {
                _playerInput = FindAnyObjectByType<PlayerInput>();
            }
        }

        private bool TryResolveFlagshipEntity(out Entity flagship, out EntityManager entityManager)
        {
            flagship = Entity.Null;
            entityManager = default;

            if (!TryResolveEntityManager(out entityManager))
                return false;

            if (_flagshipController == null)
            {
                _flagshipController = FindAnyObjectByType<Space4XPlayerFlagshipController>();
            }

            if (_flagshipController == null)
                return false;

            if (!_flagshipController.TryGetControlledFlagship(out flagship))
                return false;

            return flagship != Entity.Null && entityManager.Exists(flagship);
        }

        private bool TryResolveEntityManager(out EntityManager entityManager)
        {
            entityManager = default;

            if (_world == null || !_world.IsCreated)
            {
                _world = World.DefaultGameObjectInjectionWorld;
            }

            if (_world == null || !_world.IsCreated)
                return false;

            entityManager = _world.EntityManager;
            return true;
        }

        private static void PopulateVitals(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            if (entityManager.HasComponent<HullIntegrity>(flagship))
            {
                var hull = entityManager.GetComponentData<HullIntegrity>(flagship);
                snapshot.health_current = math.max(0f, hull.Current);
                snapshot.health_max = math.max(0f, hull.Max);
                snapshot.health_ratio = ResolveRatio(snapshot.health_current, snapshot.health_max);
            }
            else if (entityManager.HasComponent<VesselResourceLevels>(flagship))
            {
                var levels = entityManager.GetComponentData<VesselResourceLevels>(flagship);
                snapshot.health_current = math.max(0f, levels.CurrentHull);
                snapshot.health_max = math.max(0f, levels.MaxHull);
                snapshot.health_ratio = ResolveRatio(snapshot.health_current, snapshot.health_max);
            }

            if (entityManager.HasComponent<Space4XShield>(flagship))
            {
                var shields = entityManager.GetComponentData<Space4XShield>(flagship);
                snapshot.shields_current = math.max(0f, shields.Current);
                snapshot.shields_max = math.max(0f, shields.Maximum);
                snapshot.shields_ratio = ResolveRatio(snapshot.shields_current, snapshot.shields_max);
            }

            if (entityManager.HasComponent<Space4XArmor>(flagship))
            {
                var armor = entityManager.GetComponentData<Space4XArmor>(flagship);
                snapshot.armor_rating = math.max(0f, armor.Thickness);
            }

            if (entityManager.HasComponent<SupplyStatus>(flagship))
            {
                var supply = entityManager.GetComponentData<SupplyStatus>(flagship);
                snapshot.supplies_current = math.max(0f, supply.RepairParts);
                snapshot.supplies_max = math.max(0f, supply.RepairPartsCapacity);
                snapshot.supplies_ratio = ResolveRatio(snapshot.supplies_current, snapshot.supplies_max);

                snapshot.fuel_current = math.max(0f, supply.Fuel);
                snapshot.fuel_max = math.max(0f, supply.FuelCapacity);
                snapshot.fuel_ratio = ResolveRatio(snapshot.fuel_current, snapshot.fuel_max);

                snapshot.food_current = math.max(0f, supply.Provisions);
                snapshot.food_max = math.max(0f, supply.ProvisionsCapacity);
                snapshot.food_ratio = ResolveRatio(snapshot.food_current, snapshot.food_max);

                snapshot.ammo_current = math.max(0f, supply.Ammunition);
                snapshot.ammo_max = math.max(0f, supply.AmmunitionCapacity);
                snapshot.ammo_ratio = ResolveRatio(snapshot.ammo_current, snapshot.ammo_max);
            }
            else if (entityManager.HasComponent<VesselResourceLevels>(flagship))
            {
                var levels = entityManager.GetComponentData<VesselResourceLevels>(flagship);
                snapshot.fuel_current = math.max(0f, levels.CurrentFuel);
                snapshot.fuel_max = math.max(0f, levels.MaxFuel);
                snapshot.fuel_ratio = ResolveRatio(snapshot.fuel_current, snapshot.fuel_max);
            }

            if (entityManager.HasComponent<CrewCapacity>(flagship))
            {
                var crew = entityManager.GetComponentData<CrewCapacity>(flagship);
                snapshot.crew_current = math.max(0, crew.CurrentCrew);
                snapshot.crew_max = math.max(0, crew.MaxCrew);
                snapshot.crew_ratio = ResolveRatio(snapshot.crew_current, snapshot.crew_max);
            }
        }

        private void PopulatePowerMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            if (TryResolvePowerBudget(entityManager, flagship, out var capacity, out var available, out var draw))
            {
                snapshot.power_capacity_mw = capacity;
                snapshot.power_available_mw = available;
                snapshot.power_draw_mw = draw;
                snapshot.power_ratio = ResolveRatio(available, math.max(0.01f, capacity));
                snapshot.power_deficit_mw = math.max(0f, draw - capacity);
                snapshot.power_deficit_tags = ResolvePowerDeficitTags(capacity, draw, estimateOnly: false);
                return;
            }

            if (entityManager.HasComponent<ModuleRatingAggregate>(flagship))
            {
                var rating = entityManager.GetComponentData<ModuleRatingAggregate>(flagship);
                var inferredCapacity = math.max(0f, -rating.PowerBalanceMW);
                snapshot.power_capacity_mw = inferredCapacity;
                snapshot.power_available_mw = inferredCapacity;
                snapshot.power_draw_mw = math.max(0f, rating.PowerBalanceMW);
                snapshot.power_ratio = ResolveRatio(snapshot.power_available_mw, math.max(0.01f, snapshot.power_capacity_mw));
                snapshot.power_deficit_mw = math.max(0f, snapshot.power_draw_mw - snapshot.power_capacity_mw);
                snapshot.power_deficit_tags = ResolvePowerDeficitTags(snapshot.power_capacity_mw, snapshot.power_draw_mw, estimateOnly: true);
                return;
            }

            snapshot.power_deficit_mw = 0f;
            snapshot.power_deficit_tags = "no_power_data";
        }

        private bool TryResolvePowerRoutingStateEntity(EntityManager entityManager, out Entity stateEntity)
        {
            stateEntity = Entity.Null;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ShipPowerRoutingBinding>(),
                ComponentType.ReadOnly<ShipPowerRoutingProfile>(),
                ComponentType.ReadOnly<ShipPowerRoutingRuntime>(),
                ComponentType.ReadOnly<ShipPowerRoutingSensorBaseline>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                return false;
            }

            stateEntity = entities[0];
            return stateEntity != Entity.Null && entityManager.Exists(stateEntity);
        }

        private Entity EnsurePowerRoutingStateEntity(EntityManager entityManager)
        {
            if (!TryResolvePowerRoutingStateEntity(entityManager, out var stateEntity))
            {
                stateEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(stateEntity, ShipPowerRoutingBinding.Default);
                entityManager.AddComponentData(stateEntity, ShipPowerRoutingProfile.Default);
                entityManager.AddComponentData(stateEntity, ShipPowerRoutingRuntime.Default);
                entityManager.AddComponentData(stateEntity, ShipPowerRoutingSensorBaseline.Default);
            }

            return stateEntity;
        }

        private void ApplyPowerRoutingProfileToRuntimeState(EntityManager entityManager, Entity flagship)
        {
            var stateEntity = EnsurePowerRoutingStateEntity(entityManager);

            var profileState = BuildPowerRoutingProfileComponent(GetActivePowerRoutingProfile());
            entityManager.SetComponentData(stateEntity, profileState);

            var binding = entityManager.GetComponentData<ShipPowerRoutingBinding>(stateEntity);
            if (binding.Ship != flagship)
            {
                binding.Ship = flagship;
                entityManager.SetComponentData(stateEntity, binding);

                var baseline = entityManager.GetComponentData<ShipPowerRoutingSensorBaseline>(stateEntity);
                baseline.Ship = Entity.Null;
                baseline.Initialized = 0;
                entityManager.SetComponentData(stateEntity, baseline);
            }
        }

        private void PopulatePowerRoutingMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            var profile = GetActivePowerRoutingProfile();
            if (TryResolvePowerRoutingStateEntity(entityManager, out var stateEntity))
            {
                if (entityManager.HasComponent<ShipPowerRoutingProfile>(stateEntity))
                {
                    var component = entityManager.GetComponentData<ShipPowerRoutingProfile>(stateEntity);
                    profile = new PowerRoutingProfileSavedState
                    {
                        engines = component.EnginesPercent,
                        weapons = component.WeaponsPercent,
                        shields = component.ShieldsPercent,
                        reactor = component.ReactorPercent,
                        sensors = component.SensorsPercent
                    };
                }

                if (entityManager.HasComponent<ShipPowerRoutingRuntime>(stateEntity))
                {
                    var runtime = entityManager.GetComponentData<ShipPowerRoutingRuntime>(stateEntity);
                    snapshot.power_route_engines_factor = math.max(0f, runtime.EnginesFactor);
                    snapshot.power_route_weapons_factor = math.max(0f, runtime.WeaponsFactor);
                    snapshot.power_route_shields_factor = math.max(0f, runtime.ShieldsFactor);
                    snapshot.power_route_reactor_factor = math.max(0f, runtime.ReactorFactor);
                    snapshot.power_route_sensors_factor = math.max(0f, runtime.SensorsFactor);
                    snapshot.power_route_reactor_signature_factor = math.max(0f, runtime.ReactorSignatureFactor);
                    snapshot.power_route_jam_risk_per_tick = math.saturate(runtime.JamRiskPerTick);
                    snapshot.power_route_total_pct = math.max(0f, runtime.AllocationTotalPercent);
                    snapshot.power_route_overclock_pct = math.max(0f, runtime.OverclockPercent);
                    snapshot.power_route_underclock_pct = math.max(0f, runtime.UnderclockPercent);
                }

                if (entityManager.HasComponent<ShipPowerRoutingBinding>(stateEntity))
                {
                    var binding = entityManager.GetComponentData<ShipPowerRoutingBinding>(stateEntity);
                    if (binding.Ship != Entity.Null && binding.Ship != flagship)
                    {
                        snapshot.power_route_engines_factor = 1f;
                        snapshot.power_route_weapons_factor = 1f;
                        snapshot.power_route_shields_factor = 1f;
                        snapshot.power_route_reactor_factor = 1f;
                        snapshot.power_route_sensors_factor = 1f;
                        snapshot.power_route_reactor_signature_factor = 1f;
                        snapshot.power_route_jam_risk_per_tick = 0f;
                    }
                }
            }

            ApplyPowerRoutingProfileToSnapshot(profile, ref snapshot);
        }

        private void ApplyPowerRoutingProfileToSnapshot(PowerRoutingProfileSavedState profile, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            var normalized = NormalizePowerRoutingProfile(profile);
            snapshot.power_routing_profile = _powerRoutingActiveProfile + 1;
            snapshot.power_route_engines_pct = normalized.engines;
            snapshot.power_route_weapons_pct = normalized.weapons;
            snapshot.power_route_shields_pct = normalized.shields;
            snapshot.power_route_reactor_pct = normalized.reactor;
            snapshot.power_route_sensors_pct = normalized.sensors;
            snapshot.power_route_total_pct = normalized.engines + normalized.weapons + normalized.shields + normalized.reactor + normalized.sensors;
            snapshot.power_route_overclock_pct =
                math.max(0f, normalized.engines - 100f) +
                math.max(0f, normalized.weapons - 100f) +
                math.max(0f, normalized.shields - 100f) +
                math.max(0f, normalized.reactor - 100f) +
                math.max(0f, normalized.sensors - 100f);
            snapshot.power_route_underclock_pct =
                math.max(0f, 100f - normalized.engines) +
                math.max(0f, 100f - normalized.weapons) +
                math.max(0f, 100f - normalized.shields) +
                math.max(0f, 100f - normalized.reactor) +
                math.max(0f, 100f - normalized.sensors);
        }

        private static void PopulateEngineTelemetry(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            var speed = math.length(ResolveEntityVelocity(entityManager, flagship));
            snapshot.speed_current_ups = speed;

            var hasPowerData = snapshot.power_draw_mw > 0.001f ||
                               snapshot.power_capacity_mw > 0.001f ||
                               snapshot.power_available_mw > 0.001f;
            snapshot.engine_power_estimated = hasPowerData ? 0 : 1;
            snapshot.engine_power_draw_mw = hasPowerData ? math.max(0f, snapshot.power_draw_mw) : 0f;

            var hasFuelRates = entityManager.HasComponent<SupplyConsumptionRates>(flagship) &&
                               entityManager.HasComponent<SupplyStatus>(flagship);
            if (hasFuelRates)
            {
                var rates = entityManager.GetComponentData<SupplyConsumptionRates>(flagship);
                snapshot.engine_fuel_draw_per_tick = math.max(0f, rates.FuelIdle);
                snapshot.engine_fuel_estimated = 0;

                if (entityManager.HasComponent<PlayerFlagshipFlightInput>(flagship) &&
                    entityManager.HasComponent<ShipFlightProfile>(flagship) &&
                    entityManager.HasComponent<ShipFlightRuntimeState>(flagship))
                {
                    var input = entityManager.GetComponentData<PlayerFlagshipFlightInput>(flagship);
                    var profile = entityManager.GetComponentData<ShipFlightProfile>(flagship).Sanitized();
                    var runtime = entityManager.GetComponentData<ShipFlightRuntimeState>(flagship);
                    var baseMass = entityManager.HasComponent<VesselPhysicalProperties>(flagship)
                        ? math.max(0.1f, entityManager.GetComponentData<VesselPhysicalProperties>(flagship).BaseMass)
                        : 20f;
                    var engineEfficiency = 0.5f;
                    var engineBoost = 0.5f;
                    if (entityManager.HasComponent<EnginePerformanceOutput>(flagship))
                    {
                        var engine = entityManager.GetComponentData<EnginePerformanceOutput>(flagship);
                        engineEfficiency = math.saturate(engine.Efficiency);
                        engineBoost = math.saturate(engine.Boost);
                    }

                    snapshot.engine_fuel_draw_per_tick += SupplyUtility.CalculateBoostPropulsionFuelDraw(
                        in input,
                        in profile,
                        in runtime,
                        in rates,
                        baseMass,
                        engineEfficiency,
                        engineBoost);
                }
            }
            else
            {
                snapshot.engine_fuel_draw_per_tick = math.max(0f, speed * 0.01f);
                snapshot.engine_fuel_estimated = 1;
            }

            if (snapshot.engine_power_estimated == 1)
            {
                snapshot.engine_power_draw_mw = math.max(0f, speed * 0.75f + snapshot.engine_fuel_draw_per_tick * 6f);
            }
        }

        private void PopulateForceTelemetry(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            var velocityWorld = ResolveEntityVelocity(entityManager, flagship);
            var rotation = _hasFlagshipPose ? _latestFlagshipRotation : quaternion.identity;
            var inverseRotation = math.inverse(rotation);
            var localVelocity = math.rotate(inverseRotation, velocityWorld);

            snapshot.heading_deg = ResolveHeadingDegrees(rotation);
            snapshot.velocity_forward_ups = localVelocity.z;
            snapshot.velocity_lateral_ups = localVelocity.x;
            snapshot.velocity_vertical_ups = localVelocity.y;

            var now = UTime.unscaledTime;
            var accelWorld = float3.zero;
            var accelEstimated = 1;
            if (_hasForceVelocitySample)
            {
                var dt = now - _lastForceSampleTime;
                if (dt > 0.0001f)
                {
                    accelWorld = (velocityWorld - _lastForceSampleVelocity) / dt;
                    accelEstimated = 0;
                }
            }

            _lastForceSampleVelocity = velocityWorld;
            _lastForceSampleTime = now;
            _hasForceVelocitySample = true;

            var localAcceleration = math.rotate(inverseRotation, accelWorld);
            snapshot.accel_forward_ups2 = localAcceleration.z;
            snapshot.accel_lateral_ups2 = localAcceleration.x;
            snapshot.accel_vertical_ups2 = localAcceleration.y;
            snapshot.accel_total_ups2 = math.length(localAcceleration);
            snapshot.accel_estimated = accelEstimated;
        }

        private bool TryResolvePowerBudget(EntityManager entityManager, Entity flagship, out float capacityMw, out float availableMw, out float drawMw)
        {
            capacityMw = 0f;
            availableMw = 0f;
            drawMw = 0f;

            // Prefer live power telemetry from PureDOTS power-domain components.
            var resolvedCapacityMw = 0f;
            var resolvedDrawMw = 0f;
            var hasLivePowerTelemetry = false;

            if (entityManager.HasComponent<PowerDistribution>(flagship))
            {
                var distribution = entityManager.GetComponentData<PowerDistribution>(flagship);
                resolvedCapacityMw = math.max(resolvedCapacityMw, math.max(0f, distribution.OutputPower));
                hasLivePowerTelemetry = true;
            }

            if (entityManager.HasComponent<PowerGenerator>(flagship))
            {
                var generator = entityManager.GetComponentData<PowerGenerator>(flagship);
                var generated = PowerCoreMath.CalculateActualOutput(
                    generator.TheoreticalMaxOutput,
                    generator.CurrentOutputPercent,
                    generator.Efficiency,
                    generator.DegradationLevel,
                    out _);
                resolvedCapacityMw = math.max(resolvedCapacityMw, math.max(0f, generated));
                hasLivePowerTelemetry = true;
            }

            if (entityManager.HasComponent<PowerLedger>(flagship))
            {
                var ledger = entityManager.GetComponentData<PowerLedger>(flagship);
                var ledgerCapacity = math.max(0f, ledger.DistributionOutputMW + math.max(0f, ledger.BatteryDischargeMW));
                resolvedCapacityMw = math.max(resolvedCapacityMw, ledgerCapacity);
                resolvedDrawMw = math.max(resolvedDrawMw, math.max(0f, ledger.TotalRequestedMW));
                hasLivePowerTelemetry = true;
            }

            if (entityManager.HasBuffer<ShipPowerConsumer>(flagship))
            {
                var consumers = entityManager.GetBuffer<ShipPowerConsumer>(flagship);
                var requestedDrawFromConsumers = 0f;

                for (var i = 0; i < consumers.Length; i++)
                {
                    var consumerEntity = consumers[i].Consumer;
                    if (consumerEntity == Entity.Null ||
                        !entityManager.Exists(consumerEntity) ||
                        !entityManager.HasComponent<PowerConsumer>(consumerEntity))
                    {
                        continue;
                    }

                    var consumer = entityManager.GetComponentData<PowerConsumer>(consumerEntity);
                    requestedDrawFromConsumers += math.max(0f, consumer.RequestedDraw);
                }

                if (requestedDrawFromConsumers > 0.001f)
                {
                    resolvedDrawMw = math.max(resolvedDrawMw, requestedDrawFromConsumers);
                    hasLivePowerTelemetry = true;
                }
            }

            if (hasLivePowerTelemetry && (resolvedCapacityMw > 0.001f || resolvedDrawMw > 0.001f))
            {
                capacityMw = math.max(0f, resolvedCapacityMw);
                drawMw = math.max(0f, resolvedDrawMw);
                availableMw = math.max(0f, capacityMw - drawMw);
                return true;
            }

            if (!entityManager.HasBuffer<CarrierModuleSlot>(flagship))
            {
                return false;
            }

            if (!_moduleCatalogQueryValid)
            {
                _moduleCatalogQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>());
                _moduleCatalogQueryValid = true;
            }

            if (_moduleCatalogQuery.IsEmptyIgnoreFilter)
                return false;

            var moduleCatalog = _moduleCatalogQuery.GetSingleton<ModuleCatalogSingleton>();
            if (!moduleCatalog.Catalog.IsCreated)
                return false;

            var routingProfile = BuildPowerRoutingProfileComponent(GetActivePowerRoutingProfile());
            if (TryResolvePowerRoutingStateEntity(entityManager, out var routingStateEntity) &&
                entityManager.HasComponent<ShipPowerRoutingProfile>(routingStateEntity))
            {
                routingProfile = entityManager.GetComponentData<ShipPowerRoutingProfile>(routingStateEntity);
            }

            var slots = entityManager.GetBuffer<CarrierModuleSlot>(flagship);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.CurrentModule == Entity.Null || slot.State != ModuleSlotState.Active)
                    continue;

                if (!entityManager.HasComponent<ModuleTypeId>(slot.CurrentModule))
                    continue;

                var moduleId = entityManager.GetComponentData<ModuleTypeId>(slot.CurrentModule).Value;
                if (!ModuleCatalogUtility.TryGetModuleSpec(in moduleCatalog, moduleId, out var spec))
                    continue;

                var efficiency = 1f;
                if (entityManager.HasComponent<ModuleHealth>(slot.CurrentModule))
                {
                    var health = entityManager.GetComponentData<ModuleHealth>(slot.CurrentModule);
                    efficiency = math.clamp(health.CurrentHealth / math.max(0.01f, health.MaxHealth), 0f, 1f);
                }

                var routingScale = ResolveModulePowerRoutingScale(spec.Class, in routingProfile);
                var power = spec.PowerDrawMW * efficiency * routingScale;
                if (power < 0f)
                {
                    capacityMw += -power;
                }
                else
                {
                    drawMw += power;
                }
            }

            availableMw = math.max(0f, capacityMw - drawMw);
            return capacityMw > 0f || drawMw > 0f;
        }

        private static float ResolveModulePowerRoutingScale(ModuleClass moduleClass, in ShipPowerRoutingProfile profile)
        {
            switch (moduleClass)
            {
                case ModuleClass.Reactor:
                    return math.max(0.1f, profile.ReactorPercent * 0.01f);
                case ModuleClass.Engine:
                    return math.max(0.1f, profile.EnginesPercent * 0.01f);
                case ModuleClass.Laser:
                case ModuleClass.Kinetic:
                case ModuleClass.Missile:
                case ModuleClass.PointDefense:
                case ModuleClass.Hangar:
                    return math.max(0.1f, profile.WeaponsPercent * 0.01f);
                case ModuleClass.Shield:
                    return math.max(0.1f, profile.ShieldsPercent * 0.01f);
                case ModuleClass.Scanner:
                    return math.max(0.1f, profile.SensorsPercent * 0.01f);
                default:
                    return 1f;
            }
        }

        private void PopulateTimeMetrics(ref Space4XInRunHudKernelSnapshot snapshot)
        {
            if (!TryResolveEntityManager(out var entityManager))
            {
                snapshot.time_speed_multiplier = 1f;
                snapshot.time_paused = 0;
                return;
            }

            if (!_tickQueryValid)
            {
                _tickQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
                _tickQueryValid = true;
            }

            if (_tickQuery.IsEmptyIgnoreFilter)
            {
                snapshot.time_speed_multiplier = 1f;
                snapshot.time_paused = 0;
                return;
            }

            var time = _tickQuery.GetSingleton<TickTimeState>();
            snapshot.tick = time.Tick;
            snapshot.world_seconds = time.WorldSeconds;
            snapshot.time_paused = BoolToInt(time.IsPaused);
            snapshot.time_speed_multiplier = time.CurrentSpeedMultiplier;
        }

        private void PopulateMinimapMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.minimap_self_count = 0;
            snapshot.minimap_ally_count = 0;
            snapshot.minimap_neutral_count = 0;
            snapshot.minimap_hostile_count = 0;
            snapshot.minimap_unknown_count = 0;
            snapshot.minimap_contacts_window_count = 0;
            snapshot.minimap_contacts = Array.Empty<Space4XMinimapContactKernelSnapshot>();

            var trackedContacts = ResolveTrackedContactCount(entityManager, flagship);
            snapshot.minimap_contacts_tracked = math.max(0, trackedContacts);
            _minimapContactsScratch.Clear();
            var selfFaction = ResolveFactionEntity(entityManager, flagship);
            var selfVelocity = ResolveEntityVelocity(entityManager, flagship);
            var hasFlagshipPosition = TryResolveEntityPosition(entityManager, flagship, out var flagshipPosition);
            if (hasFlagshipPosition)
            {
                snapshot.minimap_self_count = 1;
                snapshot.minimap_contacts_tracked = math.max(snapshot.minimap_contacts_tracked, 1);
                _minimapContactsScratch.Add(BuildSelfContact(entityManager, flagship, selfVelocity));
            }

            var hasPerceivedBuffer = entityManager.HasBuffer<PerceivedEntity>(flagship);
            if (hasPerceivedBuffer)
            {
                var perceived = entityManager.GetBuffer<PerceivedEntity>(flagship);
                snapshot.minimap_contacts_tracked = math.max(snapshot.minimap_contacts_tracked, perceived.Length);

                if (perceived.Length == 0)
                {
                    PopulateFallbackMinimapContacts(
                        entityManager,
                        flagship,
                        selfFaction,
                        selfVelocity,
                        hasFlagshipPosition,
                        flagshipPosition,
                        ref snapshot);
                }

                for (var i = 0; i < perceived.Length; i++)
                {
                    var contact = perceived[i];
                    var target = contact.TargetEntity;
                    if (target == Entity.Null || !entityManager.Exists(target))
                    {
                        snapshot.minimap_unknown_count++;
                        continue;
                    }

                    var relation = ResolveContactRelation(
                        entityManager,
                        flagship,
                        target,
                        selfFaction,
                        in contact,
                        out var relationScore,
                        out var relationStance,
                        out var relationSource);
                    IncrementRelationCounter(relation, ref snapshot);

                    float3 relative;
                    if (hasFlagshipPosition && TryResolveEntityPosition(entityManager, target, out var targetPosition))
                    {
                        relative = targetPosition - flagshipPosition;
                    }
                    else
                    {
                        relative = math.normalizesafe(contact.Direction, new float3(0f, 0f, 1f)) * math.max(0f, contact.Distance);
                    }

                    var planar = new float2(relative.x, relative.z);
                    var distance = math.max(0f, math.length(relative));
                    if (distance <= 0.001f)
                    {
                        distance = math.max(0f, contact.Distance);
                    }

                    var targetVelocity = ResolveEntityVelocity(entityManager, target);
                    var direction = distance > 0.001f
                        ? relative / distance
                        : math.normalizesafe(contact.Direction, new float3(0f, 0f, 1f));
                    var closingSpeed = -math.dot(targetVelocity - selfVelocity, direction);
                    ResolveEntitySignatureChannels(entityManager, target, out var emSignature, out var graviticSignature, out var thermalSignature, out var psiSignature);

                    _minimapContactsScratch.Add(new Space4XMinimapContactKernelSnapshot
                    {
                        entity_ref = ResolveEntityRef(target),
                        callsign = ResolveEntityCallsign(entityManager, target),
                        relation = ResolveRelationToken(relation),
                        relation_score = relationScore,
                        relation_stance = relationStance,
                        relation_source = relationSource,
                        color_token = ResolveRelationColorToken(relation),
                        confidence = math.saturate(contact.Confidence),
                        threat_level = math.saturate(contact.ThreatLevel / 255f),
                        distance = distance,
                        bearing_deg = ResolveBearingDegrees(planar),
                        relative_x = relative.x,
                        relative_z = relative.z,
                        speed = math.length(targetVelocity),
                        closing_speed = closingSpeed,
                        em_signature = emSignature,
                        gravitic_signature = graviticSignature,
                        thermal_signature = thermalSignature,
                        psi_signature = psiSignature
                    });
                }
            }
            else
            {
                PopulateFallbackMinimapContacts(
                    entityManager,
                    flagship,
                    selfFaction,
                    selfVelocity,
                    hasFlagshipPosition,
                    flagshipPosition,
                    ref snapshot);
            }

            snapshot.minimap_contacts_tracked = math.max(snapshot.minimap_contacts_tracked, _minimapContactsScratch.Count);
            var classifiedCount = ResolveClassifiedContactCount(in snapshot);
            if (snapshot.minimap_contacts_tracked > classifiedCount)
            {
                snapshot.minimap_unknown_count += snapshot.minimap_contacts_tracked - classifiedCount;
            }

            snapshot.minimap_relation_counts = BuildRelationCountSummary(in snapshot);

            if (_minimapContactsScratch.Count == 0)
            {
                return;
            }

            _minimapContactsScratch.Sort(CompareMinimapContacts);
            var windowCount = math.min(KernelMinimapContactWindowSize, _minimapContactsScratch.Count);
            snapshot.minimap_contacts_window_count = windowCount;
            var window = new Space4XMinimapContactKernelSnapshot[windowCount];
            for (var i = 0; i < windowCount; i++)
            {
                window[i] = _minimapContactsScratch[i];
            }

            snapshot.minimap_contacts = window;
        }

        private static Space4XMinimapContactKernelSnapshot BuildSelfContact(EntityManager entityManager, Entity flagship, float3 selfVelocity)
        {
            ResolveEntitySignatureChannels(entityManager, flagship, out var emSignature, out var graviticSignature, out var thermalSignature, out var psiSignature);
            return new Space4XMinimapContactKernelSnapshot
            {
                entity_ref = ResolveEntityRef(flagship),
                callsign = ResolveEntityCallsign(entityManager, flagship),
                relation = ResolveRelationToken(KernelMinimapRelation.Self),
                relation_score = 100,
                relation_stance = DiplomaticStance.Allied.ToString(),
                relation_source = "self",
                color_token = ResolveRelationColorToken(KernelMinimapRelation.Self),
                confidence = 1f,
                threat_level = 0f,
                distance = 0f,
                bearing_deg = 0f,
                relative_x = 0f,
                relative_z = 0f,
                speed = math.length(selfVelocity),
                closing_speed = 0f,
                em_signature = emSignature,
                gravitic_signature = graviticSignature,
                thermal_signature = thermalSignature,
                psi_signature = psiSignature
            };
        }

        private void PopulateFallbackMinimapContacts(
            EntityManager entityManager,
            Entity flagship,
            Entity selfFaction,
            float3 selfVelocity,
            bool hasFlagshipPosition,
            float3 flagshipPosition,
            ref Space4XInRunHudKernelSnapshot snapshot)
        {
            if (!hasFlagshipPosition)
            {
                return;
            }

            if (!_minimapFallbackQueryValid)
            {
                _minimapFallbackQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<HullIntegrity>());
                _minimapFallbackQueryValid = true;
            }

            if (_minimapFallbackQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = _minimapFallbackQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _minimapFallbackQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var hullStates = _minimapFallbackQuery.ToComponentDataArray<HullIntegrity>(Allocator.Temp);
            var count = math.min(entities.Length, math.min(transforms.Length, hullStates.Length));
            if (count <= 0)
            {
                return;
            }

            var hasSelfSide = TryResolveScenarioSide(entityManager, flagship, out var selfSide);
            count = math.min(count, KernelFallbackScanLimit);
            for (var i = 0; i < count; i++)
            {
                var target = entities[i];
                if (target == Entity.Null || target == flagship || !entityManager.Exists(target))
                {
                    continue;
                }

                var hull = hullStates[i];
                if (hull.Current <= 0.01f)
                {
                    continue;
                }

                var relative = transforms[i].Position - flagshipPosition;
                var planar = new float2(relative.x, relative.z);
                var distance = math.length(relative);
                if (distance <= 0.05f || distance > KernelFallbackContactMaxDistance)
                {
                    continue;
                }

                var relation = ResolveFallbackRelation(
                    entityManager,
                    flagship,
                    target,
                    selfFaction,
                    hasSelfSide,
                    selfSide,
                    out var relationScore,
                    out var relationStance,
                    out var relationSource);
                IncrementRelationCounter(relation, ref snapshot);

                var targetVelocity = ResolveEntityVelocity(entityManager, target);
                var direction = relative / math.max(0.001f, distance);
                var closingSpeed = -math.dot(targetVelocity - selfVelocity, direction);
                ResolveEntitySignatureChannels(entityManager, target, out var emSignature, out var graviticSignature, out var thermalSignature, out var psiSignature);
                var threat = relation switch
                {
                    KernelMinimapRelation.Hostile => 0.85f,
                    KernelMinimapRelation.Ally => 0.20f,
                    KernelMinimapRelation.Self => 0.0f,
                    KernelMinimapRelation.Neutral => 0.45f,
                    _ => 0.35f
                };

                _minimapContactsScratch.Add(new Space4XMinimapContactKernelSnapshot
                {
                    entity_ref = ResolveEntityRef(target),
                    callsign = ResolveEntityCallsign(entityManager, target),
                    relation = ResolveRelationToken(relation),
                    relation_score = relationScore,
                    relation_stance = relationStance,
                    relation_source = relationSource,
                    color_token = ResolveRelationColorToken(relation),
                    confidence = 0.55f,
                    threat_level = threat,
                    distance = distance,
                    bearing_deg = ResolveBearingDegrees(planar),
                    relative_x = relative.x,
                    relative_z = relative.z,
                    speed = math.length(targetVelocity),
                    closing_speed = closingSpeed,
                    em_signature = emSignature,
                    gravitic_signature = graviticSignature,
                    thermal_signature = thermalSignature,
                    psi_signature = psiSignature
                });
            }
        }

        private static void PopulateTargetMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.target_selected = 0;
            snapshot.target_multi_count = 0;
            snapshot.target_entity_ref = string.Empty;
            snapshot.target_callsign = string.Empty;
            snapshot.target_relation = string.Empty;
            snapshot.target_relation_score = 0;
            snapshot.target_relation_stance = string.Empty;
            snapshot.target_relation_source = string.Empty;
            snapshot.target_color_token = "unknown_gray";
            snapshot.target_distance = 0f;
            snapshot.target_bearing_deg = 0f;
            snapshot.target_speed = 0f;
            snapshot.target_closing_speed = 0f;
            snapshot.target_hull_current = 0f;
            snapshot.target_hull_max = 0f;
            snapshot.target_hull_ratio = 0f;

            if (entityManager.HasBuffer<Space4XPlayerTargetLockEntry>(flagship))
            {
                var locks = entityManager.GetBuffer<Space4XPlayerTargetLockEntry>(flagship);
                for (var i = 0; i < locks.Length; i++)
                {
                    if (locks[i].TargetEntity != Entity.Null && entityManager.Exists(locks[i].TargetEntity))
                    {
                        snapshot.target_multi_count++;
                    }
                }
            }

            if (!entityManager.HasComponent<Space4XPlayerTargetSelection>(flagship))
            {
                return;
            }

            var selection = entityManager.GetComponentData<Space4XPlayerTargetSelection>(flagship);
            if (selection.HasSelection == 0 || selection.TargetEntity == Entity.Null)
            {
                return;
            }

            var target = selection.TargetEntity;
            if (target == flagship || !entityManager.Exists(target))
            {
                return;
            }

            if (!TryResolveEntityPosition(entityManager, flagship, out var flagshipPosition))
            {
                return;
            }

            var hasTargetPosition = TryResolveEntityPosition(entityManager, target, out var targetPosition);
            if (!hasTargetPosition)
            {
                targetPosition = selection.TargetPoint;
            }

            var relative = targetPosition - flagshipPosition;
            var distance = math.length(relative);
            if (distance <= 0.0001f)
            {
                distance = 0f;
            }

            var direction = math.normalizesafe(relative, new float3(0f, 0f, 1f));
            var selfVelocity = ResolveEntityVelocity(entityManager, flagship);
            var targetVelocity = ResolveEntityVelocity(entityManager, target);
            var closingSpeed = -math.dot(targetVelocity - selfVelocity, direction);

            var selfFaction = ResolveFactionEntity(entityManager, flagship);
            var hasSelfSide = TryResolveScenarioSide(entityManager, flagship, out var selfSide);

            KernelMinimapRelation relation;
            int relationScore;
            string relationStance;
            string relationSource;

            if (entityManager.HasBuffer<PerceivedEntity>(flagship))
            {
                var perceived = entityManager.GetBuffer<PerceivedEntity>(flagship);
                for (var i = 0; i < perceived.Length; i++)
                {
                    if (perceived[i].TargetEntity != target)
                    {
                        continue;
                    }

                    var perceivedEntry = perceived[i];
                    relation = ResolveContactRelation(
                        entityManager,
                        flagship,
                        target,
                        selfFaction,
                        in perceivedEntry,
                        out relationScore,
                        out relationStance,
                        out relationSource);
                    goto RelationResolved;
                }
            }

            relation = ResolveFallbackRelation(
                entityManager,
                flagship,
                target,
                selfFaction,
                hasSelfSide,
                selfSide,
                out relationScore,
                out relationStance,
                out relationSource);

        RelationResolved:
            snapshot.target_selected = 1;
            snapshot.target_entity_ref = ResolveEntityRef(target);
            snapshot.target_callsign = ResolveEntityCallsign(entityManager, target);
            snapshot.target_relation = ResolveRelationToken(relation);
            snapshot.target_relation_score = relationScore;
            snapshot.target_relation_stance = relationStance;
            snapshot.target_relation_source = relationSource;
            snapshot.target_color_token = ResolveRelationColorToken(relation);
            snapshot.target_distance = distance;
            snapshot.target_bearing_deg = ResolveBearingDegrees(new float2(relative.x, relative.z));
            snapshot.target_speed = math.length(targetVelocity);
            snapshot.target_closing_speed = closingSpeed;

            if (entityManager.HasComponent<HullIntegrity>(target))
            {
                var hull = entityManager.GetComponentData<HullIntegrity>(target);
                snapshot.target_hull_current = math.max(0f, hull.Current);
                snapshot.target_hull_max = math.max(0f, hull.Max);
                snapshot.target_hull_ratio = ResolveRatio(snapshot.target_hull_current, snapshot.target_hull_max);
            }
        }

        private void PopulateInventoryMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            var shipPresetId = Space4XRunStartSelection.ShipPresetId ?? string.Empty;
            var shipLabel = Space4XRunStartSelection.ShipDisplayName ?? string.Empty;
            var shipArchetype = Space4XRunStartSelection.Archetype ?? string.Empty;

            var segmentIds = ResolveActiveHullSegments();
            if (segmentIds.Count == 0)
            {
                segmentIds.Add("hull.core");
            }

            if (string.IsNullOrWhiteSpace(shipLabel) && TryResolveActiveShipPreset(out var activePreset))
            {
                shipPresetId = string.IsNullOrWhiteSpace(shipPresetId) ? activePreset.PresetId : shipPresetId;
                shipLabel = activePreset.DisplayName;
                shipArchetype = string.IsNullOrWhiteSpace(shipArchetype) ? activePreset.Archetype : shipArchetype;
            }

            if (string.IsNullOrWhiteSpace(shipLabel))
            {
                shipLabel = "Flagship";
            }

            if (string.IsNullOrWhiteSpace(shipArchetype))
            {
                shipArchetype = "Unknown Archetype";
            }

            var segments = new List<Space4XInventorySegmentKernelSnapshot>(segmentIds.Count);
            var moduleBuckets = new List<Space4XInventoryModuleKernelSnapshot>[segmentIds.Count];
            for (var i = 0; i < segmentIds.Count; i++)
            {
                var id = string.IsNullOrWhiteSpace(segmentIds[i]) ? $"segment_{i + 1:00}" : segmentIds[i].Trim();
                var label = HumanizeInventoryToken(id);
                var segmentSnapshot = new Space4XInventorySegmentKernelSnapshot
                {
                    segment_id = id,
                    segment_label = $"{i + 1:00} {label}",
                    module_count = 0,
                    modules = Array.Empty<Space4XInventoryModuleKernelSnapshot>()
                };
                segments.Add(segmentSnapshot);
                moduleBuckets[i] = new List<Space4XInventoryModuleKernelSnapshot>(4);
            }

            var moduleCount = 0;
            var hasCatalog = TryResolveModuleCatalog(entityManager, out var moduleCatalog);
            ResolveEntitySignatureChannels(entityManager, flagship, out _, out _, out var flagshipThermalSignature, out _);
            if (entityManager.HasBuffer<CarrierModuleSlot>(flagship))
            {
                var slots = entityManager.GetBuffer<CarrierModuleSlot>(flagship);
                for (var slotOrder = 0; slotOrder < slots.Length; slotOrder++)
                {
                    var segmentIndex = ResolveInventorySegmentIndex(slotOrder, slots.Length, segmentIds.Count);
                    var slot = slots[slotOrder];
                    var module = BuildInventoryModuleSnapshot(
                        entityManager,
                        in slot,
                        segmentIndex,
                        hasCatalog,
                        in moduleCatalog,
                        flagshipThermalSignature);
                    moduleBuckets[segmentIndex].Add(module);
                    moduleCount++;
                }
            }

            for (var i = 0; i < segments.Count; i++)
            {
                var modules = moduleBuckets[i];
                var segment = segments[i];
                segment.module_count = modules.Count;
                segment.modules = modules.Count == 0
                    ? Array.Empty<Space4XInventoryModuleKernelSnapshot>()
                    : modules.ToArray();
                segments[i] = segment;
            }

            var cargoLines = new List<string>(10);
            if (entityManager.HasComponent<SupplyStatus>(flagship))
            {
                var supply = entityManager.GetComponentData<SupplyStatus>(flagship);
                cargoLines.Add($"Fuel {supply.Fuel:0}/{supply.FuelCapacity:0}  Ammo {supply.Ammunition:0}/{supply.AmmunitionCapacity:0}");
                cargoLines.Add($"Food {supply.Provisions:0}/{supply.ProvisionsCapacity:0}  Supplies {supply.RepairParts:0}/{supply.RepairPartsCapacity:0}");
            }

            if (entityManager.HasBuffer<ResourceStorage>(flagship))
            {
                var storage = entityManager.GetBuffer<ResourceStorage>(flagship);
                for (var i = 0; i < storage.Length && cargoLines.Count < 10; i++)
                {
                    var entry = storage[i];
                    if (entry.Capacity <= 0.01f && entry.Amount <= 0.01f)
                    {
                        continue;
                    }

                    cargoLines.Add($"{entry.Type} {entry.Amount:0.0}/{entry.Capacity:0.0}");
                }
            }

            snapshot.inventory_ship_preset_id = shipPresetId;
            snapshot.inventory_ship_label = shipLabel;
            snapshot.inventory_ship_archetype = shipArchetype;
            snapshot.inventory_segment_count = segments.Count;
            snapshot.inventory_module_count = moduleCount;
            snapshot.inventory_popup_count = _inventoryPopups.Count;
            snapshot.inventory_highlight_module_ref = ResolveInventoryHighlightModuleRef();
            snapshot.inventory_segments = segments.Count == 0
                ? Array.Empty<Space4XInventorySegmentKernelSnapshot>()
                : segments.ToArray();
            snapshot.inventory_cargo_lines = cargoLines.Count == 0
                ? Array.Empty<string>()
                : cargoLines.ToArray();
            snapshot.inventory_available_modules = hasCatalog
                ? BuildAvailableInventoryModules(in moduleCatalog)
                : Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>();
            snapshot.inventory_available_module_count = snapshot.inventory_available_modules.Length;

            var summaryBuilder = new StringBuilder(320);
            summaryBuilder.Append(shipLabel)
                .Append("  [")
                .Append(shipArchetype)
                .Append(']');
            summaryBuilder.Append('\n');
            summaryBuilder.Append("Segments ")
                .Append(snapshot.inventory_segment_count)
                .Append("  Slots ")
                .Append(snapshot.inventory_module_count)
                .Append("  Avail ")
                .Append(snapshot.inventory_available_module_count)
                .Append("  Popups ")
                .Append(snapshot.inventory_popup_count);

            var lineCount = 2;
            var cargoPreviewCount = math.min(snapshot.inventory_cargo_lines.Length, 4);
            for (var i = 0; i < cargoPreviewCount; i++)
            {
                summaryBuilder.Append('\n').Append(snapshot.inventory_cargo_lines[i]);
                lineCount++;
            }

            if (snapshot.inventory_module_count <= 0)
            {
                summaryBuilder.Append('\n').Append("No module slots bound.");
                lineCount++;
            }

            snapshot.inventory_line_count = lineCount;
            var summary = summaryBuilder.ToString().TrimEnd('\r', '\n');
            snapshot.inventory_summary = TrimForKernel(summary, KernelInventorySummaryMaxChars);
        }

        private void PopulateShipControlMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.ship_control_visible = BoolToInt(_shipControlVisible);
            snapshot.ship_control_statuses = BuildShipControlStatuses(entityManager, flagship, in snapshot);
            snapshot.ship_control_status_count = snapshot.ship_control_statuses.Length;
            snapshot.ship_control_modules = BuildShipControlModuleThermals(in snapshot);
            snapshot.ship_control_thermal_count = snapshot.ship_control_modules.Length;
            snapshot.ship_control_summary =
                $"effects {snapshot.ship_control_status_count:0}  hot {CountHotShipModules(snapshot.ship_control_modules):0}  pwr P{math.max(1, snapshot.power_routing_profile)}  jam {(snapshot.power_route_jam_risk_per_tick * 100f):0.00}%";
        }

        private static Space4XShipStatusEffectKernelSnapshot[] BuildShipControlStatuses(
            EntityManager entityManager,
            Entity flagship,
            in Space4XInRunHudKernelSnapshot snapshot)
        {
            var statuses = new List<Space4XShipStatusEffectKernelSnapshot>(16);
            if (entityManager.HasBuffer<ActiveStatusEffect>(flagship))
            {
                var effects = entityManager.GetBuffer<ActiveStatusEffect>(flagship);
                for (var i = 0; i < effects.Length; i++)
                {
                    var effect = effects[i];
                    var maxStacks = math.max(1, effect.MaxStacks);
                    var stackRatio = math.saturate(effect.Stacks / (float)maxStacks);
                    var valueMagnitude = math.saturate(math.abs(effect.Value));
                    var severity = math.saturate(math.max(valueMagnitude, stackRatio));
                    statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                    {
                        effect_type = effect.Type.ToString(),
                        category = effect.Category.ToString(),
                        behavior = effect.Behavior.ToString(),
                        duration_seconds = effect.Duration,
                        value = effect.Value,
                        stacks = math.max(1, effect.Stacks),
                        max_stacks = maxStacks,
                        severity_01 = severity,
                        is_buff = IsLikelyBuff(effect.Type) ? 1 : 0,
                        source_entity_ref = ResolveEntityRef(effect.SourceEntity)
                    });
                }
            }

            if (snapshot.power_deficit_mw > 0.05f)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "PowerDeficit",
                    category = "Power",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = snapshot.power_deficit_mw,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = math.saturate(snapshot.power_deficit_mw / math.max(1f, snapshot.power_capacity_mw)),
                    is_buff = 0,
                    source_entity_ref = "ship_core"
                });
            }

            if (snapshot.power_route_jam_risk_per_tick > 0.001f)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "JamRisk",
                    category = "PowerRouting",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = snapshot.power_route_jam_risk_per_tick,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = math.saturate(snapshot.power_route_jam_risk_per_tick * 5f),
                    is_buff = 0,
                    source_entity_ref = $"profile_p{math.max(1, snapshot.power_routing_profile)}"
                });
            }

            if (snapshot.fuel_ratio <= LowResourceThreshold)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "LowFuel",
                    category = "Logistics",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = snapshot.fuel_ratio,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = 1f - math.saturate(snapshot.fuel_ratio / math.max(0.01f, LowResourceThreshold)),
                    is_buff = 0,
                    source_entity_ref = "supply_status"
                });
            }

            if (snapshot.food_ratio <= LowResourceThreshold)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "LowFood",
                    category = "Logistics",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = snapshot.food_ratio,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = 1f - math.saturate(snapshot.food_ratio / math.max(0.01f, LowResourceThreshold)),
                    is_buff = 0,
                    source_entity_ref = "supply_status"
                });
            }

            if (snapshot.supplies_ratio <= LowResourceThreshold)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "LowSupplies",
                    category = "Logistics",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = snapshot.supplies_ratio,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = 1f - math.saturate(snapshot.supplies_ratio / math.max(0.01f, LowResourceThreshold)),
                    is_buff = 0,
                    source_entity_ref = "supply_status"
                });
            }

            if (entityManager.HasComponent<Space4XHeatKernelOutput>(flagship))
            {
                var heat = entityManager.GetComponentData<Space4XHeatKernelOutput>(flagship);
                var thermal = math.saturate(heat.Thermal01);
                if (thermal >= 0.55f)
                {
                    statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                    {
                        effect_type = heat.IsSaturated != 0 ? "ThermalSaturation" : "ThermalLoad",
                        category = "Thermal",
                        behavior = "Derived",
                        duration_seconds = -1f,
                        value = thermal,
                        stacks = 1,
                        max_stacks = 1,
                        severity_01 = thermal,
                        is_buff = 0,
                        source_entity_ref = "heat_kernel"
                    });
                }
            }

            if (entityManager.HasComponent<FleetcrawlHeatOutputState>(flagship))
            {
                var heat = entityManager.GetComponentData<FleetcrawlHeatOutputState>(flagship);
                var thermal = math.saturate(FleetcrawlHeatResolver.ResolveHeatSignature01(in heat));
                if (thermal >= 0.55f || heat.IsOverheated != 0)
                {
                    statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                    {
                        effect_type = heat.IsOverheated != 0 ? "Overheated" : "HeatSignatureHigh",
                        category = "Thermal",
                        behavior = "Derived",
                        duration_seconds = -1f,
                        value = thermal,
                        stacks = 1,
                        max_stacks = 1,
                        severity_01 = math.max(thermal, heat.IsOverheated != 0 ? 1f : 0f),
                        is_buff = 0,
                        source_entity_ref = "fleetcrawl_heat"
                    });
                }
            }

            if (statuses.Count <= 0)
            {
                statuses.Add(new Space4XShipStatusEffectKernelSnapshot
                {
                    effect_type = "Nominal",
                    category = "System",
                    behavior = "Derived",
                    duration_seconds = -1f,
                    value = 1f,
                    stacks = 1,
                    max_stacks = 1,
                    severity_01 = 0.05f,
                    is_buff = 1,
                    source_entity_ref = "ship_core"
                });
            }

            statuses.Sort(CompareShipStatusEffects);
            return statuses.ToArray();
        }

        private static Space4XShipModuleThermalKernelSnapshot[] BuildShipControlModuleThermals(in Space4XInRunHudKernelSnapshot snapshot)
        {
            var modules = new List<Space4XShipModuleThermalKernelSnapshot>(24);
            var segments = snapshot.inventory_segments ?? Array.Empty<Space4XInventorySegmentKernelSnapshot>();
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                if (segment == null)
                {
                    continue;
                }

                var segmentModules = segment.modules ?? Array.Empty<Space4XInventoryModuleKernelSnapshot>();
                for (var moduleIndex = 0; moduleIndex < segmentModules.Length; moduleIndex++)
                {
                    var module = segmentModules[moduleIndex];
                    if (module == null)
                    {
                        continue;
                    }

                    var moduleId = module.module_id ?? string.Empty;
                    if (string.Equals(moduleId, "(empty)", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    modules.Add(new Space4XShipModuleThermalKernelSnapshot
                    {
                        module_ref = module.module_ref ?? string.Empty,
                        module_label = ResolveInventoryModuleHeadline(module),
                        thermal_current = module.thermal_current,
                        thermal_capacity = module.thermal_capacity,
                        thermal_ratio = math.saturate(module.thermal_ratio),
                        thermal_source = module.thermal_source ?? string.Empty,
                        thermal_saturated = module.thermal_saturated
                    });
                }
            }

            if (modules.Count <= 0)
            {
                return Array.Empty<Space4XShipModuleThermalKernelSnapshot>();
            }

            modules.Sort(CompareShipModuleThermals);
            if (modules.Count > 14)
            {
                modules.RemoveRange(14, modules.Count - 14);
            }

            return modules.ToArray();
        }

        private static int CountHotShipModules(Space4XShipModuleThermalKernelSnapshot[] modules)
        {
            if (modules == null || modules.Length <= 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (module != null && (module.thermal_saturated == 1 || module.thermal_ratio >= 0.75f))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareShipStatusEffects(Space4XShipStatusEffectKernelSnapshot lhs, Space4XShipStatusEffectKernelSnapshot rhs)
        {
            var severity = rhs.severity_01.CompareTo(lhs.severity_01);
            if (severity != 0)
            {
                return severity;
            }

            return string.Compare(lhs.effect_type, rhs.effect_type, StringComparison.Ordinal);
        }

        private static int CompareShipModuleThermals(Space4XShipModuleThermalKernelSnapshot lhs, Space4XShipModuleThermalKernelSnapshot rhs)
        {
            var saturated = rhs.thermal_saturated.CompareTo(lhs.thermal_saturated);
            if (saturated != 0)
            {
                return saturated;
            }

            var thermal = rhs.thermal_ratio.CompareTo(lhs.thermal_ratio);
            if (thermal != 0)
            {
                return thermal;
            }

            return string.Compare(lhs.module_label, rhs.module_label, StringComparison.Ordinal);
        }

        private static bool IsLikelyBuff(StatusEffectType type)
        {
            var raw = (int)type;
            if (raw >= 20 && raw <= 29)
            {
                return true;
            }

            return type == StatusEffectType.Invulnerable ||
                   type == StatusEffectType.Blessed ||
                   type == StatusEffectType.Inspired ||
                   type == StatusEffectType.Empowered ||
                   type == StatusEffectType.Focused;
        }

        private void PopulateProductionMetrics(EntityManager entityManager, Entity flagship, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.production_visible = BoolToInt(_productionVisible);
            snapshot.production_summary = "no_production_facilities";
            snapshot.production_facility_count = 0;
            snapshot.production_selected_index = -1;
            snapshot.production_selected_entity_ref = string.Empty;
            snapshot.production_selected_recipe_id = string.Empty;
            snapshot.production_selected_limb_id = string.Empty;
            snapshot.production_selected_shift = 1;
            snapshot.production_selected_power_scale = 1f;
            snapshot.production_selected_queue_count = 0;
            snapshot.production_facilities = Array.Empty<Space4XProductionFacilityKernelSnapshot>();

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XProductionRuntime>(),
                ComponentType.ReadOnly<Space4XProductionPowerCrewConstraint>(),
                ComponentType.ReadOnly<Space4XProductionStatus>(),
                ComponentType.ReadOnly<BusinessProduction>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var runtimes = query.ToComponentDataArray<Space4XProductionRuntime>(Allocator.Temp);
            using var constraints = query.ToComponentDataArray<Space4XProductionPowerCrewConstraint>(Allocator.Temp);
            using var statuses = query.ToComponentDataArray<Space4XProductionStatus>(Allocator.Temp);
            using var productions = query.ToComponentDataArray<BusinessProduction>(Allocator.Temp);
            var count = math.min(
                entities.Length,
                math.min(
                    runtimes.Length,
                    math.min(constraints.Length, math.min(statuses.Length, productions.Length))));
            if (count <= 0)
            {
                return;
            }

            var selfFaction = ResolveFactionEntity(entityManager, flagship);
            var sameFactionScratch = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                var facility = entities[i];
                if (facility == Entity.Null || !entityManager.Exists(facility))
                {
                    continue;
                }

                if (selfFaction != Entity.Null)
                {
                    var facilityFaction = ResolveFactionEntity(entityManager, facility);
                    if (facilityFaction != Entity.Null && facilityFaction != selfFaction)
                    {
                        continue;
                    }
                }

                sameFactionScratch.Add(i);
            }

            if (sameFactionScratch.Count <= 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var facility = entities[i];
                    if (facility != Entity.Null && entityManager.Exists(facility))
                    {
                        sameFactionScratch.Add(i);
                    }
                }
            }

            if (sameFactionScratch.Count <= 0)
            {
                return;
            }

            var hasRecipeCatalog = TryResolveProductionRecipeCatalog(entityManager, out var recipeCatalog);
            _productionFacilitiesScratch.Clear();
            for (var i = 0; i < sameFactionScratch.Count; i++)
            {
                var sourceIndex = sameFactionScratch[i];
                var facility = entities[sourceIndex];
                var runtime = runtimes[sourceIndex];
                var constraint = constraints[sourceIndex];
                var status = statuses[sourceIndex];
                var production = productions[sourceIndex];
                var facilityRef = ResolveEntityRef(facility);

                if (!_productionBaseRequiredCrewByFacility.TryGetValue(facilityRef, out var baseRequiredCrew) || baseRequiredCrew <= 0f)
                {
                    baseRequiredCrew = math.max(1f, constraint.RequiredCrew);
                    _productionBaseRequiredCrewByFacility[facilityRef] = baseRequiredCrew;
                }

                if (!_productionBaseRequiredPowerByFacility.TryGetValue(facilityRef, out var baseRequiredPower) || baseRequiredPower <= 0f)
                {
                    baseRequiredPower = math.max(0.1f, constraint.RequiredPowerMw);
                    _productionBaseRequiredPowerByFacility[facilityRef] = baseRequiredPower;
                }

                var shiftIndex = ResolveProductionShiftForFacility(facilityRef);
                var powerScale = ResolveProductionPowerScaleForFacility(facilityRef);
                var desiredRequiredCrew = math.max(1f, baseRequiredCrew * ResolveShiftCrewScalar(shiftIndex));
                var desiredRequiredPower = math.max(0.1f, baseRequiredPower);
                var desiredAssignedPower = math.max(0.1f, desiredRequiredPower * powerScale);

                var constraintDirty = false;
                if (math.abs(constraint.RequiredCrew - desiredRequiredCrew) > 0.001f)
                {
                    constraint.RequiredCrew = desiredRequiredCrew;
                    constraintDirty = true;
                }

                if (math.abs(constraint.RequiredPowerMw - desiredRequiredPower) > 0.001f)
                {
                    constraint.RequiredPowerMw = desiredRequiredPower;
                    constraintDirty = true;
                }

                if (math.abs(constraint.AssignedPowerMw - desiredAssignedPower) > 0.001f)
                {
                    constraint.AssignedPowerMw = desiredAssignedPower;
                    constraintDirty = true;
                }

                if (constraintDirty && entityManager.HasComponent<Space4XProductionPowerCrewConstraint>(facility))
                {
                    entityManager.SetComponentData(facility, constraint);
                }

                var availableRecipes = BuildProductionRecipeOptions(hasRecipeCatalog, in recipeCatalog, in production, runtime.FacilityKind);
                var availableLimbs = BuildProductionLimbOptions(runtime.FacilityKind);
                var selectedRecipeIndex = ResolveProductionSelectionIndex(_productionRecipeIndexByFacility, facilityRef, availableRecipes.Length);
                var selectedLimbIndex = ResolveProductionSelectionIndex(_productionLimbIndexByFacility, facilityRef, availableLimbs.Length);
                var selectedRecipeId = availableRecipes.Length > 0 ? availableRecipes[selectedRecipeIndex] : string.Empty;
                var selectedLimbId = availableLimbs.Length > 0 ? availableLimbs[selectedLimbIndex] : string.Empty;

                var queueSnapshots = Array.Empty<Space4XProductionQueueKernelSnapshot>();
                var queueCount = 0;
                if (entityManager.HasBuffer<Space4XProductionQueueEntry>(facility))
                {
                    var queue = entityManager.GetBuffer<Space4XProductionQueueEntry>(facility);
                    queueCount = queue.Length;
                    var queueWindowCount = math.min(queueCount, KernelProductionQueueWindowSize);
                    if (queueWindowCount > 0)
                    {
                        queueSnapshots = new Space4XProductionQueueKernelSnapshot[queueWindowCount];
                        for (var queueIndex = 0; queueIndex < queueWindowCount; queueIndex++)
                        {
                            var entry = queue[queueIndex];
                            queueSnapshots[queueIndex] = new Space4XProductionQueueKernelSnapshot
                            {
                                entry_id = entry.EntryId.ToString(),
                                recipe_id = entry.RecipeId.ToString(),
                                state = entry.State.ToString(),
                                source = entry.Source.ToString(),
                                priority = entry.Priority,
                                batch_count = entry.BatchCount,
                                eta_seconds = math.max(0f, entry.EtaSeconds),
                                required_power_mw = math.max(0f, entry.RequiredPowerMw),
                                required_crew = math.max(0f, entry.RequiredCrew)
                            };
                        }
                    }
                }

                _productionFacilitiesScratch.Add(new Space4XProductionFacilityKernelSnapshot
                {
                    entity_ref = facilityRef,
                    facility_kind = runtime.FacilityKind.ToString(),
                    business_type = production.Type.ToString(),
                    capacity = math.max(0f, production.Capacity),
                    throughput = math.max(0f, production.Throughput),
                    queue_capacity = math.max(1, runtime.QueueCapacity),
                    queue_count = queueCount,
                    active_queue_index = status.ActiveQueueIndex,
                    active_eta_seconds = math.max(0f, status.ActiveEtaSeconds),
                    blocked = status.IsBlocked != 0 ? 1 : 0,
                    required_power_mw = math.max(0f, constraint.RequiredPowerMw),
                    assigned_power_mw = math.max(0f, constraint.AssignedPowerMw),
                    required_crew = math.max(0f, constraint.RequiredCrew),
                    assigned_crew = math.max(0f, constraint.AssignedCrew),
                    seat_fill_01 = math.saturate(status.SeatFill01),
                    skill_factor_01 = math.saturate(status.SkillFactor01),
                    effective_throughput = math.max(0f, status.EffectiveThroughput),
                    shift_index = shiftIndex,
                    power_scale = powerScale,
                    selected_recipe_id = selectedRecipeId,
                    selected_limb_id = selectedLimbId,
                    available_recipes = availableRecipes,
                    available_limbs = availableLimbs,
                    queue = queueSnapshots
                });
            }

            if (_productionFacilitiesScratch.Count <= 0)
            {
                return;
            }

            _productionFacilitiesScratch.Sort((a, b) => string.Compare(a.entity_ref, b.entity_ref, StringComparison.Ordinal));
            snapshot.production_facility_count = _productionFacilitiesScratch.Count;

            var selectedIndex = -1;
            if (!string.IsNullOrWhiteSpace(_productionSelectedFacilityRef))
            {
                for (var i = 0; i < _productionFacilitiesScratch.Count; i++)
                {
                    if (string.Equals(_productionFacilitiesScratch[i].entity_ref, _productionSelectedFacilityRef, StringComparison.Ordinal))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selectedIndex < 0)
            {
                selectedIndex = math.clamp(_productionSelectedFacilityIndex, 0, _productionFacilitiesScratch.Count - 1);
            }

            var selected = _productionFacilitiesScratch[selectedIndex];
            _productionSelectedFacilityIndex = selectedIndex;
            _productionSelectedFacilityRef = selected.entity_ref ?? string.Empty;

            snapshot.production_selected_index = selectedIndex;
            snapshot.production_selected_entity_ref = selected.entity_ref ?? string.Empty;
            snapshot.production_selected_recipe_id = selected.selected_recipe_id ?? string.Empty;
            snapshot.production_selected_limb_id = selected.selected_limb_id ?? string.Empty;
            snapshot.production_selected_shift = selected.shift_index;
            snapshot.production_selected_power_scale = selected.power_scale;
            snapshot.production_selected_queue_count = selected.queue_count;
            snapshot.production_facilities = _productionFacilitiesScratch.ToArray();

            var summaryBuilder = new StringBuilder(256);
            summaryBuilder.Append("facilities ")
                .Append(snapshot.production_facility_count)
                .Append("  selected ")
                .Append(selected.entity_ref)
                .Append('\n')
                .Append(selected.facility_kind)
                .Append("  cap ")
                .Append(selected.capacity.ToString("0"))
                .Append("  thr ")
                .Append(selected.throughput.ToString("0.00"))
                .Append("  q ")
                .Append(selected.queue_count)
                .Append('/')
                .Append(selected.queue_capacity)
                .Append('\n')
                .Append("recipe ")
                .Append(string.IsNullOrWhiteSpace(selected.selected_recipe_id) ? "none" : selected.selected_recipe_id)
                .Append("  shift S")
                .Append(selected.shift_index)
                .Append("  pwr x")
                .Append(selected.power_scale.ToString("0.00"))
                .Append("  limb ")
                .Append(string.IsNullOrWhiteSpace(selected.selected_limb_id) ? "none" : selected.selected_limb_id);

            snapshot.production_summary = TrimForKernel(summaryBuilder.ToString(), KernelProductionSummaryMaxChars);
        }

        private bool TryResolveProductionRecipeCatalog(EntityManager entityManager, out ProductionRecipeCatalog catalog)
        {
            catalog = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductionRecipeCatalog>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            catalog = query.GetSingleton<ProductionRecipeCatalog>();
            return catalog.Catalog.IsCreated;
        }

        private static string[] BuildProductionRecipeOptions(
            bool hasRecipeCatalog,
            in ProductionRecipeCatalog recipeCatalog,
            in BusinessProduction production,
            Space4XProductionFacilityKind facilityKind)
        {
            if (!hasRecipeCatalog || !recipeCatalog.Catalog.IsCreated)
            {
                return Array.Empty<string>();
            }

            ref var catalog = ref recipeCatalog.Catalog.Value;
            if (catalog.Recipes.Length <= 0)
            {
                return Array.Empty<string>();
            }

            var directMatches = new List<string>(24);
            var fallbackMatches = new List<string>(24);
            var preferredStage = ResolvePreferredProductionStage(facilityKind);
            for (var i = 0; i < catalog.Recipes.Length; i++)
            {
                ref var recipe = ref catalog.Recipes[i];
                var recipeId = recipe.RecipeId.ToString();
                if (string.IsNullOrWhiteSpace(recipeId))
                {
                    continue;
                }

                if (recipe.RequiredBusinessType == production.Type)
                {
                    directMatches.Add(recipeId);
                    continue;
                }

                if (recipe.Stage == preferredStage)
                {
                    fallbackMatches.Add(recipeId);
                }
            }

            var result = directMatches.Count > 0 ? directMatches : fallbackMatches;
            result.Sort(StringComparer.Ordinal);
            return result.Count > 0 ? result.ToArray() : Array.Empty<string>();
        }

        private string[] BuildProductionLimbOptions(Space4XProductionFacilityKind facilityKind)
        {
            if (_facilityCatalog == null)
            {
                _facilityCatalog = Space4XFacilityCatalog.LoadOrFallback();
            }

            if (_facilityCatalog == null)
            {
                return Array.Empty<string>();
            }

            var limbs = _facilityCatalog.Limbs ?? Array.Empty<FacilityLimbDefinition>();
            if (limbs.Length <= 0)
            {
                return Array.Empty<string>();
            }

            var output = new List<string>(16);
            for (var i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (string.IsNullOrWhiteSpace(limb.Id))
                {
                    continue;
                }

                if (string.Equals(limb.LimbType, FacilityLimbTypeIds.Production, StringComparison.Ordinal) ||
                    string.Equals(limb.LimbType, FacilityLimbTypeIds.Power, StringComparison.Ordinal) ||
                    string.Equals(limb.LimbType, FacilityLimbTypeIds.Automation, StringComparison.Ordinal))
                {
                    output.Add(limb.Id.Trim());
                }
            }

            if (output.Count <= 0)
            {
                return Array.Empty<string>();
            }

            output.Sort(StringComparer.Ordinal);
            return output.ToArray();
        }

        private static ProductionStage ResolvePreferredProductionStage(Space4XProductionFacilityKind facilityKind)
        {
            return facilityKind switch
            {
                Space4XProductionFacilityKind.Refinery => ProductionStage.Refining,
                Space4XProductionFacilityKind.ModuleWorks => ProductionStage.Crafting,
                Space4XProductionFacilityKind.Shipyard => ProductionStage.Crafting,
                Space4XProductionFacilityKind.HangarWorks => ProductionStage.Crafting,
                _ => ProductionStage.Crafting
            };
        }

        private static int ResolveProductionSelectionIndex(Dictionary<string, int> indexByFacility, string facilityRef, int optionCount)
        {
            if (optionCount <= 0)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return 0;
            }

            if (!indexByFacility.TryGetValue(facilityRef, out var rawIndex))
            {
                indexByFacility[facilityRef] = 0;
                return 0;
            }

            var clamped = math.clamp(rawIndex, 0, optionCount - 1);
            if (clamped != rawIndex)
            {
                indexByFacility[facilityRef] = clamped;
            }

            return clamped;
        }

        private int ResolveProductionShiftForFacility(string facilityRef)
        {
            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return 1;
            }

            if (!_productionShiftByFacility.TryGetValue(facilityRef, out var shift))
            {
                shift = 1;
                _productionShiftByFacility[facilityRef] = shift;
            }

            shift = math.clamp(shift, 1, 3);
            _productionShiftByFacility[facilityRef] = shift;
            return shift;
        }

        private float ResolveProductionPowerScaleForFacility(string facilityRef)
        {
            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return 1f;
            }

            if (!_productionPowerScaleByFacility.TryGetValue(facilityRef, out var scale))
            {
                scale = 1f;
                _productionPowerScaleByFacility[facilityRef] = scale;
            }

            scale = math.clamp(scale, ProductionPowerScaleMin, ProductionPowerScaleMax);
            _productionPowerScaleByFacility[facilityRef] = scale;
            return scale;
        }

        private static float ResolveShiftCrewScalar(int shiftIndex)
        {
            return shiftIndex switch
            {
                2 => 1.2f,
                3 => 0.85f,
                _ => 1f
            };
        }

        private bool TryResolveModuleCatalog(EntityManager entityManager, out ModuleCatalogSingleton catalog)
        {
            catalog = default;
            if (!_moduleCatalogQueryValid)
            {
                _moduleCatalogQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>());
                _moduleCatalogQueryValid = true;
            }

            if (_moduleCatalogQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            catalog = _moduleCatalogQuery.GetSingleton<ModuleCatalogSingleton>();
            return catalog.Catalog.IsCreated;
        }

        private List<string> ResolveActiveHullSegments()
        {
            var result = new List<string>(6);
            var active = Space4XRunStartSelection.HullSegments;
            if (active != null)
            {
                for (var i = 0; i < active.Length; i++)
                {
                    var segment = active[i];
                    if (string.IsNullOrWhiteSpace(segment))
                    {
                        continue;
                    }

                    result.Add(segment.Trim());
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            if (TryResolveActiveShipPreset(out var preset))
            {
                var presetSegments = preset.HullSegments;
                for (var i = 0; i < presetSegments.Length; i++)
                {
                    var segment = presetSegments[i];
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        result.Add(segment.Trim());
                    }
                }
            }

            return result;
        }

        private bool TryResolveActiveShipPreset(out Space4XShipPresetEntry preset)
        {
            preset = default;
            if (_shipPresetCatalog == null)
            {
                _shipPresetCatalog = Resources.Load<Space4XShipPresetCatalog>("UI/Space4XShipPresetCatalog");
                if (_shipPresetCatalog == null)
                {
                    _shipPresetCatalog = Space4XShipPresetCatalog.CreateRuntimeFallback();
                }
            }

            if (_shipPresetCatalog == null || !_shipPresetCatalog.HasPresets)
            {
                return false;
            }

            var targetPresetId = Space4XRunStartSelection.ShipPresetId ?? string.Empty;
            var presets = _shipPresetCatalog.Presets;
            for (var i = 0; i < presets.Count; i++)
            {
                var candidate = presets[i];
                if (string.Equals(candidate.PresetId, targetPresetId, StringComparison.Ordinal))
                {
                    preset = candidate;
                    return true;
                }
            }

            preset = _shipPresetCatalog.GetPresetOrFallback(0);
            return preset.IsValid;
        }

        private Space4XInventoryModuleKernelSnapshot BuildInventoryModuleSnapshot(
            EntityManager entityManager,
            in CarrierModuleSlot slot,
            int segmentIndex,
            bool hasCatalog,
            in ModuleCatalogSingleton moduleCatalog,
            float fallbackThermalRatio)
        {
            var moduleRef = $"slot:{slot.SlotIndex}";
            var module = new Space4XInventoryModuleKernelSnapshot
            {
                module_ref = moduleRef,
                segment_index = segmentIndex,
                slot_index = slot.SlotIndex,
                slot_size = slot.SlotSize.ToString(),
                slot_state = slot.State.ToString(),
                module_id = "(empty)",
                module_class = "None",
                mount = string.Empty,
                size = string.Empty,
                manufacturer_id = string.Empty,
                health_current = 0f,
                health_max = 0f,
                health_ratio = 0f,
                quality = 0f,
                tier = 0,
                power_draw_mw = 0f,
                mass_tons = 0f,
                thermal_current = math.max(0f, fallbackThermalRatio) * 100f,
                thermal_capacity = 100f,
                thermal_ratio = math.saturate(fallbackThermalRatio),
                thermal_source = fallbackThermalRatio > 0.001f ? "ship_estimate" : "none",
                thermal_saturated = fallbackThermalRatio >= 0.95f ? 1 : 0,
                limb_profile_summary = "no_organs",
                organs_count = 0,
                organs = Array.Empty<Space4XInventoryLimbKernelSnapshot>()
            };

            var moduleEntity = slot.CurrentModule;
            if (moduleEntity == Entity.Null || !entityManager.Exists(moduleEntity))
            {
                return module;
            }

            moduleRef = ResolveEntityRef(moduleEntity);
            module.module_ref = moduleRef;

            if (entityManager.HasComponent<ModuleTypeId>(moduleEntity))
            {
                var moduleType = entityManager.GetComponentData<ModuleTypeId>(moduleEntity).Value;
                module.module_id = moduleType.ToString();
                if (hasCatalog && ModuleCatalogUtility.TryGetModuleSpec(in moduleCatalog, moduleType, out var spec))
                {
                    module.module_class = spec.Class.ToString();
                    module.mount = spec.RequiredMount.ToString();
                    module.size = spec.RequiredSize.ToString();
                    module.power_draw_mw = spec.PowerDrawMW;
                    module.mass_tons = spec.MassTons;
                    module.quality = math.saturate(spec.Quality);
                    module.tier = spec.Tier;
                    module.manufacturer_id = spec.ManufacturerId.ToString();
                }
            }

            if (entityManager.HasComponent<ModuleQuality>(moduleEntity))
            {
                module.quality = math.saturate(entityManager.GetComponentData<ModuleQuality>(moduleEntity).Value);
            }

            if (entityManager.HasComponent<ModuleTier>(moduleEntity))
            {
                module.tier = entityManager.GetComponentData<ModuleTier>(moduleEntity).Value;
            }

            if (entityManager.HasComponent<ModuleManufacturer>(moduleEntity))
            {
                module.manufacturer_id = entityManager.GetComponentData<ModuleManufacturer>(moduleEntity).ManufacturerId.ToString();
            }

            if (entityManager.HasComponent<ModuleHealth>(moduleEntity))
            {
                var health = entityManager.GetComponentData<ModuleHealth>(moduleEntity);
                module.health_current = math.max(0f, health.CurrentHealth);
                module.health_max = math.max(0f, health.MaxHealth);
                module.health_ratio = ResolveRatio(module.health_current, module.health_max);
            }
            else
            {
                module.health_current = 1f;
                module.health_max = 1f;
                module.health_ratio = 1f;
            }

            if (entityManager.HasComponent<ModuleLimbProfile>(moduleEntity))
            {
                var profile = entityManager.GetComponentData<ModuleLimbProfile>(moduleEntity);
                module.limb_profile_summary = BuildLimbProfileSummary(in profile);
                if (!entityManager.HasBuffer<ModuleLimbState>(moduleEntity))
                {
                    module.organs = BuildSyntheticOrgansFromProfile(in profile);
                    module.organs_count = module.organs.Length;
                }
            }

            if (entityManager.HasBuffer<ModuleLimbState>(moduleEntity))
            {
                var limbs = entityManager.GetBuffer<ModuleLimbState>(moduleEntity);
                if (limbs.Length > 0)
                {
                    var organStates = new Space4XInventoryLimbKernelSnapshot[limbs.Length];
                    for (var i = 0; i < limbs.Length; i++)
                    {
                        var limb = limbs[i];
                        organStates[i] = new Space4XInventoryLimbKernelSnapshot
                        {
                            limb_id = limb.LimbId.ToString(),
                            family = limb.Family.ToString(),
                            integrity = math.saturate(limb.Integrity),
                            exposure = math.saturate(limb.Exposure)
                        };
                    }

                    module.organs = organStates;
                    module.organs_count = organStates.Length;
                }
            }

            if (entityManager.HasComponent<Space4XHeatKernelOutput>(moduleEntity))
            {
                var heat = entityManager.GetComponentData<Space4XHeatKernelOutput>(moduleEntity);
                module.thermal_current = math.max(0f, heat.CurrentHeat);
                module.thermal_capacity = math.max(1f, heat.HeatCapacity);
                module.thermal_ratio = math.saturate(heat.Thermal01);
                module.thermal_source = heat.SourceKind.ToString();
                module.thermal_saturated = heat.IsSaturated != 0 ? 1 : 0;
            }
            else if (entityManager.HasComponent<FleetcrawlHeatOutputState>(moduleEntity))
            {
                var heat = entityManager.GetComponentData<FleetcrawlHeatOutputState>(moduleEntity);
                module.thermal_capacity = math.max(1f, heat.HeatCapacity);
                module.thermal_current = math.max(0f, heat.Heat01) * module.thermal_capacity;
                module.thermal_ratio = math.saturate(FleetcrawlHeatResolver.ResolveHeatSignature01(in heat));
                module.thermal_source = "fleetcrawl";
                module.thermal_saturated = heat.IsOverheated != 0 ? 1 : 0;
            }

            if (module.organs_count == 0 && string.IsNullOrWhiteSpace(module.limb_profile_summary))
            {
                module.limb_profile_summary = "no_organs";
            }

            return module;
        }

        private static Space4XInventoryLimbKernelSnapshot[] BuildSyntheticOrgansFromProfile(in ModuleLimbProfile profile)
        {
            var organs = new List<Space4XInventoryLimbKernelSnapshot>(8);
            AppendSyntheticOrgan(organs, "Cooling", "Cooling", profile.Cooling);
            AppendSyntheticOrgan(organs, "Sensors", "Sensors", profile.Sensors);
            AppendSyntheticOrgan(organs, "Lensing", "Lensing", profile.Lensing);
            AppendSyntheticOrgan(organs, "Projector", "Projector", profile.Projector);
            AppendSyntheticOrgan(organs, "Guidance", "Guidance", profile.Guidance);
            AppendSyntheticOrgan(organs, "Actuator", "Actuator", profile.Actuator);
            AppendSyntheticOrgan(organs, "Structural", "Structural", profile.Structural);
            AppendSyntheticOrgan(organs, "Power", "Power", profile.Power);
            return organs.Count == 0
                ? Array.Empty<Space4XInventoryLimbKernelSnapshot>()
                : organs.ToArray();
        }

        private static void AppendSyntheticOrgan(List<Space4XInventoryLimbKernelSnapshot> destination, string limbId, string family, float integrity)
        {
            var normalized = math.saturate(integrity);
            if (normalized <= 0.001f)
            {
                return;
            }

            destination.Add(new Space4XInventoryLimbKernelSnapshot
            {
                limb_id = limbId,
                family = family,
                integrity = normalized,
                exposure = 0f
            });
        }

        private static string BuildLimbProfileSummary(in ModuleLimbProfile profile)
        {
            return $"C:{profile.Cooling * 100f:0} S:{profile.Sensors * 100f:0} L:{profile.Lensing * 100f:0} P:{profile.Power * 100f:0}";
        }

        private static int ResolveInventorySegmentIndex(int slotOrder, int slotCount, int segmentCount)
        {
            if (segmentCount <= 1 || slotCount <= 1)
            {
                return 0;
            }

            var slotsPerSegment = math.max(1, (int)math.ceil(slotCount / (float)segmentCount));
            return math.clamp(slotOrder / slotsPerSegment, 0, segmentCount - 1);
        }

        private static Space4XInventoryCatalogModuleKernelSnapshot[] BuildAvailableInventoryModules(in ModuleCatalogSingleton moduleCatalog)
        {
            if (!moduleCatalog.Catalog.IsCreated)
            {
                return Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>();
            }

            ref var modules = ref moduleCatalog.Catalog.Value.Modules;
            if (modules.Length == 0)
            {
                return Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>();
            }

            var entries = new List<Space4XInventoryCatalogModuleKernelSnapshot>(modules.Length);
            for (var i = 0; i < modules.Length; i++)
            {
                ref var spec = ref modules[i];
                entries.Add(new Space4XInventoryCatalogModuleKernelSnapshot
                {
                    module_id = spec.Id.ToString(),
                    module_class = spec.Class.ToString(),
                    mount = spec.RequiredMount.ToString(),
                    size = spec.RequiredSize.ToString(),
                    manufacturer_id = spec.ManufacturerId.ToString(),
                    quality = math.saturate(spec.Quality),
                    tier = spec.Tier,
                    power_draw_mw = spec.PowerDrawMW,
                    mass_tons = spec.MassTons,
                    function = spec.Function.ToString(),
                    function_description = spec.FunctionDescription.ToString()
                });
            }

            entries.Sort((a, b) =>
            {
                var classCompare = string.Compare(a.module_class, b.module_class, StringComparison.Ordinal);
                if (classCompare != 0)
                {
                    return classCompare;
                }

                var sizeCompare = string.Compare(a.size, b.size, StringComparison.Ordinal);
                if (sizeCompare != 0)
                {
                    return sizeCompare;
                }

                return string.Compare(a.module_id, b.module_id, StringComparison.Ordinal);
            });

            return entries.ToArray();
        }

        private static string HumanizeInventoryToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "Unknown";
            }

            var normalized = token.Trim()
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Replace('.', ' ');
            var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "Unknown";
            }

            var builder = new StringBuilder(normalized.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                var part = parts[i];
                if (part.Length == 1)
                {
                    builder.Append(char.ToUpperInvariant(part[0]));
                }
                else
                {
                    builder.Append(char.ToUpperInvariant(part[0]));
                    for (var j = 1; j < part.Length; j++)
                    {
                        builder.Append(char.ToLowerInvariant(part[j]));
                    }
                }
            }

            return builder.ToString();
        }

        private string ResolveInventoryHighlightModuleRef()
        {
            if (!string.IsNullOrWhiteSpace(_inventoryHoveredModuleRef))
            {
                return _inventoryHoveredModuleRef;
            }

            return _inventorySelectedModuleRef ?? string.Empty;
        }

        private static int ResolveTrackedContactCount(EntityManager entityManager, Entity flagship)
        {
            if (entityManager.HasBuffer<PerceivedEntity>(flagship))
            {
                return entityManager.GetBuffer<PerceivedEntity>(flagship).Length;
            }

            if (entityManager.HasComponent<ShipSystemsSnapshot>(flagship))
            {
                return entityManager.GetComponentData<ShipSystemsSnapshot>(flagship).ContactsTracked;
            }

            if (entityManager.HasComponent<SeatInstrumentFeed>(flagship))
            {
                return entityManager.GetComponentData<SeatInstrumentFeed>(flagship).ContactsTracked;
            }

            if (entityManager.HasComponent<CaptainAggregateBrief>(flagship))
            {
                return entityManager.GetComponentData<CaptainAggregateBrief>(flagship).ContactsTracked;
            }

            return 0;
        }

        private static KernelMinimapRelation ResolveContactRelation(
            EntityManager entityManager,
            Entity flagship,
            Entity target,
            Entity selfFaction,
            in PerceivedEntity perceived,
            out int relationScore,
            out string relationStance,
            out string relationSource)
        {
            relationScore = math.clamp((int)perceived.Relationship, -100, 100);
            relationStance = string.Empty;
            relationSource = "perception_kind";

            if (target == flagship)
            {
                relationScore = 100;
                relationStance = DiplomaticStance.Allied.ToString();
                relationSource = "self";
                return KernelMinimapRelation.Self;
            }

            var perceptionRelation = ResolveRelationFromPerceivedKind(perceived.RelationKind);
            if (perceptionRelation != KernelMinimapRelation.Unknown)
            {
                return perceptionRelation;
            }

            if (relationScore >= 25)
            {
                relationSource = "perception_score";
                return KernelMinimapRelation.Ally;
            }

            if (relationScore <= -25)
            {
                relationSource = "perception_score";
                return KernelMinimapRelation.Hostile;
            }

            if (perceived.RelationFlags != PerceivedRelationFlags.None)
            {
                relationSource = "perception_flags";
                return KernelMinimapRelation.Neutral;
            }

            if (selfFaction != Entity.Null)
            {
                var targetFaction = ResolveFactionEntity(entityManager, target);
                if (TryResolveRelationScore(entityManager, selfFaction, targetFaction, out var score, out var stance, out var source))
                {
                    relationScore = score;
                    relationStance = stance.ToString();
                    relationSource = source == RelationLookupSource.DiplomaticStatus
                        ? "diplomatic_status"
                        : "faction_relation";
                    return ResolveRelationFromScoreAndStance(score, stance);
                }
            }

            relationSource = "unresolved";
            return KernelMinimapRelation.Unknown;
        }

        private static KernelMinimapRelation ResolveFallbackRelation(
            EntityManager entityManager,
            Entity flagship,
            Entity target,
            Entity selfFaction,
            bool hasSelfSide,
            byte selfSide,
            out int relationScore,
            out string relationStance,
            out string relationSource)
        {
            relationScore = 0;
            relationStance = string.Empty;
            relationSource = "fallback";

            if (target == flagship)
            {
                relationScore = 100;
                relationStance = "Self";
                relationSource = "self";
                return KernelMinimapRelation.Self;
            }

            if (hasSelfSide && TryResolveScenarioSide(entityManager, target, out var targetSide))
            {
                relationSource = "scenario_side";
                if (targetSide == selfSide)
                {
                    relationScore = 60;
                    relationStance = "Friendly";
                    return KernelMinimapRelation.Ally;
                }

                relationScore = -60;
                relationStance = "Hostile";
                return KernelMinimapRelation.Hostile;
            }

            if (selfFaction != Entity.Null)
            {
                var targetFaction = ResolveFactionEntity(entityManager, target);
                if (TryResolveRelationScore(entityManager, selfFaction, targetFaction, out var score, out var stance, out var source))
                {
                    relationScore = score;
                    relationStance = stance.ToString();
                    relationSource = source == RelationLookupSource.DiplomaticStatus
                        ? "diplomatic_status"
                        : "faction_relation";
                    return ResolveRelationFromScoreAndStance(score, stance);
                }
            }

            relationSource = "fallback_unclassified";
            return KernelMinimapRelation.Unknown;
        }

        private static KernelMinimapRelation ResolveRelationFromPerceivedKind(PerceivedRelationKind kind)
        {
            return kind switch
            {
                PerceivedRelationKind.Ally => KernelMinimapRelation.Ally,
                PerceivedRelationKind.Neutral => KernelMinimapRelation.Neutral,
                PerceivedRelationKind.Hostile => KernelMinimapRelation.Hostile,
                _ => KernelMinimapRelation.Unknown
            };
        }

        private static KernelMinimapRelation ResolveRelationFromScoreAndStance(int score, DiplomaticStance stance)
        {
            switch (stance)
            {
                case DiplomaticStance.War:
                case DiplomaticStance.Hostile:
                case DiplomaticStance.Unfriendly:
                    return KernelMinimapRelation.Hostile;
                case DiplomaticStance.Cordial:
                case DiplomaticStance.Friendly:
                case DiplomaticStance.Allied:
                case DiplomaticStance.Vassal:
                case DiplomaticStance.Overlord:
                    return KernelMinimapRelation.Ally;
                default:
                    break;
            }

            if (score >= 25)
            {
                return KernelMinimapRelation.Ally;
            }

            if (score <= -25)
            {
                return KernelMinimapRelation.Hostile;
            }

            return KernelMinimapRelation.Neutral;
        }

        private static void IncrementRelationCounter(KernelMinimapRelation relation, ref Space4XInRunHudKernelSnapshot snapshot)
        {
            switch (relation)
            {
                case KernelMinimapRelation.Self:
                    snapshot.minimap_self_count++;
                    break;
                case KernelMinimapRelation.Ally:
                    snapshot.minimap_ally_count++;
                    break;
                case KernelMinimapRelation.Neutral:
                    snapshot.minimap_neutral_count++;
                    break;
                case KernelMinimapRelation.Hostile:
                    snapshot.minimap_hostile_count++;
                    break;
                default:
                    snapshot.minimap_unknown_count++;
                    break;
            }
        }

        private static int ResolveClassifiedContactCount(in Space4XInRunHudKernelSnapshot snapshot)
        {
            return snapshot.minimap_self_count +
                   snapshot.minimap_ally_count +
                   snapshot.minimap_neutral_count +
                   snapshot.minimap_hostile_count +
                   snapshot.minimap_unknown_count;
        }

        private static string BuildRelationCountSummary(in Space4XInRunHudKernelSnapshot snapshot)
        {
            return $"self={snapshot.minimap_self_count}|ally={snapshot.minimap_ally_count}|neutral={snapshot.minimap_neutral_count}|hostile={snapshot.minimap_hostile_count}|unknown={snapshot.minimap_unknown_count}";
        }

        private static int CompareMinimapContacts(Space4XMinimapContactKernelSnapshot lhs, Space4XMinimapContactKernelSnapshot rhs)
        {
            var distanceCompare = lhs.distance.CompareTo(rhs.distance);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            return rhs.threat_level.CompareTo(lhs.threat_level);
        }

        private static float ResolveBearingDegrees(float2 planar)
        {
            if (math.lengthsq(planar) <= 0.0001f)
            {
                return 0f;
            }

            var bearing = math.degrees(math.atan2(planar.x, planar.y));
            if (bearing < 0f)
            {
                bearing += 360f;
            }

            return bearing;
        }

        private static string ResolveEntityRef(Entity entity)
        {
            return entity == Entity.Null ? "null" : $"{entity.Index}:{entity.Version}";
        }

        private static string ResolveEntityCallsign(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null ||
                !entityManager.Exists(entity) ||
                !entityManager.HasComponent<Space4XEntityCallsign>(entity))
            {
                return ResolveEntityRef(entity);
            }

            var callsign = entityManager.GetComponentData<Space4XEntityCallsign>(entity).Value;
            return callsign.Length > 0 ? callsign.ToString() : ResolveEntityRef(entity);
        }

        private static string ResolveRelationToken(KernelMinimapRelation relation)
        {
            return relation switch
            {
                KernelMinimapRelation.Self => "self",
                KernelMinimapRelation.Ally => "ally",
                KernelMinimapRelation.Neutral => "neutral",
                KernelMinimapRelation.Hostile => "hostile",
                _ => "unknown"
            };
        }

        private static string ResolveRelationColorToken(KernelMinimapRelation relation)
        {
            return relation switch
            {
                KernelMinimapRelation.Self => "self_cyan",
                KernelMinimapRelation.Ally => "ally_green",
                KernelMinimapRelation.Neutral => "neutral_amber",
                KernelMinimapRelation.Hostile => "hostile_red",
                _ => "unknown_gray"
            };
        }

        private static Color ResolveRelationColor(string colorToken)
        {
            return colorToken switch
            {
                "self_cyan" => new Color(0.38f, 0.86f, 1f, 1f),
                "ally_green" => new Color(0.40f, 0.87f, 0.58f, 1f),
                "neutral_amber" => new Color(0.88f, 0.77f, 0.43f, 1f),
                "hostile_red" => new Color(0.90f, 0.43f, 0.43f, 1f),
                _ => new Color(0.74f, 0.79f, 0.84f, 1f)
            };
        }

        private static bool TryResolveEntityPosition(EntityManager entityManager, Entity entity, out float3 position)
        {
            if (entity != Entity.Null && entityManager.Exists(entity))
            {
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    return true;
                }

                if (entityManager.HasComponent<LocalToWorld>(entity))
                {
                    position = entityManager.GetComponentData<LocalToWorld>(entity).Position;
                    return true;
                }
            }

            position = float3.zero;
            return false;
        }

        private static bool TryResolveEntityRotation(EntityManager entityManager, Entity entity, out quaternion rotation)
        {
            if (entity != Entity.Null && entityManager.Exists(entity))
            {
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    rotation = entityManager.GetComponentData<LocalTransform>(entity).Rotation;
                    return true;
                }

                if (entityManager.HasComponent<LocalToWorld>(entity))
                {
                    var localToWorld = entityManager.GetComponentData<LocalToWorld>(entity).Value;
                    rotation = quaternion.LookRotationSafe(localToWorld.c2.xyz, localToWorld.c1.xyz);
                    return true;
                }
            }

            rotation = quaternion.identity;
            return false;
        }

        private bool TryResolveMinimapAnchorPose(EntityManager entityManager, Entity flagship, out float3 position, out quaternion rotation)
        {
            if (TryResolveEntityPosition(entityManager, flagship, out position))
            {
                if (!TryResolveEntityRotation(entityManager, flagship, out rotation))
                {
                    rotation = quaternion.identity;
                }

                return true;
            }

            if (TryResolveCameraFallbackPose(out position, out rotation))
            {
                return true;
            }

            if (TryResolveFallbackMinimapAnchor(entityManager, out position, out rotation))
            {
                return true;
            }

            position = float3.zero;
            rotation = quaternion.identity;
            return false;
        }

        private static bool TryResolveCameraFallbackPose(out float3 position, out quaternion rotation)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                camera = FindAnyObjectByType<UnityEngine.Camera>();
            }

            if (camera == null)
            {
                position = float3.zero;
                rotation = quaternion.identity;
                return false;
            }

            var cameraPosition = camera.transform.position;
            var forward = camera.transform.forward;
            var anchor = cameraPosition;
            if (Mathf.Abs(forward.y) > 0.001f)
            {
                var t = -cameraPosition.y / forward.y;
                if (t > 0f)
                {
                    anchor = cameraPosition + forward * t;
                }
            }

            var planarForward = new float3(forward.x, 0f, forward.z);
            if (math.lengthsq(planarForward) < 0.0001f)
            {
                planarForward = new float3(0f, 0f, 1f);
            }

            position = new float3(anchor.x, 0f, anchor.z);
            rotation = quaternion.LookRotationSafe(math.normalizesafe(planarForward, new float3(0f, 0f, 1f)), math.up());
            return true;
        }

        private bool TryResolveFallbackMinimapAnchor(EntityManager entityManager, out float3 position, out quaternion rotation)
        {
            if (!_minimapFallbackQueryValid)
            {
                _minimapFallbackQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<HullIntegrity>());
                _minimapFallbackQueryValid = true;
            }

            if (_minimapFallbackQuery.IsEmptyIgnoreFilter)
            {
                position = float3.zero;
                rotation = quaternion.identity;
                return false;
            }

            using var transforms = _minimapFallbackQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (transforms.Length <= 0)
            {
                position = float3.zero;
                rotation = quaternion.identity;
                return false;
            }

            var transform = transforms[0];
            position = transform.Position;
            rotation = transform.Rotation;
            return true;
        }

        private static float3 ResolveEntityVelocity(EntityManager entityManager, Entity entity)
        {
            if (entity != Entity.Null &&
                entityManager.Exists(entity) &&
                entityManager.HasComponent<VesselMovement>(entity))
            {
                return entityManager.GetComponentData<VesselMovement>(entity).Velocity;
            }

            return float3.zero;
        }

        private static void ResolveEntitySignatureChannels(
            EntityManager entityManager,
            Entity entity,
            out float emSignature,
            out float graviticSignature,
            out float thermalSignature,
            out float psiSignature)
        {
            emSignature = 0f;
            graviticSignature = 0f;
            thermalSignature = 0f;
            psiSignature = 0f;

            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return;
            }

            if (entityManager.HasComponent<SensorSignature>(entity))
            {
                var signature = entityManager.GetComponentData<SensorSignature>(entity);
                emSignature = math.max(0f, signature.EMSignature);
                graviticSignature = math.max(0f, signature.GraviticSignature);
                thermalSignature = math.max(0f, signature.ExoticSignature);
                psiSignature = math.max(0f, signature.ParanormalSignature);
            }

            if (entityManager.HasComponent<Space4XHeatKernelOutput>(entity))
            {
                var heatKernel = entityManager.GetComponentData<Space4XHeatKernelOutput>(entity);
                thermalSignature = math.max(thermalSignature, math.saturate(heatKernel.Thermal01));
            }

            if (entityManager.HasComponent<FleetcrawlHeatOutputState>(entity))
            {
                var heat = entityManager.GetComponentData<FleetcrawlHeatOutputState>(entity);
                thermalSignature = math.max(thermalSignature, FleetcrawlHeatResolver.ResolveHeatSignature01(in heat));
            }
        }

        private static float ResolveHeadingDegrees(quaternion rotation)
        {
            var forward = math.mul(rotation, new float3(0f, 0f, 1f));
            var heading = math.degrees(math.atan2(forward.x, forward.z));
            if (heading < 0f)
            {
                heading += 360f;
            }

            return heading;
        }

        private static bool TryResolveScenarioSide(EntityManager entityManager, Entity entity, out byte side)
        {
            side = 0;
            if (entity == Entity.Null ||
                !entityManager.Exists(entity) ||
                !entityManager.HasComponent<ScenarioSide>(entity))
            {
                return false;
            }

            side = entityManager.GetComponentData<ScenarioSide>(entity).Side;
            return true;
        }

        private static Entity ResolveFactionEntity(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return Entity.Null;
            }

            if (entityManager.HasBuffer<AffiliationTag>(entity))
            {
                var affiliations = entityManager.GetBuffer<AffiliationTag>(entity);
                Entity fallback = Entity.Null;
                for (var i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Target == Entity.Null || !entityManager.Exists(tag.Target))
                    {
                        continue;
                    }

                    if (tag.Type == AffiliationType.Faction)
                    {
                        return tag.Target;
                    }

                    if (fallback == Entity.Null && tag.Type == AffiliationType.Fleet)
                    {
                        fallback = tag.Target;
                    }
                    else if (fallback == Entity.Null)
                    {
                        fallback = tag.Target;
                    }
                }

                if (fallback != Entity.Null &&
                    entityManager.Exists(fallback) &&
                    entityManager.HasBuffer<AffiliationTag>(fallback))
                {
                    var nested = entityManager.GetBuffer<AffiliationTag>(fallback);
                    for (var i = 0; i < nested.Length; i++)
                    {
                        var tag = nested[i];
                        if (tag.Target == Entity.Null || !entityManager.Exists(tag.Target))
                        {
                            continue;
                        }

                        if (tag.Type == AffiliationType.Faction)
                        {
                            return tag.Target;
                        }

                        if (tag.Type == AffiliationType.Fleet)
                        {
                            return tag.Target;
                        }
                    }
                }

                if (fallback != Entity.Null && entityManager.Exists(fallback))
                {
                    return fallback;
                }
            }

            if (entityManager.HasComponent<Carrier>(entity))
            {
                var carrier = entityManager.GetComponentData<Carrier>(entity);
                if (carrier.AffiliationEntity != Entity.Null &&
                    entityManager.Exists(carrier.AffiliationEntity))
                {
                    return carrier.AffiliationEntity;
                }
            }

            return Entity.Null;
        }

        private static bool TryResolveRelationScore(
            EntityManager entityManager,
            Entity selfFaction,
            Entity targetFaction,
            out sbyte score,
            out DiplomaticStance stance,
            out RelationLookupSource source)
        {
            score = 0;
            stance = DiplomaticStance.Neutral;
            source = RelationLookupSource.None;

            if (selfFaction == Entity.Null ||
                targetFaction == Entity.Null ||
                !entityManager.Exists(selfFaction) ||
                !entityManager.Exists(targetFaction))
            {
                return false;
            }

            if (selfFaction == targetFaction)
            {
                score = 100;
                stance = DiplomaticStance.Allied;
                source = RelationLookupSource.DiplomaticStatus;
                return true;
            }

            if (!entityManager.HasComponent<Space4XFaction>(targetFaction))
            {
                return false;
            }

            var targetFactionId = entityManager.GetComponentData<Space4XFaction>(targetFaction).FactionId;

            if (entityManager.HasBuffer<DiplomaticStatusEntry>(selfFaction))
            {
                var statuses = entityManager.GetBuffer<DiplomaticStatusEntry>(selfFaction);
                for (var i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i].Status;
                    if (status.OtherFactionId == targetFactionId)
                    {
                        score = status.RelationScore;
                        stance = status.Stance;
                        source = RelationLookupSource.DiplomaticStatus;
                        return true;
                    }
                }
            }

            if (entityManager.HasBuffer<FactionRelationEntry>(selfFaction))
            {
                var relations = entityManager.GetBuffer<FactionRelationEntry>(selfFaction);
                for (var i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == targetFactionId)
                    {
                        score = relation.Score;
                        stance = DiplomacyMath.DetermineStance(score, DiplomaticStance.Neutral);
                        source = RelationLookupSource.FactionRelation;
                        return true;
                    }
                }
            }

            return false;
        }

        private void RebuildMinimapContactList(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_minimapContactList == null)
            {
                return;
            }

            _minimapContactList.Clear();
            if (!_minimapVisible)
            {
                return;
            }

            var contacts = snapshot.minimap_contacts;
            if (contacts == null || contacts.Length == 0)
            {
                _minimapContactList.Add(CreateMinimapContactLabel("No contacts in sensor window.", ResolveRelationColor("unknown_gray")));
                return;
            }

            var shown = 0;

            for (var i = 0; i < contacts.Length; i++)
            {
                var contact = contacts[i];
                if (!ShouldShowContactForCurrentOverlay(contact))
                {
                    continue;
                }

                var identity = !string.IsNullOrWhiteSpace(contact.callsign)
                    ? contact.callsign
                    : contact.entity_ref;
                var text = $"{contact.relation} {identity} d={contact.distance:0.0}m brg={contact.bearing_deg:0} v={contact.speed:0.0} c={contact.closing_speed:0.0}";
                _minimapContactList.Add(CreateMinimapContactLabel(text, ResolveRelationColor(contact.color_token)));
                shown++;
            }

            if (shown == 0)
            {
                _minimapContactList.Add(CreateMinimapContactLabel("No contacts for current overlay.", ResolveRelationColor("unknown_gray")));
            }
        }

        private void EmitResourceWarnings(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (snapshot.run_active != 1)
                return;

            if (snapshot.fuel_ratio > 0f && snapshot.fuel_ratio <= LowResourceThreshold && UTime.unscaledTime >= _nextLowFuelWarningAt)
            {
                _nextLowFuelWarningAt = UTime.unscaledTime + 8f;
                AddNotification($"Fuel low ({snapshot.fuel_ratio * 100f:0}%).");
            }

            if (snapshot.food_ratio > 0f && snapshot.food_ratio <= LowResourceThreshold && UTime.unscaledTime >= _nextLowFoodWarningAt)
            {
                _nextLowFoodWarningAt = UTime.unscaledTime + 8f;
                AddNotification($"Food low ({snapshot.food_ratio * 100f:0}%).");
            }

            if (snapshot.supplies_ratio > 0f && snapshot.supplies_ratio <= LowResourceThreshold && UTime.unscaledTime >= _nextLowSuppliesWarningAt)
            {
                _nextLowSuppliesWarningAt = UTime.unscaledTime + 8f;
                AddNotification($"Supplies low ({snapshot.supplies_ratio * 100f:0}%).");
            }
        }

        private bool TryTogglePause()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (!Space4XTimeAPI.TogglePause(world))
                return false;

            AddNotification("Pause toggled.");
            return true;
        }

        private bool TrySetTimeSpeed(float speed)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (!Space4XTimeAPI.SetGlobalTimeSpeed(world, speed))
                return false;

            AddNotification($"Time speed set to {speed:0.##}x.");
            return true;
        }

        private void AddNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _notifications.Add($"[{DateTime.UtcNow:HH:mm:ss}] {TrimForKernel(message.Trim(), KernelNotificationTextMaxChars)}");
            var historyCapacity = math.max(NotificationHistoryCapacity, math.max(3, maxNotifications));
            while (_notifications.Count > historyCapacity)
            {
                _notifications.RemoveAt(0);
            }
        }

        private void RebuildNotificationList()
        {
            if (_notificationList == null)
                return;

            _notificationList.Clear();
            if (!_notificationsVisible)
                return;

            if (_notifications.Count == 0)
            {
                _notificationList.Add(CreateNotificationLabel("No feed entries."));
                return;
            }

            for (var i = 0; i < _notifications.Count; i++)
            {
                _notificationList.Add(CreateNotificationTileButton(_notifications[i]));
            }

            if (_notificationList.childCount > 0)
            {
                _notificationScroll?.ScrollTo(_notificationList[_notificationList.childCount - 1]);
            }
        }

        private void RebuildShipControlPanel(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_shipControlPanel == null)
            {
                return;
            }

            if (_shipControlSummaryLabel != null)
            {
                var summary = string.IsNullOrWhiteSpace(snapshot.ship_control_summary)
                    ? "No ship control telemetry."
                    : snapshot.ship_control_summary.Trim();
                _shipControlSummaryLabel.text = summary;
            }

            if (_shipControlStatusList != null)
            {
                _shipControlStatusList.Clear();
                var statuses = snapshot.ship_control_statuses ?? Array.Empty<Space4XShipStatusEffectKernelSnapshot>();
                if (statuses.Length <= 0)
                {
                    var none = CreateInventoryLabel("No active statuses.");
                    none.style.fontSize = Fsi(8);
                    none.style.marginBottom = 2f;
                    none.style.color = new Color(0.70f, 0.84f, 0.93f, 0.96f);
                    _shipControlStatusList.Add(none);
                }
                else
                {
                    for (var i = 0; i < statuses.Length; i++)
                    {
                        var status = statuses[i];
                        if (status == null)
                        {
                            continue;
                        }

                        _shipControlStatusList.Add(CreateShipControlStatusTile(status));
                    }
                }
            }

            if (_shipControlThermalList != null)
            {
                _shipControlThermalList.Clear();
                var modules = snapshot.ship_control_modules ?? Array.Empty<Space4XShipModuleThermalKernelSnapshot>();
                if (modules.Length <= 0)
                {
                    var none = CreateInventoryLabel("No module thermal telemetry.");
                    none.style.fontSize = Fsi(8);
                    none.style.marginBottom = 2f;
                    none.style.color = new Color(0.70f, 0.84f, 0.93f, 0.96f);
                    _shipControlThermalList.Add(none);
                }
                else
                {
                    for (var i = 0; i < modules.Length; i++)
                    {
                        var module = modules[i];
                        if (module == null)
                        {
                            continue;
                        }

                        _shipControlThermalList.Add(CreateShipControlThermalTile(module));
                    }
                }
            }
        }

        private VisualElement CreateShipControlStatusTile(Space4XShipStatusEffectKernelSnapshot status)
        {
            var severity = math.saturate(status.severity_01);
            var buffTint = status.is_buff == 1
                ? new Color(0.16f, 0.35f, 0.22f, 0.88f)
                : new Color(0.33f, 0.14f, 0.16f, 0.88f);
            var neutralTint = new Color(0.11f, 0.19f, 0.26f, 0.88f);
            var tile = new VisualElement();
            tile.style.flexDirection = FlexDirection.Column;
            tile.style.marginBottom = 2f;
            tile.style.paddingLeft = 3f;
            tile.style.paddingRight = 3f;
            tile.style.paddingTop = 2f;
            tile.style.paddingBottom = 2f;
            tile.style.backgroundColor = Color.Lerp(neutralTint, buffTint, math.max(0.25f, severity));
            tile.style.borderTopWidth = 1f;
            tile.style.borderRightWidth = 1f;
            tile.style.borderBottomWidth = 1f;
            tile.style.borderLeftWidth = 1f;
            tile.style.borderTopColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderRightColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderBottomColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderLeftColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);

            var type = string.IsNullOrWhiteSpace(status.effect_type) ? "Status" : status.effect_type;
            var stacks = status.max_stacks > 1 ? $" x{math.max(1, status.stacks)}" : string.Empty;
            var duration = status.duration_seconds < 0f ? "perm" : $"{math.max(0f, status.duration_seconds):0.0}s";
            var lineA = CreateInventoryLabel($"{type}{stacks}  {duration}");
            lineA.style.fontSize = Fsi(8);
            lineA.style.unityFontStyleAndWeight = FontStyle.Bold;
            lineA.style.marginTop = 0f;
            lineA.style.marginBottom = 0f;
            lineA.style.color = new Color(0.90f, 0.97f, 1f, 1f);
            tile.Add(lineA);

            var source = string.IsNullOrWhiteSpace(status.source_entity_ref)
                ? "local"
                : status.source_entity_ref;
            var lineB = CreateInventoryLabel(
                $"{status.category}/{status.behavior}  val {status.value:0.##}  src {TrimForKernel(source, 18)}");
            lineB.style.fontSize = Fsi(7);
            lineB.style.marginTop = 0f;
            lineB.style.marginBottom = 0f;
            lineB.style.color = new Color(0.76f, 0.88f, 0.96f, 0.96f);
            tile.Add(lineB);

            return tile;
        }

        private VisualElement CreateShipControlThermalTile(Space4XShipModuleThermalKernelSnapshot module)
        {
            var thermal = math.saturate(module.thermal_ratio);
            var tile = new VisualElement();
            tile.style.flexDirection = FlexDirection.Column;
            tile.style.marginBottom = 2f;
            tile.style.paddingLeft = 3f;
            tile.style.paddingRight = 3f;
            tile.style.paddingTop = 2f;
            tile.style.paddingBottom = 2f;
            tile.style.backgroundColor = new Color(0.10f, 0.16f, 0.22f, 0.88f);
            tile.style.borderTopWidth = 1f;
            tile.style.borderRightWidth = 1f;
            tile.style.borderBottomWidth = 1f;
            tile.style.borderLeftWidth = 1f;
            tile.style.borderTopColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderRightColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderBottomColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);
            tile.style.borderLeftColor = new Color(0.30f, 0.46f, 0.58f, 0.92f);

            var headline = CreateInventoryLabel(TrimForKernel(module.module_label, 36));
            headline.style.fontSize = Fsi(8);
            headline.style.unityFontStyleAndWeight = FontStyle.Bold;
            headline.style.marginTop = 0f;
            headline.style.marginBottom = 0f;
            headline.style.color = new Color(0.90f, 0.97f, 1f, 1f);
            tile.Add(headline);

            var track = new VisualElement();
            track.style.position = Position.Relative;
            track.style.height = 7f;
            track.style.marginTop = 1f;
            track.style.marginBottom = 1f;
            track.style.backgroundColor = new Color(0.09f, 0.14f, 0.18f, 0.94f);
            track.style.borderTopWidth = 1f;
            track.style.borderRightWidth = 1f;
            track.style.borderBottomWidth = 1f;
            track.style.borderLeftWidth = 1f;
            track.style.borderTopColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            track.style.borderRightColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            track.style.borderBottomColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            track.style.borderLeftColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            tile.Add(track);

            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0f;
            fill.style.top = 0f;
            fill.style.bottom = 0f;
            fill.style.width = Length.Percent(thermal * 100f);
            fill.style.backgroundColor = Color.Lerp(new Color(0.24f, 0.58f, 0.90f, 0.95f), new Color(0.92f, 0.35f, 0.28f, 0.98f), thermal);
            track.Add(fill);

            var source = string.IsNullOrWhiteSpace(module.thermal_source) ? "n/a" : module.thermal_source;
            var sat = module.thermal_saturated == 1 ? " SAT" : string.Empty;
            var detail = CreateInventoryLabel(
                $"{module.thermal_current:0.0}/{module.thermal_capacity:0.0} ({thermal * 100f:0}%)  {source}{sat}");
            detail.style.fontSize = Fsi(7);
            detail.style.marginTop = 0f;
            detail.style.marginBottom = 0f;
            detail.style.color = new Color(0.74f, 0.87f, 0.95f, 0.96f);
            tile.Add(detail);

            tile.tooltip = $"LMB focus in inventory: {module.module_label}";
            tile.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt == null || evt.button != (int)MouseButton.LeftMouse || string.IsNullOrWhiteSpace(module.module_ref))
                {
                    return;
                }

                _inventoryVisible = true;
                _inventorySelectedModuleRef = module.module_ref;
                _activeContextNotification = $"Ship Control focus: {module.module_label}";
                RequestImmediateHudRefresh();
                evt.StopImmediatePropagation();
            });

            return tile;
        }

        private void UpdateShipControlButtonStates()
        {
            SetControlButtonState(_toggleShipControlButton, _shipControlVisible);
            SetControlButtonState(_shipControlMapButton, _minimapVisible);
            SetControlButtonState(_shipControlInventoryButton, _inventoryVisible);
            SetControlButtonState(_shipControlProductionButton, _productionVisible);
            SetControlButtonState(_shipControlFeedButton, _notificationsVisible);
            SetControlButtonState(_shipControlForcesButton, _forcesHoloVisible);
        }

        private static void SetControlButtonState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = active
                ? new Color(0.30f, 0.49f, 0.63f, 0.98f)
                : new Color(0.17f, 0.24f, 0.31f, 1f);
            button.style.color = active
                ? new Color(0.96f, 0.99f, 1f, 1f)
                : new Color(0.90f, 0.96f, 1f, 1f);
        }

        private void RebuildInventoryList(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_inventoryList == null)
            {
                return;
            }

            if (_inventorySegmentsScroll != null)
            {
                _inventorySegmentsScrollOffset = _inventorySegmentsScroll.scrollOffset;
            }

            if (_inventoryCargoScroll != null)
            {
                _inventoryCargoScrollOffset = _inventoryCargoScroll.scrollOffset;
            }

            _inventoryModuleLookup.Clear();
            _inventoryHoveredModuleRef = string.Empty;

            if (_inventoryHeaderLabel != null)
            {
                _inventoryHeaderLabel.text = "Inventory / Loadout";
            }

            if (_inventoryHintLabel != null)
            {
                var selectedRef = string.IsNullOrWhiteSpace(_inventorySelectedModuleRef)
                    ? "none"
                    : TrimForKernel(_inventorySelectedModuleRef, 40);
                _inventoryHintLabel.text = $"LMB open module organ window. MMB quick inspect. Selected: {selectedRef}.";
            }

            _inventoryList.Clear();
            _inventorySegmentsScroll = null;
            _inventoryCargoScroll = null;
            if (!_inventoryVisible)
            {
                return;
            }

            var shipLabel = string.IsNullOrWhiteSpace(snapshot.inventory_ship_label) ? "Flagship" : snapshot.inventory_ship_label.Trim();
            var archetype = string.IsNullOrWhiteSpace(snapshot.inventory_ship_archetype) ? "Unknown Archetype" : snapshot.inventory_ship_archetype.Trim();
            var shipHeader = new Label($"{shipLabel}  [{archetype}]")
            {
                name = Space4XInRunHudElementIds.InventoryShipLabel
            };
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                shipHeader.style.unityFont = runtimeFont;
            }

            shipHeader.style.fontSize = Fsi(11);
            shipHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            shipHeader.style.color = new Color(0.88f, 0.95f, 0.99f, 1f);
            shipHeader.style.marginBottom = 3f;
            _inventoryList.Add(shipHeader);

            if (!string.IsNullOrWhiteSpace(_activeContextNotification))
            {
                var contextLabel = CreateInventoryLabel($"Context: {_activeContextNotification.Trim()}");
                contextLabel.style.fontSize = Fsi(9);
                contextLabel.style.marginBottom = 4f;
                contextLabel.style.color = new Color(0.73f, 0.84f, 0.92f, 0.96f);
                _inventoryList.Add(contextLabel);
            }

            var body = new VisualElement
            {
                name = "space4x.ui.hud.inventory.body"
            };
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1f;
            body.style.overflow = Overflow.Hidden;
            _inventoryList.Add(body);

            var frameBorder = new Color(0.23f, 0.35f, 0.45f, 0.88f);

            var attachedFrame = new VisualElement
            {
                name = "space4x.ui.hud.inventory.attached_frame"
            };
            attachedFrame.style.flexDirection = FlexDirection.Column;
            attachedFrame.style.flexGrow = 0.62f;
            attachedFrame.style.minHeight = 120f;
            attachedFrame.style.marginBottom = 4f;
            attachedFrame.style.paddingTop = 2f;
            attachedFrame.style.paddingBottom = 2f;
            attachedFrame.style.paddingLeft = 3f;
            attachedFrame.style.paddingRight = 3f;
            attachedFrame.style.backgroundColor = new Color(0.05f, 0.09f, 0.13f, 0.58f);
            attachedFrame.style.borderTopWidth = 1f;
            attachedFrame.style.borderRightWidth = 1f;
            attachedFrame.style.borderBottomWidth = 1f;
            attachedFrame.style.borderLeftWidth = 1f;
            attachedFrame.style.borderTopColor = frameBorder;
            attachedFrame.style.borderRightColor = frameBorder;
            attachedFrame.style.borderBottomColor = frameBorder;
            attachedFrame.style.borderLeftColor = frameBorder;
            body.Add(attachedFrame);

            var availableFrame = new VisualElement
            {
                name = "space4x.ui.hud.inventory.available_frame"
            };
            availableFrame.style.flexDirection = FlexDirection.Column;
            availableFrame.style.flexGrow = 0.38f;
            availableFrame.style.minHeight = 92f;
            availableFrame.style.paddingTop = 2f;
            availableFrame.style.paddingBottom = 2f;
            availableFrame.style.paddingLeft = 3f;
            availableFrame.style.paddingRight = 3f;
            availableFrame.style.backgroundColor = new Color(0.04f, 0.08f, 0.12f, 0.58f);
            availableFrame.style.borderTopWidth = 1f;
            availableFrame.style.borderRightWidth = 1f;
            availableFrame.style.borderBottomWidth = 1f;
            availableFrame.style.borderLeftWidth = 1f;
            availableFrame.style.borderTopColor = frameBorder;
            availableFrame.style.borderRightColor = frameBorder;
            availableFrame.style.borderBottomColor = frameBorder;
            availableFrame.style.borderLeftColor = frameBorder;
            body.Add(availableFrame);

            _inventorySegmentsScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = Space4XInRunHudElementIds.InventorySegmentsColumn
            };
            var segmentsColumn = _inventorySegmentsScroll;
            segmentsColumn.style.flexGrow = 1f;
            segmentsColumn.style.width = Length.Percent(100f);
            segmentsColumn.verticalScrollerVisibility = ScrollerVisibility.Auto;
            segmentsColumn.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            attachedFrame.Add(segmentsColumn);

            _inventoryCargoScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = Space4XInRunHudElementIds.InventoryCargoColumn
            };
            var cargoColumn = _inventoryCargoScroll;
            cargoColumn.style.flexGrow = 1f;
            cargoColumn.style.width = Length.Percent(100f);
            cargoColumn.verticalScrollerVisibility = ScrollerVisibility.Auto;
            cargoColumn.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            availableFrame.Add(cargoColumn);

            var segmentHeader = CreateInventoryLabel("Current Ship / Attached Modules");
            segmentHeader.style.fontSize = Fsi(10);
            segmentHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            segmentHeader.style.marginBottom = 1f;
            segmentsColumn.contentContainer.Add(segmentHeader);
            var cargoHeader = CreateInventoryLabel($"Available Modules ({snapshot.inventory_available_module_count:0})");
            cargoHeader.style.fontSize = Fsi(10);
            cargoHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            cargoHeader.style.marginBottom = 1f;
            cargoColumn.contentContainer.Add(cargoHeader);

            var cargoLines = snapshot.inventory_cargo_lines ?? Array.Empty<string>();
            if (cargoLines.Length > 0)
            {
                var cargoSummary = CreateInventoryLabel(string.Join(" | ", cargoLines));
                cargoSummary.style.fontSize = Fsi(8);
                cargoSummary.style.marginBottom = 2f;
                cargoSummary.style.color = new Color(0.73f, 0.84f, 0.92f, 0.96f);
                segmentsColumn.contentContainer.Add(cargoSummary);
            }

            var segments = snapshot.inventory_segments ?? Array.Empty<Space4XInventorySegmentKernelSnapshot>();
            var hasModules = false;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                var segmentCard = new VisualElement();
                segmentCard.style.flexDirection = FlexDirection.Column;
                segmentCard.style.marginBottom = 4f;
                segmentCard.style.paddingTop = 3f;
                segmentCard.style.paddingBottom = 3f;
                segmentCard.style.paddingLeft = 4f;
                segmentCard.style.paddingRight = 4f;
                segmentCard.style.backgroundColor = new Color(0.06f, 0.11f, 0.16f, 0.78f);
                segmentCard.style.borderTopWidth = 1f;
                segmentCard.style.borderRightWidth = 1f;
                segmentCard.style.borderBottomWidth = 1f;
                segmentCard.style.borderLeftWidth = 1f;
                segmentCard.style.borderTopColor = new Color(0.27f, 0.40f, 0.52f, 0.9f);
                segmentCard.style.borderRightColor = new Color(0.27f, 0.40f, 0.52f, 0.9f);
                segmentCard.style.borderBottomColor = new Color(0.27f, 0.40f, 0.52f, 0.9f);
                segmentCard.style.borderLeftColor = new Color(0.27f, 0.40f, 0.52f, 0.9f);

                var segmentName = string.IsNullOrWhiteSpace(segment.segment_label) ? $"Segment {i + 1:00}" : segment.segment_label;
                var segmentTitle = CreateInventoryLabel($"{segmentName}  slots:{segment.module_count}");
                segmentTitle.style.fontSize = Fsi(9);
                segmentTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                segmentCard.Add(segmentTitle);

                var modules = segment.modules ?? Array.Empty<Space4XInventoryModuleKernelSnapshot>();
                if (modules.Length == 0)
                {
                    segmentCard.Add(CreateInventoryLabel("No module slots."));
                }
                else
                {
                    for (var moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                    {
                        var module = modules[moduleIndex];
                        if (module == null)
                        {
                            continue;
                        }

                        hasModules = true;
                        var moduleRef = string.IsNullOrWhiteSpace(module.module_ref) ? $"slot:{module.slot_index}" : module.module_ref;
                        _inventoryModuleLookup[moduleRef] = module;
                        segmentCard.Add(CreateInventoryModuleCard(moduleRef, module));
                    }
                }

                segmentsColumn.contentContainer.Add(segmentCard);
            }

            if (segments.Length == 0)
            {
                segmentsColumn.contentContainer.Add(CreateInventoryLabel("No hull segment data."));
            }

            if (!string.IsNullOrWhiteSpace(_inventorySelectedModuleRef) &&
                !_inventoryModuleLookup.ContainsKey(_inventorySelectedModuleRef))
            {
                _inventorySelectedModuleRef = string.Empty;
            }

            if (_inventoryModuleLookup.TryGetValue(_inventorySelectedModuleRef, out var selectedModule))
            {
                var selectedLabel = CreateInventoryLabel($"Selected: {ResolveInventoryModuleHeadline(selectedModule)}");
                selectedLabel.style.fontSize = Fsi(9);
                selectedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                cargoColumn.contentContainer.Add(selectedLabel);
            }
            else if (!hasModules)
            {
                segmentsColumn.contentContainer.Add(CreateInventoryLabel("No modules mounted."));
            }

            var availableModules = snapshot.inventory_available_modules ?? Array.Empty<Space4XInventoryCatalogModuleKernelSnapshot>();
            if (availableModules.Length == 0)
            {
                cargoColumn.contentContainer.Add(CreateInventoryLabel("No catalog modules loaded."));
            }
            else
            {
                for (var i = 0; i < availableModules.Length; i++)
                {
                    var available = availableModules[i];
                    if (available == null)
                    {
                        continue;
                    }

                    var availableTile = new VisualElement();
                    availableTile.style.flexDirection = FlexDirection.Column;
                    availableTile.style.marginBottom = 1f;
                    availableTile.style.paddingTop = 1f;
                    availableTile.style.paddingBottom = 1f;
                    availableTile.style.paddingLeft = 2f;
                    availableTile.style.paddingRight = 2f;
                    availableTile.style.backgroundColor = new Color(0.10f, 0.16f, 0.21f, 0.9f);
                    availableTile.style.borderTopWidth = 1f;
                    availableTile.style.borderRightWidth = 1f;
                    availableTile.style.borderBottomWidth = 1f;
                    availableTile.style.borderLeftWidth = 1f;
                    availableTile.style.borderTopColor = new Color(0.28f, 0.41f, 0.53f, 0.9f);
                    availableTile.style.borderRightColor = new Color(0.28f, 0.41f, 0.53f, 0.9f);
                    availableTile.style.borderBottomColor = new Color(0.28f, 0.41f, 0.53f, 0.9f);
                    availableTile.style.borderLeftColor = new Color(0.28f, 0.41f, 0.53f, 0.9f);

                    var lineA = CreateInventoryLabel($"{available.module_class}  {available.module_id}");
                    lineA.style.fontSize = Fsi(8);
                    lineA.style.unityFontStyleAndWeight = FontStyle.Bold;
                    lineA.style.marginTop = 0f;
                    lineA.style.marginBottom = 0f;
                    availableTile.Add(lineA);

                    var lineB = CreateInventoryLabel($"{available.mount}/{available.size}  pwr {available.power_draw_mw:0}  mass {available.mass_tons:0.0}");
                    lineB.style.fontSize = Fsi(7);
                    lineB.style.marginTop = 0f;
                    lineB.style.marginBottom = 0f;
                    lineB.style.color = new Color(0.70f, 0.83f, 0.92f, 0.96f);
                    availableTile.Add(lineB);

                    availableTile.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt == null || evt.button != (int)MouseButton.LeftMouse)
                        {
                            return;
                        }

                        _activeContextNotification = $"Catalog {available.module_id} ({available.module_class}) mount:{available.mount} size:{available.size}";
                        evt.StopImmediatePropagation();
                    });

                    cargoColumn.contentContainer.Add(availableTile);
                }
            }

            _inventoryList.schedule.Execute(() =>
            {
                if (_inventorySegmentsScroll != null)
                {
                    _inventorySegmentsScroll.scrollOffset = _inventorySegmentsScrollOffset;
                }

                if (_inventoryCargoScroll != null)
                {
                    _inventoryCargoScroll.scrollOffset = _inventoryCargoScrollOffset;
                }
            });

            RefreshInventoryPopups();
        }

        private void RebuildProductionList(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_productionList == null)
            {
                return;
            }

            _productionList.Clear();
            if (!_productionVisible)
            {
                return;
            }

            var summary = string.IsNullOrWhiteSpace(snapshot.production_summary)
                ? "No production summary."
                : snapshot.production_summary.Trim();
            var summaryLabel = CreateInventoryLabel(summary);
            summaryLabel.style.fontSize = Fsi(9);
            summaryLabel.style.marginBottom = 4f;
            summaryLabel.style.color = new Color(0.76f, 0.87f, 0.94f, 0.96f);
            _productionList.Add(summaryLabel);

            var facilities = snapshot.production_facilities ?? Array.Empty<Space4XProductionFacilityKernelSnapshot>();
            if (facilities.Length <= 0)
            {
                _productionList.Add(CreateInventoryLabel("No controlled production facilities."));
                return;
            }

            var selectedIndex = math.clamp(snapshot.production_selected_index, 0, facilities.Length - 1);
            var selected = facilities[selectedIndex];
            var selectedRef = selected.entity_ref ?? string.Empty;

            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;
            selectorRow.style.marginBottom = 3f;
            _productionList.Add(selectorRow);

            var prevFacilityButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.facility_prev", "<");
            prevFacilityButton.style.width = 24f;
            prevFacilityButton.style.minWidth = 24f;
            prevFacilityButton.style.height = 22f;
            prevFacilityButton.clicked += () =>
            {
                if (facilities.Length <= 0)
                {
                    return;
                }

                var nextIndex = selectedIndex - 1;
                if (nextIndex < 0)
                {
                    nextIndex = facilities.Length - 1;
                }

                SetProductionFacilitySelection(nextIndex, facilities[nextIndex].entity_ref);
                RequestImmediateHudRefresh();
            };
            selectorRow.Add(prevFacilityButton);

            var selectorLabel = CreateInventoryLabel(
                $"Facility {selectedIndex + 1}/{facilities.Length}  {selected.facility_kind}  {selected.entity_ref}");
            selectorLabel.style.flexGrow = 1f;
            selectorLabel.style.fontSize = Fsi(9);
            selectorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectorLabel.style.whiteSpace = WhiteSpace.NoWrap;
            selectorLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            selectorLabel.style.overflow = Overflow.Hidden;
            selectorRow.Add(selectorLabel);

            var nextFacilityButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.facility_next", ">");
            nextFacilityButton.style.width = 24f;
            nextFacilityButton.style.minWidth = 24f;
            nextFacilityButton.style.height = 22f;
            nextFacilityButton.style.marginRight = 0f;
            nextFacilityButton.clicked += () =>
            {
                if (facilities.Length <= 0)
                {
                    return;
                }

                var nextIndex = (selectedIndex + 1) % facilities.Length;
                SetProductionFacilitySelection(nextIndex, facilities[nextIndex].entity_ref);
                RequestImmediateHudRefresh();
            };
            selectorRow.Add(nextFacilityButton);

            var recipeRow = new VisualElement();
            recipeRow.style.flexDirection = FlexDirection.Row;
            recipeRow.style.alignItems = Align.Center;
            recipeRow.style.marginBottom = 2f;
            _productionList.Add(recipeRow);

            var recipePrevButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.recipe_prev", "<");
            recipePrevButton.style.width = 24f;
            recipePrevButton.style.minWidth = 24f;
            recipePrevButton.style.height = 20f;
            recipePrevButton.clicked += () =>
            {
                CycleProductionRecipeSelection(selectedRef, selected.available_recipes, -1);
                RequestImmediateHudRefresh();
            };
            recipeRow.Add(recipePrevButton);

            var recipeLabel = CreateInventoryLabel(
                $"Recipe: {(string.IsNullOrWhiteSpace(selected.selected_recipe_id) ? "none" : selected.selected_recipe_id)}");
            recipeLabel.style.flexGrow = 1f;
            recipeLabel.style.fontSize = Fsi(9);
            recipeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            recipeLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            recipeLabel.style.overflow = Overflow.Hidden;
            recipeRow.Add(recipeLabel);

            var recipeNextButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.recipe_next", ">");
            recipeNextButton.style.width = 24f;
            recipeNextButton.style.minWidth = 24f;
            recipeNextButton.style.height = 20f;
            recipeNextButton.clicked += () =>
            {
                CycleProductionRecipeSelection(selectedRef, selected.available_recipes, +1);
                RequestImmediateHudRefresh();
            };
            recipeRow.Add(recipeNextButton);

            var queueButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.recipe_queue", "Q");
            queueButton.style.width = 24f;
            queueButton.style.minWidth = 24f;
            queueButton.style.height = 20f;
            queueButton.style.marginRight = 0f;
            queueButton.tooltip = "Queue selected recipe";
            queueButton.clicked += () =>
            {
                QueueSelectedProductionRecipeForFacility(selectedRef, selected.available_recipes);
                RequestImmediateHudRefresh();
            };
            recipeRow.Add(queueButton);

            var shiftRow = new VisualElement();
            shiftRow.style.flexDirection = FlexDirection.Row;
            shiftRow.style.alignItems = Align.Center;
            shiftRow.style.marginBottom = 2f;
            _productionList.Add(shiftRow);

            var shiftLabel = CreateInventoryLabel($"Shift: S{math.clamp(selected.shift_index, 1, 3)}");
            shiftLabel.style.minWidth = 62f;
            shiftLabel.style.fontSize = Fsi(9);
            shiftRow.Add(shiftLabel);

            for (var shift = 1; shift <= 3; shift++)
            {
                var shiftCapture = shift;
                var shiftButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.shift_{shiftCapture}", $"S{shiftCapture}");
                shiftButton.style.width = 30f;
                shiftButton.style.minWidth = 30f;
                shiftButton.style.height = 20f;
                shiftButton.style.backgroundColor = shiftCapture == selected.shift_index
                    ? new Color(0.29f, 0.48f, 0.62f, 0.98f)
                    : new Color(0.17f, 0.24f, 0.31f, 1f);
                shiftButton.clicked += () =>
                {
                    SetProductionShift(selectedRef, shiftCapture);
                    RequestImmediateHudRefresh();
                };
                shiftRow.Add(shiftButton);
            }

            var powerRow = new VisualElement();
            powerRow.style.flexDirection = FlexDirection.Row;
            powerRow.style.alignItems = Align.Center;
            powerRow.style.marginBottom = 2f;
            _productionList.Add(powerRow);

            var powerDecButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.power_dec", "-");
            powerDecButton.style.width = 24f;
            powerDecButton.style.minWidth = 24f;
            powerDecButton.style.height = 20f;
            powerDecButton.clicked += () =>
            {
                AdjustProductionPowerScale(selectedRef, -ProductionPowerScaleStep);
                RequestImmediateHudRefresh();
            };
            powerRow.Add(powerDecButton);

            var powerLabel = CreateInventoryLabel($"Power: x{selected.power_scale:0.00} ({selected.assigned_power_mw:0.0}/{selected.required_power_mw:0.0} MW)");
            powerLabel.style.flexGrow = 1f;
            powerLabel.style.fontSize = Fsi(9);
            powerLabel.style.whiteSpace = WhiteSpace.NoWrap;
            powerLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            powerLabel.style.overflow = Overflow.Hidden;
            powerRow.Add(powerLabel);

            var powerIncButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.power_inc", "+");
            powerIncButton.style.width = 24f;
            powerIncButton.style.minWidth = 24f;
            powerIncButton.style.height = 20f;
            powerIncButton.style.marginRight = 0f;
            powerIncButton.clicked += () =>
            {
                AdjustProductionPowerScale(selectedRef, ProductionPowerScaleStep);
                RequestImmediateHudRefresh();
            };
            powerRow.Add(powerIncButton);

            var limbRow = new VisualElement();
            limbRow.style.flexDirection = FlexDirection.Row;
            limbRow.style.alignItems = Align.Center;
            limbRow.style.marginBottom = 3f;
            _productionList.Add(limbRow);

            var limbPrevButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.limb_prev", "<");
            limbPrevButton.style.width = 24f;
            limbPrevButton.style.minWidth = 24f;
            limbPrevButton.style.height = 20f;
            limbPrevButton.clicked += () =>
            {
                CycleProductionLimbSelection(selectedRef, selected.available_limbs, -1);
                RequestImmediateHudRefresh();
            };
            limbRow.Add(limbPrevButton);

            var limbLabel = CreateInventoryLabel(
                $"Limb: {(string.IsNullOrWhiteSpace(selected.selected_limb_id) ? "none" : selected.selected_limb_id)}");
            limbLabel.style.flexGrow = 1f;
            limbLabel.style.fontSize = Fsi(9);
            limbLabel.style.whiteSpace = WhiteSpace.NoWrap;
            limbLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            limbLabel.style.overflow = Overflow.Hidden;
            limbRow.Add(limbLabel);

            var limbNextButton = CreateControlButton($"{Space4XInRunHudElementIds.ProductionPanel}.limb_next", ">");
            limbNextButton.style.width = 24f;
            limbNextButton.style.minWidth = 24f;
            limbNextButton.style.height = 20f;
            limbNextButton.style.marginRight = 0f;
            limbNextButton.clicked += () =>
            {
                CycleProductionLimbSelection(selectedRef, selected.available_limbs, +1);
                RequestImmediateHudRefresh();
            };
            limbRow.Add(limbNextButton);

            var detailLabel = CreateInventoryLabel(
                $"Type {selected.business_type}  Cap {selected.capacity:0}  Thr {selected.throughput:0.00}  Seat {selected.seat_fill_01 * 100f:0}%");
            detailLabel.style.fontSize = Fsi(8);
            detailLabel.style.color = new Color(0.72f, 0.84f, 0.91f, 1f);
            detailLabel.style.marginBottom = 2f;
            _productionList.Add(detailLabel);

            var queueHeader = CreateInventoryLabel($"Queue ({selected.queue_count}/{selected.queue_capacity})");
            queueHeader.style.fontSize = Fsi(9);
            queueHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            queueHeader.style.marginBottom = 2f;
            _productionList.Add(queueHeader);

            var queueScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = Space4XInRunHudElementIds.ProductionQueueList
            };
            queueScroll.style.flexGrow = 1f;
            queueScroll.style.minHeight = 90f;
            queueScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            queueScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            queueScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            _productionList.Add(queueScroll);

            var queue = selected.queue ?? Array.Empty<Space4XProductionQueueKernelSnapshot>();
            if (queue.Length <= 0)
            {
                queueScroll.contentContainer.Add(CreateInventoryLabel("No queued entries."));
            }
            else
            {
                for (var i = 0; i < queue.Length; i++)
                {
                    var entry = queue[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var queueRow = new VisualElement();
                    queueRow.style.flexDirection = FlexDirection.Column;
                    queueRow.style.paddingTop = 2f;
                    queueRow.style.paddingBottom = 2f;
                    queueRow.style.paddingLeft = 3f;
                    queueRow.style.paddingRight = 3f;
                    queueRow.style.marginBottom = 1f;
                    queueRow.style.backgroundColor = new Color(0.09f, 0.15f, 0.20f, 0.85f);
                    queueRow.style.borderTopWidth = 1f;
                    queueRow.style.borderRightWidth = 1f;
                    queueRow.style.borderBottomWidth = 1f;
                    queueRow.style.borderLeftWidth = 1f;
                    queueRow.style.borderTopColor = new Color(0.24f, 0.37f, 0.48f, 0.9f);
                    queueRow.style.borderRightColor = new Color(0.24f, 0.37f, 0.48f, 0.9f);
                    queueRow.style.borderBottomColor = new Color(0.24f, 0.37f, 0.48f, 0.9f);
                    queueRow.style.borderLeftColor = new Color(0.24f, 0.37f, 0.48f, 0.9f);

                    var lineA = CreateInventoryLabel($"{entry.recipe_id}  [{entry.state}]  b{entry.batch_count}");
                    lineA.style.fontSize = Fsi(8);
                    lineA.style.unityFontStyleAndWeight = FontStyle.Bold;
                    lineA.style.whiteSpace = WhiteSpace.NoWrap;
                    lineA.style.unityTextOverflowPosition = TextOverflowPosition.End;
                    lineA.style.overflow = Overflow.Hidden;
                    queueRow.Add(lineA);

                    var lineB = CreateInventoryLabel(
                        $"eta {entry.eta_seconds:0.0}s  p{entry.required_power_mw:0.0}MW  c{entry.required_crew:0.0}");
                    lineB.style.fontSize = Fsi(8);
                    lineB.style.color = new Color(0.72f, 0.84f, 0.91f, 1f);
                    queueRow.Add(lineB);

                    queueScroll.contentContainer.Add(queueRow);
                }
            }
        }

        private void SetProductionFacilitySelection(int index, string facilityRef)
        {
            _productionSelectedFacilityIndex = math.max(0, index);
            _productionSelectedFacilityRef = facilityRef ?? string.Empty;
        }

        private void SetProductionShift(string facilityRef, int shiftIndex)
        {
            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return;
            }

            var normalized = math.clamp(shiftIndex, 1, 3);
            _productionShiftByFacility[facilityRef] = normalized;
        }

        private void AdjustProductionPowerScale(string facilityRef, float delta)
        {
            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return;
            }

            var current = ResolveProductionPowerScaleForFacility(facilityRef);
            var adjusted = math.clamp(current + delta, ProductionPowerScaleMin, ProductionPowerScaleMax);
            _productionPowerScaleByFacility[facilityRef] = adjusted;
        }

        private void CycleProductionRecipeSelection(string facilityRef, string[] options, int delta)
        {
            if (string.IsNullOrWhiteSpace(facilityRef) || options == null || options.Length <= 0 || delta == 0)
            {
                return;
            }

            var count = options.Length;
            var index = ResolveProductionSelectionIndex(_productionRecipeIndexByFacility, facilityRef, count);
            index = (index + delta) % count;
            if (index < 0)
            {
                index += count;
            }

            _productionRecipeIndexByFacility[facilityRef] = index;
        }

        private void CycleProductionLimbSelection(string facilityRef, string[] options, int delta)
        {
            if (string.IsNullOrWhiteSpace(facilityRef) || options == null || options.Length <= 0 || delta == 0)
            {
                return;
            }

            var count = options.Length;
            var index = ResolveProductionSelectionIndex(_productionLimbIndexByFacility, facilityRef, count);
            index = (index + delta) % count;
            if (index < 0)
            {
                index += count;
            }

            _productionLimbIndexByFacility[facilityRef] = index;
        }

        private void QueueSelectedProductionRecipeForFacility(string facilityRef, string[] availableRecipes)
        {
            if (string.IsNullOrWhiteSpace(facilityRef))
            {
                return;
            }

            if (availableRecipes == null || availableRecipes.Length <= 0)
            {
                AddNotification("No recipe options for selected facility.");
                return;
            }

            if (!TryResolveEntityManager(out var entityManager))
            {
                AddNotification("Production queue failed: entity manager unavailable.");
                return;
            }

            if (!TryParseEntityRef(facilityRef, out var facility) ||
                facility == Entity.Null ||
                !entityManager.Exists(facility))
            {
                AddNotification("Production queue failed: selected facility not found.");
                return;
            }

            var recipeIndex = ResolveProductionSelectionIndex(_productionRecipeIndexByFacility, facilityRef, availableRecipes.Length);
            var recipeId = availableRecipes[recipeIndex];
            if (!TryQueueProductionRecipe(entityManager, facility, recipeId, out var failureReason))
            {
                AddNotification(string.IsNullOrWhiteSpace(failureReason)
                    ? "Production queue failed."
                    : failureReason);
                return;
            }

            AddNotification($"Queued recipe {recipeId} on {facilityRef}.");
        }

        private bool TryQueueProductionRecipe(EntityManager entityManager, Entity facility, string recipeId, out string failureReason)
        {
            failureReason = string.Empty;
            if (facility == Entity.Null ||
                !entityManager.Exists(facility) ||
                string.IsNullOrWhiteSpace(recipeId))
            {
                failureReason = "Production queue failed: invalid request.";
                return false;
            }

            if (!entityManager.HasBuffer<Space4XProductionQueueEntry>(facility))
            {
                entityManager.AddBuffer<Space4XProductionQueueEntry>(facility);
            }

            var queue = entityManager.GetBuffer<Space4XProductionQueueEntry>(facility);
            var queueCapacity = int.MaxValue;
            if (entityManager.HasComponent<Space4XProductionRuntime>(facility))
            {
                queueCapacity = math.max(1, entityManager.GetComponentData<Space4XProductionRuntime>(facility).QueueCapacity);
            }

            if (queue.Length >= queueCapacity)
            {
                failureReason = $"Production queue full ({queue.Length}/{queueCapacity}).";
                return false;
            }

            var recipeIdFs = new FixedString64Bytes(recipeId.Trim());
            if (entityManager.HasComponent<ProductionJobRequest>(facility))
            {
                entityManager.SetComponentData(facility, new ProductionJobRequest
                {
                    RecipeId = recipeIdFs,
                    Worker = Entity.Null
                });
            }
            else
            {
                entityManager.AddComponentData(facility, new ProductionJobRequest
                {
                    RecipeId = recipeIdFs,
                    Worker = Entity.Null
                });
            }

            var requiredPower = 0f;
            var requiredCrew = 0f;
            if (entityManager.HasComponent<Space4XProductionPowerCrewConstraint>(facility))
            {
                var constraint = entityManager.GetComponentData<Space4XProductionPowerCrewConstraint>(facility);
                requiredPower = math.max(0f, constraint.RequiredPowerMw);
                requiredCrew = math.max(0f, constraint.RequiredCrew);
            }

            var queuedTick = _latestSnapshot.tick;
            var entryId = new FixedString64Bytes($"hud.{facility.Index}.{queuedTick}.{queue.Length + 1}");
            queue.Add(new Space4XProductionQueueEntry
            {
                EntryId = entryId,
                RecipeId = recipeIdFs,
                BlueprintId = default,
                Source = Space4XProductionRequestSource.Mission,
                State = Space4XProductionEntryState.Queued,
                Priority = 1,
                BatchCount = 1,
                EtaSeconds = 0f,
                RequiredPowerMw = requiredPower,
                RequiredCrew = requiredCrew,
                QueuedTick = queuedTick,
                StartedTick = 0u
            });

            if (!entityManager.HasBuffer<Space4XProductionInputLine>(facility))
            {
                entityManager.AddBuffer<Space4XProductionInputLine>(facility);
            }

            if (!entityManager.HasBuffer<Space4XProductionOutputLine>(facility))
            {
                entityManager.AddBuffer<Space4XProductionOutputLine>(facility);
            }

            if (TryResolveProductionRecipeCatalog(entityManager, out var recipeCatalog) &&
                TryResolveProductionRecipe(in recipeCatalog, recipeIdFs, out var recipeIndex))
            {
                ref var recipes = ref recipeCatalog.Catalog.Value.Recipes;
                ref var recipe = ref recipes[recipeIndex];

                var inputs = entityManager.GetBuffer<Space4XProductionInputLine>(facility);
                for (var inputIndex = 0; inputIndex < recipe.Inputs.Length; inputIndex++)
                {
                    ref var input = ref recipe.Inputs[inputIndex];
                    inputs.Add(new Space4XProductionInputLine
                    {
                        EntryId = entryId,
                        ResourceOrPartId = input.ItemId,
                        RequiredAmount = math.max(0f, input.Quantity),
                        ReservedAmount = 0f
                    });
                }

                var outputs = entityManager.GetBuffer<Space4XProductionOutputLine>(facility);
                for (var outputIndex = 0; outputIndex < recipe.Outputs.Length; outputIndex++)
                {
                    ref var output = ref recipe.Outputs[outputIndex];
                    outputs.Add(new Space4XProductionOutputLine
                    {
                        EntryId = entryId,
                        ProductId = output.ItemId,
                        PlannedAmount = math.max(0f, output.Quantity),
                        ProducedAmount = 0f
                    });
                }
            }

            return true;
        }

        private static bool TryResolveProductionRecipe(
            in ProductionRecipeCatalog recipeCatalog,
            in FixedString64Bytes recipeId,
            out int recipeIndex)
        {
            recipeIndex = -1;
            if (!recipeCatalog.Catalog.IsCreated || recipeId.IsEmpty)
            {
                return false;
            }

            ref var recipes = ref recipeCatalog.Catalog.Value.Recipes;
            for (var i = 0; i < recipes.Length; i++)
            {
                ref var candidate = ref recipes[i];
                if (candidate.RecipeId.Equals(recipeId))
                {
                    recipeIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseEntityRef(string entityRef, out Entity entity)
        {
            entity = Entity.Null;
            if (string.IsNullOrWhiteSpace(entityRef))
            {
                return false;
            }

            var parts = entityRef.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var index) || !int.TryParse(parts[1], out var version))
            {
                return false;
            }

            if (index < 0 || version <= 0)
            {
                return false;
            }

            entity = new Entity { Index = index, Version = version };
            return true;
        }

        private void RequestImmediateHudRefresh()
        {
            _nextRefreshAt = 0f;
            RefreshSnapshot();
            ApplySnapshotToUi(_latestSnapshot);
        }

        private VisualElement CreateInventoryModuleCard(string moduleRef, Space4XInventoryModuleKernelSnapshot module)
        {
            var selected = string.Equals(_inventorySelectedModuleRef, moduleRef, StringComparison.Ordinal);
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Column;
            card.style.marginBottom = 2f;
            card.style.paddingTop = 2f;
            card.style.paddingBottom = 2f;
            card.style.paddingLeft = 3f;
            card.style.paddingRight = 3f;
            card.style.backgroundColor = selected
                ? new Color(0.22f, 0.33f, 0.45f, 0.96f)
                : new Color(0.12f, 0.19f, 0.26f, 0.92f);
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            var borderColor = selected
                ? new Color(0.53f, 0.75f, 0.95f, 0.98f)
                : new Color(0.28f, 0.43f, 0.56f, 0.94f);
            card.style.borderTopColor = borderColor;
            card.style.borderRightColor = borderColor;
            card.style.borderBottomColor = borderColor;
            card.style.borderLeftColor = borderColor;

            var headline = CreateInventoryLabel(ResolveInventoryModuleHeadline(module));
            headline.style.fontSize = Fsi(8);
            headline.style.unityFontStyleAndWeight = FontStyle.Bold;
            headline.style.whiteSpace = WhiteSpace.NoWrap;
            headline.style.unityTextOverflowPosition = TextOverflowPosition.End;
            headline.style.marginTop = 0f;
            headline.style.minHeight = 10f;
            card.Add(headline);

            var statLine = CreateInventoryLabel($"HP {module.health_current:0}/{module.health_max:0}  Q {module.quality * 100f:0}%  T{module.tier}");
            statLine.style.fontSize = Fsi(8);
            statLine.style.minHeight = 10f;
            statLine.style.marginTop = 0f;
            statLine.style.color = new Color(0.77f, 0.88f, 0.95f, 0.98f);
            card.Add(statLine);

            var thermalSource = string.IsNullOrWhiteSpace(module.thermal_source) ? "n/a" : module.thermal_source;
            var thermalLine = CreateInventoryLabel(
                $"THERM {module.thermal_current:0.0}/{module.thermal_capacity:0.0} ({module.thermal_ratio * 100f:0}%) {thermalSource}");
            thermalLine.style.fontSize = Fsi(7);
            thermalLine.style.minHeight = 10f;
            thermalLine.style.marginTop = 0f;
            thermalLine.style.color = new Color(0.66f, 0.82f, 0.92f, 0.95f);
            card.Add(thermalLine);

            var organLine = CreateInventoryLabel($"organs {module.organs_count:0}  {module.limb_profile_summary}");
            organLine.style.fontSize = Fsi(7);
            organLine.style.minHeight = 10f;
            organLine.style.marginTop = 0f;
            organLine.style.color = new Color(0.64f, 0.81f, 0.91f, 0.95f);
            card.Add(organLine);

            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _inventoryHoveredModuleRef = moduleRef;
            });

            card.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (string.Equals(_inventoryHoveredModuleRef, moduleRef, StringComparison.Ordinal))
                {
                    _inventoryHoveredModuleRef = string.Empty;
                }
            });

            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt == null)
                {
                    return;
                }

                if (evt.button == (int)MouseButton.LeftMouse)
                {
                    _inventorySelectedModuleRef = moduleRef;
                    ShowInventoryPopup(moduleRef, module, new Vector2(evt.position.x, evt.position.y));
                    evt.StopImmediatePropagation();
                    return;
                }

                if (evt.button == (int)MouseButton.MiddleMouse)
                {
                    _inventorySelectedModuleRef = moduleRef;
                    ShowInventoryPopup(moduleRef, module, new Vector2(evt.position.x, evt.position.y));
                    evt.StopImmediatePropagation();
                }
            });

            return card;
        }

        private static string ResolveInventoryModuleHeadline(Space4XInventoryModuleKernelSnapshot module)
        {
            if (module == null)
            {
                return "Unknown module";
            }

            var moduleClass = string.IsNullOrWhiteSpace(module.module_class) ? "Module" : module.module_class;
            var moduleId = string.IsNullOrWhiteSpace(module.module_id) ? "(empty)" : module.module_id;
            return $"[{module.slot_index:00}] {moduleClass}  {moduleId}";
        }

        private void RefreshInventoryPopups()
        {
            if (_inventoryPopups.Count == 0)
            {
                return;
            }

            _inventoryPopupRemovalScratch.Clear();
            foreach (var pair in _inventoryPopups)
            {
                var moduleRef = pair.Key;
                var popup = pair.Value;
                if (popup?.Panel == null || !_inventoryVisible)
                {
                    _inventoryPopupRemovalScratch.Add(moduleRef);
                    continue;
                }

                if (!_inventoryModuleLookup.TryGetValue(moduleRef, out var module) || module == null)
                {
                    _inventoryPopupRemovalScratch.Add(moduleRef);
                    continue;
                }

                if (popup.TitleLabel != null)
                {
                    popup.TitleLabel.text = ResolveInventoryModuleHeadline(module);
                }

                PopulateInventoryPopupContent(popup, module);
                ApplyInventoryPopupPosition(popup, clampToViewport: true);
            }

            for (var i = 0; i < _inventoryPopupRemovalScratch.Count; i++)
            {
                CloseInventoryPopup(_inventoryPopupRemovalScratch[i]);
            }

            _inventoryPopupRemovalScratch.Clear();
        }

        private void CloseInventoryPopup(string moduleRef)
        {
            if (string.IsNullOrWhiteSpace(moduleRef))
            {
                return;
            }

            if (!_inventoryPopups.TryGetValue(moduleRef, out var popup))
            {
                return;
            }

            _inventoryPopups.Remove(moduleRef);
            ReleaseInventoryPopupPointerCapture(popup);
            if (popup?.Panel != null && popup.Panel.parent != null)
            {
                popup.Panel.parent.Remove(popup.Panel);
            }

            _inventoryOrganOverrideRemovalScratch.Clear();
            foreach (var key in _inventoryOrganIntegrityOverrides.Keys)
            {
                if (key.StartsWith($"{moduleRef}#", StringComparison.Ordinal))
                {
                    _inventoryOrganOverrideRemovalScratch.Add(key);
                }
            }

            for (var i = 0; i < _inventoryOrganOverrideRemovalScratch.Count; i++)
            {
                _inventoryOrganIntegrityOverrides.Remove(_inventoryOrganOverrideRemovalScratch[i]);
            }

            _inventoryOrganOverrideRemovalScratch.Clear();
        }

        private void CloseAllInventoryPopups()
        {
            if (_inventoryPopups.Count == 0)
            {
                return;
            }

            _inventoryPopupRemovalScratch.Clear();
            foreach (var pair in _inventoryPopups)
            {
                _inventoryPopupRemovalScratch.Add(pair.Key);
            }

            for (var i = 0; i < _inventoryPopupRemovalScratch.Count; i++)
            {
                CloseInventoryPopup(_inventoryPopupRemovalScratch[i]);
            }

            _inventoryPopupRemovalScratch.Clear();
            _inventoryHoveredModuleRef = string.Empty;
        }

        private void ShowInventoryPopup(string moduleRef, Space4XInventoryModuleKernelSnapshot module, Vector2 pointerPosition)
        {
            if (string.IsNullOrWhiteSpace(moduleRef) || module == null || _root == null)
            {
                return;
            }

            if (_inventoryPopups.TryGetValue(moduleRef, out var existing) && existing != null)
            {
                existing.Position = pointerPosition + new Vector2(14f, -10f);
                ApplyInventoryPopupPosition(existing, clampToViewport: true);
                existing.Panel?.BringToFront();
                PopulateInventoryPopupContent(existing, module);
                return;
            }

            var popup = new InventoryPopupRuntime
            {
                ModuleRef = moduleRef,
                Position = pointerPosition + new Vector2(14f, -10f)
            };

            var panel = new VisualElement
            {
                name = $"{Space4XInRunHudElementIds.InventoryPopupPrefix}.{_inventoryPopups.Count + 1}"
            };
            panel.style.position = Position.Absolute;
            panel.style.left = popup.Position.x;
            panel.style.top = popup.Position.y;
            panel.style.width = 300f;
            panel.style.height = 228f;
            panel.style.paddingTop = 4f;
            panel.style.paddingBottom = 4f;
            panel.style.paddingLeft = 4f;
            panel.style.paddingRight = 4f;
            panel.style.backgroundColor = new Color(0.05f, 0.10f, 0.15f, 0.95f);
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderTopColor = new Color(0.35f, 0.56f, 0.73f, 0.96f);
            panel.style.borderRightColor = new Color(0.35f, 0.56f, 0.73f, 0.96f);
            panel.style.borderBottomColor = new Color(0.35f, 0.56f, 0.73f, 0.96f);
            panel.style.borderLeftColor = new Color(0.35f, 0.56f, 0.73f, 0.96f);
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.overflow = Overflow.Hidden;
            panel.focusable = false;
            popup.Panel = panel;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3f;
            header.style.paddingBottom = 2f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new Color(0.26f, 0.40f, 0.52f, 0.9f);
            popup.DragHandle = header;

            var title = new Label(ResolveInventoryModuleHeadline(module));
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                title.style.unityFont = runtimeFont;
            }

            title.style.flexGrow = 1f;
            title.style.fontSize = Fsi(10);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.91f, 0.97f, 1f, 1f);
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.unityTextOverflowPosition = TextOverflowPosition.End;
            popup.TitleLabel = title;
            header.Add(title);

            var closeButton = new Button(() => CloseInventoryPopup(moduleRef))
            {
                text = "X"
            };
            if (runtimeFont != null)
            {
                closeButton.style.unityFont = runtimeFont;
            }

            closeButton.style.width = 22f;
            closeButton.style.height = 18f;
            closeButton.style.marginLeft = 4f;
            closeButton.style.backgroundColor = new Color(0.25f, 0.16f, 0.19f, 0.95f);
            closeButton.style.color = new Color(0.98f, 0.92f, 0.92f, 1f);
            closeButton.style.fontSize = Fsi(9);
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeButton.focusable = false;
            closeButton.tabIndex = -1;
            header.Add(closeButton);
            panel.Add(header);

            var contentScroll = new ScrollView(ScrollViewMode.Vertical);
            contentScroll.style.flexGrow = 1f;
            contentScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            contentScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            contentScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            contentScroll.contentContainer.style.paddingRight = 2f;
            panel.Add(contentScroll);
            popup.Content = contentScroll.contentContainer;
            PopulateInventoryPopupContent(popup, module);

            header.RegisterCallback<PointerDownEvent>(evt => OnInventoryPopupDragPointerDown(popup, evt), TrickleDown.NoTrickleDown);
            header.RegisterCallback<PointerMoveEvent>(evt => OnInventoryPopupDragPointerMove(popup, evt), TrickleDown.NoTrickleDown);
            header.RegisterCallback<PointerUpEvent>(evt => OnInventoryPopupDragPointerUp(popup, evt), TrickleDown.NoTrickleDown);
            header.RegisterCallback<PointerCaptureOutEvent>(evt => OnInventoryPopupDragPointerCaptureOut(popup, evt), TrickleDown.NoTrickleDown);

            _root.Add(panel);
            panel.BringToFront();
            _inventoryPopups[moduleRef] = popup;
            ApplyInventoryPopupPosition(popup, clampToViewport: true);
        }

        private static Label CreateInventoryPopupLine(string text, Color color, bool bold = false)
        {
            var label = new Label(text);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(9);
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.marginTop = 0f;
            label.style.marginBottom = 1f;
            label.style.minHeight = 11f;
            return label;
        }

        private void PopulateInventoryPopupContent(InventoryPopupRuntime popup, Space4XInventoryModuleKernelSnapshot module)
        {
            if (popup?.Content == null || module == null)
            {
                return;
            }

            var content = popup.Content;
            var moduleRef = popup.ModuleRef ?? string.Empty;
            content.Clear();
            content.Add(CreateInventoryPopupLine($"slot {module.slot_index:00}  state {module.slot_state}  size {module.slot_size}", new Color(0.80f, 0.91f, 0.98f, 1f), bold: true));
            content.Add(CreateInventoryPopupLine($"class {module.module_class}  id {module.module_id}", new Color(0.74f, 0.88f, 0.96f, 1f)));
            content.Add(CreateInventoryPopupLine($"mount {module.mount}  mass {module.mass_tons:0.0}t", new Color(0.74f, 0.84f, 0.93f, 1f)));
            content.Add(CreateInventoryPopupLine($"power draw {module.power_draw_mw:0.0} MW", new Color(0.74f, 0.84f, 0.93f, 1f)));
            content.Add(CreateInventoryPopupLine(
                $"thermal {module.thermal_current:0.0}/{module.thermal_capacity:0.0} ({module.thermal_ratio * 100f:0}%) {module.thermal_source}",
                new Color(0.74f, 0.84f, 0.93f, 1f)));
            content.Add(CreateInventoryPopupLine($"health {module.health_current:0.0}/{module.health_max:0.0} ({module.health_ratio * 100f:0}%)", new Color(0.92f, 0.95f, 0.98f, 1f)));
            content.Add(CreateInventoryPopupLine($"quality {module.quality * 100f:0}%  tier {module.tier}", new Color(0.90f, 0.95f, 1f, 1f)));
            if (!string.IsNullOrWhiteSpace(module.manufacturer_id))
            {
                content.Add(CreateInventoryPopupLine($"maker {module.manufacturer_id}", new Color(0.72f, 0.85f, 0.95f, 1f)));
            }

            if (!string.IsNullOrWhiteSpace(module.limb_profile_summary))
            {
                content.Add(CreateInventoryPopupLine($"profile {module.limb_profile_summary}", new Color(0.68f, 0.82f, 0.92f, 1f)));
            }

            content.Add(CreateInventoryPopupLine("Organs (click +/- to tune kernel preview)", new Color(0.86f, 0.93f, 0.99f, 1f), bold: true));
            var organs = module.organs ?? Array.Empty<Space4XInventoryLimbKernelSnapshot>();
            if (organs.Length == 0)
            {
                content.Add(CreateInventoryPopupLine("No limb/organ entries.", new Color(0.66f, 0.78f, 0.88f, 0.95f)));
                return;
            }

            for (var i = 0; i < organs.Length; i++)
            {
                var organ = organs[i];
                if (organ == null)
                {
                    continue;
                }

                var family = string.IsNullOrWhiteSpace(organ.family) ? "Organ" : organ.family;
                var limbId = string.IsNullOrWhiteSpace(organ.limb_id) ? "limb" : organ.limb_id;
                var integrity = ResolveInventoryOrganIntegrity(moduleRef, i, organ.integrity);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 1f;

                var organLabel = CreateInventoryPopupLine(
                    $"{family} [{limbId}]  exp {math.saturate(organ.exposure) * 100f:0}%",
                    new Color(0.72f, 0.85f, 0.95f, 0.98f));
                organLabel.style.fontSize = Fsi(8);
                organLabel.style.flexGrow = 1f;
                row.Add(organLabel);

                var integrityLabel = CreateInventoryPopupLine($"{math.saturate(integrity) * 100f:0}%", new Color(0.92f, 0.97f, 1f, 1f), bold: true);
                integrityLabel.style.minWidth = 38f;
                integrityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.Add(integrityLabel);

                var decButton = CreateInventoryAdjustButton("-");
                var organIndex = i;
                decButton.clicked += () =>
                {
                    ApplyInventoryOrganIntegrityOverride(moduleRef, organIndex, integrity - 0.05f);
                    PopulateInventoryPopupContent(popup, module);
                };
                row.Add(decButton);

                var incButton = CreateInventoryAdjustButton("+");
                incButton.clicked += () =>
                {
                    ApplyInventoryOrganIntegrityOverride(moduleRef, organIndex, integrity + 0.05f);
                    PopulateInventoryPopupContent(popup, module);
                };
                row.Add(incButton);

                content.Add(row);
            }
        }

        private static Button CreateInventoryAdjustButton(string text)
        {
            var button = new Button
            {
                text = text
            };

            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                button.style.unityFont = runtimeFont;
            }

            button.style.width = 16f;
            button.style.height = 14f;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.fontSize = Fsi(9);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.18f, 0.27f, 0.36f, 0.96f);
            button.style.color = new Color(0.91f, 0.97f, 1f, 1f);
            button.focusable = false;
            button.tabIndex = -1;
            return button;
        }

        private static string BuildInventoryOrganOverrideKey(string moduleRef, int organIndex)
        {
            return $"{moduleRef}#{organIndex:00}";
        }

        private float ResolveInventoryOrganIntegrity(string moduleRef, int organIndex, float fallback)
        {
            var key = BuildInventoryOrganOverrideKey(moduleRef, organIndex);
            if (_inventoryOrganIntegrityOverrides.TryGetValue(key, out var overrideValue))
            {
                return math.saturate(overrideValue);
            }

            return math.saturate(fallback);
        }

        private void ApplyInventoryOrganIntegrityOverride(string moduleRef, int organIndex, float value)
        {
            if (string.IsNullOrWhiteSpace(moduleRef))
            {
                return;
            }

            var key = BuildInventoryOrganOverrideKey(moduleRef, organIndex);
            _inventoryOrganIntegrityOverrides[key] = math.saturate(value);
        }

        private void OnInventoryPopupDragPointerDown(InventoryPopupRuntime popup, PointerDownEvent evt)
        {
            if (popup?.DragHandle == null || evt == null || evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            popup.DragActive = true;
            popup.PointerId = evt.pointerId;
            popup.DragStartPointer = new Vector2(evt.position.x, evt.position.y);
            popup.DragStartPosition = popup.Position;
            popup.DragHandle.CapturePointer(evt.pointerId);
            popup.Panel?.BringToFront();
            evt.StopImmediatePropagation();
        }

        private void OnInventoryPopupDragPointerMove(InventoryPopupRuntime popup, PointerMoveEvent evt)
        {
            if (popup == null || !popup.DragActive || evt == null || evt.pointerId != popup.PointerId)
            {
                return;
            }

            var pointer = new Vector2(evt.position.x, evt.position.y);
            popup.Position = popup.DragStartPosition + (pointer - popup.DragStartPointer);
            ApplyInventoryPopupPosition(popup, clampToViewport: true);
            evt.StopImmediatePropagation();
        }

        private void OnInventoryPopupDragPointerUp(InventoryPopupRuntime popup, PointerUpEvent evt)
        {
            if (popup == null || !popup.DragActive || evt == null || evt.pointerId != popup.PointerId)
            {
                return;
            }

            ReleaseInventoryPopupPointerCapture(popup);
            evt.StopImmediatePropagation();
        }

        private void OnInventoryPopupDragPointerCaptureOut(InventoryPopupRuntime popup, PointerCaptureOutEvent evt)
        {
            if (popup == null || !popup.DragActive || evt == null || evt.pointerId != popup.PointerId)
            {
                return;
            }

            popup.DragActive = false;
            popup.PointerId = -1;
        }

        private static void ReleaseInventoryPopupPointerCapture(InventoryPopupRuntime popup)
        {
            if (popup?.DragHandle != null && popup.PointerId >= 0 && popup.DragHandle.HasPointerCapture(popup.PointerId))
            {
                popup.DragHandle.ReleasePointer(popup.PointerId);
            }

            if (popup != null)
            {
                popup.DragActive = false;
                popup.PointerId = -1;
            }
        }

        private void ApplyInventoryPopupPosition(InventoryPopupRuntime popup, bool clampToViewport)
        {
            if (popup?.Panel == null)
            {
                return;
            }

            var position = popup.Position;
            const float fallbackWidth = 300f;
            const float fallbackHeight = 228f;
            var width = popup.Panel.resolvedStyle.width > 1f ? popup.Panel.resolvedStyle.width : fallbackWidth;
            var height = popup.Panel.resolvedStyle.height > 1f ? popup.Panel.resolvedStyle.height : fallbackHeight;
            if (clampToViewport && _root != null)
            {
                var rootWidth = _root.resolvedStyle.width;
                var rootHeight = _root.resolvedStyle.height;
                if (rootWidth > 1f)
                {
                    position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, rootWidth - width));
                }

                if (rootHeight > 1f)
                {
                    position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, rootHeight - height));
                }
            }

            popup.Position = position;
            popup.Panel.style.left = position.x;
            popup.Panel.style.top = position.y;
            popup.Panel.style.right = StyleKeyword.Auto;
            popup.Panel.style.bottom = StyleKeyword.Auto;
        }

        private Button CreateNotificationTileButton(string text)
        {
            var button = new Button
            {
                text = text
            };

            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                button.style.unityFont = runtimeFont;
            }

            button.style.minHeight = 24f;
            button.style.height = 24f;
            button.style.paddingLeft = 4f;
            button.style.paddingRight = 4f;
            button.style.paddingTop = 1f;
            button.style.paddingBottom = 1f;
            button.style.marginTop = 1f;
            button.style.marginBottom = 1f;
            button.style.backgroundColor = new Color(0.13f, 0.19f, 0.25f, 0.96f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            var borderColor = new Color(0.32f, 0.47f, 0.62f, 1f);
            button.style.borderTopColor = borderColor;
            button.style.borderRightColor = borderColor;
            button.style.borderBottomColor = borderColor;
            button.style.borderLeftColor = borderColor;
            button.style.color = new Color(0.90f, 0.96f, 1f, 1f);
            button.style.fontSize = Fsi(9);
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.unityTextOverflowPosition = TextOverflowPosition.End;
            button.style.overflow = Overflow.Hidden;
            button.style.flexShrink = 0f;
            button.focusable = false;
            button.tabIndex = -1;
            button.clicked += () => HandleNotificationClicked(text);
            return button;
        }

        private void HandleNotificationClicked(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            _activeContextNotification = entry.Trim();
            _inventoryVisible = true;

            if (LooksLikeTacticalNotification(entry))
            {
                _minimapVisible = true;
            }
        }

        private static bool LooksLikeTacticalNotification(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return false;
            }

            var lower = entry.ToLowerInvariant();
            return lower.Contains("hostile") ||
                   lower.Contains("enemy") ||
                   lower.Contains("contact") ||
                   lower.Contains("minimap") ||
                   lower.Contains("combat");
        }

        private VisualElement CreatePowerRoutingSliderRow(
            string shortName,
            out Slider slider,
            out Label valueLabel,
            EventCallback<ChangeEvent<float>> callback)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 1f;
            row.style.marginBottom = 1f;

            var nameLabel = new Label(shortName);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                nameLabel.style.unityFont = runtimeFont;
            }

            nameLabel.style.minWidth = 28f;
            nameLabel.style.fontSize = Fsi(8);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.78f, 0.89f, 0.97f, 1f);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(nameLabel);

            slider = new Slider(PowerRoutingMinPercent, PowerRoutingMaxPercent)
            {
                pageSize = PowerRoutingStepPercent
            };
            slider.style.flexGrow = 1f;
            slider.style.height = 12f;
            slider.style.marginLeft = 2f;
            slider.style.marginRight = 4f;
            slider.style.unityBackgroundImageTintColor = new Color(0.33f, 0.56f, 0.72f, 0.95f);
            slider.focusable = false;
            slider.tabIndex = -1;
            slider.RegisterValueChangedCallback(callback);
            row.Add(slider);

            valueLabel = new Label($"{PowerRoutingDefaultPercent:0}%");
            if (runtimeFont != null)
            {
                valueLabel.style.unityFont = runtimeFont;
            }

            valueLabel.style.minWidth = 38f;
            valueLabel.style.fontSize = Fsi(8);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new Color(0.91f, 0.97f, 1f, 1f);
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            return row;
        }

        private void OnPowerRoutingEnginesChanged(ChangeEvent<float> evt)
        {
            OnPowerRoutingSliderChanged(PowerRoutingDomain.Engines, evt?.newValue ?? PowerRoutingDefaultPercent);
        }

        private void OnPowerRoutingWeaponsChanged(ChangeEvent<float> evt)
        {
            OnPowerRoutingSliderChanged(PowerRoutingDomain.Weapons, evt?.newValue ?? PowerRoutingDefaultPercent);
        }

        private void OnPowerRoutingShieldsChanged(ChangeEvent<float> evt)
        {
            OnPowerRoutingSliderChanged(PowerRoutingDomain.Shields, evt?.newValue ?? PowerRoutingDefaultPercent);
        }

        private void OnPowerRoutingReactorChanged(ChangeEvent<float> evt)
        {
            OnPowerRoutingSliderChanged(PowerRoutingDomain.Reactor, evt?.newValue ?? PowerRoutingDefaultPercent);
        }

        private void OnPowerRoutingSensorsChanged(ChangeEvent<float> evt)
        {
            OnPowerRoutingSliderChanged(PowerRoutingDomain.Sensors, evt?.newValue ?? PowerRoutingDefaultPercent);
        }

        private void OnPowerRoutingSliderChanged(PowerRoutingDomain domain, float value)
        {
            if (_powerRoutingUiSuppress)
            {
                return;
            }

            var profile = GetActivePowerRoutingProfile();
            var snapped = SnapPowerRoutingPercent(value);
            switch (domain)
            {
                case PowerRoutingDomain.Engines:
                    profile.engines = snapped;
                    break;
                case PowerRoutingDomain.Weapons:
                    profile.weapons = snapped;
                    break;
                case PowerRoutingDomain.Shields:
                    profile.shields = snapped;
                    break;
                case PowerRoutingDomain.Reactor:
                    profile.reactor = snapped;
                    break;
                case PowerRoutingDomain.Sensors:
                    profile.sensors = snapped;
                    break;
            }

            _powerRoutingProfiles[_powerRoutingActiveProfile] = NormalizePowerRoutingProfile(profile);
            ApplyPowerRoutingProfileToControls();
            SavePowerRoutingPreferences();
            TryApplyPowerRoutingProfileToCurrentFlagship();
        }

        private void SetActivePowerRoutingProfile(int profileIndex, bool emitNotification)
        {
            var clampedIndex = Mathf.Clamp(profileIndex, 0, PowerRoutingProfileCount - 1);
            if (_powerRoutingActiveProfile == clampedIndex)
            {
                return;
            }

            CapturePowerRoutingProfileFromControls();
            _powerRoutingActiveProfile = clampedIndex;
            ApplyPowerRoutingProfileToControls();
            UpdatePowerRoutingProfileButtonStyles();
            SavePowerRoutingPreferences();
            TryApplyPowerRoutingProfileToCurrentFlagship();
            if (emitNotification)
            {
                AddNotification($"Power routing profile P{_powerRoutingActiveProfile + 1} active.");
            }
        }

        private void ResetActivePowerRoutingProfileToDefaults()
        {
            _powerRoutingProfiles[_powerRoutingActiveProfile] = CreateDefaultPowerRoutingProfile();
            ApplyPowerRoutingProfileToControls();
            UpdatePowerRoutingProfileButtonStyles();
            SavePowerRoutingPreferences();
            TryApplyPowerRoutingProfileToCurrentFlagship();
            AddNotification($"Power routing P{_powerRoutingActiveProfile + 1} reset to 100%.");
        }

        private void CapturePowerRoutingProfileFromControls()
        {
            if (_powerRoutingEnginesSlider == null ||
                _powerRoutingWeaponsSlider == null ||
                _powerRoutingShieldsSlider == null ||
                _powerRoutingReactorSlider == null ||
                _powerRoutingSensorsSlider == null)
            {
                return;
            }

            _powerRoutingProfiles[_powerRoutingActiveProfile] = NormalizePowerRoutingProfile(new PowerRoutingProfileSavedState
            {
                engines = _powerRoutingEnginesSlider.value,
                weapons = _powerRoutingWeaponsSlider.value,
                shields = _powerRoutingShieldsSlider.value,
                reactor = _powerRoutingReactorSlider.value,
                sensors = _powerRoutingSensorsSlider.value
            });
        }

        private void ApplyPowerRoutingProfileToControls()
        {
            var profile = GetActivePowerRoutingProfile();
            _powerRoutingUiSuppress = true;
            if (_powerRoutingEnginesSlider != null)
            {
                _powerRoutingEnginesSlider.SetValueWithoutNotify(profile.engines);
            }

            if (_powerRoutingWeaponsSlider != null)
            {
                _powerRoutingWeaponsSlider.SetValueWithoutNotify(profile.weapons);
            }

            if (_powerRoutingShieldsSlider != null)
            {
                _powerRoutingShieldsSlider.SetValueWithoutNotify(profile.shields);
            }

            if (_powerRoutingReactorSlider != null)
            {
                _powerRoutingReactorSlider.SetValueWithoutNotify(profile.reactor);
            }

            if (_powerRoutingSensorsSlider != null)
            {
                _powerRoutingSensorsSlider.SetValueWithoutNotify(profile.sensors);
            }

            _powerRoutingUiSuppress = false;
            UpdatePowerRoutingValueLabels(profile);
            UpdatePowerRoutingProfileButtonStyles();
        }

        private void UpdatePowerRoutingProfileButtonStyles()
        {
            if (_powerRoutingProfileLabel != null)
            {
                _powerRoutingProfileLabel.text = $"P{_powerRoutingActiveProfile + 1}";
            }

            for (var i = 0; i < _powerRoutingProfileButtons.Length; i++)
            {
                var button = _powerRoutingProfileButtons[i];
                if (button == null)
                {
                    continue;
                }

                var active = i == _powerRoutingActiveProfile;
                button.style.backgroundColor = active
                    ? new Color(0.29f, 0.48f, 0.62f, 0.98f)
                    : new Color(0.18f, 0.27f, 0.36f, 0.96f);
                button.style.color = active
                    ? new Color(0.96f, 0.99f, 1f, 1f)
                    : new Color(0.86f, 0.93f, 0.98f, 1f);
                button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void UpdatePowerRoutingValueLabels(PowerRoutingProfileSavedState profile)
        {
            var normalized = NormalizePowerRoutingProfile(profile);
            if (_powerRoutingEnginesValueLabel != null)
            {
                _powerRoutingEnginesValueLabel.text = $"{normalized.engines:0}%";
            }

            if (_powerRoutingWeaponsValueLabel != null)
            {
                _powerRoutingWeaponsValueLabel.text = $"{normalized.weapons:0}%";
            }

            if (_powerRoutingShieldsValueLabel != null)
            {
                _powerRoutingShieldsValueLabel.text = $"{normalized.shields:0}%";
            }

            if (_powerRoutingReactorValueLabel != null)
            {
                _powerRoutingReactorValueLabel.text = $"{normalized.reactor:0}%";
            }

            if (_powerRoutingSensorsValueLabel != null)
            {
                _powerRoutingSensorsValueLabel.text = $"{normalized.sensors:0}%";
            }
        }

        private void UpdatePowerRoutingValueLabels(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (_powerRoutingEnginesValueLabel != null)
            {
                _powerRoutingEnginesValueLabel.text = $"{snapshot.power_route_engines_pct:0}%";
            }

            if (_powerRoutingWeaponsValueLabel != null)
            {
                _powerRoutingWeaponsValueLabel.text = $"{snapshot.power_route_weapons_pct:0}%";
            }

            if (_powerRoutingShieldsValueLabel != null)
            {
                _powerRoutingShieldsValueLabel.text = $"{snapshot.power_route_shields_pct:0}%";
            }

            if (_powerRoutingReactorValueLabel != null)
            {
                _powerRoutingReactorValueLabel.text = $"{snapshot.power_route_reactor_pct:0}%";
            }

            if (_powerRoutingSensorsValueLabel != null)
            {
                _powerRoutingSensorsValueLabel.text = $"{snapshot.power_route_sensors_pct:0}%";
            }
        }

        private static float SnapPowerRoutingPercent(float value)
        {
            var clamped = math.clamp(value, PowerRoutingMinPercent, PowerRoutingMaxPercent);
            var stepped = math.round(clamped / PowerRoutingStepPercent) * PowerRoutingStepPercent;
            return math.clamp(stepped, PowerRoutingMinPercent, PowerRoutingMaxPercent);
        }

        private static PowerRoutingProfileSavedState CreateDefaultPowerRoutingProfile()
        {
            return new PowerRoutingProfileSavedState
            {
                engines = PowerRoutingDefaultPercent,
                weapons = PowerRoutingDefaultPercent,
                shields = PowerRoutingDefaultPercent,
                reactor = PowerRoutingDefaultPercent,
                sensors = PowerRoutingDefaultPercent
            };
        }

        private static PowerRoutingProfileSavedState NormalizePowerRoutingProfile(PowerRoutingProfileSavedState source)
        {
            var profile = source ?? CreateDefaultPowerRoutingProfile();
            profile.engines = SnapPowerRoutingPercent(profile.engines);
            profile.weapons = SnapPowerRoutingPercent(profile.weapons);
            profile.shields = SnapPowerRoutingPercent(profile.shields);
            profile.reactor = SnapPowerRoutingPercent(profile.reactor);
            profile.sensors = SnapPowerRoutingPercent(profile.sensors);
            return profile;
        }

        private PowerRoutingProfileSavedState GetActivePowerRoutingProfile()
        {
            var index = Mathf.Clamp(_powerRoutingActiveProfile, 0, PowerRoutingProfileCount - 1);
            if (_powerRoutingProfiles[index] == null)
            {
                _powerRoutingProfiles[index] = CreateDefaultPowerRoutingProfile();
            }

            return NormalizePowerRoutingProfile(_powerRoutingProfiles[index]);
        }

        private static ShipPowerRoutingProfile BuildPowerRoutingProfileComponent(PowerRoutingProfileSavedState profile)
        {
            var normalized = NormalizePowerRoutingProfile(profile);
            return new ShipPowerRoutingProfile
            {
                EnginesPercent = normalized.engines,
                WeaponsPercent = normalized.weapons,
                ShieldsPercent = normalized.shields,
                ReactorPercent = normalized.reactor,
                SensorsPercent = normalized.sensors
            };
        }

        private void LoadPowerRoutingPreferences()
        {
            _powerRoutingActiveProfile = 0;
            for (var i = 0; i < PowerRoutingProfileCount; i++)
            {
                _powerRoutingProfiles[i] = CreateDefaultPowerRoutingProfile();
            }

            if (!PlayerPrefs.HasKey(PowerRoutingPrefsKey))
            {
                return;
            }

            var json = PlayerPrefs.GetString(PowerRoutingPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var blob = JsonUtility.FromJson<PowerRoutingPrefsBlob>(json);
                if (blob == null)
                {
                    return;
                }

                _powerRoutingActiveProfile = Mathf.Clamp(blob.active_profile, 0, PowerRoutingProfileCount - 1);
                var profiles = blob.profiles ?? Array.Empty<PowerRoutingProfileSavedState>();
                for (var i = 0; i < PowerRoutingProfileCount; i++)
                {
                    var saved = i < profiles.Length ? profiles[i] : null;
                    _powerRoutingProfiles[i] = NormalizePowerRoutingProfile(saved);
                }
            }
            catch
            {
                for (var i = 0; i < PowerRoutingProfileCount; i++)
                {
                    _powerRoutingProfiles[i] = CreateDefaultPowerRoutingProfile();
                }

                _powerRoutingActiveProfile = 0;
            }
        }

        private void SavePowerRoutingPreferences()
        {
            var blob = new PowerRoutingPrefsBlob
            {
                version = PowerRoutingPrefsSchemaVersion,
                active_profile = Mathf.Clamp(_powerRoutingActiveProfile, 0, PowerRoutingProfileCount - 1),
                profiles = new PowerRoutingProfileSavedState[PowerRoutingProfileCount]
            };

            for (var i = 0; i < PowerRoutingProfileCount; i++)
            {
                blob.profiles[i] = NormalizePowerRoutingProfile(_powerRoutingProfiles[i]);
            }

            var json = JsonUtility.ToJson(blob);
            PlayerPrefs.SetString(PowerRoutingPrefsKey, json);
            PlayerPrefs.Save();
        }

        private void TryApplyPowerRoutingProfileToCurrentFlagship()
        {
            if (!TryResolveEntityManager(out var entityManager))
            {
                return;
            }

            var flagship = Entity.Null;
            if (TryResolveFlagshipEntity(out var resolvedFlagship, out var resolvedManager))
            {
                entityManager = resolvedManager;
                flagship = resolvedFlagship;
            }

            ApplyPowerRoutingProfileToRuntimeState(entityManager, flagship);
        }

        private static VisualElement CreateBarGroup(string title)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Column;
            group.style.paddingTop = 1f;
            group.style.paddingBottom = 1f;
            group.style.paddingLeft = 3f;
            group.style.paddingRight = 3f;
            group.style.marginBottom = 2f;
            group.style.flexShrink = 0f;
            group.style.backgroundColor = new Color(0.05f, 0.09f, 0.12f, 0.72f);
            group.style.borderTopWidth = 1f;
            group.style.borderRightWidth = 1f;
            group.style.borderBottomWidth = 1f;
            group.style.borderLeftWidth = 1f;
            var border = new Color(0.24f, 0.35f, 0.44f, 0.9f);
            group.style.borderTopColor = border;
            group.style.borderRightColor = border;
            group.style.borderBottomColor = border;
            group.style.borderLeftColor = border;

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLabel = new Label(title);
                var runtimeFont = ResolveRuntimeFont();
                if (runtimeFont != null)
                {
                    titleLabel.style.unityFont = runtimeFont;
                }

                titleLabel.style.fontSize = Fsi(9);
                titleLabel.style.color = new Color(0.70f, 0.78f, 0.85f, 0.95f);
                titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                titleLabel.style.minHeight = 10f;
                group.Add(titleLabel);
            }

            return group;
        }

        private VisualElement CreateMetricBar(string shortName, string valueId, Color fillColor, out VisualElement fill, out Label valueLabel)
        {
            var barTrack = new VisualElement();
            barTrack.style.height = StatusBarHeight;
            barTrack.style.minHeight = StatusBarHeight;
            barTrack.style.maxHeight = StatusBarHeight;
            barTrack.style.marginTop = 1f;
            barTrack.style.flexShrink = 0f;
            barTrack.style.position = Position.Relative;
            barTrack.style.backgroundColor = new Color(0.11f, 0.15f, 0.18f, 0.94f);
            barTrack.style.borderTopWidth = 1f;
            barTrack.style.borderRightWidth = 1f;
            barTrack.style.borderBottomWidth = 1f;
            barTrack.style.borderLeftWidth = 1f;
            barTrack.style.borderTopColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            barTrack.style.borderRightColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            barTrack.style.borderBottomColor = new Color(0.19f, 0.27f, 0.35f, 1f);
            barTrack.style.borderLeftColor = new Color(0.19f, 0.27f, 0.35f, 1f);

            fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0f;
            fill.style.top = 0f;
            fill.style.bottom = 0f;
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = fillColor;
            barTrack.Add(fill);

            valueLabel = CreateMetricLabel(valueId, $"{shortName} 0/0");
            valueLabel.style.position = Position.Absolute;
            valueLabel.style.left = 0f;
            valueLabel.style.right = 0f;
            valueLabel.style.top = 0f;
            valueLabel.style.bottom = 0f;
            valueLabel.style.marginTop = 0f;
            valueLabel.style.minHeight = StatusBarHeight;
            valueLabel.style.fontSize = Fsi(6);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            valueLabel.style.color = new Color(0.02f, 0.03f, 0.04f, 0.96f);
            barTrack.Add(valueLabel);

            return barTrack;
        }

        private static void UpdateMetricBar(VisualElement fill, Label valueLabel, string shortName, float current, float max, float ratio)
        {
            if (fill != null)
            {
                fill.style.width = Length.Percent(math.saturate(ratio) * 100f);
            }

            if (valueLabel != null)
            {
                valueLabel.text = $"{shortName} {current:0}/{max:0}";
            }
        }

        private static Label CreateHeaderLabel(string text)
        {
            var label = new Label(text);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(14);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.93f, 0.97f, 1f, 1f);
            label.style.minHeight = 16f;
            return label;
        }

        private static Label CreateMetricLabel(string name, string text)
        {
            var label = new Label(text) { name = name };
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(12);
            label.style.color = new Color(0.84f, 0.91f, 0.96f, 1f);
            label.style.marginTop = 1f;
            label.style.minHeight = 14f;
            return label;
        }

        private static Label CreateNotificationLabel(string text)
        {
            var label = new Label(text);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(11);
            label.style.color = new Color(0.82f, 0.89f, 0.95f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.minHeight = 13f;
            return label;
        }

        private static Label CreateInventoryLabel(string text)
        {
            var label = new Label(text);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(11);
            label.style.color = new Color(0.83f, 0.90f, 0.95f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.minHeight = 13f;
            return label;
        }

        private static Label CreateMinimapContactLabel(string text, Color color)
        {
            var label = new Label(text);
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                label.style.unityFont = runtimeFont;
            }

            label.style.fontSize = Fsi(10);
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.minHeight = 12f;
            return label;
        }

        private static VisualElement CreateControlsPad(string name)
        {
            var panel = new VisualElement
            {
                name = name
            };
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.flexWrap = Wrap.NoWrap;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.FlexEnd;
            panel.style.paddingLeft = 4f;
            panel.style.paddingRight = 4f;
            panel.style.paddingTop = 4f;
            panel.style.paddingBottom = 4f;
            panel.style.backgroundColor = new Color(0.06f, 0.10f, 0.14f, 0.72f);
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            var border = new Color(0.24f, 0.40f, 0.54f, 0.9f);
            panel.style.borderTopColor = border;
            panel.style.borderRightColor = border;
            panel.style.borderBottomColor = border;
            panel.style.borderLeftColor = border;
            panel.style.borderTopLeftRadius = 6f;
            panel.style.borderTopRightRadius = 6f;
            panel.style.borderBottomLeftRadius = 6f;
            panel.style.borderBottomRightRadius = 6f;
            panel.focusable = false;
            return panel;
        }

        private static Button CreateControlButton(string name, string text)
        {
            var button = new Button { name = name, text = text };
            var runtimeFont = ResolveRuntimeFont();
            if (runtimeFont != null)
            {
                button.style.unityFont = runtimeFont;
            }

            button.style.width = 36f;
            button.style.height = 28f;
            button.style.minWidth = 36f;
            button.style.paddingLeft = 2f;
            button.style.paddingRight = 2f;
            button.style.marginRight = 3f;
            button.style.marginBottom = 0f;
            button.style.backgroundColor = new Color(0.17f, 0.24f, 0.31f, 1f);
            button.style.color = new Color(0.92f, 0.97f, 1f, 1f);
            button.style.fontSize = Fsi(10);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.focusable = false;
            button.tabIndex = -1;
            return button;
        }

        private void PopulateNotificationWindow(ref Space4XInRunHudKernelSnapshot snapshot)
        {
            snapshot.notifications_count = _notifications.Count;
            if (_notifications.Count == 0)
            {
                snapshot.notifications_window_count = 0;
                snapshot.notifications_latest = string.Empty;
                snapshot.notifications_window = string.Empty;
                return;
            }

            var windowCount = math.min(KernelNotificationWindowSize, _notifications.Count);
            var start = _notifications.Count - windowCount;
            snapshot.notifications_window_count = windowCount;
            snapshot.notifications_latest = _notifications[_notifications.Count - 1];

            var builder = new StringBuilder(96 * windowCount);
            for (var i = start; i < _notifications.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(_notifications[i]);
            }

            snapshot.notifications_window = builder.ToString();
        }

        private static float ResolveRatio(float current, float max)
        {
            if (max <= 0.0001f)
                return 0f;

            return math.saturate(current / max);
        }

        private float ComputeHudDeficitFadeAlpha(in Space4XInRunHudKernelSnapshot snapshot)
        {
            if (!_runtimeHudFadeOnPowerDeficit)
            {
                return 1f;
            }

            var deficitMw = math.max(0f, snapshot.power_deficit_mw);
            var normalizedDeficit = math.saturate((deficitMw - HudDeficitFadeStartMw) /
                                                  math.max(1f, HudDeficitFadeFullMw - HudDeficitFadeStartMw));
            var eased = normalizedDeficit * normalizedDeficit;
            return math.lerp(1f, HudDeficitFadeMinAlpha, eased);
        }

        private static string ResolvePowerDeficitTags(float capacityMw, float drawMw, bool estimateOnly)
        {
            const float epsilon = 0.01f;
            string tags;
            if (capacityMw <= epsilon && drawMw <= epsilon)
            {
                tags = "no_power_modules";
            }
            else if (drawMw > capacityMw + epsilon)
            {
                tags = capacityMw <= epsilon ? "deficit|capacity_zero" : "deficit";
            }
            else
            {
                tags = "nominal";
            }

            if (estimateOnly)
            {
                tags += "|estimate_only";
            }

            return tags;
        }

        private static string TrimForKernel(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || maxChars <= 0 || value.Length <= maxChars)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxChars);
        }

        private static void DisposeQuery(ref EntityQuery query, ref bool valid)
        {
            if (!valid)
                return;

            try
            {
                query.Dispose();
            }
            catch
            {
            }

            valid = false;
        }

        private static Focusable ResolveFocusedElement(VisualElement root)
        {
            if (root == null)
                return null;

            var panel = root.panel;
            if (panel == null)
                return null;

            var focusController = panel.focusController;
            if (focusController == null)
                return null;

            return focusController.focusedElement;
        }

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        private static Font ResolveRuntimeFont()
        {
            if (_runtimeFont != null)
            {
                return _runtimeFont;
            }

            try
            {
                _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                _runtimeFont = null;
            }

            return _runtimeFont;
        }

        private static ThemeStyleSheet ResolveRuntimeTheme()
        {
            if (_runtimeTheme != null)
            {
                return _runtimeTheme;
            }

            _runtimeTheme = Resources.Load<ThemeStyleSheet>(RuntimeThemeResourcePath);
            return _runtimeTheme;
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            if (keyboard == null || key == Key.None)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}

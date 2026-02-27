using System;
using System.Collections;
using System.Collections.Generic;
using PureDOTS.Runtime.Core;
using Space4X.Modes;
using Space4X.Registry;
using Space4x.Scenario;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Space4X.UI
{
    /// <summary>
    /// Smoke-scene presentation shell for FleetCrawl menu + ship select flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XMainMenuOverlay : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string ShipPresetCatalogResourcePath = "UI/Space4XShipPresetCatalog";
        private const string RuntimeThemeResourcePath = "UI/Space4XRuntimeTheme";
        private const string AutoStartRunEnv = "SPACE4X_AUTOSTART_RUN";
        private const string AutoStartPresetEnv = "SPACE4X_AUTOSTART_PRESET";
        private const string AutoStartDifficultyEnv = "SPACE4X_AUTOSTART_DIFFICULTY";
        private const float UiScale = 0.8f;
        private const float SettingsPanelWidth = 540f;
        private const float SettingsPanelMaxHeight = 520f;
        private static readonly Color StatBarBackground = new Color(0.06f, 0.10f, 0.14f, 0.95f);
        private static readonly Color StatBarBorder = new Color(0.28f, 0.45f, 0.58f, 0.65f);
        private static readonly Color SpeedAccent = new Color(0.40f, 0.84f, 0.95f, 1f);
        private static readonly Color AgilityAccent = new Color(0.58f, 0.89f, 0.56f, 1f);
        private static readonly Color ControlAccent = new Color(0.96f, 0.70f, 0.34f, 1f);

        private enum FrontendState : byte
        {
            MainMenu = 0,
            ShipSelect = 1,
            Loading = 2,
            InGame = 3
        }

        private sealed class HotkeyBindingRow
        {
            public string Name = string.Empty;
            public Func<Key> Getter;
            public Action<Key> Setter;
            public Label ValueLabel;
        }

        private FrontendState _state = FrontendState.MainMenu;
        private int _shipIndex;
        private int _difficulty;
        private string _status = "Smoke background online.";
        private bool _uiBuilt;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private VisualElement _menuPanel;
        private VisualElement _shipPanel;
        private Label _statusLabel;
        private Label _shipLabel;
        private Label _shipDescriptionLabel;
        private Label _shipRoleLabel;
        private Label _shipTraitLabel;
        private Label _shipArchetypeLabel;
        private Label _shipHullSegmentsLabel;
        private Label _shipStartingModulesLabel;
        private Label _shipMetaPerksLabel;
        private Label _shipMetaLevelLabel;
        private Label _difficultyValueLabel;
        private SliderInt _difficultySlider;
        private Button _newGameButton;
        private Button _previousShipButton;
        private Button _nextShipButton;
        private Button _startRunButton;
        private Button _backButton;
        private PlayerInput _playerInput;
        private Coroutine _startRunRoutine;
        private Space4XShipPresetCatalog _shipCatalog;
        private bool _runActive;
        [SerializeField] private bool autoStartRunByDefault = true;
        private GameObject _shipPreviewObject;
        private Material _shipPreviewMaterial;
        private Space4XShipPreviewShape _shipPreviewShape;
        private float _shipPreviewSpin;
        private VisualElement _shipStatsPanel;
        private VisualElement _speedBarFill;
        private VisualElement _agilityBarFill;
        private VisualElement _controlBarFill;
        private VisualElement _settingsBackdrop;
        private VisualElement _settingsModal;
        private DropdownField _settingsQualityDropdown;
        private DropdownField _settingsFullscreenDropdown;
        private DropdownField _settingsResolutionDropdown;
        private Slider _settingsHudScaleSlider;
        private Label _settingsHudScaleValueLabel;
        private Slider _settingsFontScaleSlider;
        private Label _settingsFontScaleValueLabel;
        private Toggle _settingsHudDeficitFadeToggle;
        private Slider _settingsMasterVolumeSlider;
        private Label _settingsMasterVolumeValueLabel;
        private Slider _settingsMusicVolumeSlider;
        private Label _settingsMusicVolumeValueLabel;
        private Slider _settingsSfxVolumeSlider;
        private Label _settingsSfxVolumeValueLabel;
        private Label _settingsFeedbackLabel;
        private Button _settingsApplyButton;
        private Button _settingsCloseButton;
        private bool _settingsVisible;
        private bool _settingsDirty;
        private bool _suppressSettingsUiEvents;
        private string _settingsRebindTarget = string.Empty;
        private InputAction _keyCaptureAction;
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private Action<Key> _pendingHotkeySetter;
        private readonly Dictionary<string, HotkeyBindingRow> _hotkeyRows = new Dictionary<string, HotkeyBindingRow>(StringComparer.Ordinal);
        private readonly List<Resolution> _resolutionOptions = new List<Resolution>(24);
        private readonly List<string> _resolutionLabels = new List<string>(24);
        private readonly List<string> _qualityLabels = new List<string>(12);
        private readonly List<FullScreenMode> _fullscreenModes = new List<FullScreenMode>(4)
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed,
            FullScreenMode.MaximizedWindow,
            FullScreenMode.ExclusiveFullScreen
        };
        private readonly List<string> _fullscreenLabels = new List<string>(4)
        {
            "Borderless Fullscreen",
            "Windowed",
            "Maximized Window",
            "Exclusive Fullscreen"
        };
        private Space4XUserSettingsData _settingsApplied;
        private Space4XUserSettingsData _settingsDraft;
        private bool _autoRunBootstrapAttempted;
        private static ThemeStyleSheet _runtimeTheme;
        private static float _runtimeFontScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
                return;

            if (UnityEngine.Object.FindAnyObjectByType<Space4XMainMenuOverlay>() != null)
                return;

            var go = new GameObject("Space4X Main Menu Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XMainMenuOverlay>();
        }

        private void OnEnable()
        {
            Space4XScenarioAuthority.NormalizeLegacyScenarioOverlayEnvironment();
            Space4XScenarioAuthority.ApplyPlayableScenarioEnvironment();
            Space4XModeSelectionState.SetMode(Space4XModeKind.FleetCrawl, applyScenarioEnvironment: true);
            _settingsApplied = Space4XUserSettingsStore.LoadOrDefault();
            _settingsDraft = _settingsApplied.Clone();
            ApplyRuntimeFontScale(_settingsApplied.font_scale);
            Space4XUserSettingsStore.ApplyVideoSettings(_settingsApplied);
            Space4XUserSettingsStore.ApplyAudioSettings(_settingsApplied);
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadShipCatalog();
            EnsureUiDocument();
            BuildUi();
            ApplyRuntimeFontScale(_settingsApplied.font_scale);
            ApplySettingsToInRunHudOverlay();
            ShowMenu(FrontendState.MainMenu);
            TryAutoStartRunFromEnvironment();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CancelActiveRebindOperation();
            DisposeKeyCaptureAction();

            if (_startRunRoutine != null)
            {
                StopCoroutine(_startRunRoutine);
                _startRunRoutine = null;
            }

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }

            SetShipPreviewVisible(false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_state == FrontendState.InGame)
            {
                HideMenu();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (_settingsVisible && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseSettingsModal(discardChanges: true);
                return;
            }

            if (_runActive && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_root != null && _root.style.display == DisplayStyle.None)
                {
                    ShowMenu(FrontendState.MainMenu);
                }
                else
                {
                    SetState(FrontendState.InGame);
                    HideMenu();
                }

                return;
            }

            if (_state == FrontendState.ShipSelect && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ShowMenu(FrontendState.MainMenu);
            }

            if (_state == FrontendState.ShipSelect && _shipPreviewObject != null && _shipPreviewObject.activeSelf)
            {
                _shipPreviewSpin = (_shipPreviewSpin + 35f * UTime.unscaledDeltaTime) % 360f;
                _shipPreviewObject.transform.localRotation = Quaternion.Euler(14f, _shipPreviewSpin, 0f);
            }
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
            if (_uiBuilt && _root != null)
                return;

            _root = _document.rootVisualElement;
            _root.Clear();
            _root.name = Space4XUiUxElementIds.Root;
            _root.style.flexGrow = 1f;
            _root.style.alignItems = Align.FlexStart;
            _root.style.justifyContent = Justify.FlexStart;

            var panel = new VisualElement
            {
                name = Space4XUiUxElementIds.RootContainer
            };
            panel.style.width = S(440f);
            panel.style.marginLeft = S(24f);
            panel.style.marginTop = S(24f);
            panel.style.paddingLeft = S(18f);
            panel.style.paddingRight = S(18f);
            panel.style.paddingTop = S(14f);
            panel.style.paddingBottom = S(14f);
            panel.style.backgroundColor = new Color(0.05f, 0.09f, 0.14f, 0.88f);
            panel.style.borderTopLeftRadius = S(8f);
            panel.style.borderTopRightRadius = S(8f);
            panel.style.borderBottomLeftRadius = S(8f);
            panel.style.borderBottomRightRadius = S(8f);
            var borderColor = new Color(0.38f, 0.63f, 0.77f, 0.9f);
            panel.style.borderTopColor = borderColor;
            panel.style.borderRightColor = borderColor;
            panel.style.borderBottomColor = borderColor;
            panel.style.borderLeftColor = borderColor;
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            _root.Add(panel);

            panel.Add(CreateTitleLabel("FLEETCRAWL"));
            panel.Add(CreateBodyLabel("Space4X dungeon-crawler slice"));
            panel.Add(CreateSpacer(12f));

            _menuPanel = BuildMainMenuPanel();
            panel.Add(_menuPanel);

            _shipPanel = BuildShipSelectPanel();
            panel.Add(_shipPanel);

            panel.Add(CreateSpacer(10f));
            _statusLabel = CreateBodyLabel(_status);
            _statusLabel.name = Space4XUiUxElementIds.StatusLabel;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_statusLabel);

            _settingsBackdrop = BuildSettingsModal();
            _root.Add(_settingsBackdrop);

            _uiBuilt = true;
            RefreshShipSelectionLabels();
            RefreshStatus();
            RefreshSettingsUiFromDraft();
        }

        private VisualElement BuildMainMenuPanel()
        {
            var container = new VisualElement
            {
                name = Space4XUiUxElementIds.MainMenuPanel
            };
            container.style.flexDirection = FlexDirection.Column;

            _newGameButton = CreatePrimaryButton("New Game");
            _newGameButton.name = Space4XUiUxElementIds.NewGameButton;
            _newGameButton.clicked += OpenShipSelectFromMainMenu;
            container.Add(_newGameButton);

            var continueButton = CreatePrimaryButton("Continue (Later)");
            continueButton.SetEnabled(false);
            container.Add(continueButton);

            var multiplayerButton = CreatePrimaryButton("Multiplayer (Later)");
            multiplayerButton.SetEnabled(false);
            container.Add(multiplayerButton);

            var settingsButton = CreatePrimaryButton("Settings");
            settingsButton.name = Space4XUiUxElementIds.SettingsButton;
            settingsButton.clicked += OpenSettingsModal;
            container.Add(settingsButton);

            var quitButton = CreatePrimaryButton("Quit");
            quitButton.clicked += QuitGame;
            container.Add(quitButton);

            return container;
        }

        private VisualElement BuildShipSelectPanel()
        {
            var container = new VisualElement
            {
                name = Space4XUiUxElementIds.ShipSelectPanel
            };
            container.style.flexDirection = FlexDirection.Column;

            container.Add(CreateBodyLabel("Choose your starter ship"));

            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;

            _previousShipButton = CreateSmallButton("<");
            _previousShipButton.name = Space4XUiUxElementIds.PrevShipButton;
            _previousShipButton.clicked += () =>
            {
                var count = Math.Max(1, _shipCatalog.PresetCount);
                _shipIndex = (_shipIndex - 1 + count) % count;
                RefreshShipSelectionLabels();
            };
            selectorRow.Add(_previousShipButton);

            _shipLabel = CreateBodyLabel(string.Empty);
            _shipLabel.name = Space4XUiUxElementIds.ShipNameLabel;
            _shipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _shipLabel.style.flexGrow = 1f;
            _shipLabel.style.marginLeft = S(6f);
            _shipLabel.style.marginRight = S(6f);
            selectorRow.Add(_shipLabel);

            _nextShipButton = CreateSmallButton(">");
            _nextShipButton.name = Space4XUiUxElementIds.NextShipButton;
            _nextShipButton.clicked += () =>
            {
                var count = Math.Max(1, _shipCatalog.PresetCount);
                _shipIndex = (_shipIndex + 1) % count;
                RefreshShipSelectionLabels();
            };
            selectorRow.Add(_nextShipButton);

            container.Add(selectorRow);

            _shipDescriptionLabel = CreateBodyLabel(string.Empty);
            _shipDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipDescriptionLabel.style.marginTop = S(4f);
            container.Add(_shipDescriptionLabel);

            _shipRoleLabel = CreateBodyLabel(string.Empty);
            _shipRoleLabel.style.marginTop = S(6f);
            _shipRoleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _shipRoleLabel.style.color = new Color(0.92f, 0.92f, 0.86f, 1f);
            container.Add(_shipRoleLabel);

            _shipTraitLabel = CreateBodyLabel(string.Empty);
            _shipTraitLabel.style.marginTop = S(2f);
            _shipTraitLabel.style.color = new Color(0.82f, 0.88f, 0.94f, 1f);
            container.Add(_shipTraitLabel);

            var loadoutHeader = CreateBodyLabel("Archetype Loadout");
            loadoutHeader.style.marginTop = S(6f);
            loadoutHeader.style.fontSize = Si(12);
            loadoutHeader.style.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            container.Add(loadoutHeader);

            _shipArchetypeLabel = CreateBodyLabel(string.Empty);
            _shipArchetypeLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipArchetypeLabel.style.marginTop = S(2f);
            container.Add(_shipArchetypeLabel);

            _shipHullSegmentsLabel = CreateBodyLabel(string.Empty);
            _shipHullSegmentsLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipHullSegmentsLabel.style.marginTop = S(2f);
            container.Add(_shipHullSegmentsLabel);

            _shipStartingModulesLabel = CreateBodyLabel(string.Empty);
            _shipStartingModulesLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipStartingModulesLabel.style.marginTop = S(2f);
            container.Add(_shipStartingModulesLabel);

            _shipMetaPerksLabel = CreateBodyLabel(string.Empty);
            _shipMetaPerksLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipMetaPerksLabel.style.marginTop = S(2f);
            container.Add(_shipMetaPerksLabel);

            _shipMetaLevelLabel = CreateBodyLabel(string.Empty);
            _shipMetaLevelLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipMetaLevelLabel.style.marginTop = S(2f);
            _shipMetaLevelLabel.style.color = new Color(0.88f, 0.95f, 0.84f, 1f);
            container.Add(_shipMetaLevelLabel);

            var statsHeader = CreateBodyLabel("Flight Profile (relative)");
            statsHeader.style.marginTop = S(6f);
            statsHeader.style.fontSize = Si(12);
            statsHeader.style.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            container.Add(statsHeader);

            _shipStatsPanel = new VisualElement();
            _shipStatsPanel.style.flexDirection = FlexDirection.Column;
            _shipStatsPanel.style.marginTop = S(2f);
            _shipStatsPanel.style.marginBottom = S(6f);
            container.Add(_shipStatsPanel);

            _shipStatsPanel.Add(CreateStatRow("Speed", out _speedBarFill, SpeedAccent));
            _shipStatsPanel.Add(CreateStatRow("Agility", out _agilityBarFill, AgilityAccent));
            _shipStatsPanel.Add(CreateStatRow("Control", out _controlBarFill, ControlAccent));

            container.Add(CreateSpacer(8f));
            container.Add(CreateBodyLabel("Difficulty"));

            _difficultySlider = new SliderInt(_shipCatalog.MinDifficulty, _shipCatalog.MaxDifficulty)
            {
                value = _difficulty
            };
            _difficultySlider.name = Space4XUiUxElementIds.DifficultySlider;
            _difficultySlider.RegisterValueChangedCallback(evt =>
            {
                _difficulty = _shipCatalog.ClampDifficulty(evt.newValue);
                RefreshShipSelectionLabels();
            });
            container.Add(_difficultySlider);

            _difficultyValueLabel = CreateBodyLabel(string.Empty);
            _difficultyValueLabel.name = Space4XUiUxElementIds.DifficultyValueLabel;
            container.Add(_difficultyValueLabel);
            container.Add(CreateSpacer(8f));

            _startRunButton = CreatePrimaryButton("Start Run");
            _startRunButton.name = Space4XUiUxElementIds.StartRunButton;
            _startRunButton.clicked += StartRun;
            container.Add(_startRunButton);

            _backButton = CreatePrimaryButton("Back");
            _backButton.name = Space4XUiUxElementIds.BackButton;
            _backButton.clicked += () => ShowMenu(FrontendState.MainMenu);
            container.Add(_backButton);

            return container;
        }

        private VisualElement BuildSettingsModal()
        {
            var backdrop = new VisualElement
            {
                name = Space4XUiUxElementIds.SettingsModal
            };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0f;
            backdrop.style.right = 0f;
            backdrop.style.top = 0f;
            backdrop.style.bottom = 0f;
            backdrop.style.display = DisplayStyle.None;
            backdrop.style.alignItems = Align.Center;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            backdrop.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());

            _settingsModal = new VisualElement();
            _settingsModal.style.width = S(SettingsPanelWidth);
            _settingsModal.style.maxHeight = S(SettingsPanelMaxHeight);
            _settingsModal.style.paddingLeft = S(14f);
            _settingsModal.style.paddingRight = S(14f);
            _settingsModal.style.paddingTop = S(12f);
            _settingsModal.style.paddingBottom = S(12f);
            _settingsModal.style.backgroundColor = new Color(0.06f, 0.10f, 0.15f, 0.96f);
            _settingsModal.style.borderTopWidth = 1f;
            _settingsModal.style.borderRightWidth = 1f;
            _settingsModal.style.borderBottomWidth = 1f;
            _settingsModal.style.borderLeftWidth = 1f;
            _settingsModal.style.borderTopColor = new Color(0.38f, 0.63f, 0.77f, 0.9f);
            _settingsModal.style.borderRightColor = new Color(0.38f, 0.63f, 0.77f, 0.9f);
            _settingsModal.style.borderBottomColor = new Color(0.38f, 0.63f, 0.77f, 0.9f);
            _settingsModal.style.borderLeftColor = new Color(0.38f, 0.63f, 0.77f, 0.9f);
            _settingsModal.style.borderTopLeftRadius = S(8f);
            _settingsModal.style.borderTopRightRadius = S(8f);
            _settingsModal.style.borderBottomLeftRadius = S(8f);
            _settingsModal.style.borderBottomRightRadius = S(8f);
            _settingsModal.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            backdrop.Add(_settingsModal);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            _settingsModal.Add(headerRow);

            var title = CreateBodyLabel("Settings Kernel v0");
            title.style.fontSize = Si(18);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            headerRow.Add(title);

            _settingsCloseButton = CreatePrimaryButton("Close");
            _settingsCloseButton.name = Space4XUiUxElementIds.SettingsCloseButton;
            _settingsCloseButton.style.width = S(90f);
            _settingsCloseButton.style.height = S(30f);
            _settingsCloseButton.style.marginBottom = 0f;
            _settingsCloseButton.style.fontSize = Si(12);
            _settingsCloseButton.clicked += () => CloseSettingsModal(discardChanges: true);
            headerRow.Add(_settingsCloseButton);

            var subtitle = CreateBodyLabel("Video, controls, and audio stubs for iterative UX testing.");
            subtitle.style.marginTop = S(3f);
            subtitle.style.marginBottom = S(6f);
            subtitle.style.color = new Color(0.81f, 0.88f, 0.95f, 1f);
            _settingsModal.Add(subtitle);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            scroll.style.marginTop = S(2f);
            scroll.style.marginBottom = S(8f);
            _settingsModal.Add(scroll);

            scroll.Add(BuildSettingsSectionHeader("Video"));

            _settingsQualityDropdown = new DropdownField
            {
                name = Space4XUiUxElementIds.SettingsQualityDropdown
            };
            _settingsQualityDropdown.RegisterValueChangedCallback(_ => MarkSettingsDirty());
            scroll.Add(CreateSettingsFieldRow("Quality", _settingsQualityDropdown));

            _settingsFullscreenDropdown = new DropdownField
            {
                name = Space4XUiUxElementIds.SettingsFullscreenDropdown
            };
            _settingsFullscreenDropdown.choices = _fullscreenLabels;
            _settingsFullscreenDropdown.RegisterValueChangedCallback(_ => MarkSettingsDirty());
            scroll.Add(CreateSettingsFieldRow("Display Mode", _settingsFullscreenDropdown));

            _settingsResolutionDropdown = new DropdownField
            {
                name = Space4XUiUxElementIds.SettingsResolutionDropdown
            };
            _settingsResolutionDropdown.RegisterValueChangedCallback(_ => MarkSettingsDirty());
            scroll.Add(CreateSettingsFieldRow("Resolution", _settingsResolutionDropdown));

            _settingsHudScaleSlider = new Slider(0.7f, 1.8f)
            {
                name = Space4XUiUxElementIds.SettingsHudScaleSlider
            };
            _settingsHudScaleSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                _settingsHudScaleValueLabel.text = $"{evt.newValue * 100f:0}%";
                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsSliderRow("HUD Scale", _settingsHudScaleSlider, out _settingsHudScaleValueLabel));

            _settingsFontScaleSlider = new Slider(Space4XUserSettingsStore.FontScaleMin, Space4XUserSettingsStore.FontScaleMax)
            {
                name = Space4XUiUxElementIds.SettingsFontScaleSlider
            };
            _settingsFontScaleSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                _settingsFontScaleValueLabel.text = $"{evt.newValue * 100f:0}%";
                ApplyRuntimeFontScale(evt.newValue);
                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsSliderRow("Font Scale", _settingsFontScaleSlider, out _settingsFontScaleValueLabel));

            _settingsHudDeficitFadeToggle = new Toggle
            {
                name = Space4XUiUxElementIds.SettingsHudDeficitFadeToggle
            };
            _settingsHudDeficitFadeToggle.RegisterValueChangedCallback(_ =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsToggleRow("Fade HUD by Deficit", _settingsHudDeficitFadeToggle));

            scroll.Add(CreateSpacer(6f));
            scroll.Add(BuildSettingsSectionHeader("Controls"));

            _hotkeyRows.Clear();
            scroll.Add(CreateHotkeyRebindRow("Toggle HUD", "toggle_hud", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_hud, Key.Backquote), key => _settingsDraft.key_toggle_hud = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Toggle Inventory", "toggle_inventory", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_inventory, Key.I), key => _settingsDraft.key_toggle_inventory = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Toggle Minimap", "toggle_minimap", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_minimap, Key.M), key => _settingsDraft.key_toggle_minimap = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Toggle Feed", "toggle_notifications", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_notifications, Key.N), key => _settingsDraft.key_toggle_notifications = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Toggle Forces", "toggle_forces", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_forces_holo, Key.F7), key => _settingsDraft.key_toggle_forces_holo = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Layout Edit", "toggle_layout_edit", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_toggle_layout_edit, Key.F8), key => _settingsDraft.key_toggle_layout_edit = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Increase UI Scale", "ui_scale_plus", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_ui_scale_increase, Key.RightBracket), key => _settingsDraft.key_ui_scale_increase = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Decrease UI Scale", "ui_scale_minus", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_ui_scale_decrease, Key.LeftBracket), key => _settingsDraft.key_ui_scale_decrease = (int)key));
            scroll.Add(CreateHotkeyRebindRow("Reset HUD Layout", "layout_reset", () => Space4XUserSettingsStore.ResolveKey(_settingsDraft.key_layout_reset, Key.F9), key => _settingsDraft.key_layout_reset = (int)key));

            scroll.Add(CreateSpacer(6f));
            scroll.Add(BuildSettingsSectionHeader("Audio"));

            _settingsMasterVolumeSlider = new Slider(0f, 1f)
            {
                name = Space4XUiUxElementIds.SettingsAudioMasterSlider
            };
            _settingsMasterVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                _settingsMasterVolumeValueLabel.text = $"{evt.newValue * 100f:0}%";
                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsSliderRow("Master Volume", _settingsMasterVolumeSlider, out _settingsMasterVolumeValueLabel));

            _settingsMusicVolumeSlider = new Slider(0f, 1f)
            {
                name = Space4XUiUxElementIds.SettingsAudioMusicSlider
            };
            _settingsMusicVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                _settingsMusicVolumeValueLabel.text = $"{evt.newValue * 100f:0}%";
                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsSliderRow("Music Volume", _settingsMusicVolumeSlider, out _settingsMusicVolumeValueLabel));

            _settingsSfxVolumeSlider = new Slider(0f, 1f)
            {
                name = Space4XUiUxElementIds.SettingsAudioSfxSlider
            };
            _settingsSfxVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressSettingsUiEvents)
                    return;

                _settingsSfxVolumeValueLabel.text = $"{evt.newValue * 100f:0}%";
                MarkSettingsDirty();
            });
            scroll.Add(CreateSettingsSliderRow("SFX Volume", _settingsSfxVolumeSlider, out _settingsSfxVolumeValueLabel));

            var audioHint = CreateBodyLabel("Music/SFX are persisted now and will map to mixer buses when audio lands.");
            audioHint.style.marginTop = S(4f);
            audioHint.style.fontSize = Si(11);
            audioHint.style.color = new Color(0.72f, 0.80f, 0.89f, 1f);
            audioHint.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(audioHint);

            _settingsFeedbackLabel = CreateBodyLabel(string.Empty);
            _settingsFeedbackLabel.style.whiteSpace = WhiteSpace.Normal;
            _settingsFeedbackLabel.style.minHeight = S(24f);
            _settingsFeedbackLabel.style.color = new Color(0.83f, 0.90f, 0.97f, 1f);
            _settingsModal.Add(_settingsFeedbackLabel);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = S(4f);
            _settingsModal.Add(footer);

            var resetButton = CreatePrimaryButton("Defaults");
            resetButton.style.width = S(94f);
            resetButton.style.height = S(30f);
            resetButton.style.fontSize = Si(12);
            resetButton.style.marginBottom = 0f;
            resetButton.clicked += ResetSettingsToDefaults;
            footer.Add(resetButton);

            _settingsApplyButton = CreatePrimaryButton("Apply");
            _settingsApplyButton.name = Space4XUiUxElementIds.SettingsApplyButton;
            _settingsApplyButton.style.width = S(84f);
            _settingsApplyButton.style.height = S(30f);
            _settingsApplyButton.style.fontSize = Si(12);
            _settingsApplyButton.style.marginBottom = 0f;
            _settingsApplyButton.style.marginLeft = S(6f);
            _settingsApplyButton.clicked += ApplySettingsFromUiAndPersist;
            footer.Add(_settingsApplyButton);

            return backdrop;
        }

        private static VisualElement BuildSettingsSectionHeader(string text)
        {
            var label = CreateBodyLabel(text);
            label.style.fontSize = Si(14);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = S(4f);
            label.style.marginBottom = S(4f);
            label.style.color = Color.white;
            return label;
        }

        private static VisualElement CreateSettingsFieldRow(string labelText, VisualElement field)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = S(4f);

            var label = CreateBodyLabel(labelText);
            label.style.width = S(160f);
            label.style.fontSize = Si(12);
            label.style.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            row.Add(label);

            field.style.flexGrow = 1f;
            field.style.height = S(24f);
            row.Add(field);
            return row;
        }

        private static VisualElement CreateSettingsSliderRow(string labelText, Slider slider, out Label valueLabel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = S(4f);

            var label = CreateBodyLabel(labelText);
            label.style.width = S(160f);
            label.style.fontSize = Si(12);
            label.style.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            row.Add(label);

            slider.style.flexGrow = 1f;
            slider.style.height = S(18f);
            row.Add(slider);

            valueLabel = CreateBodyLabel(string.Empty);
            valueLabel.style.width = S(52f);
            valueLabel.style.marginLeft = S(6f);
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);
            return row;
        }

        private static VisualElement CreateSettingsToggleRow(string labelText, Toggle toggle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = S(4f);

            var label = CreateBodyLabel(labelText);
            label.style.width = S(160f);
            label.style.fontSize = Si(12);
            label.style.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            row.Add(label);

            toggle.style.flexGrow = 1f;
            toggle.style.height = S(22f);
            row.Add(toggle);
            return row;
        }

        private VisualElement CreateHotkeyRebindRow(string displayName, string id, Func<Key> getter, Action<Key> setter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = S(2f);

            var label = CreateBodyLabel(displayName);
            label.style.width = S(160f);
            label.style.fontSize = Si(12);
            row.Add(label);

            var valueLabel = CreateBodyLabel(string.Empty);
            valueLabel.style.width = S(98f);
            valueLabel.style.fontSize = Si(12);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new Color(0.93f, 0.97f, 0.89f, 1f);
            row.Add(valueLabel);

            var rebindButton = CreatePrimaryButton("Rebind");
            rebindButton.style.width = S(80f);
            rebindButton.style.height = S(26f);
            rebindButton.style.fontSize = Si(11);
            rebindButton.style.marginBottom = 0f;
            rebindButton.clicked += () => BeginHotkeyRebind(displayName, setter);
            row.Add(rebindButton);

            _hotkeyRows[id] = new HotkeyBindingRow
            {
                Name = displayName,
                Getter = getter,
                Setter = setter,
                ValueLabel = valueLabel
            };

            return row;
        }

        private void MarkSettingsDirty()
        {
            if (_suppressSettingsUiEvents)
                return;

            _settingsDirty = true;
            if (_settingsApplyButton != null)
            {
                _settingsApplyButton.SetEnabled(true);
            }
        }

        private void OpenSettingsModal()
        {
            if (_settingsBackdrop == null)
                return;

            _settingsDraft = (_settingsApplied ?? Space4XUserSettingsStore.CreateDefaults()).Clone();
            _settingsDirty = false;
            DisposeRebindState();
            RefreshSettingsUiFromDraft();
            _settingsBackdrop.style.display = DisplayStyle.Flex;
            _settingsVisible = true;
            SetSettingsFeedback("Adjust settings and click Apply.");
            SwitchActionMap("UI");
        }

        private void CloseSettingsModal(bool discardChanges)
        {
            var hadPendingChanges = _settingsDirty;
            if (discardChanges)
            {
                _settingsDraft = (_settingsApplied ?? Space4XUserSettingsStore.CreateDefaults()).Clone();
                _settingsDirty = false;
            }

            DisposeRebindState();
            _settingsVisible = false;
            if (_settingsBackdrop != null)
            {
                _settingsBackdrop.style.display = DisplayStyle.None;
            }

            if (discardChanges && hadPendingChanges)
            {
                ApplyRuntimeFontScale((_settingsApplied ?? Space4XUserSettingsStore.CreateDefaults()).font_scale);
                SetStatus("Settings changes discarded.");
            }
        }

        private void ResetSettingsToDefaults()
        {
            _settingsDraft = Space4XUserSettingsStore.CreateDefaults();
            _settingsDirty = true;
            DisposeRebindState();
            RefreshSettingsUiFromDraft();
            ApplyRuntimeFontScale(_settingsDraft.font_scale);
            SetSettingsFeedback("Defaults loaded. Click Apply to commit.");
        }

        private void ApplySettingsFromUiAndPersist()
        {
            if (!TryReadSettingsFromUi(out var error))
            {
                SetSettingsFeedback(error);
                return;
            }

            _settingsDraft = Space4XUserSettingsStore.Normalize(_settingsDraft);
            _settingsApplied = _settingsDraft.Clone();
            Space4XUserSettingsStore.Save(_settingsApplied);
            Space4XUserSettingsStore.ApplyVideoSettings(_settingsApplied);
            Space4XUserSettingsStore.ApplyAudioSettings(_settingsApplied);
            ApplyRuntimeFontScale(_settingsApplied.font_scale);
            ApplySettingsToInRunHudOverlay();
            _settingsDirty = false;
            RefreshSettingsUiFromDraft();
            SetSettingsFeedback("Settings applied.");
            SetStatus("Settings applied.");
        }

        private bool TryReadSettingsFromUi(out string error)
        {
            error = string.Empty;
            if (_settingsQualityDropdown == null ||
                _settingsFullscreenDropdown == null ||
                _settingsResolutionDropdown == null ||
                _settingsHudScaleSlider == null ||
                _settingsFontScaleSlider == null ||
                _settingsHudDeficitFadeToggle == null ||
                _settingsMasterVolumeSlider == null ||
                _settingsMusicVolumeSlider == null ||
                _settingsSfxVolumeSlider == null)
            {
                error = "Settings UI not ready.";
                return false;
            }

            var qualityIndex = Mathf.Clamp(_settingsQualityDropdown.index, 0, Mathf.Max(0, _qualityLabels.Count - 1));
            _settingsDraft.quality_level = qualityIndex;

            var fullscreenIndex = Mathf.Clamp(_settingsFullscreenDropdown.index, 0, Mathf.Max(0, _fullscreenModes.Count - 1));
            _settingsDraft.fullscreen_mode = (int)_fullscreenModes[fullscreenIndex];

            var resolutionIndex = Mathf.Clamp(_settingsResolutionDropdown.index, 0, Mathf.Max(0, _resolutionOptions.Count - 1));
            if (resolutionIndex <= 0)
            {
                _settingsDraft.resolution_width = 0;
                _settingsDraft.resolution_height = 0;
            }
            else
            {
                var selected = _resolutionOptions[resolutionIndex];
                _settingsDraft.resolution_width = selected.width;
                _settingsDraft.resolution_height = selected.height;
            }

            _settingsDraft.hud_scale = _settingsHudScaleSlider.value;
            _settingsDraft.font_scale = _settingsFontScaleSlider.value;
            _settingsDraft.hud_fade_on_power_deficit = _settingsHudDeficitFadeToggle.value ? 1 : 0;
            _settingsDraft.audio_master = _settingsMasterVolumeSlider.value;
            _settingsDraft.audio_music = _settingsMusicVolumeSlider.value;
            _settingsDraft.audio_sfx = _settingsSfxVolumeSlider.value;
            return true;
        }

        private void RefreshSettingsUiFromDraft()
        {
            if (_settingsDraft == null)
            {
                _settingsDraft = Space4XUserSettingsStore.CreateDefaults();
            }

            _settingsDraft = Space4XUserSettingsStore.Normalize(_settingsDraft);
            _suppressSettingsUiEvents = true;
            try
            {
                PopulateQualityChoices();
                PopulateResolutionChoices();

                if (_settingsQualityDropdown != null)
                {
                    var qualityIndex = Mathf.Clamp(_settingsDraft.quality_level, 0, Mathf.Max(0, _qualityLabels.Count - 1));
                    _settingsQualityDropdown.index = qualityIndex;
                }

                if (_settingsFullscreenDropdown != null)
                {
                    _settingsFullscreenDropdown.choices = _fullscreenLabels;
                    var mode = Space4XUserSettingsStore.ResolveFullScreenMode(_settingsDraft.fullscreen_mode, Screen.fullScreenMode);
                    var index = _fullscreenModes.IndexOf(mode);
                    if (index < 0)
                    {
                        index = 0;
                    }

                    _settingsFullscreenDropdown.index = index;
                }

                if (_settingsResolutionDropdown != null)
                {
                    _settingsResolutionDropdown.choices = _resolutionLabels;
                    var selectedResolution = 0;
                    if (_settingsDraft.resolution_width > 0 && _settingsDraft.resolution_height > 0)
                    {
                        for (var i = 1; i < _resolutionOptions.Count; i++)
                        {
                            var option = _resolutionOptions[i];
                            if (option.width == _settingsDraft.resolution_width &&
                                option.height == _settingsDraft.resolution_height)
                            {
                                selectedResolution = i;
                                break;
                            }
                        }
                    }

                    _settingsResolutionDropdown.index = selectedResolution;
                }

                if (_settingsHudScaleSlider != null)
                {
                    _settingsHudScaleSlider.value = _settingsDraft.hud_scale;
                    _settingsHudScaleValueLabel.text = $"{_settingsDraft.hud_scale * 100f:0}%";
                }

                if (_settingsFontScaleSlider != null)
                {
                    _settingsFontScaleSlider.value = _settingsDraft.font_scale;
                    _settingsFontScaleValueLabel.text = $"{_settingsDraft.font_scale * 100f:0}%";
                    ApplyRuntimeFontScale(_settingsDraft.font_scale);
                }

                if (_settingsHudDeficitFadeToggle != null)
                {
                    _settingsHudDeficitFadeToggle.value = _settingsDraft.hud_fade_on_power_deficit != 0;
                }

                if (_settingsMasterVolumeSlider != null)
                {
                    _settingsMasterVolumeSlider.value = _settingsDraft.audio_master;
                    _settingsMasterVolumeValueLabel.text = $"{_settingsDraft.audio_master * 100f:0}%";
                }

                if (_settingsMusicVolumeSlider != null)
                {
                    _settingsMusicVolumeSlider.value = _settingsDraft.audio_music;
                    _settingsMusicVolumeValueLabel.text = $"{_settingsDraft.audio_music * 100f:0}%";
                }

                if (_settingsSfxVolumeSlider != null)
                {
                    _settingsSfxVolumeSlider.value = _settingsDraft.audio_sfx;
                    _settingsSfxVolumeValueLabel.text = $"{_settingsDraft.audio_sfx * 100f:0}%";
                }

                foreach (var row in _hotkeyRows.Values)
                {
                    if (row?.ValueLabel == null || row.Getter == null)
                        continue;

                    row.ValueLabel.text = Space4XUserSettingsStore.FormatKeyLabel(row.Getter());
                }
            }
            finally
            {
                _suppressSettingsUiEvents = false;
            }

            if (_settingsApplyButton != null)
            {
                _settingsApplyButton.SetEnabled(_settingsDirty);
            }
        }

        private void PopulateQualityChoices()
        {
            if (_settingsQualityDropdown == null)
                return;

            _qualityLabels.Clear();
            var qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                _qualityLabels.Add("Default");
            }
            else
            {
                for (var i = 0; i < qualityNames.Length; i++)
                {
                    _qualityLabels.Add(qualityNames[i]);
                }
            }

            _settingsQualityDropdown.choices = _qualityLabels;
        }

        private void PopulateResolutionChoices()
        {
            if (_settingsResolutionDropdown == null)
                return;

            _resolutionOptions.Clear();
            _resolutionLabels.Clear();
            _resolutionOptions.Add(default);
            _resolutionLabels.Add("Current Display");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var resolutions = Screen.resolutions;
            for (var i = 0; i < resolutions.Length; i++)
            {
                var resolution = resolutions[i];
                var token = $"{resolution.width}x{resolution.height}";
                if (!seen.Add(token))
                    continue;

                _resolutionOptions.Add(resolution);
                _resolutionLabels.Add($"{resolution.width} x {resolution.height}");
            }

            _settingsResolutionDropdown.choices = _resolutionLabels;
        }

        private void BeginHotkeyRebind(string rebindName, Action<Key> setter)
        {
            if (setter == null)
                return;

            DisposeRebindState();
            _pendingHotkeySetter = setter;
            _settingsRebindTarget = rebindName ?? "Hotkey";
            _keyCaptureAction = new InputAction("Space4XSettingsKeyCapture", InputActionType.Button, "<Keyboard>/anyKey");
            _keyCaptureAction.Enable();

            _activeRebind = _keyCaptureAction.PerformInteractiveRebinding()
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnCancel(operation =>
                {
                    var target = _settingsRebindTarget;
                    DisposeRebindState();
                    SetSettingsFeedback($"{target} rebind cancelled.");
                })
                .OnComplete(operation =>
                {
                    var target = _settingsRebindTarget;
                    var commit = _pendingHotkeySetter;
                    var key = ResolveKeyFromSelectedControl(operation.selectedControl);
                    DisposeRebindState();
                    if (commit == null || key == Key.None)
                    {
                        SetSettingsFeedback($"{target} rebind failed.");
                        return;
                    }

                    commit(key);
                    _settingsDirty = true;
                    RefreshSettingsUiFromDraft();
                    SetSettingsFeedback($"{target} set to {Space4XUserSettingsStore.FormatKeyLabel(key)}. Click Apply to persist.");
                });

            _activeRebind.Start();
            SetSettingsFeedback($"Press a key for {_settingsRebindTarget} (Esc to cancel).");
        }

        private void DisposeRebindState()
        {
            if (_activeRebind != null)
            {
                _activeRebind.Dispose();
                _activeRebind = null;
            }

            DisposeKeyCaptureAction();
            _pendingHotkeySetter = null;
            _settingsRebindTarget = string.Empty;
        }

        private void CancelActiveRebindOperation()
        {
            DisposeRebindState();
        }

        private void DisposeKeyCaptureAction()
        {
            if (_keyCaptureAction == null)
                return;

            _keyCaptureAction.Disable();
            _keyCaptureAction.Dispose();
            _keyCaptureAction = null;
        }

        private static Key ResolveKeyFromSelectedControl(InputControl selectedControl)
        {
            if (selectedControl is KeyControl keyControl)
            {
                return keyControl.keyCode;
            }

            return Key.None;
        }

        private void SetSettingsFeedback(string value)
        {
            if (_settingsFeedbackLabel != null)
            {
                _settingsFeedbackLabel.text = value ?? string.Empty;
            }
        }

        private void ApplySettingsToInRunHudOverlay()
        {
            if (_settingsApplied == null)
                return;

            var hudOverlay = FindAnyObjectByType<Space4XInRunHudOverlay>();
            if (hudOverlay != null)
            {
                hudOverlay.ApplyUserSettings(_settingsApplied);
            }
        }

        public void OpenMainMenu()
        {
            ShowMenu(FrontendState.MainMenu);
        }

        public void OpenShipSelect()
        {
            ShowMenu(FrontendState.ShipSelect);
        }

        public bool TryStartRunFromKernel()
        {
            if (_startRunRoutine != null)
                return false;

            StartRun();
            return true;
        }

        public bool TryExecuteKernelCommand(Space4XUiUxKernelCommand command)
        {
            switch (command)
            {
                case Space4XUiUxKernelCommand.OpenMainMenu:
                    OpenMainMenu();
                    return true;
                case Space4XUiUxKernelCommand.OpenShipSelect:
                    OpenShipSelect();
                    return true;
                case Space4XUiUxKernelCommand.StartRun:
                    return TryStartRunFromKernel();
                case Space4XUiUxKernelCommand.HideMenu:
                    HideMenu();
                    return true;
                case Space4XUiUxKernelCommand.OpenSettings:
                    OpenSettingsModal();
                    return true;
                case Space4XUiUxKernelCommand.CloseSettings:
                    CloseSettingsModal(discardChanges: true);
                    return true;
                case Space4XUiUxKernelCommand.ApplySettings:
                    if (_settingsVisible)
                    {
                        ApplySettingsFromUiAndPersist();
                    }
                    else if (_settingsApplied != null)
                    {
                        Space4XUserSettingsStore.ApplyVideoSettings(_settingsApplied);
                        Space4XUserSettingsStore.ApplyAudioSettings(_settingsApplied);
                        ApplyRuntimeFontScale(_settingsApplied.font_scale);
                        ApplySettingsToInRunHudOverlay();
                    }

                    return true;
                case Space4XUiUxKernelCommand.ResetSettingsToDefaults:
                    if (_settingsVisible)
                    {
                        ResetSettingsToDefaults();
                    }
                    else
                    {
                        _settingsDraft = Space4XUserSettingsStore.CreateDefaults();
                        _settingsApplied = _settingsDraft.Clone();
                        Space4XUserSettingsStore.Save(_settingsApplied);
                        Space4XUserSettingsStore.ApplyVideoSettings(_settingsApplied);
                        Space4XUserSettingsStore.ApplyAudioSettings(_settingsApplied);
                        ApplyRuntimeFontScale(_settingsApplied.font_scale);
                        ApplySettingsToInRunHudOverlay();
                        _settingsDirty = false;
                    }

                    return true;
                default:
                    return false;
            }
        }

        public Space4XUiUxKernelSnapshot CaptureKernelSnapshot()
        {
            if (_playerInput == null)
            {
                _playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
            }

            var activeScene = SceneManager.GetActiveScene();
            var preset = _shipCatalog != null ? _shipCatalog.GetPresetOrFallback(_shipIndex) : default;
            var focused = ResolveFocusedElement(_root);
            var settings = _settingsVisible
                ? (_settingsDraft ?? _settingsApplied ?? Space4XUserSettingsStore.CreateDefaults())
                : (_settingsApplied ?? Space4XUserSettingsStore.CreateDefaults());
            var qualityNames = QualitySettings.names;
            var qualityName = qualityNames != null &&
                              settings.quality_level >= 0 &&
                              settings.quality_level < qualityNames.Length
                ? qualityNames[settings.quality_level]
                : string.Empty;
            var fullscreenMode = Space4XUserSettingsStore.ResolveFullScreenMode(settings.fullscreen_mode, Screen.fullScreenMode);
            var resolutionLabel = settings.resolution_width > 0 && settings.resolution_height > 0
                ? $"{settings.resolution_width}x{settings.resolution_height}"
                : "Current Display";

            return new Space4XUiUxKernelSnapshot
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                frame = Time.frameCount,
                scene_name = activeScene.name,
                state = _state.ToString(),
                menu_visible = BoolToInt(IsElementShown(_root)),
                run_active = BoolToInt(_runActive),
                main_menu_panel_visible = BoolToInt(IsElementShown(_root) && IsElementShown(_menuPanel)),
                ship_select_panel_visible = BoolToInt(IsElementShown(_root) && IsElementShown(_shipPanel)),
                status_text = _status ?? string.Empty,
                selected_ship_index = _shipIndex,
                selected_ship_id = preset.PresetId ?? string.Empty,
                selected_ship_name = preset.DisplayName ?? string.Empty,
                difficulty = _difficulty,
                difficulty_min = _shipCatalog != null ? _shipCatalog.MinDifficulty : 0,
                difficulty_max = _shipCatalog != null ? _shipCatalog.MaxDifficulty : 0,
                action_map = _playerInput != null && _playerInput.currentActionMap != null
                    ? _playerInput.currentActionMap.name
                    : string.Empty,
                focused_element_name = focused is VisualElement focusedElement
                    ? focusedElement.name ?? string.Empty
                    : string.Empty,
                focused_element_type = focused != null ? focused.GetType().Name : string.Empty,
                new_game_enabled = BoolToInt(IsElementEnabled(_newGameButton)),
                start_run_enabled = BoolToInt(IsElementEnabled(_startRunButton)),
                back_enabled = BoolToInt(IsElementEnabled(_backButton)),
                settings_visible = BoolToInt(_settingsVisible),
                settings_dirty = BoolToInt(_settingsDirty),
                settings_rebinding = BoolToInt(_activeRebind != null),
                settings_rebind_target = _settingsRebindTarget ?? string.Empty,
                settings_quality_index = settings.quality_level,
                settings_quality_name = qualityName,
                settings_fullscreen_mode = fullscreenMode.ToString(),
                settings_resolution = resolutionLabel,
                settings_hud_scale = settings.hud_scale,
                settings_font_scale = settings.font_scale,
                settings_hud_deficit_fade_enabled = settings.hud_fade_on_power_deficit != 0 ? 1 : 0,
                settings_audio_master = settings.audio_master,
                settings_audio_music = settings.audio_music,
                settings_audio_sfx = settings.audio_sfx,
                settings_toggle_hud_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_hud, Key.Backquote)),
                settings_toggle_inventory_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_inventory, Key.I)),
                settings_toggle_minimap_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_minimap, Key.M)),
                settings_toggle_notifications_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_notifications, Key.N)),
                settings_toggle_forces_holo_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_forces_holo, Key.F7)),
                settings_toggle_layout_edit_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_toggle_layout_edit, Key.F8)),
                settings_ui_scale_increase_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_ui_scale_increase, Key.RightBracket)),
                settings_ui_scale_decrease_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_ui_scale_decrease, Key.LeftBracket)),
                settings_layout_reset_key = Space4XUserSettingsStore.FormatKeyLabel(Space4XUserSettingsStore.ResolveKey(settings.key_layout_reset, Key.F9))
            };
        }

        private void SetState(FrontendState newState)
        {
            _state = newState;

            if (_state == FrontendState.Loading || _state == FrontendState.InGame)
            {
                CloseSettingsModal(discardChanges: true);
            }

            if (_menuPanel != null)
            {
                _menuPanel.style.display = _state == FrontendState.MainMenu
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (_shipPanel != null)
            {
                _shipPanel.style.display = _state == FrontendState.ShipSelect
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (_state == FrontendState.MainMenu || _state == FrontendState.ShipSelect || _state == FrontendState.Loading)
            {
                SwitchActionMap("UI");
            }
            else if (_state == FrontendState.InGame)
            {
                SwitchActionMap("Camera");
            }
        }

        private void ShowMenu(FrontendState state)
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }

            SetState(state);
            if (state == FrontendState.ShipSelect)
            {
                RefreshShipSelectionLabels();
            }

            if (state != FrontendState.InGame)
            {
                SetFlagshipControlEnabled(false);
            }

            SetShipPreviewVisible(state == FrontendState.ShipSelect);
        }

        private void HideMenu()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }

            CloseSettingsModal(discardChanges: true);
            SetShipPreviewVisible(false);
        }

        private void StartRun()
        {
            if (_startRunRoutine != null)
                return;

            CloseSettingsModal(discardChanges: true);
            Space4XControlModeState.ResetToDefaultForRun();
            var preset = _shipCatalog.GetPresetOrFallback(_shipIndex);
            var requestedScenePath = ResolveRequestedScenePath();
            var scenePath = ResolvePlayableScenePath(requestedScenePath, out var usedFallbackScene);
            UnityEngine.Debug.Log($"[Space4XRunStart] mode={Space4XModeSelectionState.CurrentMode} requested_scene='{requestedScenePath}' resolved_scene='{scenePath}' preset='{preset.PresetId}' difficulty={_difficulty}.");

            if (usedFallbackScene)
            {
                SetStatus($"Scene '{requestedScenePath}' is not loadable in active Build Profiles. Using current scene.");
            }

            Space4XRunStartSelection.Set(preset, _difficulty, scenePath);
            if (Space4XRunStartSelection.ApplyInitialControlModeOverride())
            {
                UnityEngine.Debug.Log($"[Space4XRunStart] Applied initial control mode override mode={Space4XControlModeState.CurrentMode} variant={(Space4XControlModeState.IsVariantEnabled(Space4XControlModeState.CurrentMode) ? 1 : 0)}.");
            }
            _startRunRoutine = StartCoroutine(StartRunAsync(scenePath, preset));
        }

        private string ResolveRequestedScenePath()
        {
            if (Space4XModeSelectionState.CurrentMode == Space4XModeKind.FleetCrawl)
            {
                return SmokeScenePath;
            }

            return string.IsNullOrWhiteSpace(_shipCatalog.GameplayScenePath)
                ? Space4XShipPresetCatalog.DefaultGameplayScenePath
                : _shipCatalog.GameplayScenePath;
        }

        private IEnumerator StartRunAsync(string scenePath, Space4XShipPresetEntry preset)
        {
            SetState(FrontendState.Loading);
            HideMenu();
            SetStatus($"Loading run scene: {scenePath}");

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                SetStatus("Run start failed: gameplay scene path is empty.");
                ShowMenu(FrontendState.ShipSelect);
                _startRunRoutine = null;
                yield break;
            }

            var activeScene = SceneManager.GetActiveScene();
            var reloadingCurrentScene = string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase);

            var loadOp = LoadGameplayScene(scenePath, reloadingCurrentScene);
            if (loadOp == null)
            {
                if (reloadingCurrentScene)
                {
                    // Keep manual testing unblocked even if reload is unavailable.
                    ActivateGameplayCameraFocus();
                    _runActive = true;
                    SetState(FrontendState.InGame);
                    HideMenu();
                    SetStatus($"Run started without scene reload: {preset.DisplayName}, difficulty {_difficulty}, meta L{Space4XRunStartSelection.ActiveMetaProgressionLevel}, scenario {Space4XRunStartSelection.ScenarioId}.");
                    _startRunRoutine = null;
                    yield break;
                }

                SetStatus($"Run start failed: could not load scene '{scenePath}'. Add it to Build Settings.");
                ShowMenu(FrontendState.ShipSelect);
                _startRunRoutine = null;
                yield break;
            }

            while (!loadOp.isDone)
            {
                var normalizedProgress = Mathf.Clamp01(loadOp.progress / 0.9f);
                SetStatus($"Loading {Mathf.RoundToInt(normalizedProgress * 100f)}% - {scenePath}");
                yield return null;
            }

            yield return null;

            ActivateGameplayCameraFocus();
            _runActive = true;
            SetState(FrontendState.InGame);
            HideMenu();
            SetStatus($"Run started: {preset.DisplayName}, difficulty {_difficulty}, meta L{Space4XRunStartSelection.ActiveMetaProgressionLevel}, scenario {Space4XRunStartSelection.ScenarioId}.");
            _startRunRoutine = null;
        }

        private static AsyncOperation LoadGameplayScene(string scenePath, bool reloadingCurrentScene)
        {
#if UNITY_EDITOR
            if (reloadingCurrentScene && !string.IsNullOrWhiteSpace(scenePath))
            {
                return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
#endif
            return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }

        private bool ActivateGameplayCameraFocus()
        {
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindAnyObjectByType<UCamera>();
            if (mainCamera == null)
            {
                SetStatus("Run warning: no camera found after scene load.");
                return false;
            }

            var rigController = mainCamera.GetComponent<Space4X.Camera.Space4XCameraRigController>();
            if (rigController != null)
            {
                rigController.enabled = false;
            }

            var rigApplier = mainCamera.GetComponent<PureDOTS.Runtime.Camera.CameraRigApplier>();
            if (rigApplier != null)
            {
                rigApplier.enabled = false;
            }

            var backgroundFocus = mainCamera.GetComponent<global::FocusFirstRenderable>();
            if (backgroundFocus != null)
            {
                backgroundFocus.enabled = false;
            }

            var flagshipControl = mainCamera.GetComponent<Space4XPlayerFlagshipController>();
            if (flagshipControl == null)
            {
                flagshipControl = mainCamera.gameObject.AddComponent<Space4XPlayerFlagshipController>();
            }

            flagshipControl.enabled = true;
            flagshipControl.SnapClaimNow();

            var follow = mainCamera.GetComponent<Space4XFollowPlayerVessel>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<Space4XFollowPlayerVessel>();
            }

            follow.ConfigureForFlagshipIntro();
            follow.enabled = true;
            follow.SnapNow();
            return true;
        }

        private static void SetFlagshipControlEnabled(bool enabled)
        {
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindAnyObjectByType<UCamera>();
            if (mainCamera == null)
                return;

            var flagshipControl = mainCamera.GetComponent<Space4XPlayerFlagshipController>();
            if (flagshipControl != null)
            {
                flagshipControl.enabled = enabled;
            }
        }

        private void OpenShipSelectFromMainMenu()
        {
            _shipIndex = 0;
            _difficulty = _shipCatalog.ClampDifficulty(_shipCatalog.DefaultDifficulty);
            ShowMenu(FrontendState.ShipSelect);
        }

        private void TryAutoStartRunFromEnvironment()
        {
            if (_autoRunBootstrapAttempted)
                return;

            _autoRunBootstrapAttempted = true;
            var autoStartToken = System.Environment.GetEnvironmentVariable(AutoStartRunEnv);
            var shouldAutoStart = string.IsNullOrWhiteSpace(autoStartToken)
                ? autoStartRunByDefault
                : IsTruthy(autoStartToken);
            if (!shouldAutoStart)
                return;

            var requestedPresetId = System.Environment.GetEnvironmentVariable(AutoStartPresetEnv);
            var requestedDifficultyRaw = System.Environment.GetEnvironmentVariable(AutoStartDifficultyEnv);

            var presetMatched = false;
            if (!string.IsNullOrWhiteSpace(requestedPresetId) &&
                TryFindPresetIndexById(requestedPresetId, out var presetIndex))
            {
                _shipIndex = presetIndex;
                presetMatched = true;
            }
            else
            {
                _shipIndex = Mathf.Clamp(_shipIndex, 0, Math.Max(0, _shipCatalog.PresetCount - 1));
            }

            if (!string.IsNullOrWhiteSpace(requestedDifficultyRaw) &&
                int.TryParse(requestedDifficultyRaw, out var requestedDifficulty))
            {
                _difficulty = _shipCatalog.ClampDifficulty(requestedDifficulty);
            }
            else
            {
                _difficulty = _shipCatalog.ClampDifficulty(_difficulty <= 0 ? _shipCatalog.DefaultDifficulty : _difficulty);
            }

            ShowMenu(FrontendState.ShipSelect);
            var preset = _shipCatalog.GetPresetOrFallback(_shipIndex);
            SetStatus(presetMatched
                ? $"Auto-starting run with preset '{preset.DisplayName}' on difficulty {_difficulty}."
                : $"Auto-starting run with fallback preset '{preset.DisplayName}' on difficulty {_difficulty}.");
            StartRun();
        }

        private bool TryFindPresetIndexById(string presetId, out int index)
        {
            index = 0;
            if (string.IsNullOrWhiteSpace(presetId) || _shipCatalog == null || !_shipCatalog.HasPresets)
                return false;

            for (var i = 0; i < _shipCatalog.PresetCount; i++)
            {
                var preset = _shipCatalog.GetPresetOrFallback(i);
                if (string.Equals(preset.PresetId, presetId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("on", StringComparison.OrdinalIgnoreCase);
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

        private void SwitchActionMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return;

            if (_playerInput == null)
            {
                _playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
            }

            if (_playerInput == null || _playerInput.actions == null)
                return;

            var currentMap = _playerInput.currentActionMap;
            if (currentMap != null && string.Equals(currentMap.name, mapName, StringComparison.Ordinal))
                return;

            var map = _playerInput.actions.FindActionMap(mapName, throwIfNotFound: false);
            if (map != null)
            {
                _playerInput.SwitchCurrentActionMap(mapName);
            }
        }

        private void LoadShipCatalog()
        {
            _shipCatalog = Resources.Load<Space4XShipPresetCatalog>(ShipPresetCatalogResourcePath);
            if (_shipCatalog == null || !_shipCatalog.HasPresets)
            {
                _shipCatalog = Space4XShipPresetCatalog.CreateRuntimeFallback();
                _status = "Ship preset catalog missing - using runtime defaults.";
            }

            _difficulty = _shipCatalog.ClampDifficulty(_difficulty <= 0 ? _shipCatalog.DefaultDifficulty : _difficulty);
            _shipIndex = Mathf.Clamp(_shipIndex, 0, Math.Max(0, _shipCatalog.PresetCount - 1));
        }

        private void SetStatus(string value)
        {
            _status = value ?? string.Empty;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = _status;
            }
        }

        private void RefreshShipSelectionLabels()
        {
            var preset = _shipCatalog.GetPresetOrFallback(_shipIndex);

            if (_shipLabel != null)
            {
                _shipLabel.text = preset.DisplayName;
            }

            if (_shipDescriptionLabel != null)
            {
                _shipDescriptionLabel.text = preset.Description;
            }

            if (_shipTraitLabel != null)
            {
                _shipTraitLabel.text = ResolveTraitLabel(preset.PresetId);
            }

            if (_shipArchetypeLabel != null)
            {
                _shipArchetypeLabel.text = $"Archetype: {preset.Archetype}";
            }

            if (_shipHullSegmentsLabel != null)
            {
                _shipHullSegmentsLabel.text = FormatListLabel("Hull Segments", preset.HullSegments, "None");
            }

            if (_shipStartingModulesLabel != null)
            {
                _shipStartingModulesLabel.text = FormatListLabel("Starting Modules", preset.StartingModules, "None");
            }

            if (_shipMetaPerksLabel != null)
            {
                _shipMetaPerksLabel.text = FormatListLabel("Meta Perks", preset.MetaPerks, "None");
            }

            if (_shipMetaLevelLabel != null)
            {
                _shipMetaLevelLabel.text = $"Meta Level: {Space4XRunStartSelection.MetaProgressionLevel} (session)";
            }

            if (_difficultyValueLabel != null)
            {
                _difficultyValueLabel.text = $"Difficulty: {_difficulty}";
            }

            if (_difficultySlider != null && _difficultySlider.value != _difficulty)
            {
                _difficultySlider.value = _difficulty;
            }

            UpdateShipPreview(preset);
            UpdateShipStatBars(preset);
        }

        private static float S(float value)
        {
            return value * UiScale;
        }

        private void ApplyRuntimeFontScale(float scale)
        {
            var previous = _runtimeFontScale;
            _runtimeFontScale = Mathf.Clamp(scale, Space4XUserSettingsStore.FontScaleMin, Space4XUserSettingsStore.FontScaleMax);
            if (_root == null || Mathf.Approximately(previous, _runtimeFontScale))
            {
                return;
            }

            var ratio = _runtimeFontScale / Mathf.Max(Space4XUserSettingsStore.FontScaleMin, previous);
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
                        element.style.fontSize = Mathf.Max(1f, current * ratio);
                    }

                    var childCount = element.childCount;
                    for (var i = 0; i < childCount; i++)
                    {
                        stack.Push(element[i]);
                    }
                }
            }
        }

        private static int Si(int value)
        {
            return Mathf.Max(1, Mathf.RoundToInt(value * UiScale * _runtimeFontScale));
        }

        private static Label CreateTitleLabel(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = Si(24);
            label.style.color = Color.white;
            label.style.marginBottom = S(6f);
            return label;
        }

        private static Label CreateBodyLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = Si(13);
            label.style.color = new Color(0.89f, 0.93f, 0.98f, 1f);
            return label;
        }

        private static VisualElement CreateStatRow(string label, out VisualElement fill, Color accent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = S(4f);

            var labelElement = CreateBodyLabel(label);
            labelElement.style.width = S(70f);
            labelElement.style.fontSize = Si(12);
            labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelElement.style.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            row.Add(labelElement);

            var bar = new VisualElement();
            bar.style.flexGrow = 1f;
            bar.style.height = S(10f);
            bar.style.marginLeft = S(6f);
            bar.style.backgroundColor = StatBarBackground;
            bar.style.borderTopWidth = 1f;
            bar.style.borderRightWidth = 1f;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderLeftWidth = 1f;
            bar.style.borderTopColor = StatBarBorder;
            bar.style.borderRightColor = StatBarBorder;
            bar.style.borderBottomColor = StatBarBorder;
            bar.style.borderLeftColor = StatBarBorder;
            bar.style.borderTopLeftRadius = S(4f);
            bar.style.borderTopRightRadius = S(4f);
            bar.style.borderBottomLeftRadius = S(4f);
            bar.style.borderBottomRightRadius = S(4f);
            bar.style.overflow = Overflow.Hidden;

            fill = new VisualElement();
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(0);
            fill.style.backgroundColor = accent;
            bar.Add(fill);

            row.Add(bar);
            return row;
        }

        private void UpdateShipStatBars(in Space4XShipPresetEntry preset)
        {
            if (_shipRoleLabel != null)
            {
                _shipRoleLabel.text = ResolveRoleLabel(preset.PresetId);
            }

            var profile = preset.FlightProfile;
            var speed = ScoreSpeed(profile);
            var agility = ScoreAgility(profile);
            var control = ScoreControl(profile);

            SetBarFill(_speedBarFill, speed, SpeedAccent);
            SetBarFill(_agilityBarFill, agility, AgilityAccent);
            SetBarFill(_controlBarFill, control, ControlAccent);
        }

        private static string ResolveRoleLabel(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                return "Role: Unknown";

            if (presetId.IndexOf("carrier", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Role: Carrier";
            if (presetId.IndexOf("frigate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Role: Frigate";
            if (presetId.IndexOf("interceptor", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Role: Interceptor";
            if (presetId.IndexOf("timeship", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Role: Timeship";
            if (presetId.IndexOf("skipship", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Role: Skipship";

            return "Role: Custom";
        }

        private static string ResolvePlayableScenePath(string requestedPath, out bool usedFallbackScene)
        {
            usedFallbackScene = false;
            if (IsSceneLoadable(requestedPath))
            {
                return requestedPath;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.path))
            {
                usedFallbackScene = true;
                return activeScene.path;
            }

            return requestedPath;
        }

        private static bool IsSceneLoadable(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                !string.IsNullOrWhiteSpace(activeScene.path) &&
                string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0)
            {
                return true;
            }
            return false;
        }

        private static string ResolveTraitLabel(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                return "Concept: Unknown";

            if (presetId.IndexOf("timeship", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Concept: Time Stop (no shields)";
            if (presetId.IndexOf("skipship", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Concept: Skip Jump (no boost)";
            if (presetId.IndexOf("carrier", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Concept: Heavy frame, steady drift";
            if (presetId.IndexOf("frigate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Concept: Balanced response";
            if (presetId.IndexOf("interceptor", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Concept: High thrust, tight margins";

            return "Concept: Experimental";
        }

        private static string FormatListLabel(string label, string[] values, string emptyFallback)
        {
            if (values == null || values.Length == 0)
            {
                return $"{label}: {emptyFallback}";
            }

            return $"{label}: {string.Join(", ", values)}";
        }

        private static float ScoreSpeed(in ShipFlightProfile profile)
        {
            var baseSpeed = (profile.MaxForwardSpeed * 0.65f) +
                            (profile.MaxStrafeSpeed * 0.2f) +
                            (profile.MaxVerticalSpeed * 0.15f);
            var boostScale = Mathf.Lerp(1f, 1.15f, Mathf.InverseLerp(1.2f, 2.2f, profile.BoostMultiplier));
            var blended = baseSpeed * boostScale;
            return NormalizeToPercent(blended, 80f, 210f);
        }

        private static float ScoreAgility(in ShipFlightProfile profile)
        {
            var agility = (profile.CursorTurnSharpness * 6f) +
                          (profile.RollSpeedDegrees * 0.5f) +
                          (profile.StrafeAcceleration * 0.2f);
            return NormalizeToPercent(agility, 80f, 180f);
        }

        private static float ScoreControl(in ShipFlightProfile profile)
        {
            var control = (profile.DampenerDeceleration * 0.55f) +
                          (profile.RetroBrakeAcceleration * 0.35f) +
                          (profile.PassiveDriftDrag * 220f);
            return NormalizeToPercent(control, 45f, 90f);
        }

        private static float NormalizeToPercent(float value, float min, float max)
        {
            if (max <= min + 0.001f)
                return 0f;

            var normalized = Mathf.Clamp01((value - min) / (max - min));
            return normalized * 100f;
        }

        private static bool IsElementShown(VisualElement element)
        {
            if (element == null)
                return false;

            return element.visible && element.resolvedStyle.display != DisplayStyle.None;
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

        private static bool IsElementEnabled(VisualElement element)
        {
            return element != null && element.enabledInHierarchy;
        }

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        private static void SetBarFill(VisualElement fill, float percent, Color accent)
        {
            if (fill == null)
                return;

            var clamped = Mathf.Clamp(percent, 0f, 100f);
            fill.style.width = Length.Percent(clamped);
            fill.style.backgroundColor = Color.Lerp(StatBarBackground, accent, clamped / 100f);
        }

        private static VisualElement CreateSpacer(float height)
        {
            var spacer = new VisualElement();
            spacer.style.height = S(height);
            return spacer;
        }

        private static Button CreatePrimaryButton(string text)
        {
            var button = new Button
            {
                text = text
            };
            button.style.height = S(36f);
            button.style.fontSize = Si(14);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.16f, 0.22f, 0.30f, 1f);
            button.style.color = Color.white;
            button.style.marginBottom = S(4f);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonTextStyle(button, Si(14));
            button.RegisterCallback<AttachToPanelEvent>(_ => ApplyButtonTextStyle(button, Si(14)));
            return button;
        }

        private static Button CreateSmallButton(string text)
        {
            var button = new Button
            {
                text = text
            };
            button.style.width = S(32f);
            button.style.height = S(28f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.18f, 0.26f, 0.34f, 1f);
            button.style.color = Color.white;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonTextStyle(button, Si(16));
            button.RegisterCallback<AttachToPanelEvent>(_ => ApplyButtonTextStyle(button, Si(16)));
            return button;
        }

        private static void ApplyButtonTextStyle(Button button, int fontSize)
        {
            if (button == null)
                return;

            var label = button.Q<Label>();
            if (label == null)
                return;

            label.style.color = new Color(0.96f, 0.98f, 1f, 1f);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void UpdateShipPreview(in Space4XShipPresetEntry preset)
        {
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindAnyObjectByType<UCamera>();
            if (mainCamera == null)
                return;

            if (_shipPreviewObject == null || _shipPreviewShape != preset.PreviewShape)
            {
                CreateShipPreview(mainCamera.transform, preset.PreviewShape);
            }

            if (_shipPreviewObject == null)
                return;

            _shipPreviewObject.transform.SetParent(mainCamera.transform, false);
            _shipPreviewObject.transform.localPosition = new Vector3(2.4f, -0.8f, 8f);
            _shipPreviewObject.transform.localScale = Vector3.one * 1.8f;
            _shipPreviewObject.transform.localRotation = Quaternion.Euler(14f, _shipPreviewSpin, 0f);
            SetShipPreviewVisible(_state == FrontendState.ShipSelect && _root != null && _root.style.display == DisplayStyle.Flex);

            if (_shipPreviewMaterial != null)
            {
                _shipPreviewMaterial.color = GetPreviewColor(preset);
            }
        }

        private void CreateShipPreview(Transform parent, Space4XShipPreviewShape shape)
        {
            if (_shipPreviewObject != null)
            {
                Destroy(_shipPreviewObject);
                _shipPreviewObject = null;
            }

            var primitive = shape switch
            {
                Space4XShipPreviewShape.Sphere => PrimitiveType.Sphere,
                Space4XShipPreviewShape.Capsule => PrimitiveType.Capsule,
                Space4XShipPreviewShape.Cylinder => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };

            _shipPreviewObject = GameObject.CreatePrimitive(primitive);
            _shipPreviewObject.name = "Space4X Ship Preview";
            _shipPreviewObject.transform.SetParent(parent, false);
            _shipPreviewObject.transform.localPosition = new Vector3(2.4f, -0.8f, 8f);
            _shipPreviewObject.transform.localRotation = Quaternion.Euler(14f, _shipPreviewSpin, 0f);
            _shipPreviewObject.transform.localScale = Vector3.one * 1.8f;

            var collider = _shipPreviewObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = _shipPreviewObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = EnsureShipPreviewMaterial();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                    material.color = GetPreviewColor(shape);
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _shipPreviewShape = shape;
        }

        private Material EnsureShipPreviewMaterial()
        {
            if (_shipPreviewMaterial != null)
                return _shipPreviewMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            _shipPreviewMaterial = new Material(shader)
            {
                name = "Space4XShipPreviewMaterial"
            };
            return _shipPreviewMaterial;
        }

        private static Color GetPreviewColor(Space4XShipPreviewShape shape)
        {
            return shape switch
            {
                Space4XShipPreviewShape.Sphere => new Color(0.40f, 0.84f, 0.95f, 1f),
                Space4XShipPreviewShape.Capsule => new Color(0.96f, 0.70f, 0.34f, 1f),
                Space4XShipPreviewShape.Cylinder => new Color(0.58f, 0.89f, 0.56f, 1f),
                _ => new Color(0.92f, 0.92f, 0.98f, 1f)
            };
        }

        private static Color GetPreviewColor(in Space4XShipPresetEntry preset)
        {
            var presetId = preset.PresetId ?? string.Empty;
            if (presetId.IndexOf("timeship", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.54f, 0.70f, 0.98f, 1f);
            }

            if (presetId.IndexOf("skipship", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.98f, 0.58f, 0.28f, 1f);
            }

            return GetPreviewColor(preset.PreviewShape);
        }

        private void SetShipPreviewVisible(bool visible)
        {
            if (_shipPreviewObject != null)
            {
                _shipPreviewObject.SetActive(visible);
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

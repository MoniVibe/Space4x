using System;
using System.Collections;
using System.IO;
using Space4X.Modes;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;

namespace Space4X.UI
{
    /// <summary>
    /// Smoke-scene presentation shell for FleetCrawl menu + ship select flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XMainMenuOverlay : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string ShipPresetCatalogResourcePath = "UI/Space4XShipPresetCatalog";
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
        private Label _shipManufacturerLabel;
        private Label _shipOutlookLabel;
        private Label _shipModuleOriginsLabel;
        private Label _shipConsumablesLabel;
        private Label _shipCrewLabel;
        private Label _difficultyValueLabel;
        private SliderInt _difficultySlider;
        private PlayerInput _playerInput;
        private Coroutine _startRunRoutine;
        private Space4XShipPresetCatalog _shipCatalog;
        private Space4XManufacturingCatalog _manufacturingCatalog;
        private bool _runActive;
        private GameObject _shipPreviewObject;
        private Material _shipPreviewMaterial;
        private Space4XShipPreviewShape _shipPreviewShape;
        private float _shipPreviewSpin;
        private VisualElement _shipStatsPanel;
        private VisualElement _speedBarFill;
        private VisualElement _agilityBarFill;
        private VisualElement _controlBarFill;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
                return;

            Space4XModeSelectionState.EnsureInitialized();
            if (!Space4XModeSelectionState.IsFleetCrawlModeActive)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
                return;

            if (UnityEngine.Object.FindFirstObjectByType<Space4XMainMenuOverlay>() != null)
                return;

            var go = new GameObject("Space4X Main Menu Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XMainMenuOverlay>();
        }

        private void OnEnable()
        {
            Space4XModeSelectionState.EnsureInitialized();
            if (!Space4XModeSelectionState.IsFleetCrawlModeActive)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadShipCatalog();
            LoadManufacturingCatalog();
            EnsureUiDocument();
            BuildUi();
            ShowMenu(FrontendState.MainMenu);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

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
            Space4XModeSelectionState.EnsureInitialized();
            if (!Space4XModeSelectionState.IsFleetCrawlModeActive)
            {
                Destroy(gameObject);
                return;
            }

            if (_state == FrontendState.InGame)
            {
                HideMenu();
            }
        }

        private void Update()
        {
            Space4XModeSelectionState.EnsureInitialized();
            if (!Space4XModeSelectionState.IsFleetCrawlModeActive)
            {
                Destroy(gameObject);
                return;
            }

            if (Keyboard.current == null)
                return;

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
                _panelSettings.themeStyleSheet = FindThemeStyleSheet();
                _document.panelSettings = _panelSettings;
            }
        }

        private void BuildUi()
        {
            if (_uiBuilt && _root != null)
                return;

            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.alignItems = Align.FlexStart;
            _root.style.justifyContent = Justify.FlexStart;

            var panel = new VisualElement();
            panel.style.width = 440f;
            panel.style.marginLeft = 24f;
            panel.style.marginTop = 24f;
            panel.style.paddingLeft = 18f;
            panel.style.paddingRight = 18f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor = new Color(0.05f, 0.09f, 0.14f, 0.88f);
            panel.style.borderTopLeftRadius = 8f;
            panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f;
            panel.style.borderBottomRightRadius = 8f;
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
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_statusLabel);

            _uiBuilt = true;
            RefreshShipSelectionLabels();
            RefreshStatus();
        }

        private VisualElement BuildMainMenuPanel()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;

            var newGameButton = CreatePrimaryButton("New Game");
            newGameButton.clicked += OpenShipSelectFromMainMenu;
            container.Add(newGameButton);

            var continueButton = CreatePrimaryButton("Continue (Later)");
            continueButton.SetEnabled(false);
            container.Add(continueButton);

            var multiplayerButton = CreatePrimaryButton("Multiplayer (Later)");
            multiplayerButton.SetEnabled(false);
            container.Add(multiplayerButton);

            var settingsButton = CreatePrimaryButton("Settings");
            settingsButton.clicked += () => SetStatus("Settings screen comes next in the presentation slice.");
            container.Add(settingsButton);

            var quitButton = CreatePrimaryButton("Quit");
            quitButton.clicked += QuitGame;
            container.Add(quitButton);

            return container;
        }

        private VisualElement BuildShipSelectPanel()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;

            container.Add(CreateBodyLabel("Choose your starter ship"));

            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;

            var previousShip = CreateSmallButton("<");
            previousShip.clicked += () =>
            {
                var count = Math.Max(1, _shipCatalog.PresetCount);
                _shipIndex = (_shipIndex - 1 + count) % count;
                RefreshShipSelectionLabels();
            };
            selectorRow.Add(previousShip);

            _shipLabel = CreateBodyLabel(string.Empty);
            _shipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _shipLabel.style.flexGrow = 1f;
            _shipLabel.style.marginLeft = 6f;
            _shipLabel.style.marginRight = 6f;
            selectorRow.Add(_shipLabel);

            var nextShip = CreateSmallButton(">");
            nextShip.clicked += () =>
            {
                var count = Math.Max(1, _shipCatalog.PresetCount);
                _shipIndex = (_shipIndex + 1) % count;
                RefreshShipSelectionLabels();
            };
            selectorRow.Add(nextShip);

            container.Add(selectorRow);

            _shipDescriptionLabel = CreateBodyLabel(string.Empty);
            _shipDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipDescriptionLabel.style.marginTop = 4f;
            container.Add(_shipDescriptionLabel);

            _shipRoleLabel = CreateBodyLabel(string.Empty);
            _shipRoleLabel.style.marginTop = 6f;
            _shipRoleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _shipRoleLabel.style.color = new Color(0.92f, 0.92f, 0.86f, 1f);
            container.Add(_shipRoleLabel);

            _shipTraitLabel = CreateBodyLabel(string.Empty);
            _shipTraitLabel.style.marginTop = 2f;
            _shipTraitLabel.style.color = new Color(0.82f, 0.88f, 0.94f, 1f);
            container.Add(_shipTraitLabel);

            var statsHeader = CreateBodyLabel("Flight Profile (relative)");
            statsHeader.style.marginTop = 6f;
            statsHeader.style.fontSize = 12;
            statsHeader.style.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            container.Add(statsHeader);

            _shipStatsPanel = new VisualElement();
            _shipStatsPanel.style.flexDirection = FlexDirection.Column;
            _shipStatsPanel.style.marginTop = 2f;
            _shipStatsPanel.style.marginBottom = 6f;
            container.Add(_shipStatsPanel);

            _shipStatsPanel.Add(CreateStatRow("Speed", out _speedBarFill, SpeedAccent));
            _shipStatsPanel.Add(CreateStatRow("Agility", out _agilityBarFill, AgilityAccent));
            _shipStatsPanel.Add(CreateStatRow("Control", out _controlBarFill, ControlAccent));

            var manufacturingHeader = CreateBodyLabel("Manufacturing Snapshot");
            manufacturingHeader.style.marginTop = 6f;
            manufacturingHeader.style.fontSize = 12;
            manufacturingHeader.style.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            container.Add(manufacturingHeader);

            _shipManufacturerLabel = CreateBodyLabel(string.Empty);
            _shipManufacturerLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipManufacturerLabel.style.fontSize = 12;
            container.Add(_shipManufacturerLabel);

            _shipOutlookLabel = CreateBodyLabel(string.Empty);
            _shipOutlookLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipOutlookLabel.style.fontSize = 12;
            container.Add(_shipOutlookLabel);

            _shipModuleOriginsLabel = CreateBodyLabel(string.Empty);
            _shipModuleOriginsLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipModuleOriginsLabel.style.fontSize = 12;
            _shipModuleOriginsLabel.style.marginTop = 4f;
            container.Add(_shipModuleOriginsLabel);

            _shipConsumablesLabel = CreateBodyLabel(string.Empty);
            _shipConsumablesLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipConsumablesLabel.style.fontSize = 12;
            _shipConsumablesLabel.style.marginTop = 4f;
            container.Add(_shipConsumablesLabel);

            _shipCrewLabel = CreateBodyLabel(string.Empty);
            _shipCrewLabel.style.whiteSpace = WhiteSpace.Normal;
            _shipCrewLabel.style.fontSize = 12;
            _shipCrewLabel.style.marginTop = 4f;
            container.Add(_shipCrewLabel);

            container.Add(CreateSpacer(8f));
            container.Add(CreateBodyLabel("Difficulty"));

            _difficultySlider = new SliderInt(_shipCatalog.MinDifficulty, _shipCatalog.MaxDifficulty)
            {
                value = _difficulty
            };
            _difficultySlider.RegisterValueChangedCallback(evt =>
            {
                _difficulty = _shipCatalog.ClampDifficulty(evt.newValue);
                RefreshShipSelectionLabels();
            });
            container.Add(_difficultySlider);

            _difficultyValueLabel = CreateBodyLabel(string.Empty);
            container.Add(_difficultyValueLabel);
            container.Add(CreateSpacer(8f));

            var startRunButton = CreatePrimaryButton("Start Run");
            startRunButton.clicked += StartRun;
            container.Add(startRunButton);

            var backButton = CreatePrimaryButton("Back");
            backButton.clicked += () => ShowMenu(FrontendState.MainMenu);
            container.Add(backButton);

            return container;
        }

        private void SetState(FrontendState newState)
        {
            _state = newState;

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

            SetShipPreviewVisible(false);
        }

        private void StartRun()
        {
            if (_startRunRoutine != null)
                return;

            Space4XControlModeState.ResetToDefaultForRun();
            var preset = _shipCatalog.GetPresetOrFallback(_shipIndex);
            var configuredScenePath = string.IsNullOrWhiteSpace(_shipCatalog.GameplayScenePath)
                ? Space4XShipPresetCatalog.DefaultGameplayScenePath
                : _shipCatalog.GameplayScenePath;
            var scenePath = ResolveGameplayScenePath(configuredScenePath);

            Space4XRunStartSelection.Set(preset, _difficulty, scenePath);
            UnityEngine.Debug.Log($"[Space4XRunStart] Requested scenario id='{Space4XRunStartSelection.ScenarioId}' path='{Space4XRunStartSelection.ScenarioPath}' scene='{scenePath}' preset='{preset.PresetId}'.");
            RequestScenarioReloadAcrossWorlds();
            _startRunRoutine = StartCoroutine(StartRunAsync(scenePath, preset));
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

            if (!TryLoadSceneSingle(scenePath, out var loadOp, out var loadError))
            {
                SetStatus($"Run start failed: {loadError}");
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
            SetStatus($"Run started: {preset.DisplayName}, difficulty {_difficulty}, scenario {Space4XRunStartSelection.ScenarioId}.");
            _startRunRoutine = null;
        }

        private static bool TryLoadSceneSingle(string scenePath, out AsyncOperation loadOp, out string loadError)
        {
            loadOp = null;
            loadError = null;

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                loadError = "gameplay scene path is empty.";
                return false;
            }

            var normalizedPath = scenePath.Replace('\\', '/');

#if UNITY_EDITOR
            if (File.Exists(normalizedPath))
            {
                try
                {
                    loadOp = EditorSceneManager.LoadSceneAsyncInPlayMode(
                        normalizedPath,
                        new LoadSceneParameters(LoadSceneMode.Single));
                }
                catch (Exception ex)
                {
                    loadError = ex.Message;
                }

                if (loadOp != null)
                {
                    return true;
                }
            }
#endif

            try
            {
                loadOp = SceneManager.LoadSceneAsync(normalizedPath, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                loadError = ex.Message;
            }

            if (loadOp != null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(loadError))
            {
                loadError = $"could not load scene '{normalizedPath}'. Add it to Build Profiles.";
            }

            return false;
        }

        private static string ResolveGameplayScenePath(string configuredScenePath)
        {
            if (string.IsNullOrWhiteSpace(configuredScenePath))
            {
                return Space4XShipPresetCatalog.DefaultGameplayScenePath;
            }

            var normalizedPath = configuredScenePath.Replace('\\', '/');
#if UNITY_EDITOR
            if (!File.Exists(normalizedPath))
            {
                return Space4XShipPresetCatalog.DefaultGameplayScenePath;
            }
#endif
            return normalizedPath;
        }

        private static void RequestScenarioReloadAcrossWorlds()
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var miningScenarioSystem = world.GetExistingSystemManaged<Space4XMiningScenarioSystem>();
                if (miningScenarioSystem == null)
                {
                    continue;
                }

                miningScenarioSystem.RequestReloadForModeSwitch();
            }
        }

        private static ThemeStyleSheet FindThemeStyleSheet()
        {
            var panels = Resources.FindObjectsOfTypeAll<PanelSettings>();
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].themeStyleSheet != null)
                {
                    return panels[i].themeStyleSheet;
                }
            }

            var themes = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
            for (var i = 0; i < themes.Length; i++)
            {
                if (themes[i] != null)
                {
                    return themes[i];
                }
            }

            return null;
        }

        private bool ActivateGameplayCameraFocus()
        {
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindFirstObjectByType<UCamera>();
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

            var follow = mainCamera.GetComponent<Space4XFollowPlayerVessel>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<Space4XFollowPlayerVessel>();
            }

            var flagshipControl = mainCamera.GetComponent<Space4XPlayerFlagshipController>();
            if (flagshipControl == null)
            {
                flagshipControl = mainCamera.gameObject.AddComponent<Space4XPlayerFlagshipController>();
            }

            flagshipControl.enabled = true;
            flagshipControl.SnapClaimNow();

            follow.ConfigureForFlagshipIntro();
            follow.enabled = true;
            follow.SnapNow();
            return true;
        }

        private static void SetFlagshipControlEnabled(bool enabled)
        {
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindFirstObjectByType<UCamera>();
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

        private void SwitchActionMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return;

            if (_playerInput == null)
            {
                _playerInput = UnityEngine.Object.FindFirstObjectByType<PlayerInput>();
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

        private void LoadManufacturingCatalog()
        {
            _manufacturingCatalog = Resources.Load<Space4XManufacturingCatalog>(Space4XManufacturingCatalog.ResourcePath);
            if (_manufacturingCatalog == null || _manufacturingCatalog.Manufacturers == null || _manufacturingCatalog.Manufacturers.Length == 0)
            {
                _manufacturingCatalog = Space4XManufacturingCatalog.CreateRuntimeFallback();
                _status = string.IsNullOrWhiteSpace(_status)
                    ? "Manufacturing catalog missing - using runtime defaults."
                    : $"{_status} Manufacturing catalog missing - using runtime defaults.";
            }
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

            if (_difficultyValueLabel != null)
            {
                _difficultyValueLabel.text = $"Difficulty: {_difficulty}";
            }

            if (_difficultySlider != null && _difficultySlider.value != _difficulty)
            {
                _difficultySlider.value = _difficulty;
            }

            var preview = _manufacturingCatalog != null
                ? _manufacturingCatalog.CreatePreview(
                    preset.PresetId,
                    preset.StartingModules,
                    _difficulty,
                    Space4XRunStartSelection.ResolveScenarioSeedForDifficulty(_difficulty))
                : Space4XManufacturingPreview.Empty;

            if (_shipManufacturerLabel != null)
            {
                _shipManufacturerLabel.text = preview.ManufacturerSummary;
            }

            if (_shipOutlookLabel != null)
            {
                _shipOutlookLabel.text = preview.OutlookSummary;
            }

            if (_shipModuleOriginsLabel != null)
            {
                _shipModuleOriginsLabel.text = FormatMultilineLabel("Module Origins", preview.ModuleOrigins);
            }

            if (_shipConsumablesLabel != null)
            {
                _shipConsumablesLabel.text = FormatMultilineLabel("Consumables", preview.Consumables);
            }

            if (_shipCrewLabel != null)
            {
                _shipCrewLabel.text = FormatMultilineLabel("Crew", preview.CrewRoster);
            }

            UpdateShipPreview(preset);
            UpdateShipStatBars(preset);
        }

        private static Label CreateTitleLabel(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 24;
            label.style.color = Color.white;
            label.style.marginBottom = 6f;
            return label;
        }

        private static Label CreateBodyLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 13;
            label.style.color = new Color(0.89f, 0.93f, 0.98f, 1f);
            return label;
        }

        private static string FormatMultilineLabel(string label, string[] entries)
        {
            if (entries == null || entries.Length == 0)
                return $"{label}: None";

            var lines = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = string.IsNullOrWhiteSpace(entries[i]) ? "Unknown" : entries[i];
                lines[i] = $"- {entry}";
            }

            return $"{label}:\n{string.Join("\n", lines)}";
        }

        private static VisualElement CreateStatRow(string label, out VisualElement fill, Color accent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;

            var labelElement = CreateBodyLabel(label);
            labelElement.style.width = 70f;
            labelElement.style.fontSize = 12;
            labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelElement.style.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            row.Add(labelElement);

            var bar = new VisualElement();
            bar.style.flexGrow = 1f;
            bar.style.height = 10f;
            bar.style.marginLeft = 6f;
            bar.style.backgroundColor = StatBarBackground;
            bar.style.borderTopWidth = 1f;
            bar.style.borderRightWidth = 1f;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderLeftWidth = 1f;
            bar.style.borderTopColor = StatBarBorder;
            bar.style.borderRightColor = StatBarBorder;
            bar.style.borderBottomColor = StatBarBorder;
            bar.style.borderLeftColor = StatBarBorder;
            bar.style.borderTopLeftRadius = 4f;
            bar.style.borderTopRightRadius = 4f;
            bar.style.borderBottomLeftRadius = 4f;
            bar.style.borderBottomRightRadius = 4f;
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
            spacer.style.height = height;
            return spacer;
        }

        private static Button CreatePrimaryButton(string text)
        {
            var button = new Button
            {
                text = text
            };
            button.style.height = 36f;
            button.style.fontSize = 14f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.16f, 0.22f, 0.30f, 1f);
            button.style.color = Color.white;
            button.style.marginBottom = 4f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonTextStyle(button, 14);
            button.RegisterCallback<AttachToPanelEvent>(_ => ApplyButtonTextStyle(button, 14));
            return button;
        }

        private static Button CreateSmallButton(string text)
        {
            var button = new Button
            {
                text = text
            };
            button.style.width = 32f;
            button.style.height = 28f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.18f, 0.26f, 0.34f, 1f);
            button.style.color = Color.white;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonTextStyle(button, 16);
            button.RegisterCallback<AttachToPanelEvent>(_ => ApplyButtonTextStyle(button, 16));
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
            var mainCamera = UCamera.main ?? UnityEngine.Object.FindFirstObjectByType<UCamera>();
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

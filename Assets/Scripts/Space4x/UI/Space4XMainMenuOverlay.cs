using System;
using System.Collections;
using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
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
        private Label _difficultyValueLabel;
        private SliderInt _difficultySlider;
        private PlayerInput _playerInput;
        private Coroutine _startRunRoutine;
        private Space4XShipPresetCatalog _shipCatalog;
        private bool _runActive;
        private GameObject _shipPreviewObject;
        private Material _shipPreviewMaterial;
        private Space4XShipPreviewShape _shipPreviewShape;
        private float _shipPreviewSpin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
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
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadShipCatalog();
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
            if (_state == FrontendState.InGame)
            {
                HideMenu();
            }
        }

        private void Update()
        {
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
            var scenePath = string.IsNullOrWhiteSpace(_shipCatalog.GameplayScenePath)
                ? Space4XShipPresetCatalog.DefaultGameplayScenePath
                : _shipCatalog.GameplayScenePath;

            Space4XRunStartSelection.Set(preset, _difficulty, scenePath);
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

            var activeScene = SceneManager.GetActiveScene();
            if (string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                ActivateGameplayCameraFocus();
                _runActive = true;
                SetState(FrontendState.InGame);
                HideMenu();
                SetStatus($"Run started: {preset.DisplayName}, difficulty {_difficulty}, scenario {Space4XRunStartSelection.ScenarioId}.");
                _startRunRoutine = null;
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            if (loadOp == null)
            {
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
            SetStatus($"Run started: {preset.DisplayName}, difficulty {_difficulty}, scenario {Space4XRunStartSelection.ScenarioId}.");
            _startRunRoutine = null;
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

            if (_difficultyValueLabel != null)
            {
                _difficultyValueLabel.text = $"Difficulty: {_difficulty}";
            }

            if (_difficultySlider != null && _difficultySlider.value != _difficulty)
            {
                _difficultySlider.value = _difficulty;
            }

            UpdateShipPreview(preset);
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
                _shipPreviewMaterial.color = GetPreviewColor(preset.PreviewShape);
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

using System;
using System.Collections;
using NUnit.Framework;
using Space4X.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public class Space4XUiUxKernelTests
{
    private const float OverlayTimeoutSeconds = 30f;

    [UnityTest]
    public IEnumerator UiUxKernelSnapshot_ExposesStableContract()
    {
        using var autoStartScope = new EnvironmentVariableScope("SPACE4X_AUTOSTART_RUN", "0");
        yield return LoadIsolatedScene();
        yield return DestroyExistingKernelOverlays();

        var overlayGo = new GameObject("Space4X Main Menu Overlay Test");
        var overlay = overlayGo.AddComponent<Space4XMainMenuOverlay>();
        Assert.IsNotNull(overlay);
        yield return null;

        yield return WaitForCondition(
            () =>
            {
                var snapshot = overlay.CaptureKernelSnapshot();
                return !string.IsNullOrWhiteSpace(snapshot.state);
            },
            OverlayTimeoutSeconds,
            "Kernel snapshot never became readable.");

        var kernel = overlay.CaptureKernelSnapshot();
        Assert.AreEqual("MainMenu", kernel.state, "Kernel should initialize in MainMenu when auto-start is disabled.");
        Assert.AreEqual(1, kernel.menu_visible, "Menu root should be visible.");
        Assert.AreEqual(1, kernel.main_menu_panel_visible, "Main menu panel should be visible.");
        Assert.AreEqual(0, kernel.ship_select_panel_visible, "Ship select panel should be hidden in MainMenu.");
        Assert.AreEqual(1, kernel.new_game_enabled, "New Game button should be enabled.");
        Assert.GreaterOrEqual(kernel.difficulty, kernel.difficulty_min, "Difficulty should be in-range.");
        Assert.LessOrEqual(kernel.difficulty, kernel.difficulty_max, "Difficulty should be in-range.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(kernel.selected_ship_id), "Kernel should expose selected preset ID.");

        var document = overlay.GetComponent<UIDocument>();
        Assert.IsNotNull(document, "Overlay should own a UIDocument.");
        var root = document.rootVisualElement;
        Assert.IsNotNull(root, "UIDocument root should exist.");
        Assert.AreEqual(Space4XUiUxElementIds.Root, root.name, "Root should use stable kernel ID.");
        Assert.IsNotNull(root.Q<Button>(Space4XUiUxElementIds.NewGameButton), "New Game button ID missing.");
        Assert.IsNotNull(root.Q<Button>(Space4XUiUxElementIds.SettingsButton), "Settings button ID missing.");
        Assert.IsNotNull(root.Q<Button>(Space4XUiUxElementIds.StartRunButton), "Start Run button ID missing.");
        Assert.IsNotNull(root.Q<SliderInt>(Space4XUiUxElementIds.DifficultySlider), "Difficulty slider ID missing.");
    }

    [UnityTest]
    public IEnumerator UiUxKernelCommands_ReflectPanelTransitions()
    {
        using var autoStartScope = new EnvironmentVariableScope("SPACE4X_AUTOSTART_RUN", "0");
        yield return LoadIsolatedScene();
        yield return DestroyExistingKernelOverlays();

        var overlayGo = new GameObject("Space4X Main Menu Overlay Test");
        var overlay = overlayGo.AddComponent<Space4XMainMenuOverlay>();
        Assert.IsNotNull(overlay);
        yield return null;

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XUiUxKernelCommand.OpenShipSelect));
        yield return null;

        var shipSelectSnapshot = overlay.CaptureKernelSnapshot();
        Assert.AreEqual("ShipSelect", shipSelectSnapshot.state);
        Assert.AreEqual(1, shipSelectSnapshot.ship_select_panel_visible);
        Assert.AreEqual(0, shipSelectSnapshot.main_menu_panel_visible);
        Assert.AreEqual(1, shipSelectSnapshot.start_run_enabled);
        Assert.AreEqual(1, shipSelectSnapshot.back_enabled);

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XUiUxKernelCommand.OpenMainMenu));
        yield return null;

        var mainMenuSnapshot = overlay.CaptureKernelSnapshot();
        Assert.AreEqual("MainMenu", mainMenuSnapshot.state);
        Assert.AreEqual(1, mainMenuSnapshot.main_menu_panel_visible);
        Assert.AreEqual(0, mainMenuSnapshot.ship_select_panel_visible);
    }

    [UnityTest]
    public IEnumerator UiUxKernelSettingsModal_ExposesSnapshotAndElementIds()
    {
        using var autoStartScope = new EnvironmentVariableScope("SPACE4X_AUTOSTART_RUN", "0");
        yield return LoadIsolatedScene();
        yield return DestroyExistingKernelOverlays();

        var overlayGo = new GameObject("Space4X Main Menu Overlay Test");
        var overlay = overlayGo.AddComponent<Space4XMainMenuOverlay>();
        Assert.IsNotNull(overlay);
        yield return null;

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XUiUxKernelCommand.OpenSettings));
        yield return null;

        var snapshot = overlay.CaptureKernelSnapshot();
        Assert.AreEqual(1, snapshot.settings_visible, "Settings modal should report visible when opened.");
        Assert.GreaterOrEqual(snapshot.settings_quality_index, 0, "Settings quality index should be initialized.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.settings_fullscreen_mode), "Settings fullscreen mode should be populated.");

        var document = overlay.GetComponent<UIDocument>();
        Assert.IsNotNull(document, "Overlay should own a UIDocument.");
        var root = document.rootVisualElement;
        Assert.IsNotNull(root, "UIDocument root should exist.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XUiUxElementIds.SettingsModal), "Settings modal ID missing.");
        Assert.IsNotNull(root.Q<Button>(Space4XUiUxElementIds.SettingsApplyButton), "Settings apply button ID missing.");
        Assert.IsNotNull(root.Q<DropdownField>(Space4XUiUxElementIds.SettingsQualityDropdown), "Settings quality dropdown ID missing.");
    }

    private static IEnumerator LoadIsolatedScene()
    {
        var isolatedScene = SceneManager.CreateScene($"Space4XUiUxKernel_{Guid.NewGuid():N}");
        Assert.IsTrue(isolatedScene.IsValid() && isolatedScene.isLoaded, "Failed to create isolated kernel test scene.");
        SceneManager.SetActiveScene(isolatedScene);
        yield return null;
    }

    private static IEnumerator DestroyExistingKernelOverlays()
    {
        DestroyAll<Space4XMainMenuOverlay>();
        DestroyAll<Space4XInRunHudOverlay>();
        DestroyAll<Space4XUiUxKernelProbe>();
        yield return null;
    }

    private static void DestroyAll<T>() where T : UnityEngine.Object
    {
        var objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        for (var i = 0; i < objects.Length; i++)
        {
            var instance = objects[i];
            if (instance == null)
            {
                continue;
            }

            if (instance is Component component && component.gameObject != null)
            {
                UnityEngine.Object.Destroy(component.gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(instance);
            }
        }
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, float timeoutSeconds, string failureMessage)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup <= deadline)
        {
            if (predicate())
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail(failureMessage);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string _previousValue;
        private readonly bool _hadValue;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            _hadValue = _previousValue != null;
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _hadValue ? _previousValue : null);
        }
    }
}

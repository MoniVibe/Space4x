using System;
using System.Collections;
using NUnit.Framework;
using Space4X.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class Space4XInRunHudKernelTests
{
    private const float OverlayTimeoutSeconds = 40f;

    [UnityTest]
    public IEnumerator InRunHudKernelSnapshot_ExposesCoreMetricsAndIds()
    {
        using var forceRunScope = new EnvironmentVariableScope("SPACE4X_UIUX_FORCE_RUN_ACTIVE", "1");
        yield return LoadIsolatedScene();
        yield return DestroyExistingKernelOverlays();

        var overlayGo = new GameObject("Space4X In-Run HUD Overlay Test");
        var overlay = overlayGo.AddComponent<Space4XInRunHudOverlay>();
        Assert.IsNotNull(overlay);
        yield return null;

        yield return WaitForCondition(
            () =>
            {
                var snapshot = overlay.CaptureKernelSnapshot();
                return snapshot.hud_visible == 1;
            },
            OverlayTimeoutSeconds,
            "In-run HUD kernel never became visible.");

        var kernel = overlay.CaptureKernelSnapshot();
        Assert.AreEqual(1, kernel.hud_visible, "HUD should be visible in active run.");
        Assert.AreEqual(1, kernel.run_active, "Run should be active for HUD kernel.");
        Assert.GreaterOrEqual(kernel.health_ratio, 0f);
        Assert.LessOrEqual(kernel.health_ratio, 1f);
        Assert.GreaterOrEqual(kernel.shields_ratio, 0f);
        Assert.LessOrEqual(kernel.shields_ratio, 1f);
        Assert.GreaterOrEqual(kernel.fuel_ratio, 0f);
        Assert.LessOrEqual(kernel.fuel_ratio, 1f);
        Assert.GreaterOrEqual(kernel.food_ratio, 0f);
        Assert.LessOrEqual(kernel.food_ratio, 1f);
        Assert.GreaterOrEqual(kernel.supplies_ratio, 0f);
        Assert.LessOrEqual(kernel.supplies_ratio, 1f);
        Assert.Greater(kernel.time_speed_multiplier, 0f, "Time speed should always be positive.");
        Assert.GreaterOrEqual(kernel.minimap_contacts_tracked, 0, "Minimap contacts should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_self_count, 0, "Self contact count should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_ally_count, 0, "Ally contact count should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_neutral_count, 0, "Neutral contact count should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_hostile_count, 0, "Hostile contact count should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_unknown_count, 0, "Unknown contact count should be non-negative.");
        Assert.GreaterOrEqual(kernel.minimap_contacts_window_count, 0, "Minimap contact window should be non-negative.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(kernel.minimap_relation_counts), "Minimap relation summary should always be populated.");
        Assert.IsNotNull(kernel.minimap_contacts, "Minimap contact window payload should always be available.");
        Assert.AreEqual(kernel.minimap_contacts_window_count, kernel.minimap_contacts.Length, "Window count should match payload length.");
        for (var i = 0; i < kernel.minimap_contacts.Length; i++)
        {
            var contact = kernel.minimap_contacts[i];
            Assert.IsFalse(string.IsNullOrWhiteSpace(contact.entity_ref), "Contact entity ref should be set.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(contact.relation), "Contact relation token should be set.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(contact.color_token), "Contact color token should be set.");
            Assert.GreaterOrEqual(contact.distance, 0f, "Contact distance should be non-negative.");
            Assert.GreaterOrEqual(contact.confidence, 0f, "Contact confidence should be non-negative.");
            Assert.LessOrEqual(contact.confidence, 1f, "Contact confidence should be normalized.");
        }
        Assert.GreaterOrEqual(kernel.power_deficit_mw, 0f, "Power deficit should be non-negative.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(kernel.power_deficit_tags), "Power deficit tags should always be populated.");
        Assert.GreaterOrEqual(kernel.inventory_visible, 0, "Inventory visibility flag should be non-negative.");
        Assert.GreaterOrEqual(kernel.inventory_line_count, 0, "Inventory line count should be non-negative.");
        Assert.IsNotNull(kernel.inventory_summary, "Inventory summary should always be initialized.");
        Assert.GreaterOrEqual(kernel.notifications_window_count, 0, "Notification window count should be non-negative.");
        Assert.LessOrEqual(kernel.notifications_window_count, kernel.notifications_count, "Notification window count should not exceed total notifications.");
        Assert.IsNotNull(kernel.notifications_window, "Notification window text should be populated.");
        if (kernel.notifications_count > 0)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(kernel.notifications_latest), "Latest notification should be populated when notifications exist.");
        }

        var document = overlay.GetComponent<UIDocument>();
        Assert.IsNotNull(document, "HUD overlay should own a UIDocument.");
        var root = document.rootVisualElement;
        Assert.IsNotNull(root, "HUD root should exist.");
        Assert.AreEqual(Space4XInRunHudElementIds.Root, root.name);
        Assert.IsNotNull(root.Q<Label>(Space4XInRunHudElementIds.HealthLabel), "Health label ID missing.");
        Assert.IsNotNull(root.Q<Label>(Space4XInRunHudElementIds.ShieldsLabel), "Shields label ID missing.");
        Assert.IsNotNull(root.Q<Label>(Space4XInRunHudElementIds.PowerLabel), "Power label ID missing.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XInRunHudElementIds.MinimapPanel), "Minimap panel ID missing.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XInRunHudElementIds.MinimapContactList), "Minimap contact list ID missing.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XInRunHudElementIds.InventoryPanel), "Inventory panel ID missing.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XInRunHudElementIds.InventoryList), "Inventory list ID missing.");
        Assert.IsNotNull(root.Q<VisualElement>(Space4XInRunHudElementIds.NotificationPanel), "Notification panel ID missing.");
    }

    [UnityTest]
    public IEnumerator InRunHudKernelCommands_TogglePanelsAndTimeControls()
    {
        using var forceRunScope = new EnvironmentVariableScope("SPACE4X_UIUX_FORCE_RUN_ACTIVE", "1");
        yield return LoadIsolatedScene();
        yield return DestroyExistingKernelOverlays();

        var overlayGo = new GameObject("Space4X In-Run HUD Overlay Test");
        var overlay = overlayGo.AddComponent<Space4XInRunHudOverlay>();
        Assert.IsNotNull(overlay);
        yield return null;

        yield return WaitForCondition(
            () =>
            {
                var snapshot = overlay.CaptureKernelSnapshot();
                return snapshot.hud_visible == 1;
            },
            OverlayTimeoutSeconds,
            "In-run HUD kernel never became visible.");

        var before = overlay.CaptureKernelSnapshot();

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleMinimap));
        yield return null;
        var afterMinimap = overlay.CaptureKernelSnapshot();
        Assert.AreNotEqual(before.minimap_visible, afterMinimap.minimap_visible, "Minimap visibility should toggle.");

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleNotifications));
        yield return null;
        var afterNotifications = overlay.CaptureKernelSnapshot();
        Assert.AreNotEqual(before.notifications_visible, afterNotifications.notifications_visible, "Notification visibility should toggle.");
        Assert.GreaterOrEqual(afterNotifications.notifications_count, before.notifications_count + 2, "Kernel commands should append notifications to feed history.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(afterNotifications.notifications_window), "Notification window should include recent feed text.");

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XInRunHudKernelCommand.ToggleInventory));
        yield return null;
        var afterInventory = overlay.CaptureKernelSnapshot();
        Assert.AreNotEqual(before.inventory_visible, afterInventory.inventory_visible, "Inventory visibility should toggle.");

        Assert.IsTrue(overlay.TryExecuteKernelCommand(Space4XInRunHudKernelCommand.SetTimeNormal), "Time control command should be accepted.");
        yield return null;
        var afterTime = overlay.CaptureKernelSnapshot();
        Assert.Greater(afterTime.time_speed_multiplier, 0f);
    }

    private static IEnumerator LoadIsolatedScene()
    {
        var isolatedScene = SceneManager.CreateScene($"Space4XInRunHudKernel_{Guid.NewGuid():N}");
        Assert.IsTrue(isolatedScene.IsValid() && isolatedScene.isLoaded, "Failed to create isolated HUD kernel test scene.");
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UDebug = UnityEngine.Debug;
using UTime = UnityEngine.Time;
using SysEnv = System.Environment;

namespace Space4X.UI
{
    /// <summary>
    /// Emits JSONL snapshots of the FleetCrawl front-end kernel + visual tree for agent-driven UX checks.
    /// </summary>
    [DefaultExecutionOrder(-9340)]
    [DisallowMultipleComponent]
    public sealed class Space4XUiUxKernelProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_UIUX_KERNEL_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_UIUX_KERNEL_PROBE_OUT";
        private const string ProbeOutDirEnv = "SPACE4X_PROBE_OUT_DIR";
        private const string DefaultFileName = "space4x_uiux_kernel_probe.jsonl";
        private const int MaxWarningCount = 8;

        [Serializable]
        private sealed class ProbeRecord
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public int sample_index;
            public int overlay_present;
            public int main_menu_overlay_present;
            public int in_run_hud_overlay_present;
            public string active_kernel = string.Empty;
            public Space4XUiUxKernelSnapshot kernel;
            public Space4XInRunHudKernelSnapshot in_run_hud;
            public List<Space4XUiUxElementSnapshot> elements = new List<Space4XUiUxElementSnapshot>();
            public List<string> warnings = new List<string>();
        }

        [SerializeField] private bool enabledByDefault = true;
        [SerializeField] private float sampleIntervalSeconds = 0.25f;
        [SerializeField] private int maxElementsPerSample = 96;
        [SerializeField] private bool includeUnnamedInteractive = true;
        [SerializeField] private bool echoStateChangesToUnityLog;

        private readonly List<Space4XUiUxElementSnapshot> _scratchElements = new List<Space4XUiUxElementSnapshot>(128);
        private readonly Queue<string> _recentWarnings = new Queue<string>(MaxWarningCount);

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private int _sampleIndex;
        private string _lastKernelState = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindAnyObjectByType<Space4XUiUxKernelProbe>() != null)
                return;

            var go = new GameObject("Space4X UIUX Kernel Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XUiUxKernelProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            _nextSampleAt = 0f;
            _sampleIndex = 0;
            _lastKernelState = string.Empty;
            _recentWarnings.Clear();
            Application.logMessageReceived += OnUnityLog;
            UDebug.Log($"[Space4XUiUxKernelProbe] boot active={_active} path='{_outputPath}'");
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        private void Update()
        {
            if (!_active || UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = UTime.unscaledTime + Mathf.Max(0.05f, sampleIntervalSeconds);
            SampleAndEmit();
        }

        private void SampleAndEmit()
        {
            var record = new ProbeRecord
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                sample_index = _sampleIndex++
            };

            var mainMenuOverlay = FindAnyObjectByType<Space4XMainMenuOverlay>();
            var inRunHudOverlay = FindAnyObjectByType<Space4XInRunHudOverlay>();

            record.main_menu_overlay_present = mainMenuOverlay != null ? 1 : 0;
            record.in_run_hud_overlay_present = inRunHudOverlay != null ? 1 : 0;
            record.overlay_present = (record.main_menu_overlay_present == 1 || record.in_run_hud_overlay_present == 1) ? 1 : 0;

            if (record.overlay_present == 0)
            {
                record.warnings.Add("overlay_missing");
                AppendRecentWarnings(record.warnings);
                WriteRecord(record);
                return;
            }

            if (mainMenuOverlay != null)
            {
                record.kernel = mainMenuOverlay.CaptureKernelSnapshot();
            }

            if (inRunHudOverlay != null)
            {
                record.in_run_hud = inRunHudOverlay.CaptureKernelSnapshot();
            }

            UIDocument document = null;
            if (inRunHudOverlay != null && record.in_run_hud.hud_visible == 1)
            {
                document = inRunHudOverlay.GetComponent<UIDocument>();
                record.active_kernel = "in_run_hud";
            }
            else if (mainMenuOverlay != null)
            {
                document = mainMenuOverlay.GetComponent<UIDocument>();
                record.active_kernel = "main_menu";
            }
            else if (inRunHudOverlay != null)
            {
                document = inRunHudOverlay.GetComponent<UIDocument>();
                record.active_kernel = "in_run_hud";
            }

            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                record.warnings.Add("uidocument_root_missing");
            }
            else
            {
                CollectElements(root, record.elements);
            }

            AppendRecentWarnings(record.warnings);
            WriteRecord(record);

            var stateToken = record.active_kernel == "in_run_hud"
                ? $"InRunHud(run={record.in_run_hud.run_active},hud={record.in_run_hud.hud_visible},tick={record.in_run_hud.tick})"
                : (record.kernel.state ?? string.Empty);

            if (echoStateChangesToUnityLog && !string.Equals(_lastKernelState, stateToken, StringComparison.Ordinal))
            {
                _lastKernelState = stateToken;
                UDebug.Log($"[Space4XUiUxKernelProbe] state='{_lastKernelState}' sample={record.sample_index} elements={record.elements.Count}.");
            }
        }

        private void CollectElements(VisualElement root, List<Space4XUiUxElementSnapshot> destination)
        {
            _scratchElements.Clear();

            var stack = new Stack<VisualElement>();
            stack.Push(root);
            var focused = root.panel != null ? root.panel.focusController.focusedElement : null;
            var limit = Mathf.Max(8, maxElementsPerSample);

            while (stack.Count > 0 && _scratchElements.Count < limit)
            {
                var element = stack.Pop();
                if (ShouldCaptureElement(element))
                {
                    _scratchElements.Add(BuildElementSnapshot(element, focused));
                }

                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    var child = element[i];
                    if (child != null)
                    {
                        stack.Push(child);
                    }
                }
            }

            for (var i = 0; i < _scratchElements.Count; i++)
            {
                destination.Add(_scratchElements[i]);
            }
        }

        private bool ShouldCaptureElement(VisualElement element)
        {
            if (element == null)
                return false;

            if (!string.IsNullOrWhiteSpace(element.name))
                return true;

            if (element.focusable)
                return true;

            if (!includeUnnamedInteractive)
                return false;

            return element is Button ||
                   element is Slider ||
                   element is SliderInt ||
                   element is Toggle ||
                   element is TextField ||
                   element is DropdownField;
        }

        private static Space4XUiUxElementSnapshot BuildElementSnapshot(VisualElement element, Focusable focused)
        {
            var id = string.IsNullOrWhiteSpace(element.name)
                ? $"unnamed:{element.GetType().Name}"
                : element.name;
            var bounds = element.worldBound;
            return new Space4XUiUxElementSnapshot
            {
                id = id,
                hierarchy_path = BuildHierarchyPath(element),
                element_type = element.GetType().Name,
                text = ResolveText(element),
                class_list = BuildClassList(element),
                displayed = ToInt(element.resolvedStyle.display != DisplayStyle.None),
                visible = ToInt(element.visible),
                enabled = ToInt(element.enabledInHierarchy),
                focusable = ToInt(element.focusable),
                focused = ToInt(ReferenceEquals(focused, element)),
                x = bounds.x,
                y = bounds.y,
                width = bounds.width,
                height = bounds.height
            };
        }

        private static string ResolveText(VisualElement element)
        {
            if (element is TextField textField)
                return textField.value ?? string.Empty;

            if (element is DropdownField dropdownField)
                return dropdownField.value ?? string.Empty;

            if (element is SliderInt sliderInt)
                return sliderInt.value.ToString();

            if (element is Slider slider)
                return slider.value.ToString("0.###");

            if (element is TextElement textElement)
                return textElement.text ?? string.Empty;

            return string.Empty;
        }

        private static string BuildHierarchyPath(VisualElement element)
        {
            if (element == null)
                return string.Empty;

            var tokens = new Stack<string>();
            var current = element;
            while (current != null)
            {
                tokens.Push(string.IsNullOrWhiteSpace(current.name)
                    ? current.GetType().Name
                    : current.name);
                current = current.parent;
            }

            return string.Join("/", tokens);
        }

        private static string BuildClassList(VisualElement element)
        {
            var builder = new StringBuilder();
            foreach (var className in element.GetClasses())
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(className);
            }

            return builder.ToString();
        }

        private void AppendRecentWarnings(List<string> warnings)
        {
            foreach (var warning in _recentWarnings)
            {
                warnings.Add(warning);
            }
        }

        private void OnUnityLog(string condition, string stacktrace, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            if (string.IsNullOrWhiteSpace(condition) ||
                condition.StartsWith("[Space4XUiUxKernelProbe]", StringComparison.Ordinal))
            {
                return;
            }

            if (condition.IndexOf("UI", StringComparison.OrdinalIgnoreCase) < 0 &&
                condition.IndexOf("PanelSettings", StringComparison.OrdinalIgnoreCase) < 0 &&
                condition.IndexOf("Style Sheet", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (_recentWarnings.Count >= MaxWarningCount)
            {
                _recentWarnings.Dequeue();
            }

            _recentWarnings.Enqueue(condition.Trim());
        }

        private void WriteRecord(ProbeRecord record)
        {
            try
            {
                var directory = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_outputPath, JsonUtility.ToJson(record) + System.Environment.NewLine);
            }
            catch (Exception exception)
            {
                UDebug.LogWarning($"[Space4XUiUxKernelProbe] failed to write '{_outputPath}': {exception.Message}");
            }
        }

        private bool ResolveActiveFromEnvOrDefault()
        {
            var raw = SysEnv.GetEnvironmentVariable(ProbeEnabledEnv);
            return string.IsNullOrWhiteSpace(raw) ? enabledByDefault : IsTruthy(raw);
        }

        private static bool IsEnabledByEnvironment()
        {
            var raw = SysEnv.GetEnvironmentVariable(ProbeEnabledEnv);
            return !string.IsNullOrWhiteSpace(raw) && IsTruthy(raw);
        }

        private static string ResolveOutputPath()
        {
            var explicitPath = SysEnv.GetEnvironmentVariable(ProbeOutputEnv);
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;

            var directory = SysEnv.GetEnvironmentVariable(ProbeOutDirEnv);
            if (!string.IsNullOrWhiteSpace(directory))
                return Path.Combine(directory, DefaultFileName);

            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        private static int ToInt(bool value)
        {
            return value ? 1 : 0;
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
    }
}

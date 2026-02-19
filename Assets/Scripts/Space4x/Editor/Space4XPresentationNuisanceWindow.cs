#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Space4X.EditorTools
{
    /// <summary>
    /// Editor utility for running presentation nuisance checks without manual CLI wiring.
    /// </summary>
    public sealed class Space4XPresentationNuisanceWindow : EditorWindow
    {
        private enum CheckMode
        {
            Mode1Auto,
            CustomFilter
        }

        [Serializable]
        private sealed class Mode1Envelope
        {
            public bool ok;
            public string mode1_status = string.Empty;
            public string filter_verdict = string.Empty;
            public string recommendation = string.Empty;
            public string output_json_path = string.Empty;
            public string output_markdown_path = string.Empty;
        }

        [Serializable]
        private sealed class FilterEnvelope
        {
            public string verdict = string.Empty;
            public string recommendation = string.Empty;
        }

        private CheckMode _mode = CheckMode.Mode1Auto;
        private string _repoRoot = string.Empty;
        private string _probePath = string.Empty;
        private string _runSummaryPath = string.Empty;
        private string _invariantsPath = string.Empty;
        private string _outputDir = string.Empty;

        private string _lastStatus = "idle";
        private string _lastSummary = string.Empty;
        private string _lastStdout = string.Empty;
        private string _lastStderr = string.Empty;
        private string _lastJsonPath = string.Empty;
        private string _lastMarkdownPath = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Space4X/Diagnostics/Presentation Nuisance Filter")]
        public static void ShowWindow()
        {
            var window = GetWindow<Space4XPresentationNuisanceWindow>("Presentation Filter");
            window.minSize = new Vector2(660f, 500f);
            window.Show();
        }

        [MenuItem("Tools/Space4X/Presentation Nuisance Filter")]
        public static void ShowWindowToolsAlias()
        {
            ShowWindow();
        }

        private void OnEnable()
        {
            _repoRoot = ResolveRepoRoot();
            AutoDiscover();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Space4X Presentation Nuisance Filter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this window to run Mode 1 camera nuisance checks or custom tier filtering without hand-writing CLI commands.",
                MessageType.Info);

            _mode = (CheckMode)EditorGUILayout.EnumPopup("Check Mode", _mode);
            EditorGUILayout.Space(6f);

            DrawPathField("Probe JSONL", ref _probePath, "jsonl");
            DrawPathField("Run Summary JSON", ref _runSummaryPath, "json");
            DrawPathField("Invariants JSON", ref _invariantsPath, "json");
            DrawPathField("Output Directory", ref _outputDir, null, true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto-Discover Paths", GUILayout.Height(26f)))
                {
                    AutoDiscover();
                }

                if (GUILayout.Button("Run Check", GUILayout.Height(26f)))
                {
                    RunCheck();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Last Status", _lastStatus, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(_lastSummary))
            {
                EditorGUILayout.HelpBox(_lastSummary, MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_lastJsonPath) || !File.Exists(_lastJsonPath)))
                {
                    if (GUILayout.Button("Reveal JSON"))
                    {
                        EditorUtility.RevealInFinder(_lastJsonPath);
                    }
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_lastMarkdownPath) || !File.Exists(_lastMarkdownPath)))
                {
                    if (GUILayout.Button("Reveal Markdown"))
                    {
                        EditorUtility.RevealInFinder(_lastMarkdownPath);
                    }
                }
                if (GUILayout.Button("Open Nuisance Doc"))
                {
                    var docPath = Path.Combine(_repoRoot, "Docs", "Presentation", "Space4X_Presentation_Nuisance_Filter.md");
                    if (File.Exists(docPath))
                    {
                        EditorUtility.RevealInFinder(docPath);
                    }
                }
            }

            EditorGUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (!string.IsNullOrWhiteSpace(_lastStdout))
            {
                EditorGUILayout.LabelField("stdout", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_lastStdout, GUILayout.ExpandHeight(true));
            }
            if (!string.IsNullOrWhiteSpace(_lastStderr))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("stderr", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_lastStderr, GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPathField(string label, ref string value, string extension, bool isFolder = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    if (isFolder)
                    {
                        var folder = EditorUtility.OpenFolderPanel(label, string.IsNullOrWhiteSpace(value) ? _repoRoot : value, string.Empty);
                        if (!string.IsNullOrWhiteSpace(folder))
                        {
                            value = folder;
                        }
                    }
                    else
                    {
                        var start = string.IsNullOrWhiteSpace(value) ? _repoRoot : Path.GetDirectoryName(value);
                        var file = EditorUtility.OpenFilePanel(label, start, extension ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(file))
                        {
                            value = file;
                        }
                    }
                }
            }
        }

        private static string ResolveRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private void AutoDiscover()
        {
            var triRoot = Directory.GetParent(_repoRoot)?.FullName ?? _repoRoot;
            var roots = new[]
            {
                Path.Combine(_repoRoot, "reports"),
                Path.Combine(triRoot, "reports")
            };

            if (string.IsNullOrWhiteSpace(_probePath) || !File.Exists(_probePath))
            {
                _probePath = FindLatest(roots, "*camera*probe*.jsonl");
            }

            if (string.IsNullOrWhiteSpace(_runSummaryPath) || !File.Exists(_runSummaryPath))
            {
                _runSummaryPath = FindLatest(roots, "run_summary.json");
            }

            if (string.IsNullOrWhiteSpace(_invariantsPath) || !File.Exists(_invariantsPath))
            {
                _invariantsPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(_runSummaryPath))
                {
                    var sibling = Path.Combine(Path.GetDirectoryName(_runSummaryPath) ?? string.Empty, "invariants.json");
                    if (File.Exists(sibling))
                    {
                        _invariantsPath = sibling;
                    }
                }
                if (string.IsNullOrWhiteSpace(_invariantsPath))
                {
                    _invariantsPath = FindLatest(roots, "invariants.json");
                }
            }

            if (string.IsNullOrWhiteSpace(_outputDir))
            {
                _outputDir = Path.Combine(_repoRoot, "reports");
            }
        }

        private static string FindLatest(string[] roots, string pattern)
        {
            FileInfo best = null;
            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    foreach (var file in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
                    {
                        var info = new FileInfo(file);
                        if (best == null || info.LastWriteTimeUtc > best.LastWriteTimeUtc)
                        {
                            best = info;
                        }
                    }
                }
                catch
                {
                    // Keep scanning other roots.
                }
            }
            return best?.FullName ?? string.Empty;
        }

        private void RunCheck()
        {
            _lastStdout = string.Empty;
            _lastStderr = string.Empty;
            _lastSummary = string.Empty;
            _lastJsonPath = string.Empty;
            _lastMarkdownPath = string.Empty;

            if (string.IsNullOrWhiteSpace(_repoRoot) || !Directory.Exists(_repoRoot))
            {
                _lastStatus = "error";
                _lastSummary = "Repo root could not be resolved.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_outputDir))
            {
                _outputDir = Path.Combine(_repoRoot, "reports");
            }
            Directory.CreateDirectory(_outputDir);

            var scriptPath = _mode == CheckMode.Mode1Auto
                ? Path.Combine(_repoRoot, "scripts", "presentation_mode1_check.ps1")
                : Path.Combine(_repoRoot, "scripts", "presentation_nuisance_filter.ps1");
            if (!File.Exists(scriptPath))
            {
                _lastStatus = "error";
                _lastSummary = $"Script not found: {scriptPath}";
                return;
            }

            var args = new StringBuilder();
            args.Append("-NoProfile -File ").Append(Quote(scriptPath));

            if (_mode == CheckMode.Mode1Auto)
            {
                AppendArg(args, "-ProbePath", _probePath);
                AppendArg(args, "-RunSummaryPath", _runSummaryPath);
                AppendArg(args, "-InvariantsPath", _invariantsPath);
                AppendArg(args, "-OutDir", _outputDir);
            }
            else
            {
                var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
                var jsonOut = Path.Combine(_outputDir, $"nuisance_custom_{stamp}.json");
                var mdOut = Path.Combine(_outputDir, $"nuisance_custom_{stamp}.md");
                AppendArg(args, "-RunSummaryPath", _runSummaryPath);
                AppendArg(args, "-InvariantsPath", _invariantsPath);
                AppendArg(args, "-CameraProbePath", _probePath);
                AppendArg(args, "-OutJsonPath", jsonOut);
                AppendArg(args, "-OutMarkdownPath", mdOut);
                _lastJsonPath = jsonOut;
                _lastMarkdownPath = mdOut;
            }

            if (!TryRunPowerShell(args.ToString(), out var stdout, out var stderr, out var exitCode, out var launchError))
            {
                _lastStatus = "error";
                _lastSummary = $"Failed to launch PowerShell: {launchError}";
                return;
            }

            _lastStdout = stdout?.Trim() ?? string.Empty;
            _lastStderr = stderr?.Trim() ?? string.Empty;

            if (_mode == CheckMode.Mode1Auto)
            {
                var envelope = TryParseJson<Mode1Envelope>(_lastStdout);
                if (envelope != null)
                {
                    _lastStatus = envelope.mode1_status;
                    _lastSummary = $"{envelope.mode1_status}: {envelope.recommendation}";
                    _lastJsonPath = envelope.output_json_path;
                    _lastMarkdownPath = envelope.output_markdown_path;
                }
                else
                {
                    _lastStatus = exitCode == 0 ? "ok" : "error";
                    _lastSummary = "Mode1 checker completed but JSON response could not be parsed.";
                }
            }
            else
            {
                var envelope = TryParseJson<FilterEnvelope>(_lastStdout);
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.verdict))
                {
                    _lastStatus = envelope.verdict;
                    _lastSummary = $"{envelope.verdict}: {envelope.recommendation}";
                }
                else
                {
                    _lastStatus = exitCode == 0 ? "ok" : "error";
                    _lastSummary = "Custom filter completed; review stdout for details.";
                }
            }

            if (exitCode != 0 && _mode == CheckMode.CustomFilter)
            {
                _lastStatus = "error";
                _lastSummary = $"Custom filter exited with code {exitCode}.";
            }
        }

        private static bool TryRunPowerShell(
            string arguments,
            out string stdout,
            out string stderr,
            out int exitCode,
            out string launchError)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            exitCode = -1;
            launchError = string.Empty;

            if (TryRunProcess("pwsh", arguments, out stdout, out stderr, out exitCode))
            {
                return true;
            }

            if (TryRunProcess("powershell", arguments, out stdout, out stderr, out exitCode))
            {
                return true;
            }

            launchError = "Neither 'pwsh' nor 'powershell' could be started.";
            return false;
        }

        private static bool TryRunProcess(
            string executable,
            string arguments,
            out string stdout,
            out string stderr,
            out int exitCode)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            exitCode = -1;
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AppendArg(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            sb.Append(' ').Append(key).Append(' ').Append(Quote(value));
        }

        private static string Quote(string text)
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static T TryParseJson<T>(string raw) where T : class
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var trimmed = raw.Trim();
            var start = trimmed.IndexOf('{');
            if (start < 0)
                return null;
            var json = trimmed.Substring(start);
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif

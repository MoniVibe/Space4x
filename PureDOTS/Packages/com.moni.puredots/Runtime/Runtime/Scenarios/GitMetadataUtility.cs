using System;
using System.Diagnostics;
using System.IO;

namespace PureDOTS.Runtime.Scenarios
{
    public static class GitMetadataUtility
    {
        public struct GitMetadata
        {
            public string Commit;
            public string Branch;
            public bool IsDirty;
        }

        public static bool TryReadMetadata(out GitMetadata metadata)
        {
            metadata = default;
            try
            {
                var current = Directory.GetCurrentDirectory();
                while (!string.IsNullOrEmpty(current))
                {
                    var gitPath = Path.Combine(current, ".git");
                    if (Directory.Exists(gitPath))
                    {
                        return TryExtract(gitPath, current, out metadata);
                    }

                    if (File.Exists(gitPath))
                    {
                        var pointer = File.ReadAllText(gitPath).Trim();
                        const string prefix = "gitdir:";
                        if (pointer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var relative = pointer.Substring(prefix.Length).Trim();
                            if (!Path.IsPathRooted(relative))
                            {
                                relative = Path.GetFullPath(Path.Combine(current, relative));
                            }
                            return TryExtract(relative, current, out metadata);
                        }
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }
            catch
            {
                // ignored - metadata remains default
            }

            return false;
        }

        private static bool TryExtract(string gitDir, string repoRoot, out GitMetadata metadata)
        {
            metadata = default;
            var headPath = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headPath))
            {
                return false;
            }

            var head = File.ReadAllText(headPath).Trim();
            string branch = string.Empty;
            string commit = string.Empty;

            if (head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                var refName = head.Substring(4).Trim();
                branch = SanitizeBranch(refName);
                var refPath = Path.Combine(gitDir, refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refPath))
                {
                    commit = File.ReadAllText(refPath).Trim();
                }
                else
                {
                    var packedRefs = Path.Combine(gitDir, "packed-refs");
                    if (File.Exists(packedRefs))
                    {
                        foreach (var line in File.ReadLines(packedRefs))
                        {
                            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                            {
                                continue;
                            }

                            if (line.EndsWith(refName, StringComparison.Ordinal))
                            {
                                var idx = line.IndexOf(' ');
                                if (idx > 0)
                                {
                                    commit = line.Substring(0, idx).Trim();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                commit = head;
                branch = "detached";
            }

            var isDirty = TryDetectDirty(repoRoot);
            metadata = new GitMetadata
            {
                Commit = commit,
                Branch = branch,
                IsDirty = isDirty
            };
            return !string.IsNullOrEmpty(commit);
        }

        private static string SanitizeBranch(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw.Replace("\\", "/");
            if (normalized.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("refs/heads/".Length);
            }
            return normalized;
        }

        private static bool TryDetectDirty(string repoRoot)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "status --porcelain")
                {
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(500))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // ignored
                    }
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd();
                return !string.IsNullOrWhiteSpace(output);
            }
            catch
            {
                return false;
            }
        }
    }
}

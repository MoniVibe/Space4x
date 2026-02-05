using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Space4X.SimServer
{
    internal static class Space4XSimServerPaths
    {
        private const string SaveDirEnv = "SPACE4X_SIM_SAVE_DIR";
        private const string SaveKeepEnv = "SPACE4X_SIM_SAVE_KEEP";
        private const string SaveKeepPerSlotEnv = "SPACE4X_SIM_SAVE_KEEP_PER_SLOT";
        private const string SaveKeepTotalEnv = "SPACE4X_SIM_SAVE_KEEP_TOTAL";
        private const string StatusLogEnv = "SPACE4X_SIM_STATUS_LOG";
        private const string StatusLogMaxEnv = "SPACE4X_SIM_STATUS_LOG_MAX_MB";
        private static string s_baseDir;

        internal static int SaveVersion => 1;

        internal static string BaseDir
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(s_baseDir))
                {
                    return s_baseDir;
                }

                var overrideDir = Environment.GetEnvironmentVariable(SaveDirEnv);
                if (!string.IsNullOrWhiteSpace(overrideDir))
                {
                    s_baseDir = overrideDir.Trim();
                    return s_baseDir;
                }

                var preferred = @"C:\polish\sim\space4x";
                if (Directory.Exists(preferred))
                {
                    s_baseDir = preferred;
                    return s_baseDir;
                }

                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                s_baseDir = Path.Combine(localApp, "Space4XSim");
                return s_baseDir;
            }
        }

        internal static string SaveDir => Path.Combine(BaseDir, "saves");

        internal static string StatusDir => Path.Combine(BaseDir, "status");

        internal static string StatusFile => Path.Combine(StatusDir, "status.json");

        internal static string StatusLogFile => Path.Combine(StatusDir, "status.jsonl");

        internal static int SaveKeepCount => ReadInt(SaveKeepEnv, 10);

        internal static int SaveKeepPerSlot => ReadInt(SaveKeepPerSlotEnv, SaveKeepCount);

        internal static int SaveKeepTotal => ReadInt(SaveKeepTotalEnv, 0);

        internal static bool StatusLogEnabled => ReadBool(StatusLogEnv, false);

        internal static long StatusLogMaxBytes => (long)ReadFloat(StatusLogMaxEnv, 5f) * 1024L * 1024L;

        internal static void EnsureDirectories()
        {
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(SaveDir);
            Directory.CreateDirectory(StatusDir);
        }

        internal static string SanitizeSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return "autosave";
            }

            var builder = new StringBuilder(slot.Length);
            foreach (var ch in slot)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    builder.Append(ch);
                }
            }

            var cleaned = builder.ToString();
            return string.IsNullOrWhiteSpace(cleaned) ? "autosave" : cleaned;
        }

        internal static string BuildSavePath(string slot, bool overwrite, out string resolvedSlot)
        {
            resolvedSlot = SanitizeSlot(slot);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var name = overwrite ? $"{resolvedSlot}.json" : $"{resolvedSlot}_{timestamp}.json";
            return Path.Combine(SaveDir, name);
        }

        internal static string BuildStatusJsonPath()
        {
            return StatusFile;
        }

        internal static string BuildSaveListJson()
        {
            EnsureDirectories();
            var files = Directory.Exists(SaveDir)
                ? Directory.GetFiles(SaveDir, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

            var builder = new StringBuilder();
            builder.Append("{\"saves\":[");
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var name = Path.GetFileName(file);
                var slot = GetSlotKeyFromFilename(name);
                var timestamp = GetTimestampFromFilename(name);
                if (i > 0)
                {
                    builder.Append(',');
                }
                builder.Append("{\"file\":\"").Append(name.Replace("\"", string.Empty)).Append("\"");
                builder.Append(",\"slot\":\"").Append(slot.Replace("\"", string.Empty)).Append("\"");
                if (!string.IsNullOrWhiteSpace(timestamp))
                {
                    builder.Append(",\"timestamp\":\"").Append(timestamp.Replace("\"", string.Empty)).Append("\"");
                }
                builder.Append("}");
            }
            builder.Append("]}");
            return builder.ToString();
        }

        internal static void TrimSaves()
        {
            if (!Directory.Exists(SaveDir))
            {
                return;
            }

            var keepPerSlot = SaveKeepPerSlot;
            var keepTotal = SaveKeepTotal;

            var files = new DirectoryInfo(SaveDir).GetFiles("*.json", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                return;
            }

            if (keepPerSlot > 0)
            {
                var bySlot = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var slot = GetSlotKeyFromFilename(file.Name);
                    if (!bySlot.TryGetValue(slot, out var list))
                    {
                        list = new System.Collections.Generic.List<FileInfo>();
                        bySlot[slot] = list;
                    }
                    list.Add(file);
                }

                foreach (var entry in bySlot)
                {
                    var list = entry.Value;
                    list.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                    for (int i = keepPerSlot; i < list.Count; i++)
                    {
                        TryDelete(list[i]);
                    }
                }
            }

            if (keepTotal > 0)
            {
                files = new DirectoryInfo(SaveDir).GetFiles("*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = keepTotal; i < files.Length; i++)
                {
                    TryDelete(files[i]);
                }
            }
        }

        private static void TryDelete(FileInfo file)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // ignore deletion failures
            }
        }

        private static string GetSlotKeyFromFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "autosave";
            }

            var stem = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrWhiteSpace(stem))
            {
                return "autosave";
            }

            var underscore = stem.LastIndexOf('_');
            if (underscore <= 0)
            {
                return stem;
            }

            var suffix = stem.Substring(underscore + 1);
            if (IsTimestampSuffix(suffix))
            {
                return stem.Substring(0, underscore);
            }

            return stem;
        }

        private static string GetTimestampFromFilename(string name)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrWhiteSpace(stem))
            {
                return string.Empty;
            }

            var underscore = stem.LastIndexOf('_');
            if (underscore <= 0)
            {
                return string.Empty;
            }

            var suffix = stem.Substring(underscore + 1);
            return IsTimestampSuffix(suffix) ? suffix : string.Empty;
        }

        private static bool IsTimestampSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 15)
            {
                return false;
            }

            if (value[8] != '_')
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (i == 8) continue;
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void WriteStatus(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            EnsureDirectories();

            try
            {
                File.WriteAllText(StatusFile, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XSimServer] Failed to write status file: {ex.Message}");
            }

            if (!StatusLogEnabled)
            {
                return;
            }

            try
            {
                RotateStatusLogIfNeeded();
                File.AppendAllText(StatusLogFile, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XSimServer] Failed to write status log: {ex.Message}");
            }
        }

        private static void RotateStatusLogIfNeeded()
        {
            if (!File.Exists(StatusLogFile))
            {
                return;
            }

            var length = new FileInfo(StatusLogFile).Length;
            if (length < StatusLogMaxBytes)
            {
                return;
            }

            var backup = StatusLogFile + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Move(StatusLogFile, backup);
        }

        private static int ReadInt(string key, int fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return int.TryParse(v, out var parsed) ? parsed : fallback;
        }

        private static float ReadFloat(string key, float fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static bool ReadBool(string key, bool fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(v))
            {
                return fallback;
            }
            return v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}

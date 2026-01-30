using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using SysEnvironment = System.Environment;

namespace Space4X.Editor.Diagnostics
{
    public static class Space4XPPtrCastScanner
    {
        private const string MenuPath = "Tools/Diagnostics/Space4X/Find PPtr Cast Issues";
        private const string LogFileName = "Space4X_HeadlessPPtrCastScan.log";

        private struct PPtrRecord
        {
            public string AssetPath;
            public string ObjectName;
            public string PropertyPath;
            public int InstanceId;
            public string Message;
        }

        [MenuItem(MenuPath)]
        public static void FindPPtrCastIssues()
        {
            RunScan(exitWhenBatch: false, logDirectory: null);
        }

        public static int RunHeadlessScan(string logDirectory, bool exitWhenBatch = false)
        {
            return RunScan(exitWhenBatch: exitWhenBatch, logDirectory: logDirectory);
        }

        private static int RunScan(bool exitWhenBatch, string logDirectory)
        {
            var records = new List<PPtrRecord>();
            var prefabPaths = CollectAssetPaths("t:GameObject", ".prefab");
            var assetPaths = CollectAssetPaths("t:ScriptableObject", ".asset");

            var scanPrefabsFlag = SysEnvironment.GetEnvironmentVariable("SPACE4X_HEADLESS_PPTR_SCAN_PREFABS");
            var scanPrefabs = string.IsNullOrWhiteSpace(scanPrefabsFlag) ||
                              scanPrefabsFlag.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                              scanPrefabsFlag.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                              scanPrefabsFlag.Equals("yes", StringComparison.OrdinalIgnoreCase);

            var currentPath = string.Empty;
            void HandleLog(string condition, string stackTrace, LogType type)
            {
                if (string.IsNullOrEmpty(currentPath))
                {
                    return;
                }

                if (condition.IndexOf("PPtr cast failed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    records.Add(new PPtrRecord
                    {
                        AssetPath = currentPath,
                        ObjectName = string.Empty,
                        PropertyPath = string.Empty,
                        InstanceId = 0,
                        Message = condition
                    });
                }
            }

            Application.logMessageReceived += HandleLog;
            try
            {
                if (scanPrefabs)
                {
                    ScanAssets(prefabPaths, "prefab", records, ref currentPath);
                }
                ScanAssets(assetPaths, "asset", records, ref currentPath);
            }
            finally
            {
                Application.logMessageReceived -= HandleLog;
                EditorUtility.ClearProgressBar();
            }

            WriteReport(records, prefabPaths.Count, assetPaths.Count, logDirectory);

            if (exitWhenBatch && Application.isBatchMode)
            {
                EditorApplication.Exit(records.Count == 0 ? 0 : 1);
            }

            return records.Count;
        }

        private static List<string> CollectAssetPaths(string filter, string extension)
        {
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets(filter);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                results.Add(path);
            }
            return results;
        }

        private static void ScanAssets(List<string> paths, string scope, List<PPtrRecord> records, ref string currentPath)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                currentPath = paths[i];
                EditorUtility.DisplayProgressBar("PPtr Cast Scan", $"{scope} {i + 1}/{paths.Count}", (float)(i + 1) / paths.Count);

                var assets = AssetDatabase.LoadAllAssetsAtPath(currentPath);
                if (assets == null || assets.Length == 0)
                {
                    continue;
                }

                foreach (var asset in assets)
                {
                    if (asset == null)
                    {
                        continue;
                    }

                    ScanSerializedObject(currentPath, asset, records);
                }
            }
        }

        private static void ScanSerializedObject(string assetPath, UnityEngine.Object target, List<PPtrRecord> records)
        {
            SerializedObject so = null;
            try
            {
                so = new SerializedObject(target);
            }
            catch
            {
                return;
            }

            var iterator = so.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (iterator.type.IndexOf("MonoScript", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var referenced = iterator.objectReferenceValue;
                var instanceId = iterator.objectReferenceInstanceIDValue;
                if (referenced != null && referenced is not MonoScript)
                {
                    records.Add(new PPtrRecord
                    {
                        AssetPath = assetPath,
                        ObjectName = target.name,
                        PropertyPath = iterator.propertyPath,
                        InstanceId = instanceId,
                        Message = $"MonoScript field references {referenced.GetType().Name}"
                    });
                    continue;
                }

                if (referenced == null && instanceId != 0)
                {
                    records.Add(new PPtrRecord
                    {
                        AssetPath = assetPath,
                        ObjectName = target.name,
                        PropertyPath = iterator.propertyPath,
                        InstanceId = instanceId,
                        Message = "MonoScript field points to invalid instance ID"
                    });
                }
            }
        }

        private static void WriteReport(List<PPtrRecord> records, int prefabCount, int assetCount, string logDirectory)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Space4XPPtrCastScanner] Scan complete.");
            builder.AppendLine($"[Space4XPPtrCastScanner] prefabs={prefabCount} scriptable_assets={assetCount} issues={records.Count}");

            foreach (var record in records)
            {
                builder.AppendLine($"asset={record.AssetPath}");
                builder.AppendLine($"object={record.ObjectName}");
                builder.AppendLine($"property={record.PropertyPath}");
                if (record.InstanceId != 0)
                {
                    builder.AppendLine($"instance_id={record.InstanceId}");
                }
                if (!string.IsNullOrEmpty(record.Message))
                {
                    builder.AppendLine($"message={record.Message}");
                }
                builder.AppendLine("--");
            }

            var outputDir = logDirectory;
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Application.dataPath;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
                var path = Path.Combine(outputDir, LogFileName);
                File.WriteAllText(path, builder.ToString());
                UDebug.LogWarning($"[Space4XPPtrCastScanner] Wrote report to {path}");
            }
            catch (Exception ex)
            {
                UDebug.LogWarning($"[Space4XPPtrCastScanner] Failed to write report: {ex.GetType().Name}");
                UDebug.Log(builder.ToString());
            }
        }
    }
}

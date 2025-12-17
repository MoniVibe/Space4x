using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Hash report for idempotency checking. Tracks per-asset hashes to detect changes.
    /// </summary>
    [Serializable]
    public class PrefabMakerHashReport
    {
        public string Version = "1.0";
        public string GeneratedAt;
        public Dictionary<string, string> PrefabHashes = new Dictionary<string, string>();
        public Dictionary<string, string> BindingHashes = new Dictionary<string, string>();
        public Dictionary<string, int> CatalogCounts = new Dictionary<string, int>();

        public static string GetReportPath(string catalogPath)
        {
            var reportDir = Path.Combine(Path.GetDirectoryName(catalogPath), "Reports");
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }
            return Path.Combine(reportDir, "prefab_maker_hash_report.json");
        }

        public static PrefabMakerHashReport Load(string catalogPath)
        {
            var reportPath = GetReportPath(catalogPath);
            if (File.Exists(reportPath))
            {
                try
                {
                    var json = File.ReadAllText(reportPath);
                    return JsonConvert.DeserializeObject<PrefabMakerHashReport>(json);
                }
                catch (Exception ex)
                {
                    UnityDebug.LogWarning($"Failed to load hash report: {ex.Message}");
                }
            }
            return new PrefabMakerHashReport();
        }

        public void Save(string catalogPath)
        {
            GeneratedAt = DateTime.UtcNow.ToString("O");
            var reportPath = GetReportPath(catalogPath);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(reportPath, json);
            AssetDatabase.ImportAsset(reportPath);
        }

        public static string ComputeFileHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning($"Failed to compute hash for {filePath}: {ex.Message}");
                return null;
            }
        }

        public static string ComputeContentHash(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            try
            {
                using (var md5 = MD5.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    var hash = md5.ComputeHash(bytes);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning($"Failed to compute content hash: {ex.Message}");
                return null;
            }
        }

        public HashComparisonResult Compare(PrefabMakerHashReport previous)
        {
            var result = new HashComparisonResult();

            // Compare prefab hashes
            foreach (var kvp in PrefabHashes)
            {
                if (previous.PrefabHashes.TryGetValue(kvp.Key, out var previousHash))
                {
                    if (previousHash != kvp.Value)
                    {
                        result.ChangedPrefabs.Add(kvp.Key);
                    }
                }
                else
                {
                    result.NewPrefabs.Add(kvp.Key);
                }
            }

            // Find removed prefabs
            foreach (var kvp in previous.PrefabHashes)
            {
                if (!PrefabHashes.ContainsKey(kvp.Key))
                {
                    result.RemovedPrefabs.Add(kvp.Key);
                }
            }

            // Compare binding hashes
            foreach (var kvp in BindingHashes)
            {
                if (previous.BindingHashes.TryGetValue(kvp.Key, out var previousHash))
                {
                    if (previousHash != kvp.Value)
                    {
                        result.ChangedBindings.Add(kvp.Key);
                    }
                }
            }

            // Compare catalog counts
            foreach (var kvp in CatalogCounts)
            {
                if (previous.CatalogCounts.TryGetValue(kvp.Key, out var previousCount))
                {
                    if (previousCount != kvp.Value)
                    {
                        result.ChangedCatalogCounts.Add(kvp.Key, new CatalogCountChange
                        {
                            Previous = previousCount,
                            Current = kvp.Value
                        });
                    }
                }
            }

            return result;
        }
    }

    public class HashComparisonResult
    {
        public List<string> ChangedPrefabs = new List<string>();
        public List<string> NewPrefabs = new List<string>();
        public List<string> RemovedPrefabs = new List<string>();
        public List<string> ChangedBindings = new List<string>();
        public Dictionary<string, CatalogCountChange> ChangedCatalogCounts = new Dictionary<string, CatalogCountChange>();

        public bool HasChanges => ChangedPrefabs.Count > 0 || NewPrefabs.Count > 0 || 
                                  RemovedPrefabs.Count > 0 || ChangedBindings.Count > 0 || 
                                  ChangedCatalogCounts.Count > 0;
    }

    public class CatalogCountChange
    {
        public int Previous;
        public int Current;
    }
}


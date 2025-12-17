using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor.PrefabMakerTool.Models
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Serialization helpers for template models.
    /// Supports JSON export/import for diff-friendly bulk editing.
    /// </summary>
    public static class TemplateSerialization
    {
        /// <summary>
        /// Export templates to JSON file.
        /// </summary>
        public static void ExportTemplatesToJson<T>(List<T> templates, string filePath) where T : PrefabTemplate
        {
            var json = JsonConvert.SerializeObject(templates, Formatting.Indented);
            File.WriteAllText(filePath, json);
            AssetDatabase.Refresh();
            UnityDebug.Log($"Exported {templates.Count} templates to {filePath}");
        }
        
        /// <summary>
        /// Import templates from JSON file.
        /// </summary>
        public static List<T> ImportTemplatesFromJson<T>(string filePath) where T : PrefabTemplate
        {
            if (!File.Exists(filePath))
            {
                UnityDebug.LogError($"Template file not found: {filePath}");
                return new List<T>();
            }
            
            var json = File.ReadAllText(filePath);
            var templates = JsonConvert.DeserializeObject<List<T>>(json);
            return templates ?? new List<T>();
        }
        
        /// <summary>
        /// Save template snapshot for comparison.
        /// </summary>
        public static void SaveTemplateSnapshot(Dictionary<string, List<PrefabTemplate>> templatesByCategory, string catalogPath)
        {
            var snapshotPath = $"{catalogPath}/TemplateSnapshot.json";
            var json = JsonConvert.SerializeObject(templatesByCategory, Formatting.Indented);
            File.WriteAllText(snapshotPath, json);
            UnityDebug.Log($"Saved template snapshot to {snapshotPath}");
        }
        
        /// <summary>
        /// Load template snapshot for comparison.
        /// </summary>
        public static Dictionary<string, List<PrefabTemplate>> LoadTemplateSnapshot(string catalogPath)
        {
            var snapshotPath = $"{catalogPath}/TemplateSnapshot.json";
            if (!File.Exists(snapshotPath))
            {
                return new Dictionary<string, List<PrefabTemplate>>();
            }
            
            var json = File.ReadAllText(snapshotPath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<PrefabTemplate>>>(json) 
                ?? new Dictionary<string, List<PrefabTemplate>>();
        }
    }
}


using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor.MCP.Tests
{
    /// <summary>
    /// Batch render preview frames for BlueSnowOrb parameter samples.
    /// </summary>
    public static class BatchRenderVfxPreview
    {
        private const string GraphPath = "Assets/VFX/BlueSnowOrb.vfx";
        private const string SamplesJsonRelative = "Scripts/data/BlueSnowOrb.json";

        [MenuItem("Tools/MCP/Batch Render BlueSnowOrb Frames")]
        public static void RenderAll()
        {
            Debug.Log("=== Batch rendering BlueSnowOrb previews ===");

            try
            {
                var jsonPath = Path.Combine(Application.dataPath, "..", SamplesJsonRelative);
                if (!File.Exists(jsonPath))
                {
                    Debug.LogError($"Sample data not found: {jsonPath}");
                    return;
                }

                var content = File.ReadAllText(jsonPath);
                var samples = JArray.Parse(content);
                if (samples.Count == 0)
                {
                    Debug.LogWarning("No samples found in JSON.");
                    return;
                }

                int rendered = 0;
                foreach (var sampleToken in samples)
                {
                    if (!(sampleToken is JObject sample))
                    {
                        continue;
                    }

                    var framePathRel = sample["frame_path"]?.ToString();
                    var paramsJson = sample["params_json"]?.ToString();
                    if (string.IsNullOrEmpty(framePathRel) || string.IsNullOrEmpty(paramsJson))
                    {
                        Debug.LogWarning("Sample missing frame_path or params_json; skipping.");
                        continue;
                    }

                    var normalizedParams = NormalizeParams(JObject.Parse(paramsJson));

                    var request = new JObject
                    {
                        ["graph_path"] = GraphPath,
                        ["size"] = 320,
                        ["seconds"] = 0.5f,
                        ["fps"] = 16,
                        ["frames"] = 1,
                        ["params"] = normalizedParams
                    };

                    var result = RenderVfxPreviewTool.HandleCommand(request) as JObject;
                    if (result == null || result["success"]?.ToObject<bool>() != true)
                    {
                        Debug.LogWarning($"Render failed for frame {framePathRel}: {result}");
                        continue;
                    }

                    var framesToken = result["data"]?["frames"] as JArray;
                    var generatedPath = framesToken?.FirstOrDefault()?.ToString();
                    if (string.IsNullOrEmpty(generatedPath) || !File.Exists(generatedPath))
                    {
                        Debug.LogWarning($"Generated frame missing: {generatedPath}");
                        continue;
                    }

                    var targetPath = Path.Combine(Application.dataPath, "..", framePathRel);
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(generatedPath, targetPath, overwrite: true);
                    Debug.Log($"Rendered frame -> {targetPath}");
                    rendered++;
                }

                Debug.Log($"Batch rendering complete. Frames written: {rendered}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Batch render failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static JObject NormalizeParams(JObject raw)
        {
            var normalized = new JObject();

            foreach (var property in raw.Properties())
            {
                JToken value = property.Value;
                if (value.Type == JTokenType.String)
                {
                    var str = value.ToString();
                    if (TryParseVector(str, out var array))
                    {
                        normalized[property.Name] = new JArray(array);
                    }
                    else
                    {
                        normalized[property.Name] = str;
                    }
                }
                else if (value.Type == JTokenType.Object)
                {
                    var obj = (JObject)value;
                    if (IsNumericVector(obj, out var vectorComponents))
                    {
                        normalized[property.Name] = new JArray(vectorComponents);
                    }
                    else
                    {
                        normalized[property.Name] = value;
                    }
                }
                else
                {
                    normalized[property.Name] = value;
                }
            }

            return normalized;
        }

        private static bool IsNumericVector(JObject obj, out float[] components)
        {
            components = Array.Empty<float>();
            var keys = new[] { "x", "y", "z", "w" };
            var present = keys.Where(k => obj[k] != null).ToList();
            if (present.Count == 0)
            {
                return false;
            }

            var list = present.Select(k =>
            {
                var token = obj[k];
                return token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                    ? (float?)token.ToObject<float>()
                    : null;
            }).ToList();

            if (list.Any(v => v == null))
            {
                return false;
            }

            components = list.Select(v => v.Value).ToArray();
            return true;
        }

        private static bool TryParseVector(string str, out float[] components)
        {
            components = Array.Empty<float>();
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            if (!str.StartsWith("(") || !str.EndsWith(")"))
            {
                return false;
            }

            var parts = str.Trim('(', ')').Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            var floats = parts.Select(p => float.TryParse(p, out var f) ? (float?)f : null).ToArray();
            if (floats.Any(f => f == null))
            {
                return false;
            }

            components = floats.Select(f => f.Value).ToArray();
            return true;
        }
    }
}

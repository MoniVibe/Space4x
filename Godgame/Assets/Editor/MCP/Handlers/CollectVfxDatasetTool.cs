using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.VFX;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("collect_vfx_dataset")]
    public static class CollectVfxDatasetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphId = @params["graph_id"]?.ToString();
                var graphPath = @params["graph_path"]?.ToString();
                int samples = @params["samples"]?.ToObject<int>() ?? 32;
                string outputDir = @params["output_dir"]?.ToString() ?? "Data/MCP_Exports";
                int size = @params["size"]?.ToObject<int>() ?? 320;
                float seconds = @params["seconds"]?.ToObject<float>() ?? 0.5f;
                int fps = @params["fps"]?.ToObject<int>() ?? 16;
                int seed = @params["seed"]?.ToObject<int>() ?? 0;

                // Resolve graph path if graph_id provided
                if (!string.IsNullOrEmpty(graphId) && string.IsNullOrEmpty(graphPath))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                    {
                        return Response.Error($"Graph with id '{graphId}' not found");
                    }
                }

                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path or graph_id is required");
                }

                // Get graph descriptor via dispatcher
                var descParams = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["lod"] = 2
                };
                var descResponse = DescribeVfxGraphTool.HandleCommand(descParams);
                Debug.Log($"[CollectVfxDataset] describe response type: {descResponse?.GetType().FullName ?? "null"}");
                Debug.Log($"[CollectVfxDataset] describe response json: {Newtonsoft.Json.JsonConvert.SerializeObject(descResponse)}");

                var descJObject = descResponse as JObject;
                if (descJObject == null && descResponse != null)
                {
                    descJObject = JObject.FromObject(descResponse);
                }

                if (descJObject == null || descJObject["success"]?.ToObject<bool>() != true)
                {
                    var errorMsg = descJObject?["message"]?.ToString()
                                   ?? descJObject?["error"]?.ToString()
                                   ?? "Failed to get descriptor";
                    return Response.Error($"Failed to get graph descriptor: {errorMsg}");
                }

                var dataToken = descJObject["data"] as JObject;
                var exposedParamsArray = dataToken?["exposed_params"] as JArray;

                if (exposedParamsArray == null || exposedParamsArray.Count == 0)
                {
                    return Response.Error("No exposed parameters found in graph");
                }

                // Convert exposed params to list of dictionaries for sampling
                var exposedParams = exposedParamsArray
                    .Select(token => token?.ToObject<Dictionary<string, object>>())
                    .Where(dict => dict != null)
                    .ToList();

                // Save descriptor to disk
                var graphIdFinal = System.IO.Path.GetFileNameWithoutExtension(graphPath);
                var outputPath = Path.Combine(Application.dataPath, "..", outputDir);
                Directory.CreateDirectory(outputPath);
                
                var descPath = Path.Combine(outputPath, $"{graphIdFinal}_descriptor.json");
                File.WriteAllText(descPath, descJObject.ToString(Newtonsoft.Json.Formatting.Indented));
                Debug.Log($"Saved descriptor to {descPath}");

                // Sample parameters
                var paramSamples = SampleParameters(exposedParams, samples, seed);

                // Collect data
                var rows = new List<Dictionary<string, object>>();
                var rng = new System.Random(seed);

                for (int i = 0; i < paramSamples.Count; i++)
                {
                    var sampleParams = paramSamples[i];
                    
                    // Render preview via dispatcher
                    var renderParams = new JObject
                    {
                        ["graph_path"] = graphPath,
                        ["params"] = JObject.FromObject(sampleParams),
                        ["size"] = size,
                        ["seconds"] = seconds,
                        ["fps"] = fps,
                        ["frames"] = 1
                    };

                    var renderResponse = RenderVfxPreviewTool.HandleCommand(renderParams);

                    // Parse response
                    Dictionary<string, object> renderDict = null;
                    if (renderResponse is Dictionary<string, object> renderResponseDict)
                    {
                        renderDict = renderResponseDict;
                    }
                    else if (renderResponse != null)
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(renderResponse);
                        renderDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    }

                    if (renderDict == null || !renderDict.ContainsKey("success") || !(bool)renderDict["success"])
                    {
                        var errorMsg = renderDict?.GetValueOrDefault("message", "Unknown error")?.ToString() ?? "Render failed";
                        Debug.LogWarning($"Failed to render preview {i}: {errorMsg}");
                        continue;
                    }

                    var renderData = renderDict.GetValueOrDefault("data", new Dictionary<string, object>()) as Dictionary<string, object>;
                    var framePath = renderData?.GetValueOrDefault("framePath", "")?.ToString() ?? 
                                   renderData?.GetValueOrDefault("frame_path", "")?.ToString() ?? "";

                    if (string.IsNullOrEmpty(framePath))
                    {
                        var frames = renderData?.GetValueOrDefault("frames", null);
                        if (frames is List<object> frameList && frameList.Count > 0)
                        {
                            framePath = frameList[0]?.ToString() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(framePath))
                    {
                        rows.Add(new Dictionary<string, object>
                        {
                            ["graph_id"] = graphIdFinal,
                            ["graph_path"] = graphPath,
                            ["params_json"] = Newtonsoft.Json.JsonConvert.SerializeObject(sampleParams),
                            ["frame_path"] = framePath,
                            ["sample_index"] = i
                        });
                    }
                }

                // Save CSV
                if (rows.Count > 0)
                {
                    var csvPath = Path.Combine(outputPath, $"{graphIdFinal}.csv");
                    SaveCsv(csvPath, rows);
                    Debug.Log($"Saved {rows.Count} samples to {csvPath}");
                }

                return Response.Success($"Collected {rows.Count} samples for {graphIdFinal}", new
                {
                    graph_id = graphIdFinal,
                    samples_collected = rows.Count,
                    samples_requested = samples,
                    output_dir = outputPath,
                    descriptor_path = descPath,
                    csv_path = rows.Count > 0 ? Path.Combine(outputPath, $"{graphIdFinal}.csv") : null
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to collect dataset: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string ResolveGraphIdToPath(string graphId)
        {
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(graphId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return null;
        }

        private static List<Dictionary<string, object>> SampleParameters(List<Dictionary<string, object>> exposedParams, int n, int seed)
        {
            var rng = new System.Random(seed);
            var samples = new List<Dictionary<string, object>>();

            for (int i = 0; i < n; i++)
            {
                var sample = new Dictionary<string, object>();
                
                foreach (var paramObj in exposedParams)
                {
                    if (!(paramObj is Dictionary<string, object> param))
                        continue;

                    var exposedName = param.GetValueOrDefault("exposedName", "")?.ToString() ?? "";
                    var paramType = param.GetValueOrDefault("type", "")?.ToString() ?? "";
                    var value = param.GetValueOrDefault("value", null);
                    var min = param.GetValueOrDefault("min", null);
                    var max = param.GetValueOrDefault("max", null);

                    if (string.IsNullOrEmpty(exposedName))
                        continue;

                    // Sample based on type
                    if (paramType == "Float")
                    {
                        var minVal = min != null ? Convert.ToSingle(min) : 0f;
                        var maxVal = max != null ? Convert.ToSingle(max) : 100f;
                        sample[exposedName] = (float)(rng.NextDouble() * (maxVal - minVal) + minVal);
                    }
                    else if (paramType == "Int")
                    {
                        var minVal = min != null ? Convert.ToInt32(min) : 0;
                        var maxVal = max != null ? Convert.ToInt32(max) : 100;
                        sample[exposedName] = rng.Next(minVal, maxVal + 1);
                    }
                    else if (paramType == "Vector2")
                    {
                        var minVec = min as Dictionary<string, object> ?? new Dictionary<string, object> { ["x"] = -10f, ["y"] = -10f };
                        var maxVec = max as Dictionary<string, object> ?? new Dictionary<string, object> { ["x"] = 10f, ["y"] = 10f };
                        sample[exposedName] = new float[]
                        {
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("x", 10f)) - Convert.ToSingle(minVec.GetValueOrDefault("x", -10f))) + Convert.ToSingle(minVec.GetValueOrDefault("x", -10f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("y", 10f)) - Convert.ToSingle(minVec.GetValueOrDefault("y", -10f))) + Convert.ToSingle(minVec.GetValueOrDefault("y", -10f)))
                        };
                    }
                    else if (paramType == "Vector3")
                    {
                        var minVec = min as Dictionary<string, object> ?? new Dictionary<string, object> { ["x"] = -10f, ["y"] = -10f, ["z"] = -10f };
                        var maxVec = max as Dictionary<string, object> ?? new Dictionary<string, object> { ["x"] = 10f, ["y"] = 10f, ["z"] = 10f };
                        sample[exposedName] = new float[]
                        {
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("x", 10f)) - Convert.ToSingle(minVec.GetValueOrDefault("x", -10f))) + Convert.ToSingle(minVec.GetValueOrDefault("x", -10f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("y", 10f)) - Convert.ToSingle(minVec.GetValueOrDefault("y", -10f))) + Convert.ToSingle(minVec.GetValueOrDefault("y", -10f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("z", 10f)) - Convert.ToSingle(minVec.GetValueOrDefault("z", -10f))) + Convert.ToSingle(minVec.GetValueOrDefault("z", -10f)))
                        };
                    }
                    else if (paramType == "Color")
                    {
                        var minVec = min as Dictionary<string, object> ?? new Dictionary<string, object> { ["r"] = 0f, ["g"] = 0f, ["b"] = 0f, ["a"] = 0f };
                        var maxVec = max as Dictionary<string, object> ?? new Dictionary<string, object> { ["r"] = 1f, ["g"] = 1f, ["b"] = 1f, ["a"] = 1f };
                        sample[exposedName] = new float[]
                        {
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("r", 1f)) - Convert.ToSingle(minVec.GetValueOrDefault("r", 0f))) + Convert.ToSingle(minVec.GetValueOrDefault("r", 0f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("g", 1f)) - Convert.ToSingle(minVec.GetValueOrDefault("g", 0f))) + Convert.ToSingle(minVec.GetValueOrDefault("g", 0f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("b", 1f)) - Convert.ToSingle(minVec.GetValueOrDefault("b", 0f))) + Convert.ToSingle(minVec.GetValueOrDefault("b", 0f))),
                            (float)(rng.NextDouble() * (Convert.ToSingle(maxVec.GetValueOrDefault("a", 1f)) - Convert.ToSingle(minVec.GetValueOrDefault("a", 0f))) + Convert.ToSingle(minVec.GetValueOrDefault("a", 0f)))
                        };
                    }
                    else
                    {
                        // Use default value for unsupported types
                        sample[exposedName] = value;
                    }
                }

                samples.Add(sample);
            }

            return samples;
        }

        private static void SaveCsv(string csvPath, List<Dictionary<string, object>> rows)
        {
            if (rows.Count == 0)
                return;

            using (var writer = new StreamWriter(csvPath))
            {
                // Write header
                var columns = new[] { "graph_id", "graph_path", "params_json", "frame_path", "sample_index" };
                writer.WriteLine(string.Join(",", columns.Select(c => $"\"{c}\"")));

                // Write rows
                foreach (var row in rows)
                {
                    var values = new List<string>();
                    foreach (var col in columns)
                    {
                        var value = row.GetValueOrDefault(col, "")?.ToString() ?? "";
                        // Escape quotes and wrap in quotes
                        value = value.Replace("\"", "\"\"");
                        values.Add($"\"{value}\"");
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }
    }
}


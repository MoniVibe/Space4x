using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Newtonsoft.Json.Linq;
using PureDOTS.Editor.MCP;

namespace PureDOTS.Editor.MCP.Helpers
{
    /// <summary>
    /// Shared helper for extracting VFX parameters and asset bindings from VisualEffect components.
    /// Used by all capture tools to ensure consistent parameter/asset logging.
    /// </summary>
    public static class VfxParameterExtractor
    {
        /// <summary>
        /// Extracts all exposed parameters and asset bindings from a VisualEffect component.
        /// </summary>
        /// <param name="visualEffect">The VisualEffect component to extract from</param>
        /// <returns>Tuple of (parameters dictionary, asset bindings dictionary)</returns>
        public static (Dictionary<string, object> parameters, Dictionary<string, string> assetBindings) ExtractParametersAndAssets(VisualEffect visualEffect)
        {
            var paramsDict = new Dictionary<string, object>();
            var assetBindings = new Dictionary<string, string>();
            
            if (visualEffect == null || visualEffect.visualEffectAsset == null)
                return (paramsDict, assetBindings);

            // Get exposed parameters from the graph descriptor
            try
            {
                var graphPath = AssetDatabase.GetAssetPath(visualEffect.visualEffectAsset);
                var descParams = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["lod"] = 1 // Just need parameter list
                };
                var descResponse = DescribeVfxGraphTool.HandleCommand(descParams);
                
                if (descResponse is JObject responseObj && responseObj["success"]?.ToObject<bool>() == true)
                {
                    var data = responseObj["data"] as JObject;
                    if (data == null)
                        return (paramsDict, assetBindings);
                        
                    var exposedParams = data["exposed_params"] as JArray;
                    if (exposedParams != null)
                    {
                        foreach (var paramToken in exposedParams)
                        {
                            if (paramToken is JObject paramObj)
                            {
                                var name = paramObj["k"]?.ToString();
                                var type = paramObj["t"]?.ToString();
                                
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                try
                                {
                                    bool valueExtracted = false;
                                    
                                    switch (type)
                                    {
                                        case "Float":
                                            if (visualEffect.HasFloat(name))
                                            {
                                                paramsDict[name] = visualEffect.GetFloat(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                paramsDict[name] = paramObj["def"].ToObject<float>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Int":
                                            if (visualEffect.HasInt(name))
                                            {
                                                paramsDict[name] = visualEffect.GetInt(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                paramsDict[name] = paramObj["def"].ToObject<int>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Vector2":
                                            if (visualEffect.HasVector2(name))
                                            {
                                                var v2 = visualEffect.GetVector2(name);
                                                paramsDict[name] = new[] { v2.x, v2.y };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 2)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Vector3":
                                            if (visualEffect.HasVector3(name))
                                            {
                                                var v3 = visualEffect.GetVector3(name);
                                                paramsDict[name] = new[] { v3.x, v3.y, v3.z };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 3)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>(), defArray[2].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Vector4":
                                        case "Color":
                                            if (visualEffect.HasVector4(name))
                                            {
                                                var v4 = visualEffect.GetVector4(name);
                                                paramsDict[name] = new[] { v4.x, v4.y, v4.z, v4.w };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 4)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>(), defArray[2].ToObject<float>(), defArray[3].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Bool":
                                            if (visualEffect.HasBool(name))
                                            {
                                                paramsDict[name] = visualEffect.GetBool(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                paramsDict[name] = paramObj["def"].ToObject<bool>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Texture2D":
                                        case "Texture":
                                            if (visualEffect.HasTexture(name))
                                            {
                                                var tex = visualEffect.GetTexture(name);
                                                if (tex != null)
                                                {
                                                    var texPath = AssetDatabase.GetAssetPath(tex);
                                                    if (!string.IsNullOrEmpty(texPath))
                                                    {
                                                        paramsDict[name] = texPath;
                                                        assetBindings[$"texture_{name}"] = texPath;
                                                        valueExtracted = true;
                                                    }
                                                }
                                            }
                                            break;
                                        case "Mesh":
                                            if (visualEffect.HasMesh(name))
                                            {
                                                var mesh = visualEffect.GetMesh(name);
                                                if (mesh != null)
                                                {
                                                    var meshPath = AssetDatabase.GetAssetPath(mesh);
                                                    if (!string.IsNullOrEmpty(meshPath))
                                                    {
                                                        paramsDict[name] = meshPath;
                                                        assetBindings[$"mesh_{name}"] = meshPath;
                                                        valueExtracted = true;
                                                    }
                                                }
                                            }
                                            break;
                                    }
                                    
                                    if (!valueExtracted)
                                    {
                                        Debug.LogWarning($"[VfxParameterExtractor] Could not extract parameter '{name}' (type: {type}) - no value set and no default found");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[VfxParameterExtractor] Failed to extract parameter '{name}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VfxParameterExtractor] Failed to get parameter list: {ex.Message}");
            }

            return (paramsDict, assetBindings);
        }

        /// <summary>
        /// Saves parameters and asset bindings to JSON files in the output directory.
        /// </summary>
        public static void SaveParametersAndAssets(string outputDir, string graphName, Dictionary<string, object> parameters, Dictionary<string, string> assetBindings)
        {
            try
            {
                System.IO.Directory.CreateDirectory(outputDir);

                // Save parameters as JSON
                var paramsJson = Newtonsoft.Json.JsonConvert.SerializeObject(parameters, Newtonsoft.Json.Formatting.Indented);
                var paramsPath = System.IO.Path.Combine(outputDir, $"{graphName}_params.json");
                System.IO.File.WriteAllText(paramsPath, paramsJson);
                Debug.Log($"[VfxParameterExtractor] Saved {parameters.Count} parameters to {paramsPath}");

                // Save asset bindings as JSON (textures, meshes, materials)
                if (assetBindings.Count > 0)
                {
                    var assetsJson = Newtonsoft.Json.JsonConvert.SerializeObject(assetBindings, Newtonsoft.Json.Formatting.Indented);
                    var assetsPath = System.IO.Path.Combine(outputDir, $"{graphName}_assets.json");
                    System.IO.File.WriteAllText(assetsPath, assetsJson);
                    Debug.Log($"[VfxParameterExtractor] Saved {assetBindings.Count} asset bindings to {assetsPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VfxParameterExtractor] Failed to save parameters/assets: {ex.Message}");
            }
        }
    }
}


using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_vfx_instance")]
    public static class CreateVfxInstanceTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var graphId = @params["template_id"]?.ToString() ?? @params["graph_id"]?.ToString();
                var transformObj = @params["transform"];
                var parametersToken = @params["params"];
                var instanceName = @params["instance_name"]?.ToString() ?? "VFXInstance";

                // Resolve graph path if graph_id/template_id provided
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
                    return Response.Error("graph_path or template_id/graph_id is required");
                }

                var graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                {
                    return Response.Error($"VFX graph not found at path: {graphPath}");
                }

                // Parse transform
                Vector3 position = Vector3.zero;
                Vector3 rotation = Vector3.zero;
                float scale = 1.0f;
                GameObject parent = null;

                if (transformObj != null && transformObj.Type == JTokenType.Object)
                {
                    var transform = transformObj.ToObject<JObject>();
                    if (transform["position"] != null)
                    {
                        var pos = transform["position"].ToObject<JObject>();
                        position = new Vector3(
                            pos["x"]?.ToObject<float>() ?? 0f,
                            pos["y"]?.ToObject<float>() ?? 0f,
                            pos["z"]?.ToObject<float>() ?? 0f
                        );
                    }
                    if (transform["rotation"] != null)
                    {
                        var rot = transform["rotation"].ToObject<JObject>();
                        rotation = new Vector3(
                            rot["x"]?.ToObject<float>() ?? 0f,
                            rot["y"]?.ToObject<float>() ?? 0f,
                            rot["z"]?.ToObject<float>() ?? 0f
                        );
                    }
                    if (transform["scale"] != null)
                    {
                        scale = transform["scale"].ToObject<float>();
                    }
                    if (transform["parent"] != null)
                    {
                        var parentName = transform["parent"].ToString();
                        parent = GameObject.Find(parentName);
                    }
                }

                // Create GameObject with VisualEffect component
                var go = new GameObject(instanceName);
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(rotation);
                go.transform.localScale = Vector3.one * scale;
                if (parent != null)
                {
                    go.transform.SetParent(parent.transform);
                }

                var visualEffect = go.AddComponent<UnityEngine.VFX.VisualEffect>();
                visualEffect.visualEffectAsset = graphAsset;

                // Apply parameters
                if (parametersToken != null && parametersToken.Type == JTokenType.Object)
                {
                    var paramObj = parametersToken.ToObject<JObject>();
                    foreach (var prop in paramObj.Properties())
                    {
                        ApplyParameter(visualEffect, prop.Name, prop.Value);
                    }
                }

                // Play the effect
                visualEffect.Play();

                Undo.RegisterCreatedObjectUndo(go, "Create VFX Instance");
                Selection.activeGameObject = go;

                return Response.Success($"VFX instance created: {instanceName}", new
                {
                    instanceName = go.name,
                    instanceId = go.GetInstanceID(),
                    graphPath,
                    position = new { x = position.x, y = position.y, z = position.z },
                    rotation = new { x = rotation.x, y = rotation.y, z = rotation.z },
                    scale
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create VFX instance: {ex.Message}");
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

        private static void ApplyParameter(UnityEngine.VFX.VisualEffect vfx, string paramName, JToken value)
        {
            try
            {
                if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                {
                    var num = value.ToObject<double>();
                    if (num == Math.Floor(num))
                    {
                        vfx.SetInt(paramName, (int)num);
                    }
                    else
                    {
                        vfx.SetFloat(paramName, (float)num);
                    }
                }
                else if (value.Type == JTokenType.Boolean)
                {
                    vfx.SetBool(paramName, value.ToObject<bool>());
                }
                else if (value.Type == JTokenType.Array)
                {
                    var arr = value.ToObject<float[]>();
                    if (arr.Length == 2)
                    {
                        vfx.SetVector2(paramName, new Vector2(arr[0], arr[1]));
                    }
                    else if (arr.Length == 3)
                    {
                        vfx.SetVector3(paramName, new Vector3(arr[0], arr[1], arr[2]));
                    }
                    else if (arr.Length == 4)
                    {
                        vfx.SetVector4(paramName, new Vector4(arr[0], arr[1], arr[2], arr[3]));
                    }
                }
                else if (value.Type == JTokenType.Object)
                {
                    var obj = value.ToObject<JObject>();
                    if (obj["x"] != null && obj["y"] != null)
                    {
                        if (obj["z"] != null && obj["w"] != null)
                        {
                            vfx.SetVector4(paramName, new Vector4(
                                obj["x"].ToObject<float>(),
                                obj["y"].ToObject<float>(),
                                obj["z"].ToObject<float>(),
                                obj["w"].ToObject<float>()
                            ));
                        }
                        else if (obj["z"] != null)
                        {
                            vfx.SetVector3(paramName, new Vector3(
                                obj["x"].ToObject<float>(),
                                obj["y"].ToObject<float>(),
                                obj["z"].ToObject<float>()
                            ));
                        }
                        else
                        {
                            vfx.SetVector2(paramName, new Vector2(
                                obj["x"].ToObject<float>(),
                                obj["y"].ToObject<float>()
                            ));
                        }
                    }
                    else if (obj["r"] != null || obj["g"] != null)
                    {
                        vfx.SetVector4(paramName, new Vector4(
                            obj["r"]?.ToObject<float>() ?? 1f,
                            obj["g"]?.ToObject<float>() ?? 1f,
                            obj["b"]?.ToObject<float>() ?? 1f,
                            obj["a"]?.ToObject<float>() ?? 1f
                        ));
                    }
                }
                else if (value.Type == JTokenType.String)
                {
                    var str = value.ToString();
                    if (str.StartsWith("Assets/"))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(str);
                        if (tex != null)
                        {
                            vfx.SetTexture(paramName, tex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CreateVfxInstanceTool] Failed to set parameter '{paramName}': {ex.Message}");
            }
        }
    }
}


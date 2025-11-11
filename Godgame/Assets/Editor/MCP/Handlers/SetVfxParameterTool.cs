using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("set_vfx_parameter")]
    public static class SetVfxParameterTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();
                var parameterValue = @params["parameter_value"];
                var parameterType = @params["parameter_type"]?.ToString()?.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (parameterValue == null)
                {
                    return Response.Error("parameter_value is required");
                }

                var go = GameObject.Find(gameObjectName);
                if (go == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }

                var visualEffect = go.GetComponent<UnityEngine.VFX.VisualEffect>();
                if (visualEffect == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' does not have a VisualEffect component");
                }

                // Auto-detect type if not specified
                if (string.IsNullOrEmpty(parameterType))
                {
                    parameterType = DetectParameterType(parameterValue);
                }

                // Set parameter based on type
                switch (parameterType)
                {
                    case "float":
                        visualEffect.SetFloat(parameterName, parameterValue.ToObject<float>());
                        break;
                    case "int":
                        visualEffect.SetInt(parameterName, parameterValue.ToObject<int>());
                        break;
                    case "bool":
                        visualEffect.SetBool(parameterName, parameterValue.ToObject<bool>());
                        break;
                    case "vector2":
                        var v2Obj = parameterValue.ToObject<JObject>();
                        if (v2Obj != null)
                        {
                            visualEffect.SetVector2(parameterName, new Vector2(
                                v2Obj["x"]?.ToObject<float>() ?? 0f,
                                v2Obj["y"]?.ToObject<float>() ?? 0f
                            ));
                        }
                        break;
                    case "vector3":
                        var v3Obj = parameterValue.ToObject<JObject>();
                        if (v3Obj != null)
                        {
                            visualEffect.SetVector3(parameterName, new Vector3(
                                v3Obj["x"]?.ToObject<float>() ?? 0f,
                                v3Obj["y"]?.ToObject<float>() ?? 0f,
                                v3Obj["z"]?.ToObject<float>() ?? 0f
                            ));
                        }
                        break;
                    case "vector4":
                    case "color":
                        var v4Obj = parameterValue.ToObject<JObject>();
                        if (v4Obj != null)
                        {
                            visualEffect.SetVector4(parameterName, new Vector4(
                                v4Obj["x"]?.ToObject<float>() ?? v4Obj["r"]?.ToObject<float>() ?? 0f,
                                v4Obj["y"]?.ToObject<float>() ?? v4Obj["g"]?.ToObject<float>() ?? 0f,
                                v4Obj["z"]?.ToObject<float>() ?? v4Obj["b"]?.ToObject<float>() ?? 0f,
                                v4Obj["w"]?.ToObject<float>() ?? v4Obj["a"]?.ToObject<float>() ?? 1f
                            ));
                        }
                        break;
                    case "texture2d":
                        var texturePath = parameterValue.ToString();
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if (texture != null)
                        {
                            visualEffect.SetTexture(parameterName, texture);
                        }
                        else
                        {
                            return Response.Error($"Texture not found at path: {texturePath}");
                        }
                        break;
                    default:
                        return Response.Error($"Unsupported parameter type: {parameterType}");
                }

                EditorUtility.SetDirty(visualEffect);

                return Response.Success($"Parameter '{parameterName}' set on {gameObjectName}", new
                {
                    gameObjectName,
                    parameterName,
                    parameterType,
                    parameterValue = parameterValue.ToObject<object>()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set VFX parameter: {ex.Message}");
            }
        }

        private static string DetectParameterType(JToken value)
        {
            if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
            {
                var num = value.ToObject<double>();
                if (num == Math.Floor(num))
                {
                    return "int";
                }
                return "float";
            }
            if (value.Type == JTokenType.Boolean)
            {
                return "bool";
            }
            if (value.Type == JTokenType.Object)
            {
                var obj = value.ToObject<JObject>();
                if (obj["x"] != null && obj["y"] != null && obj["z"] != null && obj["w"] != null)
                {
                    return "vector4";
                }
                if (obj["x"] != null && obj["y"] != null && obj["z"] != null)
                {
                    return "vector3";
                }
                if (obj["x"] != null && obj["y"] != null)
                {
                    return "vector2";
                }
                if (obj["r"] != null || obj["g"] != null)
                {
                    return "color";
                }
            }
            if (value.Type == JTokenType.String)
            {
                var str = value.ToString();
                if (str.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    str.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    str.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    return "texture2d";
                }
            }
            return "float"; // Default fallback
        }
    }
}


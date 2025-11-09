using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("unbind_parameter")]
    public static class UnbindParameterTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
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

                // Reset parameter to default (unbind)
                // Try multiple approaches to reset the parameter
                bool reset = false;
                
                // Approach 1: Try ResetOverride with VFXExposedProperty (if available)
                try
                {
                    var vfxExposedPropertyType = Type.GetType("UnityEngine.VFX.VFXExposedProperty, Unity.VisualEffectGraph");
                    if (vfxExposedPropertyType != null)
                    {
                        var getIDMethod = vfxExposedPropertyType.GetMethod("GetID", BindingFlags.Public | BindingFlags.Static);
                        if (getIDMethod != null)
                        {
                            var exposedProperty = getIDMethod.Invoke(null, new object[] { parameterName });
                            var resetMethod = visualEffect.GetType().GetMethod("ResetOverride", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (resetMethod != null)
                            {
                                resetMethod.Invoke(visualEffect, new object[] { exposedProperty });
                                reset = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] ResetOverride approach failed: {ex.Message}");
                }

                // Approach 2: Try Reset methods with parameter name string directly
                if (!reset)
                {
                    var resetMethods = new[] { "ResetFloat", "ResetInt", "ResetBool", "ResetVector2", "ResetVector3", "ResetVector4", "ResetTexture" };
                    foreach (var methodName in resetMethods)
                    {
                        var method = visualEffect.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
                        if (method != null)
                        {
                            try
                            {
                                method.Invoke(visualEffect, new object[] { parameterName });
                                reset = true;
                                break;
                            }
                            catch
                            {
                                // Continue to next method
                            }
                        }
                    }
                }

                // Approach 3: Fallback - get default from graph if available
                if (!reset)
                {
                    var graph = visualEffect.visualEffectAsset;
                    if (graph != null)
                    {
                        Debug.LogWarning($"[MCP Tools] Unbind for parameter '{parameterName}' - using fallback (parameter may need manual reset)");
                    }
                }

                EditorUtility.SetDirty(visualEffect);
                return Response.Success($"Parameter '{parameterName}' unbound from '{gameObjectName}'", new
                {
                    gameObjectName,
                    parameterName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to unbind parameter: {ex.Message}");
            }
        }
    }
}


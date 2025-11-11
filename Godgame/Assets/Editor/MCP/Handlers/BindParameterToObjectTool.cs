using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("bind_parameter_to_object")]
    public static class BindParameterToObjectTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();
                var targetObjectName = @params["target_object_name"]?.ToString();
                var propertyPath = @params["property_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (string.IsNullOrWhiteSpace(targetObjectName))
                {
                    return Response.Error("target_object_name is required");
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

                var targetGo = GameObject.Find(targetObjectName);
                if (targetGo == null)
                {
                    return Response.Error($"Target GameObject '{targetObjectName}' not found");
                }

                if (string.IsNullOrEmpty(propertyPath))
                {
                    return Response.Error("property_path is required for binding");
                }

                Component targetComponent = null;
                PropertyInfo propertyInfo = null;

                foreach (var component in targetGo.GetComponents<Component>())
                {
                    propertyInfo = component.GetType().GetProperty(propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (propertyInfo != null)
                    {
                        targetComponent = component;
                        break;
                    }
                }

                if (targetComponent == null || propertyInfo == null)
                {
                    return Response.Error($"Property '{propertyPath}' not found on target object");
                }

                var value = propertyInfo.GetValue(targetComponent);
                SetParameterValue(visualEffect, parameterName, value);

                EditorUtility.SetDirty(visualEffect);
                return Response.Success($"Parameter '{parameterName}' bound to '{targetObjectName}.{propertyPath}'", new
                {
                    gameObjectName,
                    parameterName,
                    targetObjectName,
                    propertyPath
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to bind parameter to object: {ex.Message}");
            }
        }

        private static void SetParameterValue(UnityEngine.VFX.VisualEffect vfx, string parameterName, object value)
        {
            if (value is float f)
            {
                vfx.SetFloat(parameterName, f);
            }
            else if (value is int i)
            {
                vfx.SetInt(parameterName, i);
            }
            else if (value is bool b)
            {
                vfx.SetBool(parameterName, b);
            }
            else if (value is Vector2 v2)
            {
                vfx.SetVector2(parameterName, v2);
            }
            else if (value is Vector3 v3)
            {
                vfx.SetVector3(parameterName, v3);
            }
            else if (value is Vector4 v4)
            {
                vfx.SetVector4(parameterName, v4);
            }
            else if (value is Color c)
            {
                vfx.SetVector4(parameterName, new Vector4(c.r, c.g, c.b, c.a));
            }
        }
    }
}


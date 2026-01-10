#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Space4X.Headless.Editor
{
    internal sealed class HeadlessVisualScriptingBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        private bool _disabled;
        private bool _previousValue;
        private static PropertyInfo s_usageProperty;
        private static bool s_usagePropertyResolved;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                return;
            }

            if (!TryGetUsageProperty(out var property))
            {
                return;
            }

            _previousValue = (bool)property.GetValue(null);
            if (_previousValue)
            {
                property.SetValue(null, false);
                _disabled = true;
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (_disabled)
            {
                if (TryGetUsageProperty(out var property))
                {
                    property.SetValue(null, _previousValue);
                }
                _disabled = false;
            }
        }

        private static bool TryGetUsageProperty(out PropertyInfo property)
        {
            if (!s_usagePropertyResolved)
            {
                s_usagePropertyResolved = true;
                var type = Type.GetType("Unity.VisualScripting.VSUsageUtility, Unity.VisualScripting.Core.Editor")
                           ?? Type.GetType("Unity.VisualScripting.VSUsageUtility, Unity.VisualScripting.Editor");
                if (type == null)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    for (var i = 0; i < assemblies.Length; i++)
                    {
                        type = assemblies[i].GetType("Unity.VisualScripting.VSUsageUtility", throwOnError: false);
                        if (type != null)
                        {
                            break;
                        }
                    }
                }
                if (type != null)
                {
                    s_usageProperty = type.GetProperty("isVisualScriptingUsed", BindingFlags.Public | BindingFlags.Static);
                }
            }

            property = s_usageProperty;
            return property != null && property.CanRead && property.CanWrite;
        }
    }
}
#endif

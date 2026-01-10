#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Space4X.Headless.Editor
{
    internal sealed class HeadlessEntitiesGraphicsBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                return;
            }

            TryRemoveEntitiesGraphicsRootsHandler();
        }

        private static void TryRemoveEntitiesGraphicsRootsHandler()
        {
            var utilityType = Type.GetType("Unity.Entities.UnityObjectRefUtility, Unity.Entities");
            if (utilityType == null)
            {
                return;
            }

            var field = utilityType.GetField("s_AdditionalRootsHandlerDelegates", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                return;
            }

            if (field.GetValue(null) is not IList list)
            {
                return;
            }

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is not Delegate handler)
                {
                    continue;
                }

                var declaringType = handler.Method.DeclaringType;
                if (declaringType != null && declaringType.FullName == "Unity.Rendering.EntitiesGraphicsSystemUtility")
                {
                    list.RemoveAt(i);
                }
            }
        }
    }
}
#endif

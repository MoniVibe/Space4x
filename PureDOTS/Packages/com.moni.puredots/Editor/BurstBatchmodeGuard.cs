using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor
{
    internal static class BurstBatchmodeGuard
    {
        [InitializeOnLoadMethod]
        private static void DisableBurstForBatchmodeTests()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            var args = System.Environment.GetCommandLineArgs();
            var isTestRun = Array.Exists(args, arg => string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase))
                || Array.Exists(args, arg => string.Equals(arg, "-testFilter", StringComparison.OrdinalIgnoreCase))
                || Array.Exists(args, arg => string.Equals(arg, "-testResults", StringComparison.OrdinalIgnoreCase));
            if (!isTestRun)
            {
                return;
            }

            var burstCompilerType = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
            if (burstCompilerType == null)
            {
                return;
            }

            var optionsProperty = burstCompilerType.GetProperty("Options", BindingFlags.Public | BindingFlags.Static);
            var optionsValue = optionsProperty?.GetValue(null);
            if (optionsValue == null)
            {
                return;
            }

            var enableProperty = optionsValue.GetType().GetProperty("EnableBurstCompilation", BindingFlags.Public | BindingFlags.Instance);
            if (enableProperty == null || !enableProperty.CanWrite)
            {
                return;
            }

            enableProperty.SetValue(optionsValue, false);
        }
    }
}

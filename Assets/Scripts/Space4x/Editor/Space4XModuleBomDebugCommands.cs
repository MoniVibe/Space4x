#if UNITY_EDITOR
using Space4X.Systems.Modules.Bom;
using UnityEditor;
using UnityEngine;

namespace Space4X.EditorTools
{
    public static class Space4XModuleBomDebugCommands
    {
        [MenuItem("Space4X/Modules/BOM V0/Roll 100 Modules Digest")]
        public static void Roll100ModulesDigestMenu()
        {
            Roll100ModulesDigest();
        }

        public static void Roll100ModulesDigest()
        {
            if (!Space4XModuleBomRollProbe.TryRollDigest100(out var result, out var error))
            {
                UnityEngine.Debug.LogError($"[Space4XModuleBomV0] roll probe failed: {error}");
                return;
            }

            UnityEngine.Debug.Log(Space4XModuleBomRollProbe.FormatMetricLine(result));
        }
    }
}
#endif

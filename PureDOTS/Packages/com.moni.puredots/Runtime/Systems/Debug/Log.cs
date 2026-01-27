using System.Diagnostics;

namespace PureDOTS.Systems.Debug
{
    internal static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal static void Info(string msg) => global::UnityEngine.Debug.Log(msg);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal static void Warn(string msg) => global::UnityEngine.Debug.LogWarning(msg);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal static void Error(string msg) => global::UnityEngine.Debug.LogError(msg);
    }
}

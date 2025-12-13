#if UNITY_EDITOR
using Unity.Burst;

namespace Space4X.Debug
{
    /// <summary>
    /// Burst-safe logging helpers for Space4X systems.
    /// </summary>
    static class Space4XBurstDebug
    {
        [BurstDiscard]
        public static void Log(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        [BurstDiscard]
        public static void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        [BurstDiscard]
        public static void LogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
#endif

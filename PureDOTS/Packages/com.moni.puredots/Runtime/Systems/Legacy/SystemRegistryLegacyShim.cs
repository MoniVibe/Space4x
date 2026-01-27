using Unity.Entities;
using UDebug = UnityEngine.Debug;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Legacy no-op shim to satisfy legacy scenario references to PureDOTS.Systems.SystemRegistry.
    /// Real engine/game code should use the current SystemRegistry APIs directly.
    /// </summary>
    public static partial class SystemRegistry
    {
        /// <summary>
        /// Legacy scenario helper: returns default handle, logs warning.
        /// </summary>
        public static SystemHandle Require<T>() where T : unmanaged, ISystem
        {
            UDebug.LogWarning($"[SystemRegistryLegacyShim] Require<{typeof(T).Name}> called from legacy scenario code; returning default handle.");
            return default;
        }

        /// <summary>
        /// Legacy scenario helper: returns false with default handle, logs warning.
        /// </summary>
        public static bool TryGet<T>(out SystemHandle handle) where T : unmanaged, ISystem
        {
            handle = default;
            UDebug.LogWarning($"[SystemRegistryLegacyShim] TryGet<{typeof(T).Name}> called from legacy scenario code; returning false.");
            return false;
        }

        /// <summary>
        /// Legacy scenario helper for groups: returns default handle, logs warning.
        /// </summary>
        public static SystemHandle RequireGroup<T>() where T : unmanaged, ISystem
        {
            UDebug.LogWarning($"[SystemRegistryLegacyShim] RequireGroup<{typeof(T).Name}> called from legacy scenario code; returning default handle.");
            return default;
        }

        /// <summary>
        /// Legacy scenario helper for groups: returns false with default handle, logs warning.
        /// </summary>
        public static bool TryGetGroup<T>(out SystemHandle handle) where T : unmanaged, ISystem
        {
            handle = default;
            UDebug.LogWarning($"[SystemRegistryLegacyShim] TryGetGroup<{typeof(T).Name}> called from legacy scenario code; returning false.");
            return false;
        }
    }
}

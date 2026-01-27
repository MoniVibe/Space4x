using Unity.Entities;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Component storing the effective delta time for an entity.
    /// Effective delta = global delta * LocalTimeScale.Value
    /// Systems that advance progress/cooldowns/XP should use this instead of raw delta.
    /// </summary>
    public struct EffectiveDeltaTime : IComponentData
    {
        /// <summary>
        /// Effective delta time in seconds (global delta * local time scale).
        /// </summary>
        public float Value;
    }
}




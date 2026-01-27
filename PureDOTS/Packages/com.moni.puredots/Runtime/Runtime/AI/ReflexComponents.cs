using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Reflex layer state (HOT path).
    /// Dodge incoming projectile, step back from cliff, break collision.
    /// Triggered by events ("hit", "near grenade") or very cheap checks.
    /// </summary>
    public struct ReflexState : IComponentData
    {
        /// <summary>
        /// Reflex action type.
        /// </summary>
        public ReflexAction CurrentAction;

        /// <summary>
        /// Direction for dodge/step back (normalized).
        /// </summary>
        public float3 ReflexDirection;

        /// <summary>
        /// Remaining duration for current reflex action (in ticks).
        /// </summary>
        public byte RemainingDuration;

        /// <summary>
        /// Tick when reflex was triggered.
        /// </summary>
        public uint TriggerTick;
    }

    /// <summary>
    /// Reflex action types.
    /// </summary>
    public enum ReflexAction : byte
    {
        None = 0,
        Dodge = 1,
        StepBack = 2,
        BreakCollision = 3,
        Evade = 4
    }

    /// <summary>
    /// Reflex trigger event - fired when entity needs immediate reflex response.
    /// </summary>
    public struct ReflexTrigger : IComponentData
    {
        /// <summary>
        /// Type of reflex needed.
        /// </summary>
        public ReflexAction Action;

        /// <summary>
        /// Direction for reflex (normalized).
        /// </summary>
        public float3 Direction;

        /// <summary>
        /// Duration of reflex action (in ticks).
        /// </summary>
        public byte Duration;

        /// <summary>
        /// Tick when triggered.
        /// </summary>
        public uint TriggerTick;
    }
}


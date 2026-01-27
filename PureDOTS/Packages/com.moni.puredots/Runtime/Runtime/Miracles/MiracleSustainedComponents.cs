using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Component attached to sustained miracle effect entities.
    /// Tracks the effect's owner, target, and channeling state.
    /// </summary>
    public struct MiracleSustainedEffect : IComponentData
    {
        /// <summary>Entity that owns/channels this effect (caster/hand).</summary>
        public Entity Owner;

        /// <summary>Miracle ID that created this effect.</summary>
        public MiracleId Id;

        /// <summary>Target point in world space where effect is centered.</summary>
        public float3 TargetPoint;

        /// <summary>Radius of the effect.</summary>
        public float Radius;

        /// <summary>Intensity multiplier (0-1 or more).</summary>
        public float Intensity;

        /// <summary>Whether currently channeling (1 = active, 0 = stopped).</summary>
        public byte IsChanneling;
    }

    /// <summary>
    /// Component attached to caster entities to track active channeling state.
    /// </summary>
    public struct MiracleChannelState : IComponentData
    {
        /// <summary>Entity of the active sustained effect (Entity.Null if not channeling).</summary>
        public Entity ActiveEffectEntity;

        /// <summary>Miracle ID currently being channeled.</summary>
        public MiracleId ChannelingId;

        /// <summary>Time when channeling started (in seconds since game start).</summary>
        public float ChannelStartTime;
    }
}


using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Pre-computed awareness snapshot for hot path AI decisions.
    /// AI systems read these flags directly without raycasts or distance calculations.
    /// </summary>
    public struct AwarenessSnapshot : IComponentData
    {
        /// <summary>
        /// Flags indicating what the entity is aware of.
        /// </summary>
        public AwarenessFlags Flags;

        /// <summary>
        /// Threat level (0..1, 0 = no threat, 1 = maximum threat).
        /// </summary>
        public float ThreatLevel;

        /// <summary>
        /// Alarm state (0 = calm, 1 = alert, 2 = alarmed, 3 = panicked).
        /// </summary>
        public byte AlarmState;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Awareness flags indicating what the entity currently knows.
    /// </summary>
    [System.Flags]
    public enum AwarenessFlags : ushort
    {
        None = 0,
        SeesEnemy = 1 << 0,
        SeesAlly = 1 << 1,
        SeesNeutral = 1 << 2,
        HearsCombat = 1 << 3,
        HearsAlarm = 1 << 4,
        DetectsThreat = 1 << 5,
        DetectsResource = 1 << 6,
        DetectsDanger = 1 << 7,
        HasLineOfSight = 1 << 8,
        InCombat = 1 << 9,
        UnderAttack = 1 << 10
    }

    /// <summary>
    /// Short known fact snapshot - cached key information for quick AI decisions.
    /// </summary>
    public struct KnownFact : IComponentData
    {
        /// <summary>
        /// Nearest enemy entity (Entity.Null if none).
        /// </summary>
        public Entity NearestEnemy;

        /// <summary>
        /// Distance to nearest enemy (float.MaxValue if none).
        /// </summary>
        public float NearestEnemyDistance;

        /// <summary>
        /// Direction to nearest enemy (normalized, zero if none).
        /// </summary>
        public float3 NearestEnemyDirection;

        /// <summary>
        /// Nearest ally entity (Entity.Null if none).
        /// </summary>
        public Entity NearestAlly;

        /// <summary>
        /// Distance to nearest ally (float.MaxValue if none).
        /// </summary>
        public float NearestAllyDistance;

        /// <summary>
        /// Threat direction (normalized, zero if no threat).
        /// </summary>
        public float3 ThreatDirection;

        /// <summary>
        /// Tick when this fact was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}


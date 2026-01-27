using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat.Targeting
{
    /// <summary>
    /// Target priority and selection state for an entity.
    /// Updated by TargetSelectionSystem based on strategy.
    /// </summary>
    public struct TargetPriority : IComponentData
    {
        /// <summary>
        /// Currently selected target entity.
        /// </summary>
        public Entity CurrentTarget;

        /// <summary>
        /// Threat score of current target.
        /// </summary>
        public float ThreatScore;

        /// <summary>
        /// Tick when target was last engaged.
        /// </summary>
        public uint LastEngagedTick;

        /// <summary>
        /// Tick when target was selected.
        /// </summary>
        public uint TargetSelectedTick;

        /// <summary>
        /// Strategy used for target selection.
        /// </summary>
        public TargetingStrategy Strategy;

        /// <summary>
        /// Whether to allow automatic target switching.
        /// </summary>
        public bool AllowAutoSwitch;

        /// <summary>
        /// Minimum ticks before allowing target switch.
        /// </summary>
        public uint TargetLockDuration;
    }

    /// <summary>
    /// Targeting strategy enumeration.
    /// Games can extend with custom strategies in higher ranges.
    /// </summary>
    public enum TargetingStrategy : byte
    {
        /// <summary>
        /// Target the nearest enemy.
        /// </summary>
        Nearest = 0,

        /// <summary>
        /// Target the enemy with lowest health.
        /// </summary>
        LowestHealth = 1,

        /// <summary>
        /// Target the enemy with highest threat score.
        /// </summary>
        HighestThreat = 2,

        /// <summary>
        /// Random target selection (deterministic based on seed).
        /// </summary>
        Random = 3,

        /// <summary>
        /// Player-assigned target (no automatic selection).
        /// </summary>
        PlayerAssigned = 4,

        /// <summary>
        /// Target the enemy that last attacked this entity.
        /// </summary>
        LastAttacker = 5,

        /// <summary>
        /// Target enemies attacking allies.
        /// </summary>
        DefendAlly = 6,

        /// <summary>
        /// Target high-value targets (healers, commanders).
        /// </summary>
        HighValue = 7
    }

    /// <summary>
    /// Potential target buffer element.
    /// Populated by TargetSelectionSystem from spatial queries.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PotentialTarget : IBufferElementData
    {
        /// <summary>
        /// Target entity.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Distance to target.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Calculated threat score for this target.
        /// </summary>
        public float ThreatScore;

        /// <summary>
        /// Priority level (0 = highest).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Target's current health percentage (0-1).
        /// </summary>
        public float HealthPercent;

        /// <summary>
        /// Whether target is currently attacking us.
        /// </summary>
        public bool IsAttackingUs;

        /// <summary>
        /// Whether target is a high-value target.
        /// </summary>
        public bool IsHighValue;
    }

    /// <summary>
    /// Threat source buffer element.
    /// Tracks entities that have attacked this entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ThreatSource : IBufferElementData
    {
        /// <summary>
        /// Entity that caused threat.
        /// </summary>
        public Entity Source;

        /// <summary>
        /// Accumulated threat from this source.
        /// </summary>
        public float ThreatAmount;

        /// <summary>
        /// Tick when threat was last updated.
        /// </summary>
        public uint LastThreatTick;
    }

    /// <summary>
    /// High-value target tag.
    /// Entities with this tag are prioritized by HighValue strategy.
    /// </summary>
    public struct HighValueTargetTag : IComponentData
    {
        /// <summary>
        /// Priority modifier (higher = more valuable target).
        /// </summary>
        public float PriorityModifier;
    }

    /// <summary>
    /// Target selection configuration.
    /// Games set these values to tune targeting behavior.
    /// </summary>
    public struct TargetSelectionConfig : IComponentData
    {
        /// <summary>
        /// Maximum range for target detection.
        /// </summary>
        public float MaxDetectionRange;

        /// <summary>
        /// Threat decay rate per second.
        /// </summary>
        public float ThreatDecayRate;

        /// <summary>
        /// Threat multiplier for damage received.
        /// </summary>
        public float DamageThreatMultiplier;

        /// <summary>
        /// Bonus threat for targets attacking allies.
        /// </summary>
        public float AllyDefenseThreatBonus;

        /// <summary>
        /// How much distance affects target priority (0-1).
        /// </summary>
        public float DistanceWeight;

        /// <summary>
        /// How much health affects target priority (0-1).
        /// </summary>
        public float HealthWeight;

        /// <summary>
        /// How much threat affects target priority (0-1).
        /// </summary>
        public float ThreatWeight;
    }

    /// <summary>
    /// Static helpers for target selection.
    /// </summary>
    public static class TargetSelectionHelpers
    {
        /// <summary>
        /// Selects the best target from potential targets based on strategy.
        /// </summary>
        public static Entity SelectBest(
            in DynamicBuffer<PotentialTarget> targets,
            TargetingStrategy strategy,
            uint randomSeed = 0)
        {
            if (targets.Length == 0)
            {
                return Entity.Null;
            }

            int bestIndex = 0;
            float bestScore = float.MinValue;

            for (int i = 0; i < targets.Length; i++)
            {
                float score = CalculateScore(targets[i], strategy, randomSeed, i);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return targets[bestIndex].Target;
        }

        /// <summary>
        /// Calculates priority score for a target based on strategy.
        /// </summary>
        public static float CalculateScore(
            in PotentialTarget target,
            TargetingStrategy strategy,
            uint randomSeed,
            int index)
        {
            return strategy switch
            {
                TargetingStrategy.Nearest => -target.Distance,
                TargetingStrategy.LowestHealth => -target.HealthPercent,
                TargetingStrategy.HighestThreat => target.ThreatScore,
                TargetingStrategy.Random => GetDeterministicRandom(randomSeed, index),
                TargetingStrategy.LastAttacker => target.IsAttackingUs ? 1000f : target.ThreatScore,
                TargetingStrategy.DefendAlly => target.IsAttackingUs ? 500f : -target.Distance,
                TargetingStrategy.HighValue => target.IsHighValue ? 1000f - target.Distance : -target.Distance,
                _ => -target.Distance
            };
        }

        private static float GetDeterministicRandom(uint seed, int index)
        {
            uint hash = seed * 1103515245 + 12345 + (uint)index * 31;
            return (hash % 10000) / 10000f;
        }
    }
}


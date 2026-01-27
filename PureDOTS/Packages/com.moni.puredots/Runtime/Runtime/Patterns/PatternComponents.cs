using Unity.Entities;

namespace PureDOTS.Runtime.Patterns
{
    public enum PatternId : int
    {
        Unknown = 0,
        HardworkingVillage = 1,
        ChaoticBand = 2,
        OverstressedGroup = 3
    }

    /// <summary>
    /// Scope a pattern applies to so systems can filter their queries.
    /// </summary>
    public enum PatternScope : byte
    {
        Group = 0,
        Individual = 1
    }

    /// <summary>
    /// Data-only description of a pattern for config/authoring.
    /// Note: Parsed/consumed by the pattern system; kept minimal and game-agnostic.
    /// </summary>
    public struct PatternDefinition
    {
        public PatternId Id;
        public PatternScope Scope;

        // Simple condition thresholds (optional)
        public float MinCohesion;
        public float MaxCohesion;
        public float MinMorale;
        public float MaxMorale;
        public int MinMembers;

        // Effect multipliers/deltas (baseline = 1.0f for rates)
        public float WorkRateMultiplier;
        public float WanderRateMultiplier;
        public float DisbandRiskDelta;
    }

    /// <summary>
    /// Buffer of active pattern tags on an entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ActivePatternTag : IBufferElementData
    {
        public PatternId Id;
    }

    /// <summary>
    /// Generic pattern-driven modifiers for groups.
    /// Other systems can read these without caring which pattern set them.
    /// </summary>
    public struct GroupPatternModifiers : IComponentData
    {
        /// <summary>Multiplier applied to work/gather/production speeds (1 = no change).</summary>
        public float WorkRateMultiplier;

        /// <summary>Multiplier applied to wander/idle tendencies (1 = no change).</summary>
        public float WanderRateMultiplier;

        /// <summary>Additional disband risk applied per tick (0 = no change).</summary>
        public float DisbandRiskPerTick;

        public static GroupPatternModifiers Identity => new GroupPatternModifiers
        {
            WorkRateMultiplier = 1f,
            WanderRateMultiplier = 1f,
            DisbandRiskPerTick = 0f
        };
    }
}

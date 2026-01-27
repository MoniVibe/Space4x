// [TRI-STUB] Stub components for magic circle cooperation
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Magic circle - coordination of multiple casters.
    /// </summary>
    public struct MagicCircle : IComponentData
    {
        public Entity PrimaryCaster;
        public float PooledMana;
        public float CastSpeedBonus;
        public float EfficiencyBonus;
        public byte ContributorCount;
    }

    /// <summary>
    /// Circle member - contributor to magic circle.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CircleMember : IBufferElementData
    {
        public Entity ContributorEntity;
        public float ManaContributionRate;
        public float ChannelingEfficiency;
        public byte IsChanneling;
    }

    /// <summary>
    /// Mana pool - pooled mana from contributors.
    /// </summary>
    public struct ManaPool : IComponentData
    {
        public float CurrentMana;
        public float MaxMana;
        public float RegenRate;
    }

    /// <summary>
    /// Ritual casting state.
    /// </summary>
    public struct RitualCasting : IComponentData
    {
        public FixedString64Bytes RitualName;
        public RitualPhase CurrentPhase;
        public float SynchronizationLevel;
        public float RitualPower;
        public float TimeInPhase;
        public float RequiredCohesion;
    }

    /// <summary>
    /// Ritual phases.
    /// </summary>
    public enum RitualPhase : byte
    {
        Preparation = 0,
        Invocation = 1,
        Channeling = 2,
        Climax = 3,
        Completion = 4,
        Failure = 5
    }
}


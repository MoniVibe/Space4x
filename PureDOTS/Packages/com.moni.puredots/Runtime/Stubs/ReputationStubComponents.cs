// [TRI-STUB] Stub components for reputation system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Reputation
{
    /// <summary>
    /// Entity reputation - how entity is perceived by others.
    /// </summary>
    public struct EntityReputation : IComponentData
    {
        public Entity ObserverEntity;
        public ReputationDomain Domain;
        public float ReputationScore;
        public ReputationTier Tier;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Reputation domains - different aspects of reputation.
    /// </summary>
    public enum ReputationDomain : byte
    {
        Trading = 0,
        Combat = 1,
        Diplomacy = 2,
        Magic = 3,
        Crafting = 4,
        General = 5
    }

    /// <summary>
    /// Reputation tiers - categorical reputation levels.
    /// </summary>
    public enum ReputationTier : byte
    {
        Hated = 0,
        Hostile = 1,
        Unfriendly = 2,
        Neutral = 3,
        Friendly = 4,
        Honored = 5,
        Exalted = 6
    }

    /// <summary>
    /// Reputation event - action that affects reputation.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ReputationEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public ReputationDomain Domain;
        public float ReputationDelta;
        public uint EventTick;
    }
}


// [TRI-STUB] Stub components for faction relations
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Faction relation - relation between two factions.
    /// </summary>
    public struct FactionRelation : IComponentData
    {
        public Entity FactionA;
        public Entity FactionB;
        public float RelationScore;
        public FactionRelationType Type;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Faction relation types.
    /// </summary>
    public enum FactionRelationType : byte
    {
        Alliance = 0,
        Neutral = 1,
        Hostile = 2,
        AtWar = 3,
        Vassal = 4,
        Overlord = 5
    }

    /// <summary>
    /// Faction relationship - bidirectional faction relation.
    /// </summary>
    public struct FactionRelationship : IComponentData
    {
        public Entity OtherFaction;
        public FactionRelationType RelationType;
        public float RelationScore;
        public float TensionLevel;
    }
}


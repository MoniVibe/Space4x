using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Aggregated stats for a group (fleet, colony, party, etc.).
    /// </summary>
    public struct GroupStatAggregate : IComponentData
    {
        // Command stat aggregates
        public half AvgCommand;
        public half MaxCommand;
        public half MinCommand;
        
        // Tactics stat aggregates
        public half AvgTactics;
        public half MaxTactics;
        public half MinTactics;
        
        // Logistics stat aggregates
        public half AvgLogistics;
        public half MaxLogistics;
        public half MinLogistics;
        
        // Diplomacy stat aggregates
        public half AvgDiplomacy;
        public half MaxDiplomacy;
        public half MinDiplomacy;
        
        // Engineering stat aggregates
        public half AvgEngineering;
        public half MaxEngineering;
        public half MinEngineering;
        
        // Resolve stat aggregates
        public half AvgResolve;
        public half MaxResolve;
        public half MinResolve;
        
        // Physical attribute aggregates
        public half AvgPhysique;
        public half AvgFinesse;
        public half AvgWill;
        
        // Group metadata
        public ushort MemberCount;
        public uint LastUpdateTick;
        public float TotalExperience;
    }

    /// <summary>
    /// Reference to members of a group for aggregation.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct GroupMemberRef : IBufferElementData
    {
        public Entity MemberEntity;
        public byte Role;             // Member's role in group
        public float Weight;          // Weight for averaging (e.g., officer vs crew)
    }

    /// <summary>
    /// Configuration for stat aggregation.
    /// </summary>
    public struct StatAggregationConfig : IComponentData
    {
        public uint UpdateInterval;   // Ticks between updates
        public bool UseWeightedAverage;
        public float OfficerWeight;   // Weight for officers vs crew
        public float CrewWeight;
    }

    /// <summary>
    /// Tag for entities that need stat aggregation.
    /// </summary>
    public struct RequiresStatAggregation : IComponentData
    {
        public uint LastRequestTick;
    }

    /// <summary>
    /// Stat modifier from group membership.
    /// </summary>
    public struct GroupStatModifier : IComponentData
    {
        public Entity GroupEntity;
        public half CommandBonus;
        public half TacticsBonus;
        public half LogisticsBonus;
        public half ResolveBonus;
        public half MoraleBonus;
    }
}


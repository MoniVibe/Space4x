using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Construction
{
    /// <summary>
    /// Component on group entities (village, guild, colony, etc.) that coordinates construction decisions.
    /// </summary>
    public struct BuildCoordinator : IComponentData
    {
        /// <summary>Group entity this coordinator belongs to (self-reference or linked entity).</summary>
        public Entity GroupEntity;
        
        /// <summary>Abstract "construction capacity" budget.</summary>
        public float BuildBudget;
        
        /// <summary>Whether auto-build is enabled (0/1, respects player setting).</summary>
        public byte AutoBuildEnabled;
        
        /// <summary>Maximum active construction sites allowed.</summary>
        public byte MaxActiveSites;
        
        /// <summary>Current number of active construction sites.</summary>
        public byte ActiveSiteCount;
    }
    
    /// <summary>
    /// Construction intent: what the group wants to build, prioritized by urgency.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConstructionIntent : IBufferElementData
    {
        /// <summary>Pattern ID pointing into BuildingPatternCatalog.</summary>
        public int PatternId;
        
        /// <summary>Category of this building.</summary>
        public BuildCategory Category;
        
        /// <summary>Urgency score (0-1), computed from needs + motivations.</summary>
        public float Urgency;
        
        /// <summary>Rough location to aim for.</summary>
        public float3 SuggestedCenter;
        
        /// <summary>Desired count of this building type in area.</summary>
        public float DesiredCount;
        
        /// <summary>Existing count of this building type (computed during aggregation).</summary>
        public float ExistingCount;
        
        /// <summary>Source: 0=Needs, 1=Motivation, 2=Player.</summary>
        public byte Source;
        
        /// <summary>Status: 0=Planned, 1=Approved, 2=Realized, 3=Blocked.</summary>
        public byte Status;
        
        /// <summary>Tick when intent was created.</summary>
        public uint CreatedTick;
    }
}

























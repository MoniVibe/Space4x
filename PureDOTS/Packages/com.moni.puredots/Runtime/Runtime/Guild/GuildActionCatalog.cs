using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Guild
{
    /// <summary>
    /// Precondition for a guild action.
    /// </summary>
    public struct ActionPrecondition
    {
        /// <summary>Condition type enum index (Alignment, Governance, Relation, CrisisTag).</summary>
        public byte ConditionType;
        
        /// <summary>Operator enum index (Equals, GreaterThan, LessThan, HasFlag).</summary>
        public byte Operator;
        
        /// <summary>Value to compare against.</summary>
        public float Value;
    }
    
    /// <summary>
    /// Defines an action guilds can take (strike, riot, coup, declare war, etc.).
    /// </summary>
    public struct GuildActionSpec
    {
        /// <summary>Unique action ID.</summary>
        public ushort ActionId;
        
        /// <summary>Label for this action ("Strike", "Declare War", etc.).</summary>
        public FixedString64Bytes Label;
        
        /// <summary>Preconditions (alignment, governance, relations, crisis tags).</summary>
        public BlobArray<ActionPrecondition> Preconditions;
        
        /// <summary>Resource cost to execute this action.</summary>
        public float ResourceCost;
        
        /// <summary>Risk level (0-1).</summary>
        public float RiskLevel;
        
        /// <summary>Reputation impact (-1 to +1).</summary>
        public float ReputationImpact;
        
        // AI strategy tags
        /// <summary>Is this a defensive action? (0 or 1)</summary>
        public byte IsDefensive;
        
        /// <summary>Is this an expansion action? (0 or 1)</summary>
        public byte IsExpansion;
        
        /// <summary>Is this an ideological action? (0 or 1)</summary>
        public byte IsIdeological;
        
        /// <summary>Is this an economic action? (0 or 1)</summary>
        public byte IsEconomic;
    }
    
    /// <summary>
    /// Blob asset catalog containing all guild action specs.
    /// </summary>
    public struct GuildActionCatalog
    {
        public BlobArray<GuildActionSpec> ActionSpecs;
    }
    
    /// <summary>
    /// Singleton component holding the global guild action catalog.
    /// </summary>
    public struct GuildActionConfigState : IComponentData
    {
        /// <summary>Reference to the guild action catalog blob asset.</summary>
        public BlobAssetReference<GuildActionCatalog> Catalog;
    }
}


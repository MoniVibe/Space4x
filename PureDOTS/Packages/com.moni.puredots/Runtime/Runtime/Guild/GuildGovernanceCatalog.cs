using PureDOTS.Runtime.Aggregates;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Guild
{
    /// <summary>
    /// Defines governance rules for a specific governance type.
    /// </summary>
    public struct GuildGovernanceSpec
    {
        /// <summary>Governance type this spec applies to.</summary>
        public GuildLeadership.GovernanceType Type;
        
        // Voting rules
        /// <summary>Required quorum percentage (0-100).</summary>
        public byte RequiresQuorum;
        
        /// <summary>Veto threshold percentage for oligarchic (0-100).</summary>
        public byte VetoThreshold;
        
        // Term lengths (in ticks)
        /// <summary>Leader term length in ticks.</summary>
        public uint LeaderTermLength;
        
        /// <summary>Vote duration in ticks.</summary>
        public uint VoteDuration;
        
        // Coup thresholds
        /// <summary>Support threshold for coup (0-1, fraction of members needed).</summary>
        public float CoupSupportThreshold;
        
        /// <summary>Loyalty threshold for coup (0-1, loyalty level needed).</summary>
        public float CoupLoyaltyThreshold;
    }
    
    /// <summary>
    /// Blob asset catalog containing all governance specs.
    /// </summary>
    public struct GuildGovernanceCatalog
    {
        public BlobArray<GuildGovernanceSpec> GovernanceSpecs;
    }
    
    /// <summary>
    /// Singleton component holding the global governance catalog.
    /// </summary>
    public struct GuildGovernanceConfigState : IComponentData
    {
        /// <summary>Reference to the governance catalog blob asset.</summary>
        public BlobAssetReference<GuildGovernanceCatalog> Catalog;
    }
}

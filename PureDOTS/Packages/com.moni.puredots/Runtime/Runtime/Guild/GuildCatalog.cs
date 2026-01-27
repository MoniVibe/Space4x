using PureDOTS.Runtime.Aggregates;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Guild
{
    /// <summary>
    /// Recruitment scoring rule for guild membership.
    /// </summary>
    public struct RecruitmentRule
    {
        /// <summary>Stat type enum index (SkillLevel, Achievement, Alignment, etc.).</summary>
        public byte StatType;
        
        /// <summary>Weight for this rule in scoring.</summary>
        public float Weight;
        
        /// <summary>Minimum value to consider.</summary>
        public float Threshold;
    }
    
    /// <summary>
    /// Defines a guild type (Heroes, Merchants, Rebels, etc.).
    /// </summary>
    public struct GuildTypeSpec
    {
        /// <summary>Type ID matching AggregateIdentity.TypeId.</summary>
        public ushort TypeId;
        
        /// <summary>Label for this guild type ("Heroes' Guild", "Merchants' League").</summary>
        public FixedString64Bytes Label;
        
        /// <summary>Recruitment scoring rules (which stats/achievements matter).</summary>
        public BlobArray<RecruitmentRule> RecruitmentRules;
        
        /// <summary>Default governance type.</summary>
        public GuildLeadership.GovernanceType DefaultGovernance;
        
        // Preferred alignments/outlooks (for filtering)
        /// <summary>Minimum corrupt/pure alignment (-100 to +100).</summary>
        public sbyte MinCorruptPure;
        
        /// <summary>Maximum corrupt/pure alignment (-100 to +100).</summary>
        public sbyte MaxCorruptPure;
        
        /// <summary>Minimum chaotic/lawful alignment (-100 to +100).</summary>
        public sbyte MinChaoticLawful;
        
        /// <summary>Maximum chaotic/lawful alignment (-100 to +100).</summary>
        public sbyte MaxChaoticLawful;
        
        /// <summary>Minimum evil/good alignment (-100 to +100).</summary>
        public sbyte MinEvilGood;
        
        /// <summary>Maximum evil/good alignment (-100 to +100).</summary>
        public sbyte MaxEvilGood;
        
        // Default behaviors (flags)
        /// <summary>Can this guild type declare strikes? (0 or 1)</summary>
        public byte CanDeclareStrikes;
        
        /// <summary>Can this guild type declare coups? (0 or 1)</summary>
        public byte CanDeclareCoups;
        
        /// <summary>Can this guild type declare war? (0 or 1)</summary>
        public byte CanDeclareWar;
        
        /// <summary>Is this guild type economic-only (no military actions)? (0 or 1)</summary>
        public byte OnlyTrade;
        
        /// <summary>Can this guild type open embassies? (0 or 1)</summary>
        public byte CanOpenEmbassies;
    }
    
    /// <summary>
    /// Blob asset catalog containing all guild type specs.
    /// </summary>
    public struct GuildTypeCatalog
    {
        public BlobArray<GuildTypeSpec> TypeSpecs;
    }
    
    /// <summary>
    /// Singleton component holding the global guild type catalog.
    /// </summary>
    public struct GuildConfigState : IComponentData
    {
        /// <summary>Reference to the guild type catalog blob asset.</summary>
        public BlobAssetReference<GuildTypeCatalog> Catalog;
        
        /// <summary>Frequency of formation checks in ticks.</summary>
        public uint FormationCheckFrequency;
    }
}

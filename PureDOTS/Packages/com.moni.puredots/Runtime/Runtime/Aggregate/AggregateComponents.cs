using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Identifies an aggregate entity (village, guild, fleet, empire, etc.).
    /// </summary>
    public struct AggregateIdentity : IComponentData
    {
        /// <summary>Data-driven type ID (Village, Guild, Fleet, etc.).</summary>
        public ushort TypeId;
        
        /// <summary>Seed for per-group randomization (names, quirks).</summary>
        public uint Seed;
    }
    
    /// <summary>
    /// Aggregate statistics averaged from member traits.
    /// </summary>
    public struct AggregateStats : IComponentData
    {
        // Averaged MoralProfile traits
        /// <summary>Average initiative (0-100).</summary>
        public float AvgInitiative;
        
        /// <summary>Average vengeful/forgiving (-100 to +100).</summary>
        public float AvgVengefulForgiving;
        
        /// <summary>Average bold/craven (-100 to +100).</summary>
        public float AvgBoldCraven;
        
        /// <summary>Average corrupt/pure (-100 to +100).</summary>
        public float AvgCorruptPure;
        
        /// <summary>Average chaotic/lawful (-100 to +100).</summary>
        public float AvgChaoticLawful;
        
        /// <summary>Average evil/good (-100 to +100).</summary>
        public float AvgEvilGood;
        
        /// <summary>Average might/magic (-100 to +100).</summary>
        public float AvgMightMagic;
        
        /// <summary>Average ambition (0-100).</summary>
        public float AvgAmbition;
        
        // Desire coverage (0-1, not necessarily sum=1)
        /// <summary>Average desire for status (0-1).</summary>
        public float StatusCoverage;
        
        /// <summary>Average desire for wealth (0-1).</summary>
        public float WealthCoverage;
        
        /// <summary>Average desire for power (0-1).</summary>
        public float PowerCoverage;
        
        /// <summary>Average desire for knowledge (0-1).</summary>
        public float KnowledgeCoverage;
        
        /// <summary>Number of members contributing to these stats.</summary>
        public int MemberCount;
        
        /// <summary>Last tick when stats were recalculated.</summary>
        public uint LastRecalcTick;
    }
}

























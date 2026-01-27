using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Guild
{
    /// <summary>
    /// Short numeric ID for guilds (useful for registries).
    /// </summary>
    public struct GuildId : IComponentData
    {
        public ushort Id;
    }
    
    /// <summary>
    /// Guild wealth metrics (aggregate wealth + optional pooled treasury).
    /// </summary>
    public struct GuildWealth : IComponentData
    {
        /// <summary>Average member fortune (aggregate metric).</summary>
        public float AverageMemberFortune;
        
        /// <summary>Pooled treasury (if guild uses shared resources).</summary>
        public float PooledTreasury;
        
        /// <summary>Total guild assets value.</summary>
        public float TotalAssets;
    }
    
    /// <summary>
    /// Guild knowledge and progression (learned bonuses, research fields, etc.).
    /// </summary>
    public struct GuildKnowledge : IComponentData
    {
        // Threat-specific bonuses (0-100%)
        public byte DemonSlayingBonus;
        public byte UndeadSlayingBonus;
        public byte BossHuntingBonus;
        public byte CelestialCombatBonus;
        
        // Tactical knowledge (0-100%)
        public byte EspionageEffectiveness;
        public byte CoordinationBonus;
        public byte SurvivalBonus;
        
        // Total kills (for learning)
        public ushort DemonsKilled;
        public ushort UndeadKilled;
        public ushort BossesKilled;
        public ushort CelestialsKilled;
        
        // Research fields (bit flags or separate component)
        public uint ResearchFields;
    }
    
    /// <summary>
    /// Hot state component: Active guild strike.
    /// Only present when strike is active.
    /// </summary>
    public struct GuildStrike : IComponentData
    {
        public uint StrikeStartTick;
        public uint StrikeDuration; // Ticks
        public float ProductivityPenalty; // 0-1
        public Entity TargetVillage; // Which village is being struck
    }
    
    /// <summary>
    /// Hot state component: Active guild riot.
    /// Only present when riot is active.
    /// </summary>
    public struct GuildRiot : IComponentData
    {
        public uint RiotStartTick;
        public uint RiotDuration; // Ticks
        public float StabilityPenalty; // 0-1
        public Entity TargetVillage; // Which village is rioting
        public float3 RiotLocation;
    }
    
    /// <summary>
    /// Hot state component: Active guild coup attempt.
    /// Only present when coup is active.
    /// </summary>
    public struct GuildCoup : IComponentData
    {
        public uint CoupStartTick;
        public Entity InstigatorEntity; // Who is leading the coup
        public float SupportLevel; // 0-1 (fraction of members supporting)
        public bool Succeeded;
    }
}

























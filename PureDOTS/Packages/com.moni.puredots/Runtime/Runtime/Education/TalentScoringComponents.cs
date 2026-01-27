using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Precomputed talent score for an entity.
    /// Updated periodically by TalentScoringSystem (future system).
    /// </summary>
    public struct TalentScore : IComponentData
    {
        /// <summary>Overall talent score (0-100+).</summary>
        public float Value;
        
        /// <summary>Talent tier (0=none, 1=promising, 2=elite, 3=phenomenal).</summary>
        public byte Tier;
        
        /// <summary>Last update timestamp.</summary>
        public float LastUpdateTime;
    }
    
    /// <summary>
    /// Tags an entity as a candidate for a specific court/guild role.
    /// </summary>
    public struct CourtCandidateTag : IComponentData
    {
        /// <summary>Desired role ID (Champion, Steward, Wizard, etc. - references catalog).</summary>
        public ushort DesiredRoleId;
        
        /// <summary>Family/guild considering this candidate.</summary>
        public Entity InterestedFamily;
        
        /// <summary>Candidate score for this specific role.</summary>
        public float RoleScore;
    }
    
    /// <summary>
    /// Tags an entity as a candidate for guild apprenticeship/promotion.
    /// </summary>
    public struct GuildCandidateTag : IComponentData
    {
        /// <summary>Guild considering this candidate.</summary>
        public Entity Guild;
        
        /// <summary>Candidate score for guild roles.</summary>
        public float GuildScore;
    }
}

























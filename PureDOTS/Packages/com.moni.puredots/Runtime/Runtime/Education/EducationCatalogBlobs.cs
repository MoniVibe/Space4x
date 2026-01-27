using PureDOTS.Runtime.Knowledge;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Catalog of education institution types (moddable).
    /// </summary>
    public struct EducationInstitutionCatalogBlob
    {
        public BlobArray<InstitutionTypeSpec> InstitutionTypes;
    }
    
    /// <summary>
    /// Specification for an institution type.
    /// </summary>
    public struct InstitutionTypeSpec
    {
        public FixedString64Bytes InstitutionTypeId;
        public FixedString64Bytes DisplayName;
        public byte BaseQuality;           // 1-10
        public ushort BaseCapacity;        // Students
        public float CostPerStudent;       // Annual
        public InstitutionCategory Category;
    }
    
    /// <summary>
    /// Category of education institution.
    /// </summary>
    public enum InstitutionCategory : byte
    {
        VillageSchool = 0,
        PrivateSchool = 1,
        GuildSchool = 2,
        University = 3,
        Academy = 4
    }
    
    /// <summary>
    /// Catalog of elite education policies (moddable).
    /// </summary>
    public struct EliteEducationPolicyCatalogBlob
    {
        public BlobArray<EliteEducationPolicySpec> Policies;
    }
    
    /// <summary>
    /// Specification for an elite education policy.
    /// </summary>
    public struct EliteEducationPolicySpec
    {
        public FixedString64Bytes PolicyId;
        public FixedString64Bytes DisplayName;
        public float PublicSchoolBudget;      // Default fraction
        public float PrivateTutorBudget;
        public float UniversityEndowment;
        public float ScholarshipBiasCommoners;
        public float ScholarshipBiasGuild;
    }
    
    /// <summary>
    /// Catalog of court role specifications (moddable).
    /// </summary>
    public struct CourtRoleCatalogBlob
    {
        public BlobArray<CourtRoleSpec> Roles;
    }
    
    /// <summary>
    /// Specification for a court role.
    /// </summary>
    public struct CourtRoleSpec
    {
        public FixedString64Bytes RoleId;
        public FixedString64Bytes DisplayName;
        public byte MinEducation;          // Required education level
        public byte MinIntelligence;      // Required intelligence
        public byte MinWisdom;            // Required wisdom
        public BlobArray<RoleSkillRequirement> SkillRequirements;
    }
    
    /// <summary>
    /// Skill requirement for a court role.
    /// </summary>
    public struct RoleSkillRequirement
    {
        public FixedString64Bytes SkillId;
        public byte MinLevel;
    }
    
    /// <summary>
    /// Catalog of talent filter specifications (moddable).
    /// Defines what counts as "talent" for different roles.
    /// </summary>
    public struct TalentFilterCatalogBlob
    {
        public BlobArray<TalentFilterSpec> Filters;
    }
    
    /// <summary>
    /// Specification for a talent filter.
    /// </summary>
    public struct TalentFilterSpec
    {
        public FixedString64Bytes FilterId;
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes TargetRoleId;  // Which role this filter applies to
        public float EducationWeight;            // How much education matters
        public float WisdomWeight;
        public float IntelligenceWeight;
        public float PhysiqueWeight;
        public float FinesseWeight;
        public float AchievementWeight;          // Combat victories, etc.
        public float LessonQualityWeight;        // Rare lesson bonuses
    }
    
    /// <summary>
    /// Singleton reference to education institution catalog blob.
    /// </summary>
    public struct EducationInstitutionCatalogRef : IComponentData
    {
        public BlobAssetReference<EducationInstitutionCatalogBlob> Blob;
    }
    
    /// <summary>
    /// Singleton reference to elite education policy catalog blob.
    /// </summary>
    public struct EliteEducationPolicyCatalogRef : IComponentData
    {
        public BlobAssetReference<EliteEducationPolicyCatalogBlob> Blob;
    }
    
    /// <summary>
    /// Singleton reference to court role catalog blob.
    /// </summary>
    public struct CourtRoleCatalogRef : IComponentData
    {
        public BlobAssetReference<CourtRoleCatalogBlob> Blob;
    }
    
    /// <summary>
    /// Singleton reference to talent filter catalog blob.
    /// </summary>
    public struct TalentFilterCatalogRef : IComponentData
    {
        public BlobAssetReference<TalentFilterCatalogBlob> Blob;
    }
}

























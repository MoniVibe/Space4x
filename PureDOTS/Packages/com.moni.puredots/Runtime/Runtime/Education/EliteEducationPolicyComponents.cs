using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Education policy for an elite family/ruling house.
    /// Defines how much to fund different education types and who gets scholarships.
    /// </summary>
    public struct EliteEducationPolicy : IComponentData
    {
        /// <summary>Policy type ID (references catalog blob for moddable policies).</summary>
        public ushort PolicyId;
        
        /// <summary>Fraction of income allocated to public schools (0-1).</summary>
        public float PublicSchoolBudget;
        
        /// <summary>Fraction of income allocated to private tutors (0-1).</summary>
        public float PrivateTutorBudget;
        
        /// <summary>Fraction of income allocated to university endowments (0-1).</summary>
        public float UniversityEndowment;
        
        /// <summary>Bias toward commoner scholarships (0-1, 0=nepotism, 1=meritocracy).</summary>
        public float ScholarshipBiasCommoners;
        
        /// <summary>Bias toward guild recommendations (0-1).</summary>
        public float ScholarshipBiasGuild;
    }
}

























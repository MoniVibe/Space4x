using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Identifies an education institution (school, guild school, university).
    /// </summary>
    public struct EducationInstitution : IComponentData
    {
        /// <summary>Institution type ID (references catalog blob).</summary>
        public ushort InstitutionTypeId;
        
        /// <summary>Quality level 1-10 (affects lesson quality).</summary>
        public byte Level;
        
        /// <summary>Owner entity (Village, Guild, Family, or Entity.Null for autonomous).</summary>
        public Entity Owner;
        
        /// <summary>Current student capacity.</summary>
        public ushort Capacity;
        
        /// <summary>Current enrollment count.</summary>
        public ushort CurrentEnrollment;
        
        /// <summary>Annual operating budget.</summary>
        public float AnnualBudget;
    }
    
    /// <summary>
    /// Tracks enrollment status for a student in an institution.
    /// </summary>
    public struct InstitutionEnrollment : IComponentData
    {
        /// <summary>Institution entity.</summary>
        public Entity Institution;
        
        /// <summary>Enrollment start timestamp.</summary>
        public float EnrollmentDate;
        
        /// <summary>Years enrolled.</summary>
        public float YearsEnrolled;
        
        /// <summary>Scholarship flag (tuition waived).</summary>
        public bool HasScholarship;
    }
}

























using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Tracks formal education level and learning progress for an entity.
    /// Extends VillagerAttributes (which has Intelligence/Wisdom) with education-specific tracking.
    /// </summary>
    public struct EducationStats : IComponentData
    {
        /// <summary>Education level 0-100 (cumulative formal learning).</summary>
        public byte Education;
        
        /// <summary>Wisdom 0-100 (life experience, cultural knowledge).</summary>
        public byte Wisdom;
        
        /// <summary>Intelligence bonus from education (+0 to +25).</summary>
        public byte IntelligenceBonus;
        
        /// <summary>Skill acquisition speed multiplier (1.0× to 3.0×).</summary>
        public float SkillAcquisitionMultiplier;
        
        /// <summary>Current school/institution entity (or Entity.Null).</summary>
        public Entity CurrentInstitution;
        
        /// <summary>Current teacher/mentor entity (or Entity.Null).</summary>
        public Entity CurrentTeacher;
        
        /// <summary>Total years completed in institutions.</summary>
        public ushort YearsCompleted;
        
        /// <summary>Total lessons learned (completed).</summary>
        public ushort LessonsLearned;
        
        /// <summary>Total lessons fully refined.</summary>
        public ushort LessonsRefined;
    }
}

























using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Links a mentor to a student for one-on-one or small group tutoring.
    /// </summary>
    public struct MentorLink : IComponentData
    {
        /// <summary>Mentor entity (teacher).</summary>
        public Entity Mentor;
        
        /// <summary>Student entity (learner).</summary>
        public Entity Student;
        
        /// <summary>Optional: specific lesson ID being focused on (or 0 for general).</summary>
        public ushort FocusLessonId;
        
        /// <summary>Weekly hours allocated to mentoring (scheduling weight).</summary>
        public float WeeklyHours;
        
        /// <summary>Mentoring start timestamp.</summary>
        public float StartDate;
    }
    
    /// <summary>
    /// Tracks mentoring session progress and lesson refinement.
    /// </summary>
    public struct MentoringProgress : IComponentData
    {
        /// <summary>Total mentoring hours completed.</summary>
        public float TotalHours;
        
        /// <summary>Lessons refined through mentoring.</summary>
        public ushort LessonsRefined;
        
        /// <summary>Outlook shift progress (toward mentor's outlooks).</summary>
        public float OutlookShiftProgress;
    }
}

























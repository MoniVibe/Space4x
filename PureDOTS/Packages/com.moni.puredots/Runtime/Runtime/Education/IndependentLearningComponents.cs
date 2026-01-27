using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Tracks independent learning through activities (observation, practice).
    /// </summary>
    public struct IndependentLearningTracker : IComponentData
    {
        /// <summary>Days spent on current activity.</summary>
        public ushort DaysPerformingActivity;
        
        /// <summary>Current activity type.</summary>
        public ActivityType CurrentActivity;
        
        /// <summary>Learning progress 0-1 (toward next lesson).</summary>
        public float LearningProgress;
        
        /// <summary>Learning chance % per 30 days.</summary>
        public byte LearningChance;
        
        /// <summary>Total combats observed.</summary>
        public ushort CombatsObserved;
        
        /// <summary>Total spells observed.</summary>
        public ushort SpellsObserved;
        
        /// <summary>Total crafts observed.</summary>
        public ushort CraftsObserved;
    }
    
    /// <summary>
    /// Activity types for independent learning.
    /// </summary>
    public enum ActivityType : byte
    {
        None = 0,
        AnimalHandling = 1,
        Farming = 2,
        CombatObservation = 3,
        MagicObservation = 4,
        CraftingObservation = 5,
        Smithing = 6,
        Alchemy = 7
    }
}

























using Unity.Entities;

namespace PureDOTS.Runtime.Construction
{
    /// <summary>
    /// Group preferences for building categories, derived from alignment, archetype, and motivations.
    /// </summary>
    public struct BuildPreferenceProfile : IComponentData
    {
        public float HousingWeight;
        public float StorageWeight;
        public float WorshipWeight;
        public float DefenseWeight;
        public float FoodWeight;
        public float ProductionWeight;
        public float InfrastructureWeight;
        public float AestheticWeight;
        
        /// <summary>Last tick when preferences were updated.</summary>
        public uint LastUpdateTick;
    }
}

























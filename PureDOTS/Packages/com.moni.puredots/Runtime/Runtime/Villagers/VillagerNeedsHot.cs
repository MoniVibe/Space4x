using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Hot component for villager needs - cached utility values for scheduler.
    /// AOSoA-friendly: kept contiguous for cache-friendly access during priority scheduling.
    /// </summary>
    public struct VillagerNeedsHot : IComponentData
    {
        public float Hunger;           // Normalized 0-1
        public float Energy;           // Normalized 0-1
        public float Morale;           // Normalized 0-1
        public float UtilityWork;      // Cached work utility from need curves
        public float UtilityRest;      // Cached rest utility from need curves
        
        // Helper: compute urgency (higher = more urgent need)
        public float HungerUrgency => 1f - Hunger;
        public float EnergyUrgency => 1f - Energy;
        public float MoraleUrgency => 1f - Morale;
    }
}


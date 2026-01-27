using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Aggregate power state per village, colony, ship, or station.
    /// </summary>
    public struct AggregatePowerState : IComponentData
    {
        public float TotalGeneration;   // MW
        public float TotalDemand;
        public float SuppliedDemand;
        public float Coverage;          // SuppliedDemand / TotalDemand
        public float BlackoutLevel;     // 0..1
    }
}


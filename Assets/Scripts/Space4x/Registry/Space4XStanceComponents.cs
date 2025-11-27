using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Stance-based routing parameters shared across AI systems.
    /// </summary>
    public static class StanceRouting
    {
        public static float GetAvoidanceRadius(VesselStanceMode stance)
        {
            return stance switch
            {
                VesselStanceMode.Aggressive => 10f,  // Minimal avoidance
                VesselStanceMode.Balanced => 30f,    // Standard avoidance
                VesselStanceMode.Defensive => 50f,   // Wide berth
                VesselStanceMode.Evasive => 80f,     // Maximum avoidance
                _ => 30f
            };
        }

        public static float GetAvoidanceStrength(VesselStanceMode stance)
        {
            return stance switch
            {
                VesselStanceMode.Aggressive => 0.2f,  // Barely avoids
                VesselStanceMode.Balanced => 0.5f,    // Moderate steering
                VesselStanceMode.Defensive => 0.8f,   // Strong avoidance
                VesselStanceMode.Evasive => 1.2f,     // Extreme avoidance
                _ => 0.5f
            };
        }

        public static float GetSpeedMultiplier(VesselStanceMode stance)
        {
            return stance switch
            {
                VesselStanceMode.Aggressive => 1.1f,  // Slightly faster (pushing hard)
                VesselStanceMode.Balanced => 1.0f,    // Normal speed
                VesselStanceMode.Defensive => 0.95f,  // Slightly cautious
                VesselStanceMode.Evasive => 0.85f,    // Slower, more careful
                _ => 1.0f
            };
        }

        public static float GetRotationMultiplier(VesselStanceMode stance)
        {
            return stance switch
            {
                VesselStanceMode.Aggressive => 1.2f,  // Quick turns for interception
                VesselStanceMode.Balanced => 1.0f,    // Normal turning
                VesselStanceMode.Defensive => 1.1f,   // Responsive maneuvering
                VesselStanceMode.Evasive => 1.5f,     // Very responsive (jinking)
                _ => 1.0f
            };
        }

        public static bool ShouldEngageThreats(VesselStanceMode stance)
        {
            return stance == VesselStanceMode.Aggressive;
        }

        public static bool ShouldFleeThreats(VesselStanceMode stance)
        {
            return stance == VesselStanceMode.Evasive;
        }
    }

    /// <summary>
    /// Patrol stance for parent carriers/fleets that child vessels inherit.
    /// </summary>
    public struct PatrolStance : IComponentData
    {
        public VesselStanceMode Stance;
        public VesselStanceMode ThreatResponseStance;
        public byte ChildrenInheritStance;

        public static PatrolStance Default => new PatrolStance
        {
            Stance = VesselStanceMode.Balanced,
            ThreatResponseStance = VesselStanceMode.Defensive,
            ChildrenInheritStance = 1
        };
    }
}


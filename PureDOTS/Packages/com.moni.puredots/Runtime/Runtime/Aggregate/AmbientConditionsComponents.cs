using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Ambient conditions derived from aggregate stats (what it "feels like" to be in the group).
    /// </summary>
    public struct AmbientGroupConditions : IComponentData
    {
        // Pressures (0-1)
        /// <summary>Ambient courage pressure (0-1).</summary>
        public float AmbientCourage;
        
        /// <summary>Ambient caution pressure (0-1).</summary>
        public float AmbientCaution;
        
        /// <summary>Ambient anger pressure (0-1).</summary>
        public float AmbientAnger;
        
        /// <summary>Ambient compassion pressure (0-1).</summary>
        public float AmbientCompassion;
        
        /// <summary>Ambient drive/push to act (0-1).</summary>
        public float AmbientDrive;
        
        // Expectations (0-1)
        /// <summary>Expectation of loyalty (0-1).</summary>
        public float ExpectationLoyalty;
        
        /// <summary>Expectation of conformity (0-1).</summary>
        public float ExpectationConformity;
        
        /// <summary>Tolerance for outliers and non-conformity (0-1).</summary>
        public float ToleranceForOutliers;
        
        /// <summary>Last tick when ambient conditions were updated.</summary>
        public uint LastUpdateTick;
    }
}

























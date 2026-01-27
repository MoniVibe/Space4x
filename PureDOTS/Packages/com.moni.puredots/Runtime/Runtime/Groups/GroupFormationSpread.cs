using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Runtime spread metrics for formation cohesion calculations.
    /// Populated by formation aggregation systems; consumed by squad cohesion logic.
    /// </summary>
    public struct GroupFormationSpread : IComponentData
    {
        /// <summary>0-1 measure, 1 = perfectly tight, 0 = fully scattered.</summary>
        public float CohesionNormalized;
    }
}






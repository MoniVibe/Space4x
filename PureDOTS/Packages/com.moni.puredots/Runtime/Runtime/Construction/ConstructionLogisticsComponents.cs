using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Construction
{
    /// <summary>
    /// Optional per-site settings for phased usability / logistics hints.
    /// </summary>
    public struct ConstructionSitePhaseSettings : IComponentData
    {
        /// <summary>Owning group/authority entity (optional).</summary>
        public Entity OwningGroup;

        /// <summary>
        /// Normalized progress threshold (0-1) where the site becomes partially usable.
        /// </summary>
        public float PartialUseThreshold01;

        /// <summary>
        /// Priority hint for material hauling (0 = default).
        /// </summary>
        public byte LogisticsPriority;
    }

    /// <summary>
    /// Computed material needs for a construction site (outstanding haul bulletin).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ConstructionMaterialNeed : IBufferElementData
    {
        public ushort ResourceTypeIndex;
        public float OutstandingUnits;
        public byte Priority;
    }
}



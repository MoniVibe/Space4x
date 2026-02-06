using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// System traits applied during galaxy generation (black holes, nebulae, etc.).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct Space4XSystemTrait : IBufferElementData
    {
        public GalaxySystemTraitKind Kind;
        public half Intensity;
    }

    /// <summary>
    /// Point of interest marker for exploration and mission hooks.
    /// </summary>
    public struct Space4XPoi : IComponentData
    {
        public GalaxyPoiKind Kind;
        public half Reward;
        public half Risk;
        public ushort SystemId;
        public byte RingIndex;
    }
}

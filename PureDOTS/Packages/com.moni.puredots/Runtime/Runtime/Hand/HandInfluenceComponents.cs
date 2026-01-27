using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    [Flags]
    public enum InfluenceSourceFlags : byte
    {
        None = 0,
        Village = 1 << 0,
        Miracle = 1 << 1,
        Structure = 1 << 2,
        Temporary = 1 << 7
    }

    /// <summary>
    /// Defines an area of control that allows Divine Hand interactions.
    /// Any point within <see cref="Radius"/> units of the entity's position is considered inside the ring.
    /// </summary>
    public struct InfluenceSource : IComponentData
    {
        public float Radius;
        public byte PlayerId;
        public InfluenceSourceFlags Flags;
    }
}

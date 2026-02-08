using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Minimal identity components used by Space4X when the full identity package is absent.
    /// </summary>
    public struct RaceId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public struct CultureId : IComponentData
    {
        public FixedString64Bytes Value;
    }
}

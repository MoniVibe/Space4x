using Unity.Collections;
using Unity.Entities;

namespace Space4X
{
    /// <summary>
    /// Runtime component that tracks which profile was applied to this world.
    /// Lives in runtime asmdef; authoring uses plain strings.
    /// </summary>
    public struct Space4XWorldProfile : IComponentData
    {
        public FixedString64Bytes ProfileId;
    }
}


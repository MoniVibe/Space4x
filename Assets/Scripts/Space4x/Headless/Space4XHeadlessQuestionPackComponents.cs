using Unity.Collections;
using Unity.Entities;

namespace Space4X.Headless
{
    public struct Space4XHeadlessQuestionPackTag : IComponentData
    {
    }

    [InternalBufferCapacity(8)]
    public struct Space4XHeadlessQuestionPackItem : IBufferElementData
    {
        public FixedString64Bytes Id;
        public byte Required;
    }
}

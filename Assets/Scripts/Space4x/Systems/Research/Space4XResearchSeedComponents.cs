using Unity.Entities;

namespace Space4X.Systems.Research
{
    public struct Space4XResearchSeedRequest : IComponentData
    {
        public byte ClearExisting;
    }
}

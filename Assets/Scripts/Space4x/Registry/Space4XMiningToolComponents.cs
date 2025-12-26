using PureDOTS.Environment;
using Unity.Entities;

namespace Space4X.Registry
{
    public struct Space4XMiningToolProfile : IComponentData
    {
        public TerrainModificationToolKind ToolKind;

        public static Space4XMiningToolProfile Default => new()
        {
            ToolKind = TerrainModificationToolKind.Drill
        };
    }
}

using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Singleton component tracking the current terrain modification version.
    /// Increment whenever a terraforming action changes surface data.
    /// </summary>
    public struct TerrainVersion : IComponentData
    {
        public uint Value;
    }

    /// <summary>
    /// Event buffer published whenever the terrain changes. Consumers read, react, then clear.
    /// </summary>
    public struct TerrainChangeEvent : IBufferElementData
    {
        public uint Version;
        public float3 WorldMin;
        public float3 WorldMax;
        public byte Flags;

        public const byte FlagHeightChanged = 1 << 0;
        public const byte FlagSurfaceMaterialChanged = 1 << 1;
        public const byte FlagBiomeChanged = 1 << 2;
        public const byte FlagVolumeChanged = 1 << 3;
    }
}

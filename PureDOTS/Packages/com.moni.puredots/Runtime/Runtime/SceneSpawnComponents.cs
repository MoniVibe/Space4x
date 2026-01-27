using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public enum SceneSpawnCategory : byte
    {
        Villager = 0,
        Vegetation = 1,
        Resource = 2,
        Miracle = 3,
        Animal = 4,
        Structure = 5,
        Generic = 6,
        Faction = 7,
        ClimateHazard = 8,
        AreaEffect = 9,
        Culture = 10
    }

    public enum SpawnPlacementMode : byte
    {
        Point = 0,
        RandomCircle = 1,
        Ring = 2,
        Grid = 3,
        CustomPoints = 4
    }

    public enum SpawnRotationMode : byte
    {
        Identity = 0,
        RandomYaw = 1,
        FixedYaw = 2,
        AlignOutward = 3
    }

    /// <summary>
    /// Root component that holds seed and flags for scene spawning.
    /// </summary>
    public struct SceneSpawnController : IComponentData
    {
        public uint Seed;
        public byte Flags;
    }

    /// <summary>
    /// Tag added once a spawn controller has processed its requests.
    /// </summary>
    public struct SceneSpawnProcessedTag : IComponentData
    {
    }

    /// <summary>
    /// Combined spawn request entry. Additional payload data can be used for job/role/species identifiers.
    /// </summary>
    public struct SceneSpawnRequest : IBufferElementData
    {
        public SceneSpawnCategory Category;
        public Entity Prefab;
        public int Count;
        public SpawnPlacementMode Placement;
        public SpawnRotationMode Rotation;
        public float3 Offset;
        public float Radius;
        public float InnerRadius;
        public int2 GridDimensions;
        public float2 GridSpacing;
        public float2 HeightRange;
        public float FixedYawDegrees;
        public FixedString64Bytes PayloadId;
        public float PayloadValue;
        public uint SeedOffset;
        public int CustomPointStart;
        public int CustomPointCount;
        public Hash128 PresentationDescriptor;
        public float3 PresentationOffset;
        public quaternion PresentationRotationOffset;
        public float PresentationScaleMultiplier;
        public float4 PresentationTint;
        public uint PresentationVariantSeed;
        public PresentationSpawnFlags PresentationFlags;
    }

    /// <summary>
    /// Stores custom local offsets for entries that use SpawnPlacementMode.CustomPoints.
    /// </summary>
    public struct SceneSpawnPoint : IBufferElementData
    {
        public float3 LocalPoint;
    }
}

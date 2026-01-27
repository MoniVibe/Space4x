using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Platform;

namespace PureDOTS.Runtime.Platform.Blobs
{
    /// <summary>
    /// Module category classification.
    /// </summary>
    public enum ModuleCategory : byte
    {
        Engine = 0,
        Shield = 1,
        Weapon = 2,
        Hangar = 3,
        Facility = 4,
        Utility = 5,
        Colony = 6
    }

    /// <summary>
    /// Hardpoint slot type.
    /// </summary>
    public enum HardpointSlotType : byte
    {
        Weapon = 0,
        Engine = 1,
        Shield = 2,
        Utility = 3,
        ExternalFacility = 4,
        Internal = 5
    }

    /// <summary>
    /// Hardpoint definition for Hardpoint layout mode.
    /// </summary>
    public struct HardpointDef
    {
        public short Index;
        public HardpointSlotType SlotType;
        public byte IsExternal;
        public float3 LocalPosition;
        public quaternion LocalRotation;
        public short SegmentIndex;
    }

    /// <summary>
    /// Voxel cell definition for VoxelHull layout mode.
    /// </summary>
    public struct VoxelCellDef
    {
        public int CellIndex;
        public float3 LocalPosition;
        public byte IsExternal;
        public short SegmentIndex;
    }

    /// <summary>
    /// Module definition stored in blob registry.
    /// </summary>
    public struct ModuleDef
    {
        public int ModuleId;
        public ModuleCategory Category;
        public float Mass;
        public float PowerDraw;
        public float Volume;
        public byte AllowedPlacementMask;
        public byte AllowedLayoutMask;
        public BlobArray<byte> CapabilityPayload;
    }

    /// <summary>
    /// Armor profile defining resistances per damage type.
    /// </summary>
    public struct ArmorProfile
    {
        public int ProfileId;
        public float KineticResist;
        public float EnergyResist;
        public float EMPResist;
        public float RadiationResist;
        public float InternalExplosionResist;
    }

    /// <summary>
    /// Platform segment definition (static, stored in blob registry).
    /// </summary>
    public struct PlatformSegmentDef
    {
        public int SegmentIndex;
        public byte IsCore;
        public byte DirectionMask;
        public float3 LocalPosition;
        public float3 Extents;
        public float BaseHP;
        public float ArmorThickness;
        public int ArmorProfileId;
        public float MassCapacity;
        public float PowerCapacity;
        public int NeighborStart;
        public int NeighborCount;
    }

    /// <summary>
    /// Hull definition stored in blob registry.
    /// </summary>
    public struct HullDef
    {
        public int HullId;
        public PlatformFlags Flags;
        public PlatformLayoutMode LayoutMode;
        public float BaseMass;
        public float BaseHP;
        public float BaseVolume;
        public float BasePowerCapacity;
        public int MaxModuleCount;
        public float MassCapacity;
        public float VolumeCapacity;
        public int HardpointOffset;
        public int HardpointCount;
        public int VoxelLayoutOffset;
        public int VoxelCellCount;
        public int SegmentOffset;
        public int SegmentCount;
        public byte TechTier;
    }

    /// <summary>
    /// Registry blob containing all hull definitions and their hardpoints/voxel cells.
    /// </summary>
    public struct HullDefRegistryBlob
    {
        public BlobArray<HullDef> Hulls;
        public BlobArray<HardpointDef> Hardpoints;
        public BlobArray<VoxelCellDef> VoxelCells;
        public BlobArray<PlatformSegmentDef> Segments;
        public BlobArray<ArmorProfile> ArmorProfiles;
        public BlobArray<int> SegmentAdjacency;
    }

    /// <summary>
    /// Registry blob containing all module definitions.
    /// </summary>
    public struct ModuleDefRegistryBlob
    {
        public BlobArray<ModuleDef> Modules;
    }

    /// <summary>
    /// Singleton component holding reference to hull definition registry blob.
    /// </summary>
    public struct HullDefRegistry : IComponentData
    {
        public BlobAssetReference<HullDefRegistryBlob> Registry;
    }

    /// <summary>
    /// Singleton component holding reference to module definition registry blob.
    /// </summary>
    public struct ModuleDefRegistry : IComponentData
    {
        public BlobAssetReference<ModuleDefRegistryBlob> Registry;
    }
}


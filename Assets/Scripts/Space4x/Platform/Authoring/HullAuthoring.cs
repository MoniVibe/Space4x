using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Platform.Authoring
{
    /// <summary>
    /// ScriptableObject for hull definitions.
    /// Contributes to HullDefRegistry blob.
    /// </summary>
    [CreateAssetMenu(fileName = "Hull", menuName = "Space4X/Platform/Hull")]
    public sealed class HullAuthoring : ScriptableObject
    {
        [SerializeField]
        private int hullId;

        [SerializeField]
        private PlatformFlags flags;

        [SerializeField]
        private PlatformLayoutMode layoutMode;

        [SerializeField]
        private float baseMass;

        [SerializeField]
        private float baseHP;

        [SerializeField]
        private float baseVolume;

        [SerializeField]
        private float basePowerCapacity;

        [SerializeField]
        private int maxModuleCount;

        [SerializeField]
        private float massCapacity;

        [SerializeField]
        private float volumeCapacity;

        [SerializeField]
        private HardpointDefinition[] hardpoints = System.Array.Empty<HardpointDefinition>();

        [SerializeField]
        private VoxelCellDefinition[] voxelCells = System.Array.Empty<VoxelCellDefinition>();

        [SerializeField]
        private byte techTier = 1;

        public int HullId => hullId;
        public PlatformFlags Flags => flags;
        public PlatformLayoutMode LayoutMode => layoutMode;
        public float BaseMass => baseMass;
        public float BaseHP => baseHP;
        public float BaseVolume => baseVolume;
        public float BasePowerCapacity => basePowerCapacity;
        public int MaxModuleCount => maxModuleCount;
        public float MassCapacity => massCapacity;
        public float VolumeCapacity => volumeCapacity;
        public HardpointDefinition[] Hardpoints => hardpoints;
        public VoxelCellDefinition[] VoxelCells => voxelCells;
        public byte TechTier => techTier;

        [System.Serializable]
        public struct HardpointDefinition
        {
            public short index;
            public HardpointSlotType slotType;
            public bool isExternal;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        [System.Serializable]
        public struct VoxelCellDefinition
        {
            public int cellIndex;
            public Vector3 localPosition;
            public bool isExternal;
        }
    }
}






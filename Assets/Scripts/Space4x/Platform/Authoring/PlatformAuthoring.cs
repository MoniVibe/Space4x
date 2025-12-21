using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Perception;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Platform.Authoring
{
    /// <summary>
    /// Authoring component for platforms (ships, stations, crafts, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlatformAuthoring : MonoBehaviour
    {
        [SerializeField]
        private int hullId;

        [SerializeField]
        private PlatformFlags flags;

        [SerializeField]
        private ModuleSlotDefinition[] initialModules = System.Array.Empty<ModuleSlotDefinition>();

        [SerializeField]
        private CrewAssignmentDefinition[] initialCrew = System.Array.Empty<CrewAssignmentDefinition>();

        [SerializeField]
        private HangarBayDefinition[] hangarBays = System.Array.Empty<HangarBayDefinition>();

        [SerializeField]
        private int manufacturerId;

        [SerializeField]
        private byte baseQualityTier = 50;

        [SerializeField]
        private byte techTier = 1;

        public int HullId => hullId;
        public PlatformFlags Flags => flags;
        public ModuleSlotDefinition[] InitialModules => initialModules;
        public CrewAssignmentDefinition[] InitialCrew => initialCrew;
        public HangarBayDefinition[] HangarBays => hangarBays;
        public int ManufacturerId => manufacturerId;
        public byte BaseQualityTier => baseQualityTier;
        public byte TechTier => techTier;

        [System.Serializable]
        public struct ModuleSlotDefinition
        {
            public int moduleId;
            public short slotIndex;
            public int cellIndex;
            public short segmentIndex;
            public bool isExternal;
            public ModuleSlotState state;
        }

        [System.Serializable]
        public struct CrewAssignmentDefinition
        {
            public GameObject crewEntity;
            public int roleId;
        }

        [System.Serializable]
        public struct HangarBayDefinition
        {
            public int hangarClassId;
            public int capacity;
            public float launchRate;
            public float recoveryRate;
        }

        private sealed class Baker : Unity.Entities.Baker<PlatformAuthoring>
        {
            public override void Bake(PlatformAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PlatformKind
                {
                    Flags = authoring.flags
                });

                AddComponent<CommunicationModuleTag>(entity);
                AddComponent(entity, MediumContext.Vacuum);

                AddComponent(entity, new PlatformHullRef
                {
                    HullId = authoring.hullId
                });

                var moduleBuffer = AddBuffer<PlatformModuleSlot>(entity);
                if (authoring.initialModules != null)
                {
                    foreach (var moduleDef in authoring.initialModules)
                    {
                        moduleBuffer.Add(new PlatformModuleSlot
                        {
                            ModuleId = moduleDef.moduleId,
                            SlotIndex = moduleDef.slotIndex,
                            CellIndex = moduleDef.cellIndex,
                            SegmentIndex = moduleDef.segmentIndex,
                            IsExternal = (byte)(moduleDef.isExternal ? 1 : 0),
                            State = moduleDef.state
                        });
                    }
                }

                var crewBuffer = AddBuffer<PlatformCrewMember>(entity);
                if (authoring.initialCrew != null)
                {
                    foreach (var crewDef in authoring.initialCrew)
                    {
                        crewBuffer.Add(new PlatformCrewMember
                        {
                            CrewEntity = GetEntity(crewDef.crewEntity, TransformUsageFlags.None),
                            RoleId = crewDef.roleId
                        });
                    }
                }

                var hangarBuffer = AddBuffer<HangarBay>(entity);
                if (authoring.hangarBays != null)
                {
                    foreach (var hangarDef in authoring.hangarBays)
                    {
                        hangarBuffer.Add(new HangarBay
                        {
                            HangarClassId = hangarDef.hangarClassId,
                            Capacity = hangarDef.capacity,
                            ReservedSlots = 0,
                            OccupiedSlots = 0,
                            LaunchRate = hangarDef.launchRate,
                            RecoveryRate = hangarDef.recoveryRate
                        });
                    }
                }

                AddComponent(entity, new PlatformManufacturer
                {
                    ManufacturerId = authoring.manufacturerId,
                    BaseQualityTier = authoring.baseQualityTier,
                    TechTier = authoring.techTier
                });

                AddComponent(entity, new PlatformTuningState
                {
                    Reliability = 1f,
                    PerformanceFactor = 1f,
                    MaintenanceDebt = 0f,
                    WearLevel = 0f
                });
            }
        }
    }
}

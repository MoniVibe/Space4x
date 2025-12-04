using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Space4X.Platform.Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Platform.Systems
{
    /// <summary>
    /// Builds blob registries from ScriptableObject catalog.
    /// Creates singleton entities for registry access.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct PlatformRegistryBootstrap : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        public static void BuildRegistries(EntityManager entityManager, PlatformRegistryCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            BuildHullRegistry(entityManager, catalog.Hulls);
            BuildModuleRegistry(entityManager, catalog.Modules);
        }

        private static void BuildHullRegistry(EntityManager entityManager, HullAuthoring[] hulls)
        {
            if (hulls == null || hulls.Length == 0)
            {
                return;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<HullDefRegistryBlob>();

            var hullList = new NativeList<HullDef>(hulls.Length, Allocator.Temp);
            var hardpointList = new NativeList<HardpointDef>(Allocator.Temp);
            var voxelList = new NativeList<VoxelCellDef>(Allocator.Temp);

            int hardpointOffset = 0;
            int voxelOffset = 0;

            foreach (var hullAuthoring in hulls)
            {
                if (hullAuthoring == null)
                {
                    continue;
                }

                var hullDef = new HullDef
                {
                    HullId = hullAuthoring.HullId,
                    Flags = hullAuthoring.Flags,
                    LayoutMode = hullAuthoring.LayoutMode,
                    BaseMass = hullAuthoring.BaseMass,
                    BaseHP = hullAuthoring.BaseHP,
                    BaseVolume = hullAuthoring.BaseVolume,
                    BasePowerCapacity = hullAuthoring.BasePowerCapacity,
                    MaxModuleCount = hullAuthoring.MaxModuleCount,
                    MassCapacity = hullAuthoring.MassCapacity,
                    VolumeCapacity = hullAuthoring.VolumeCapacity,
                    HardpointOffset = hardpointOffset,
                    HardpointCount = hullAuthoring.Hardpoints != null ? hullAuthoring.Hardpoints.Length : 0,
                    VoxelLayoutOffset = voxelOffset,
                    VoxelCellCount = hullAuthoring.VoxelCells != null ? hullAuthoring.VoxelCells.Length : 0,
                    TechTier = hullAuthoring.TechTier
                };

                hullList.Add(hullDef);

                if (hullAuthoring.Hardpoints != null)
                {
                    foreach (var hardpointAuthoring in hullAuthoring.Hardpoints)
                    {
                        hardpointList.Add(new HardpointDef
                        {
                            Index = hardpointAuthoring.index,
                            SlotType = hardpointAuthoring.slotType,
                            IsExternal = (byte)(hardpointAuthoring.isExternal ? 1 : 0),
                            LocalPosition = hardpointAuthoring.localPosition,
                            LocalRotation = hardpointAuthoring.localRotation
                        });
                    }
                    hardpointOffset += hullAuthoring.Hardpoints.Length;
                }

                if (hullAuthoring.VoxelCells != null)
                {
                    foreach (var voxelAuthoring in hullAuthoring.VoxelCells)
                    {
                        voxelList.Add(new VoxelCellDef
                        {
                            CellIndex = voxelAuthoring.cellIndex,
                            LocalPosition = voxelAuthoring.localPosition,
                            IsExternal = (byte)(voxelAuthoring.isExternal ? 1 : 0)
                        });
                    }
                    voxelOffset += hullAuthoring.VoxelCells.Length;
                }
            }

            builder.Allocate(ref root.Hulls, hullList.Length);
            for (int i = 0; i < hullList.Length; i++)
            {
                root.Hulls[i] = hullList[i];
            }

            builder.Allocate(ref root.Hardpoints, hardpointList.Length);
            for (int i = 0; i < hardpointList.Length; i++)
            {
                root.Hardpoints[i] = hardpointList[i];
            }

            builder.Allocate(ref root.VoxelCells, voxelList.Length);
            for (int i = 0; i < voxelList.Length; i++)
            {
                root.VoxelCells[i] = voxelList[i];
            }

            var blob = builder.CreateBlobAssetReference<HullDefRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            hullList.Dispose();
            hardpointList.Dispose();
            voxelList.Dispose();

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HullDefRegistry>());
            Entity registryEntity;
            if (query.IsEmptyIgnoreFilter)
            {
                registryEntity = entityManager.CreateEntity(typeof(HullDefRegistry));
            }
            else
            {
                registryEntity = query.GetSingletonEntity();
            }

            entityManager.SetComponentData(registryEntity, new HullDefRegistry { Registry = blob });
        }

        private static void BuildModuleRegistry(EntityManager entityManager, ModuleAuthoring[] modules)
        {
            if (modules == null || modules.Length == 0)
            {
                return;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModuleDefRegistryBlob>();

            var moduleList = new NativeList<ModuleDef>(modules.Length, Allocator.Temp);

            foreach (var moduleAuthoring in modules)
            {
                if (moduleAuthoring == null)
                {
                    continue;
                }

                var placementMask = (byte)0;
                if (moduleAuthoring.AllowedInternal)
                    placementMask |= 1;
                if (moduleAuthoring.AllowedExternal)
                    placementMask |= 2;

                var layoutMask = (byte)0;
                if (moduleAuthoring.AllowedMassMode)
                    layoutMask |= 1;
                if (moduleAuthoring.AllowedHardpointMode)
                    layoutMask |= 2;
                if (moduleAuthoring.AllowedVoxelMode)
                    layoutMask |= 4;

                var payloadSize = moduleAuthoring.CapabilityPayload != null ? moduleAuthoring.CapabilityPayload.Length * 4 : 0;
                
                var moduleDef = new ModuleDef
                {
                    ModuleId = moduleAuthoring.ModuleId,
                    Category = moduleAuthoring.Category,
                    Mass = moduleAuthoring.Mass,
                    PowerDraw = moduleAuthoring.PowerDraw,
                    Volume = moduleAuthoring.Volume,
                    AllowedPlacementMask = placementMask,
                    AllowedLayoutMask = layoutMask,
                    CapabilityPayload = default
                };

                moduleList.Add(moduleDef);
            }

            builder.Allocate(ref root.Modules, moduleList.Length);
            int moduleIndex = 0;
            for (int i = 0; i < modules.Length; i++)
            {
                var moduleAuthoring = modules[i];
                if (moduleAuthoring == null)
                {
                    continue;
                }

                var payloadSize = moduleAuthoring.CapabilityPayload != null ? moduleAuthoring.CapabilityPayload.Length * 4 : 0;
                var payloadArray = builder.Allocate(ref root.Modules[moduleIndex].CapabilityPayload, payloadSize);
                
                if (moduleAuthoring.CapabilityPayload != null)
                {
                    for (int j = 0; j < moduleAuthoring.CapabilityPayload.Length; j++)
                    {
                        var bytes = System.BitConverter.GetBytes(moduleAuthoring.CapabilityPayload[j]);
                        for (int k = 0; k < 4; k++)
                        {
                            payloadArray[j * 4 + k] = bytes[k];
                        }
                    }
                }

                root.Modules[moduleIndex] = new ModuleDef
                {
                    ModuleId = moduleAuthoring.ModuleId,
                    Category = moduleAuthoring.Category,
                    Mass = moduleAuthoring.Mass,
                    PowerDraw = moduleAuthoring.PowerDraw,
                    Volume = moduleAuthoring.Volume,
                    AllowedPlacementMask = moduleList[moduleIndex].AllowedPlacementMask,
                    AllowedLayoutMask = moduleList[moduleIndex].AllowedLayoutMask,
                    CapabilityPayload = payloadArray
                };
                moduleIndex++;
            }

            var blob = builder.CreateBlobAssetReference<ModuleDefRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            moduleList.Dispose();

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleDefRegistry>());
            Entity registryEntity;
            if (query.IsEmptyIgnoreFilter)
            {
                registryEntity = entityManager.CreateEntity(typeof(ModuleDefRegistry));
            }
            else
            {
                registryEntity = query.GetSingletonEntity();
            }

            entityManager.SetComponentData(registryEntity, new ModuleDefRegistry { Registry = blob });
        }
    }
}


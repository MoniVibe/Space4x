using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Builds arc instances from module positions for weapons/shields.
    /// Converts hardpoint/voxel positions to world space.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct PlatformArcSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullDefRegistry>();
            state.RequireForUpdate<ModuleDefRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hullRegistry = SystemAPI.GetSingleton<HullDefRegistry>();
            var moduleRegistry = SystemAPI.GetSingleton<ModuleDefRegistry>();

            if (!hullRegistry.Registry.IsCreated || !moduleRegistry.Registry.IsCreated)
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (hullRef, moduleSlots, transform, entity) in SystemAPI.Query<RefRO<PlatformHullRef>, DynamicBuffer<PlatformModuleSlot>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var hullId = hullRef.ValueRO.HullId;
                ref var hullRegistryBlob = ref hullRegistry.Registry.Value;
                
                if (hullId < 0 || hullId >= hullRegistryBlob.Hulls.Length)
                {
                    continue;
                }

                ref var hullDef = ref hullRegistryBlob.Hulls[hullId];
                ref var moduleRegistryBlob = ref moduleRegistry.Registry.Value;

                BuildArcInstances(
                    ref state,
                    ref ecb,
                    entity,
                    in hullDef,
                    in moduleSlots,
                    in transform.ValueRO,
                    ref moduleRegistryBlob,
                    ref hullRegistryBlob);
            }
        }

        private static void BuildArcInstances(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity platformEntity,
            in HullDef hullDef,
            in DynamicBuffer<PlatformModuleSlot> moduleSlots,
            in LocalTransform transform,
            ref ModuleDefRegistryBlob moduleRegistry,
            ref HullDefRegistryBlob hullRegistry)
        {
            if (!state.EntityManager.HasBuffer<PlatformArcInstance>(platformEntity))
            {
                ecb.AddBuffer<PlatformArcInstance>(platformEntity);
                return; // Buffer will be available next frame
            }
            var arcBuffer = state.EntityManager.GetBuffer<PlatformArcInstance>(platformEntity);
            arcBuffer.Clear();

            for (int i = 0; i < moduleSlots.Length; i++)
            {
                var slot = moduleSlots[i];
                
                if (slot.State != ModuleSlotState.Installed && slot.State != ModuleSlotState.Damaged)
                {
                    continue;
                }

                if (slot.ModuleId < 0 || slot.ModuleId >= moduleRegistry.Modules.Length)
                {
                    continue;
                }

                ref var moduleDef = ref moduleRegistry.Modules[slot.ModuleId];
                
                if (moduleDef.Category != ModuleCategory.Weapon && moduleDef.Category != ModuleCategory.Shield)
                {
                    continue;
                }

                float3 localPosition = float3.zero;
                float3 forwardDirection = math.forward(quaternion.identity);

                switch (hullDef.LayoutMode)
                {
                    case PlatformLayoutMode.Hardpoint:
                        if (slot.SlotIndex >= 0 && slot.SlotIndex < hullDef.HardpointCount)
                        {
                            var hardpointIndex = hullDef.HardpointOffset + slot.SlotIndex;
                            if (hardpointIndex >= 0 && hardpointIndex < hullRegistry.Hardpoints.Length)
                            {
                                ref var hardpoint = ref hullRegistry.Hardpoints[hardpointIndex];
                                localPosition = hardpoint.LocalPosition;
                                forwardDirection = math.mul(hardpoint.LocalRotation, math.forward());
                            }
                        }
                        break;

                    case PlatformLayoutMode.VoxelHull:
                        if (slot.CellIndex >= 0 && slot.CellIndex < hullDef.VoxelCellCount)
                        {
                            var cellIndex = hullDef.VoxelLayoutOffset + slot.CellIndex;
                            if (cellIndex >= 0 && cellIndex < hullRegistry.VoxelCells.Length)
                            {
                                ref var cell = ref hullRegistry.VoxelCells[cellIndex];
                                localPosition = cell.LocalPosition;
                                forwardDirection = math.forward();
                            }
                        }
                        break;

                    case PlatformLayoutMode.MassOnly:
                        localPosition = float3.zero;
                        forwardDirection = math.forward();
                        break;
                }

                var worldPosition = math.transform(transform.ToMatrix(), localPosition);
                var worldForward = math.mul(transform.Rotation, forwardDirection);
                var arcAngle = ExtractArcAngle(ref moduleDef.CapabilityPayload);

                arcBuffer.Add(new PlatformArcInstance
                {
                    ModuleId = slot.ModuleId,
                    WorldPosition = worldPosition,
                    ForwardDirection = worldForward,
                    ArcAngle = arcAngle > 0f ? arcAngle : math.PI * 2f
                });
            }

            if (arcBuffer.Length > 0 && !state.EntityManager.HasComponent<PlatformArcInstance>(platformEntity))
            {
                ecb.AddBuffer<PlatformArcInstance>(platformEntity);
            }
        }

        [BurstCompile]
        private static float ExtractArcAngle(ref BlobArray<byte> payload)
        {
            if (payload.Length < 8)
                return math.PI * 2f;
            var bytes = new uint4(payload[4], payload[5], payload[6], payload[7]);
            return math.asfloat(bytes.x);
        }
    }
}






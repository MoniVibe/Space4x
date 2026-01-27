using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Validates platform loadouts against segment-level capacity constraints.
    /// Enforces mass and power limits per segment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlatformLoadoutValidationSystem : ISystem
    {
        private BufferLookup<PlatformModuleSlot> _moduleSlotsLookup;
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullDefRegistry>();
            state.RequireForUpdate<ModuleDefRegistry>();
            _moduleSlotsLookup = state.GetBufferLookup<PlatformModuleSlot>(false);
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(false);
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

            ref var hullRegistryBlob = ref hullRegistry.Registry.Value;
            ref var moduleRegistryBlob = ref moduleRegistry.Registry.Value;

            _moduleSlotsLookup.Update(ref state);
            _segmentStatesLookup.Update(ref state);

            foreach (var (hullRef, entity) in SystemAPI.Query<
                RefRO<PlatformHullRef>>().WithEntityAccess())
            {
                if (!_moduleSlotsLookup.HasBuffer(entity) || !_segmentStatesLookup.HasBuffer(entity))
                {
                    continue;
                }

                var hullId = hullRef.ValueRO.HullId;
                if (hullId < 0 || hullId >= hullRegistryBlob.Hulls.Length)
                {
                    continue;
                }

                var moduleSlots = _moduleSlotsLookup[entity];
                var segmentStates = _segmentStatesLookup[entity];
                ref var hullDef = ref hullRegistryBlob.Hulls[hullId];
                if (hullDef.SegmentCount == 0)
                {
                    continue;
                }

                ValidateLoadout(
                    ref moduleSlots,
                    ref segmentStates,
                    in hullDef,
                    ref moduleRegistryBlob,
                    ref hullRegistryBlob);
            }
        }

        [BurstCompile]
        private static void ValidateLoadout(
            ref DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in HullDef hullDef,
            ref ModuleDefRegistryBlob moduleRegistry,
            ref HullDefRegistryBlob hullRegistry)
        {
            var segmentMassUsed = new NativeHashMap<int, float>(segmentStates.Length, Allocator.Temp);
            var segmentPowerUsed = new NativeHashMap<int, float>(segmentStates.Length, Allocator.Temp);

            for (int i = 0; i < moduleSlots.Length; i++)
            {
                var slot = moduleSlots[i];
                if (slot.State == ModuleSlotState.Destroyed || slot.ModuleId < 0)
                {
                    continue;
                }

                if (slot.ModuleId >= moduleRegistry.Modules.Length)
                {
                    continue;
                }

                ref var moduleDef = ref moduleRegistry.Modules[slot.ModuleId];
                var segmentIndex = slot.SegmentIndex;

                if (segmentMassUsed.TryGetValue(segmentIndex, out var mass))
                {
                    segmentMassUsed[segmentIndex] = mass + moduleDef.Mass;
                }
                else
                {
                    segmentMassUsed[segmentIndex] = moduleDef.Mass;
                }

                if (segmentPowerUsed.TryGetValue(segmentIndex, out var power))
                {
                    segmentPowerUsed[segmentIndex] = power + moduleDef.PowerDraw;
                }
                else
                {
                    segmentPowerUsed[segmentIndex] = moduleDef.PowerDraw;
                }
            }

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                var segmentIndex = segmentState.SegmentIndex;

                if (segmentIndex < 0 || segmentIndex >= hullDef.SegmentCount)
                {
                    continue;
                }

                var segmentOffset = hullDef.SegmentOffset + segmentIndex;
                if (segmentOffset < 0 || segmentOffset >= hullRegistry.Segments.Length)
                {
                    continue;
                }

                ref var segmentDef = ref hullRegistry.Segments[segmentOffset];

                var massUsed = segmentMassUsed.TryGetValue(segmentIndex, out var m) ? m : 0f;
                var powerUsed = segmentPowerUsed.TryGetValue(segmentIndex, out var p) ? p : 0f;

                segmentState.MassUsed = massUsed;
                segmentState.PowerUsed = powerUsed;

                if (massUsed > segmentDef.MassCapacity || powerUsed > segmentDef.PowerCapacity)
                {
                    for (int j = 0; j < moduleSlots.Length; j++)
                    {
                        if (moduleSlots[j].SegmentIndex == segmentIndex &&
                            moduleSlots[j].State == ModuleSlotState.Installed)
                        {
                            var slot = moduleSlots[j];
                            slot.State = ModuleSlotState.Offline;
                            moduleSlots[j] = slot;
                        }
                    }
                }

                segmentStates[i] = segmentState;
            }
        }
    }
}


using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Tracks reactor modules per segment and aggregates reactor state.
    /// Marks segments with ReactorPresent flag.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlatformReactorSystem : ISystem
    {
        private BufferLookup<PlatformModuleSlot> _moduleSlotsLookup;
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleDefRegistry>();
            _moduleSlotsLookup = state.GetBufferLookup<PlatformModuleSlot>(isReadOnly: true);
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var moduleRegistry = SystemAPI.GetSingleton<ModuleDefRegistry>();

            if (!moduleRegistry.Registry.IsCreated)
            {
                return;
            }

            ref var moduleRegistryBlob = ref moduleRegistry.Registry.Value;

            _moduleSlotsLookup.Update(ref state);
            _segmentStatesLookup.Update(ref state);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlatformTag>>()
                .WithAll<PlatformModuleSlot>()
                .WithAll<PlatformSegmentState>()
                .WithEntityAccess())
            {
                if (!_moduleSlotsLookup.HasBuffer(entity) || !_segmentStatesLookup.HasBuffer(entity))
                {
                    continue;
                }

                var moduleSlots = _moduleSlotsLookup[entity];
                var segmentStates = _segmentStatesLookup[entity];

                UpdateReactorFlags(
                    ref segmentStates,
                    in moduleSlots,
                    ref moduleRegistryBlob);
            }
        }

        [BurstCompile]
        private static void UpdateReactorFlags(
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref ModuleDefRegistryBlob moduleRegistry)
        {
            var segmentHasReactor = new NativeHashMap<int, bool>(segmentStates.Length, Allocator.Temp);

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
                if (moduleDef.Category == ModuleCategory.Utility)
                {
                    segmentHasReactor[slot.SegmentIndex] = true;
                }
            }

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                var segmentIndex = segmentState.SegmentIndex;

                if (segmentHasReactor.TryGetValue(segmentIndex, out var hasReactor) && hasReactor)
                {
                    segmentState.Status |= SegmentStatusFlags.ReactorPresent;
                }
                else
                {
                    segmentState.Status &= ~SegmentStatusFlags.ReactorPresent;
                }

                segmentStates[i] = segmentState;
            }
        }
    }
}


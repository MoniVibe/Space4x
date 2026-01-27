using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Platform ownership component.
    /// </summary>
    public struct PlatformOwnership : IComponentData
    {
        public int FactionId;
    }

    /// <summary>
    /// Checks capture conditions and transfers platform ownership.
    /// Capture requires: bridge/control segments + reactor/engineering + >50% intact segments under attacker control.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlatformBoardingResolutionSystem))]
    public partial struct PlatformCaptureSystem : ISystem
    {
        private BufferLookup<SegmentControl> _segmentControlsLookup;
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;
        private BufferLookup<PlatformModuleSlot> _moduleSlotsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullDefRegistry>();
            state.RequireForUpdate<ModuleDefRegistry>();
            _segmentControlsLookup = state.GetBufferLookup<SegmentControl>(isReadOnly: true);
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(isReadOnly: true);
            _moduleSlotsLookup = state.GetBufferLookup<PlatformModuleSlot>(isReadOnly: true);
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

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            _segmentControlsLookup.Update(ref state);
            _segmentStatesLookup.Update(ref state);
            _moduleSlotsLookup.Update(ref state);

            foreach (var (hullRef, boardingState, entity) in SystemAPI.Query<
                RefRO<PlatformHullRef>,
                RefRW<BoardingState>>().WithEntityAccess())
            {
                if (boardingState.ValueRO.Phase != BoardingPhase.Fighting)
                {
                    continue;
                }

                var hullId = hullRef.ValueRO.HullId;
                if (hullId < 0 || hullId >= hullRegistryBlob.Hulls.Length)
                {
                    continue;
                }

                if (!_segmentControlsLookup.HasBuffer(entity) ||
                    !_segmentStatesLookup.HasBuffer(entity) ||
                    !_moduleSlotsLookup.HasBuffer(entity))
                {
                    continue;
                }

                var segmentControls = _segmentControlsLookup[entity];
                var segmentStates = _segmentStatesLookup[entity];
                var moduleSlots = _moduleSlotsLookup[entity];

                ref var hullDef = ref hullRegistryBlob.Hulls[hullId];

                var attackerFactionId = boardingState.ValueRO.AttackerFactionId;

                if (CheckCaptureConditions(
                    attackerFactionId,
                    in hullDef,
                    ref segmentControls,
                    ref segmentStates,
                    in moduleSlots,
                    ref moduleRegistryBlob,
                    ref hullRegistryBlob))
                {
                    TransferOwnership(
                        ref state,
                        ref ecb,
                        entity,
                        attackerFactionId,
                        ref boardingState.ValueRW);
                }
            }
        }

        [BurstCompile]
        private static bool CheckCaptureConditions(
            int attackerFactionId,
            in HullDef hullDef,
            ref DynamicBuffer<SegmentControl> segmentControls,
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref ModuleDefRegistryBlob moduleRegistry,
            ref HullDefRegistryBlob hullRegistry)
        {
            bool hasBridge = false;
            bool hasReactor = false;
            int intactSegments = 0;
            int controlledSegments = 0;

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                if ((segmentState.Status & SegmentStatusFlags.Destroyed) != 0)
                {
                    continue;
                }

                intactSegments++;

                var segmentIndex = segmentState.SegmentIndex;
                var segmentOffset = hullDef.SegmentOffset + segmentIndex;
                if (segmentOffset < 0 || segmentOffset >= hullRegistry.Segments.Length)
                {
                    continue;
                }

                ref var segmentDef = ref hullRegistry.Segments[segmentOffset];

                bool isControlled = false;
                for (int j = 0; j < segmentControls.Length; j++)
                {
                    if (segmentControls[j].SegmentIndex == segmentIndex &&
                        segmentControls[j].FactionId == attackerFactionId &&
                        segmentControls[j].ControlLevel > 0.5f)
                    {
                        isControlled = true;
                        break;
                    }
                }

                if (isControlled)
                {
                    controlledSegments++;

                    if (segmentDef.IsCore != 0)
                    {
                        hasBridge = true;
                    }

                    if ((segmentState.Status & SegmentStatusFlags.ReactorPresent) != 0)
                    {
                        hasReactor = true;
                    }
                }
            }

            if (intactSegments == 0)
            {
                return false;
            }

            var controlRatio = (float)controlledSegments / intactSegments;
            return hasBridge && hasReactor && controlRatio > 0.5f;
        }

        private static void TransferOwnership(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity platformEntity,
            int attackerFactionId,
            ref BoardingState boardingState)
        {
            int previousFactionId = -1;
            if (state.EntityManager.HasComponent<PlatformOwnership>(platformEntity))
            {
                var ownership = state.EntityManager.GetComponentData<PlatformOwnership>(platformEntity);
                previousFactionId = ownership.FactionId;
            }

            if (!state.EntityManager.HasComponent<PlatformOwnership>(platformEntity))
            {
                ecb.AddComponent(platformEntity, new PlatformOwnership
                {
                    FactionId = attackerFactionId
                });
            }
            else
            {
                ecb.SetComponent(platformEntity, new PlatformOwnership
                {
                    FactionId = attackerFactionId
                });
            }

            boardingState.Phase = BoardingPhase.Resolution;

            ecb.AddBuffer<PlatformCapturedEvent>(platformEntity);
            var captureBuffer = ecb.SetBuffer<PlatformCapturedEvent>(platformEntity);
            captureBuffer.Add(new PlatformCapturedEvent
            {
                PlatformEntity = platformEntity,
                PreviousFactionId = previousFactionId,
                NewFactionId = attackerFactionId
            });
        }
    }
}


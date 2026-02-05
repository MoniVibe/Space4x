using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Issues docking requests for returning mining vessels once they are within range of their carrier.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    public partial struct Space4XVesselDockingRequestSystem : ISystem
    {
        private const float DockingRange = 4.5f;

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<DockingPolicy> _policyLookup;
        private ComponentLookup<DockingState> _stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningVessel>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _policyLookup = state.GetComponentLookup<DockingPolicy>(true);
            _stateLookup = state.GetComponentLookup<DockingState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _stateLookup.Update(ref state);
            var currentTick = time.Tick;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (vessel, aiState, transform, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<VesselAIState>, RefRO<LocalTransform>>()
                         .WithNone<DockingRequest, DockedTag, SimulationDisabledTag>()
                         .WithEntityAccess())
            {
                if (aiState.ValueRO.CurrentState != VesselAIState.State.Returning)
                {
                    continue;
                }

                var carrier = vessel.ValueRO.CarrierEntity;
                if (carrier == Entity.Null || !_transformLookup.HasComponent(carrier))
                {
                    continue;
                }

                var carrierPos = _transformLookup[carrier].Position;
                var dockingRange = DockingRange;
                var presenceMode = DockingPresenceMode.Latch;
                if (_policyLookup.HasComponent(carrier))
                {
                    var policy = _policyLookup[carrier];
                    if (policy.AllowDocking == 0)
                    {
                        continue;
                    }

                    if (policy.DockingRange > 0f)
                    {
                        dockingRange = policy.DockingRange;
                    }

                    presenceMode = policy.DefaultPresence;
                }

                var dockingRangeSq = dockingRange * dockingRange;
                var distanceSq = math.lengthsq(transform.ValueRO.Position - carrierPos);
                if (distanceSq > dockingRangeSq)
                {
                    continue;
                }

                ecb.AddComponent(entity, new DockingRequest
                {
                    TargetCarrier = carrier,
                    RequiredSlot = DockingSlotType.Utility,
                    RequestTick = currentTick,
                    Priority = 0
                });

                var dockingState = new DockingState
                {
                    Phase = DockingPhase.Docking,
                    Target = carrier,
                    SlotType = DockingSlotType.Utility,
                    PresenceMode = presenceMode,
                    RequestTick = currentTick,
                    PhaseTick = currentTick
                };
                if (_stateLookup.HasComponent(entity))
                {
                    ecb.SetComponent(entity, dockingState);
                }
                else
                {
                    ecb.AddComponent(entity, dockingState);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Interaction;
using Space4X.Runtime.Interaction;
using Space4X.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using HandStateData = PureDOTS.Runtime.Hand.HandState;

namespace Space4X.Systems.Interaction
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Hand.HandCommandEmitterSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandQueueThrowSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandPickupSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandHoldFollowSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandThrowSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandSlingshotSystem))]
    public partial struct Space4XCelestialHandCommandSystem : ISystem
    {
        private ComponentLookup<Space4XCelestialManipulable> _celestialLookup;
        private ComponentLookup<Space4XCelestialHoldState> _holdLookup;
        private ComponentLookup<HandHeldTag> _heldLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();
            _celestialLookup = state.GetComponentLookup<Space4XCelestialManipulable>(true);
            _holdLookup = state.GetComponentLookup<Space4XCelestialHoldState>(false);
            _heldLookup = state.GetComponentLookup<HandHeldTag>(true);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (modeState.IsDivineHandEnabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            _celestialLookup.Update(ref state);
            _holdLookup.Update(ref state);
            _heldLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer, handEntity) in SystemAPI.Query<RefRW<HandStateData>, DynamicBuffer<HandCommand>>().WithEntityAccess())
            {
                var handState = handStateRef.ValueRW;
                var buffer = commandBuffer;

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick != currentTick || cmd.TargetEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (!_celestialLookup.HasComponent(cmd.TargetEntity))
                    {
                        continue;
                    }

                    switch (cmd.Type)
                    {
                        case HandCommandType.Pick:
                            handState.HeldEntity = cmd.TargetEntity;
                            EnsureHoldState(ref ecb, cmd.TargetEntity, cmd.TargetPosition, handEntity);
                            buffer.RemoveAt(i);
                            break;
                        case HandCommandType.Hold:
                            EnsureHoldState(ref ecb, cmd.TargetEntity, cmd.TargetPosition, handEntity);
                            buffer.RemoveAt(i);
                            break;
                        case HandCommandType.Throw:
                        case HandCommandType.SlingshotThrow:
                        case HandCommandType.QueueThrow:
                            ApplyImpulse(ref ecb, cmd.TargetEntity, cmd.Direction * cmd.Speed, currentTick);
                            ClearHold(ref ecb, cmd.TargetEntity);
                            if (handState.HeldEntity == cmd.TargetEntity)
                            {
                                handState.HeldEntity = Entity.Null;
                                handState.CurrentState = HandStateType.Cooldown;
                                handState.StateTimer = 0;
                            }
                            buffer.RemoveAt(i);
                            break;
                    }
                }

                handStateRef.ValueRW = handState;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void EnsureHoldState(ref EntityCommandBuffer ecb, Entity target, float3 position, Entity holder)
        {
            var hold = new Space4XCelestialHoldState
            {
                TargetPosition = position,
                FollowStrength = 0f,
                Active = 1
            };

            if (_holdLookup.HasComponent(target))
            {
                ecb.SetComponent(target, hold);
            }
            else
            {
                ecb.AddComponent(target, hold);
            }

            var heldTag = new HandHeldTag { Holder = holder };
            if (_heldLookup.HasComponent(target))
            {
                ecb.SetComponent(target, heldTag);
            }
            else
            {
                ecb.AddComponent(target, heldTag);
            }

            if (_movementSuppressedLookup.HasComponent(target))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(target, true);
            }
            else
            {
                ecb.AddComponent<MovementSuppressed>(target);
            }
        }

        private static void ApplyImpulse(ref EntityCommandBuffer ecb, Entity target, float3 deltaV, uint tick)
        {
            ecb.AddComponent(target, new Space4XCelestialImpulseRequest
            {
                DeltaV = deltaV,
                Tick = tick
            });
        }

        private void ClearHold(ref EntityCommandBuffer ecb, Entity target)
        {
            ecb.RemoveComponent<Space4XCelestialHoldState>(target);
            ecb.RemoveComponent<HandHeldTag>(target);
            if (_movementSuppressedLookup.HasComponent(target))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(target, false);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCelestialHandCommandSystem))]
    public partial struct Space4XCelestialManipulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (modeState.IsDivineHandEnabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : timeState.DeltaTime;

            foreach (var (transform, velocity, hold) in SystemAPI.Query<
                RefRW<LocalTransform>,
                RefRW<SpaceVelocity>,
                RefRO<Space4XCelestialHoldState>>().WithAll<Space4XCelestialManipulable>())
            {
                if (hold.ValueRO.Active == 0)
                {
                    continue;
                }

                var current = transform.ValueRO;
                current.Position = hold.ValueRO.TargetPosition;
                transform.ValueRW = current;
                velocity.ValueRW = new SpaceVelocity
                {
                    Linear = float3.zero,
                    Angular = float3.zero
                };
            }

            foreach (var (transform, velocity) in SystemAPI.Query<
                RefRW<LocalTransform>,
                RefRW<SpaceVelocity>>()
                     .WithAll<Space4XCelestialManipulable>()
                     .WithNone<Space4XCelestialHoldState>())
            {
                velocity.ValueRW.Linear *= 0.995f;
                transform.ValueRW.Position += velocity.ValueRO.Linear * deltaTime;
            }

            foreach (var (impulse, velocity, entity) in SystemAPI.Query<
                RefRO<Space4XCelestialImpulseRequest>,
                RefRW<SpaceVelocity>>().WithAll<Space4XCelestialManipulable>().WithEntityAccess())
            {
                velocity.ValueRW.Linear += impulse.ValueRO.DeltaV;
                state.EntityManager.RemoveComponent<Space4XCelestialImpulseRequest>(entity);
            }

        }
    }
}

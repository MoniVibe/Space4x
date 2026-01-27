using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using HandStateData = PureDOTS.Runtime.Hand.HandState;

namespace PureDOTS.Systems.Hand
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandCommandEmitterSystem))]
    [UpdateBefore(typeof(HandThrowSystem))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial struct HandQueueThrowSystem : ISystem
    {
        private ComponentLookup<HandHeldTag> _heldLookup;
        private ComponentLookup<MovementSuppressed> _movementLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _heldLookup = state.GetComponentLookup<HandHeldTag>(false);
            _movementLookup = state.GetComponentLookup<MovementSuppressed>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            _heldLookup.Update(ref state);
            _movementLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer, throwQueue) in SystemAPI.Query<
                RefRW<HandStateData>,
                DynamicBuffer<HandCommand>,
                DynamicBuffer<ThrowQueue>>())
            {
                var handState = handStateRef.ValueRW;
                var buffer = commandBuffer;

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick != currentTick || cmd.Type != HandCommandType.QueueThrow)
                    {
                        continue;
                    }

                    if (cmd.TargetEntity == Entity.Null)
                    {
                        buffer.RemoveAt(i);
                        continue;
                    }

                    throwQueue.Add(new ThrowQueue
                    {
                        Value = new ThrowQueueEntry
                        {
                            Target = cmd.TargetEntity,
                            Direction = math.normalizesafe(cmd.Direction, new float3(0f, 1f, 0f)),
                            Force = cmd.Speed
                        }
                    });

                    if (_heldLookup.HasComponent(cmd.TargetEntity))
                    {
                        ecb.RemoveComponent<HandHeldTag>(cmd.TargetEntity);
                    }

                    if (_movementLookup.HasComponent(cmd.TargetEntity))
                    {
                        ecb.SetComponentEnabled<MovementSuppressed>(cmd.TargetEntity, false);
                    }

                    if (handState.HeldEntity == cmd.TargetEntity)
                    {
                        handState.HeldEntity = Entity.Null;
                        handState.CurrentState = HandStateType.Cooldown;
                        handState.StateTimer = 0;
                    }

                    buffer.RemoveAt(i);
                }

                handStateRef.ValueRW = handState;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    // Burst disabled; headless player reported SIGSEGV in this system.
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandQueueThrowSystem))]
    [UpdateBefore(typeof(HandThrowSystem))]
    public partial struct HandQueueReleaseSystem : ISystem
    {
        private uint _lastInputSampleId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HandInputFrame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            var input = SystemAPI.GetSingleton<HandInputFrame>();
            bool isNewSample = input.SampleId != _lastInputSampleId;
            bool releaseOnePressed = isNewSample && input.ReleaseOnePressed;
            bool releaseAllPressed = isNewSample && input.ReleaseAllPressed;

            if (!releaseOnePressed && !releaseAllPressed)
            {
                return;
            }

            foreach (var (commandBuffer, throwQueue) in SystemAPI.Query<
                DynamicBuffer<HandCommand>,
                DynamicBuffer<ThrowQueue>>())
            {
                if (throwQueue.Length == 0)
                {
                    continue;
                }

                if (releaseAllPressed)
                {
                    for (int i = 0; i < throwQueue.Length; i++)
                    {
                        EmitThrow(commandBuffer, currentTick, throwQueue[i].Value);
                    }

                    throwQueue.Clear();
                }
                else
                {
                    var entry = throwQueue[0].Value;
                    throwQueue.RemoveAt(0);
                    EmitThrow(commandBuffer, currentTick, entry);
                }
            }

            if (isNewSample)
            {
                _lastInputSampleId = input.SampleId;
            }
        }

        private static void EmitThrow(DynamicBuffer<HandCommand> commands, uint currentTick, in ThrowQueueEntry entry)
        {
            commands.Add(new HandCommand
            {
                Tick = currentTick,
                Type = HandCommandType.Throw,
                TargetEntity = entry.Target,
                TargetPosition = float3.zero,
                Direction = entry.Direction,
                Speed = entry.Force,
                ChargeLevel = 0f,
                ResourceTypeIndex = 0,
                Amount = 0f
            });
        }
    }
}

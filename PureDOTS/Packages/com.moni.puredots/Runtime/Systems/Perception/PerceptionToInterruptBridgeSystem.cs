using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Bridges PerceptionState to Interrupts.
    /// Emits interrupts when perception detects new threats or loses threats.
    /// Phase 1: Basic threat detection interrupts.
    /// Phase 2: Extended with more perception-based interrupts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(PerceptionUpdateSystem))]
    public partial struct PerceptionToInterruptBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var thresholds = SystemAPI.TryGetSingleton<SignalPerceptionThresholds>(out var thresholdConfig)
                ? thresholdConfig
                : SignalPerceptionThresholds.Default;
            var cooldownTicks = thresholds.CooldownTicks == 0 ? 1u : thresholds.CooldownTicks;

            // Process entities with perception state
            foreach (var (perceptionState, perceivedBuffer, entity) in
                SystemAPI.Query<RefRO<PerceptionState>, DynamicBuffer<PerceivedEntity>>()
                .WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<Interrupt>(entity))
                {
                    continue;
                }

                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);

                // Check for new threats
                if (perceptionState.ValueRO.HighestThreatEntity != Entity.Null)
                {
                    // Check if we already have a NewThreatDetected interrupt for this entity
                    bool hasThreatInterrupt = false;
                    for (int i = 0; i < interruptBuffer.Length; i++)
                    {
                        var interrupt = interruptBuffer[i];
                        if (interrupt.Type == InterruptType.NewThreatDetected &&
                            interrupt.TargetEntity == perceptionState.ValueRO.HighestThreatEntity &&
                            interrupt.IsProcessed == 0)
                        {
                            hasThreatInterrupt = true;
                            break;
                        }
                    }

                    if (!hasThreatInterrupt && perceptionState.ValueRO.HighestThreat > 100)
                    {
                        // Emit new threat interrupt
                        InterruptUtils.EmitPerception(
                            ref interruptBuffer,
                            InterruptType.NewThreatDetected,
                            entity,
                            perceptionState.ValueRO.HighestThreatEntity,
                            float3.zero, // TODO: Get position from perceived buffer
                            timeState.Tick,
                            InterruptPriority.High);
                    }
                }

                // Check for lost threats (Phase 1: simple check)
                // TODO Phase 2: Track previous highest threat and detect when it's lost
            }

            foreach (var (signalState, transform, entity) in
                SystemAPI.Query<RefRW<SignalPerceptionState>, RefRO<Unity.Transforms.LocalTransform>>()
                    .WithEntityAccess())
            {
                if (signalState.ValueRO.LastUpdateTick != timeState.Tick)
                {
                    continue;
                }

                if (!SystemAPI.HasBuffer<Interrupt>(entity))
                {
                    continue;
                }

                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                var position = transform.ValueRO.Position;

                if (signalState.ValueRO.SmellConfidence >= thresholds.SmellThreshold &&
                    timeState.Tick - signalState.ValueRO.LastSmellInterruptTick >= cooldownTicks &&
                    !HasPendingInterrupt(ref interruptBuffer, InterruptType.SmellSignalDetected))
                {
                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.SmellSignalDetected,
                        InterruptPriority.Low,
                        entity,
                        timeState.Tick,
                        Entity.Null,
                        position,
                        signalState.ValueRO.SmellConfidence);
                    signalState.ValueRW.LastSmellInterruptTick = timeState.Tick;
                }

                if (signalState.ValueRO.SoundConfidence >= thresholds.SoundThreshold &&
                    timeState.Tick - signalState.ValueRO.LastSoundInterruptTick >= cooldownTicks &&
                    !HasPendingInterrupt(ref interruptBuffer, InterruptType.SoundSignalDetected))
                {
                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.SoundSignalDetected,
                        InterruptPriority.Low,
                        entity,
                        timeState.Tick,
                        Entity.Null,
                        position,
                        signalState.ValueRO.SoundConfidence);
                    signalState.ValueRW.LastSoundInterruptTick = timeState.Tick;
                }

                if (signalState.ValueRO.EMConfidence >= thresholds.EMThreshold &&
                    timeState.Tick - signalState.ValueRO.LastEMInterruptTick >= cooldownTicks &&
                    !HasPendingInterrupt(ref interruptBuffer, InterruptType.EMSignalDetected))
                {
                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.EMSignalDetected,
                        InterruptPriority.Low,
                        entity,
                        timeState.Tick,
                        Entity.Null,
                        position,
                        signalState.ValueRO.EMConfidence);
                    signalState.ValueRW.LastEMInterruptTick = timeState.Tick;
                }
            }
        }

        [BurstCompile]
        private static bool HasPendingInterrupt(ref DynamicBuffer<Interrupt> interruptBuffer, InterruptType type)
        {
            for (int i = 0; i < interruptBuffer.Length; i++)
            {
                var interrupt = interruptBuffer[i];
                if (interrupt.Type == type && interrupt.IsProcessed == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

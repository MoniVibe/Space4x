using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Interrupts
{
    /// <summary>
    /// Processes interrupts and writes EntityIntent for behavior systems.
    /// Picks highest-priority interrupt and converts to intent.
    /// Runs after perception/combat/group logic, before AI/GOAP systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    public partial struct InterruptHandlerSystem : ISystem
    {
        private ComponentLookup<IntentCommitmentConfig> _commitmentConfigLookup;
        private ComponentLookup<IntentCommitmentState> _commitmentStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _commitmentConfigLookup = state.GetComponentLookup<IntentCommitmentConfig>(true);
            _commitmentStateLookup = state.GetComponentLookup<IntentCommitmentState>(false);
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

            var interruptBufferLookup = SystemAPI.GetBufferLookup<Interrupt>(false);
            interruptBufferLookup.Update(ref state);
            _commitmentConfigLookup.Update(ref state);
            _commitmentStateLookup.Update(ref state);

            // Process interrupts for all entities with interrupt buffers
            foreach (var (intent, entity) in
                SystemAPI.Query<RefRW<EntityIntent>>()
                .WithEntityAccess())
            {
                if (!interruptBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var interruptBuffer = interruptBufferLookup[entity];
                
                if (interruptBuffer.Length == 0)
                {
                    // No interrupts, clear intent if invalid
                    if (intent.ValueRO.IsValid == 0)
                    {
                        continue; // Already cleared
                    }
                    // Intent may still be valid from previous tick
                    continue;
                }

                // Find highest priority unprocessed interrupt
                Interrupt bestInterrupt = default;
                int bestIndex = -1;
                InterruptPriority bestPriority = InterruptPriority.Low;

                for (int i = 0; i < interruptBuffer.Length; i++)
                {
                    var interrupt = interruptBuffer[i];
                    if (interrupt.IsProcessed != 0)
                    {
                        continue;
                    }

                    // Compare priority (higher byte value = higher priority)
                    if ((byte)interrupt.Priority > (byte)bestPriority)
                    {
                        bestInterrupt = interrupt;
                        bestIndex = i;
                        bestPriority = interrupt.Priority;
                    }
                }

                // If found valid interrupt, convert to intent
                if (bestIndex >= 0)
                {
                    ConvertInterruptToIntent(bestInterrupt, timeState.Tick, out var newIntent);

                    if (!CanApplyIntent(entity, intent.ValueRO, newIntent, timeState.Tick))
                    {
                        bestInterrupt.IsProcessed = 1;
                        interruptBuffer[bestIndex] = bestInterrupt;
                        CleanupProcessedInterrupts(ref interruptBuffer, timeState.Tick, 300);
                        continue;
                    }

                    // Update intent
                    intent.ValueRW = newIntent;
                    UpdateCommitmentState(entity, newIntent, timeState.Tick);

                    // Mark interrupt as processed
                    bestInterrupt.IsProcessed = 1;
                    interruptBuffer[bestIndex] = bestInterrupt;

                    // Clear processed interrupts older than N ticks (cleanup)
                    CleanupProcessedInterrupts(ref interruptBuffer, timeState.Tick, 300); // Keep for 5 seconds at 60fps
                }
                else
                {
                    // No valid interrupts, but keep existing intent if still valid
                    // Intent will be cleared by behavior systems when completed
                }
            }
        }

        [BurstCompile]
        private bool CanApplyIntent(Entity entity, in EntityIntent currentIntent, in EntityIntent newIntent, uint currentTick)
        {
            if (!_commitmentConfigLookup.HasComponent(entity) || !_commitmentStateLookup.HasComponent(entity))
            {
                return true;
            }

            if (currentIntent.IsValid == 0 || currentIntent.Mode == IntentMode.Idle)
            {
                return true;
            }

            if (IsSameIntent(in currentIntent, in newIntent))
            {
                return false;
            }

            var config = _commitmentConfigLookup[entity];
            var state = _commitmentStateLookup[entity];
            var overridePriority = (byte)config.OverridePriority;
            var newPriority = (byte)newIntent.Priority;
            var currentPriority = (byte)currentIntent.Priority;

            if (newPriority < currentPriority && newPriority < overridePriority)
            {
                return false;
            }

            if (currentTick < state.LockUntilTick && newPriority < overridePriority)
            {
                return false;
            }

            if (currentTick < state.CooldownUntilTick && newPriority < overridePriority)
            {
                return false;
            }

            return true;
        }

        [BurstCompile]
        private void UpdateCommitmentState(Entity entity, in EntityIntent newIntent, uint currentTick)
        {
            if (!_commitmentConfigLookup.HasComponent(entity) || !_commitmentStateLookup.HasComponent(entity))
            {
                return;
            }

            var config = _commitmentConfigLookup[entity];
            var state = _commitmentStateLookup[entity];
            state.LockUntilTick = currentTick + config.CommitmentTicks;
            state.CooldownUntilTick = currentTick + config.ReplanCooldownTicks;
            state.LastIntentTick = currentTick;
            _commitmentStateLookup[entity] = state;
        }

        [BurstCompile]
        private static bool IsSameIntent(in EntityIntent currentIntent, in EntityIntent newIntent)
        {
            if (currentIntent.Mode != newIntent.Mode)
            {
                return false;
            }

            if (currentIntent.TargetEntity != newIntent.TargetEntity)
            {
                return false;
            }

            var distanceSq = math.lengthsq(currentIntent.TargetPosition - newIntent.TargetPosition);
            return distanceSq <= 0.01f;
        }

        /// <summary>
        /// Converts an interrupt to an EntityIntent.
        /// Phase 1: Simple mapping.
        /// Phase 2: More sophisticated intent generation based on context.
        /// </summary>
        [BurstCompile]
        private static void ConvertInterruptToIntent(in Interrupt interrupt, uint currentTick, out EntityIntent intent)
        {
            intent = new EntityIntent
            {
                TriggeringInterrupt = interrupt.Type,
                Priority = interrupt.Priority,
                IntentSetTick = currentTick,
                TargetEntity = interrupt.TargetEntity,
                TargetPosition = interrupt.TargetPosition,
                IsValid = 1
            };

            // Map interrupt type to intent mode
            intent.Mode = interrupt.Type switch
            {
                InterruptType.UnderAttack => IntentMode.Attack,
                InterruptType.TookDamage => IntentMode.Flee, // Default: flee when damaged
                InterruptType.LostTarget => IntentMode.Idle,
                InterruptType.TargetDestroyed => IntentMode.Idle,
                InterruptType.NewThreatDetected => IntentMode.Attack,
                InterruptType.LostThreat => IntentMode.Idle,
                InterruptType.AllyInDanger => IntentMode.Defend,
                InterruptType.ResourceSpotted => IntentMode.Gather,
                InterruptType.ObjectiveSpotted => IntentMode.MoveTo,
                InterruptType.SmellSignalDetected => IntentMode.Custom0,
                InterruptType.SoundSignalDetected => IntentMode.Custom0,
                InterruptType.EMSignalDetected => IntentMode.Custom0,
                InterruptType.CommsMessageReceived => IntentMode.Custom0,
                InterruptType.CommsAckReceived => IntentMode.Idle,
                InterruptType.NewOrder => IntentMode.ExecuteOrder,
                InterruptType.OrderCancelled => IntentMode.Idle,
                InterruptType.ObjectiveChanged => IntentMode.ExecuteOrder,
                InterruptType.LowHealth => IntentMode.Flee,
                InterruptType.LowResources => IntentMode.Gather,
                InterruptType.AbilityReady => IntentMode.UseAbility,
                InterruptType.LieDetected => IntentMode.Custom0,
                InterruptType.IdentityExposed => IntentMode.Custom0,
                _ => IntentMode.Idle
            };
        }

        /// <summary>
        /// Removes processed interrupts older than specified age.
        /// </summary>
        [BurstCompile]
        private static void CleanupProcessedInterrupts(ref DynamicBuffer<Interrupt> buffer, uint currentTick, uint maxAgeTicks)
        {
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                var interrupt = buffer[i];
                if (interrupt.IsProcessed != 0)
                {
                    var age = currentTick - interrupt.Timestamp;
                    if (age > maxAgeTicks)
                    {
                        buffer.RemoveAt(i);
                    }
                }
            }
        }
    }
}

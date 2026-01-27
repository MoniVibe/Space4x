using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Intent;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Systems.Interrupts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Intent
{
    /// <summary>
    /// Validates entity intents to ensure they're still valid.
    /// Checks if target entities exist, if intents have expired, etc.
    /// Runs after InterruptHandlerSystem to clean up stale intents.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    [UpdateAfter(typeof(InterruptHandlerSystem))]
    public partial struct IntentValidationSystem : ISystem
    {
        private EntityStorageInfoLookup _entityStorageInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _entityStorageInfoLookup = state.GetEntityStorageInfoLookup();
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

            _entityStorageInfoLookup.Update(ref state);

            // Validate all intents
            foreach (var (intent, entity) in
                SystemAPI.Query<RefRW<EntityIntent>>()
                .WithEntityAccess())
            {
                if (IntentService.ValidateIntent(intent.ValueRO, _entityStorageInfoLookup, timeState.Tick))
                {
                    continue;
                }

                IntentService.ClearIntent(ref intent.ValueRW);
            }
        }
    }

    /// <summary>
    /// Processes intents and provides additional intent management logic.
    /// Can be extended for intent prioritization, intent queuing, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    [UpdateAfter(typeof(InterruptHandlerSystem))]
    [UpdateBefore(typeof(IntentValidationSystem))]
    public partial struct IntentProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
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

            var currentTick = timeState.Tick;

            // Process intents - currently just a pass-through
            // Future: Add intent queuing, intent prioritization, intent chaining, etc.
            foreach (var (intent, entity) in
                SystemAPI.Query<RefRW<EntityIntent>>()
                .WithEntityAccess())
            {
                // Phase 1: Simple pass-through
                // Phase 2: Add intent queuing, intent chaining, intent conditions, etc.
                
                // Example future logic:
                // - Queue lower-priority intents when high-priority interrupts occur
                // - Chain intents (Gather -> Deliver -> Rest)
                // - Add intent conditions (only execute if health > threshold)
            }

            // Promote queued intents (if entities are using the optional QueuedIntent buffer)
            // Note: Currently processes queued intents in FIFO order. Priority-based promotion may be added in future.
            foreach (var (intent, queueBuffer, entity) in
                SystemAPI.Query<RefRW<EntityIntent>, DynamicBuffer<QueuedIntent>>()
                .WithEntityAccess())
            {
                if (queueBuffer.Length == 0)
                {
                    continue;
                }

                var nextIntent = queueBuffer[0];

                bool shouldPromote = intent.ValueRO.IsValid == 0 ||
                                     intent.ValueRO.Mode == IntentMode.Idle ||
                                     IntentService.CanOverride(intent.ValueRO, nextIntent.Priority);

                if (!shouldPromote)
                {
                    continue;
                }

                ApplyQueuedIntent(ref intent.ValueRW, nextIntent, currentTick);
                queueBuffer.RemoveAt(0);
            }
        }

        [BurstCompile]
        private static void ApplyQueuedIntent(
            ref EntityIntent destination,
            in QueuedIntent queuedIntent,
            uint currentTick)
        {
            IntentService.SetIntent(
                ref destination,
                queuedIntent.Mode,
                queuedIntent.TargetEntity,
                queuedIntent.TargetPosition,
                queuedIntent.Priority,
                queuedIntent.TriggeringInterrupt,
                currentTick);
        }
    }

    /// <summary>
    /// Enhanced interrupt handler that works alongside the base InterruptHandlerSystem.
    /// Provides additional interrupt-to-intent mapping logic and intent management.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    [UpdateAfter(typeof(InterruptHandlerSystem))]
    public partial struct EnhancedInterruptHandlerSystem : ISystem
    {
        private ComponentLookup<EntityIntent> _intentLookup;
        private BufferLookup<Interrupt> _interruptBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _intentLookup = state.GetComponentLookup<EntityIntent>(false);
            _interruptBufferLookup = state.GetBufferLookup<Interrupt>(false);
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

            _intentLookup.Update(ref state);
            _interruptBufferLookup.Update(ref state);

            // Process interrupts that might override existing intents
            foreach (var (intent, entity) in
                SystemAPI.Query<RefRW<EntityIntent>>()
                .WithEntityAccess())
            {
                if (!_interruptBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var interruptBuffer = _interruptBufferLookup[entity];
                
                // Check if any unprocessed interrupts can override current intent
                for (int i = 0; i < interruptBuffer.Length; i++)
                {
                    var interrupt = interruptBuffer[i];
                    if (interrupt.IsProcessed != 0)
                    {
                        continue;
                    }

                    // Check if this interrupt can override current intent
                    if (IntentService.CanOverride(intent.ValueRO, interrupt.Priority))
                    {
                        // Convert interrupt to intent
                        ConvertInterruptToIntent(interrupt, timeState.Tick, out var newIntent);
                        
                        // Update intent
                        intent.ValueRW = newIntent;
                        
                        // Mark interrupt as processed
                        interrupt.IsProcessed = 1;
                        interruptBuffer[i] = interrupt;
                        
                        break; // Only process one interrupt per tick
                    }
                }
            }
        }

        /// <summary>
        /// Converts an interrupt to an EntityIntent with enhanced mapping logic.
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

            // Enhanced interrupt-to-intent mapping
            intent.Mode = interrupt.Type switch
            {
                // Combat interrupts
                InterruptType.UnderAttack => IntentMode.Attack,
                InterruptType.TookDamage => IntentMode.Flee, // Default: flee when damaged
                InterruptType.LostTarget => IntentMode.Idle,
                InterruptType.TargetDestroyed => IntentMode.Idle,
                InterruptType.WeaponReady => IntentMode.Attack, // If we have a target
                InterruptType.OutOfAmmo => IntentMode.Flee, // Retreat when out of ammo

                // Perception interrupts
                InterruptType.NewThreatDetected => IntentMode.Attack,
                InterruptType.LostThreat => IntentMode.Idle,
                InterruptType.AllyInDanger => IntentMode.Defend,
                InterruptType.ResourceSpotted => IntentMode.Gather,
                InterruptType.ObjectiveSpotted => IntentMode.MoveTo,
                InterruptType.SmellSignalDetected => IntentMode.MoveTo, // Investigate smell
                InterruptType.SoundSignalDetected => IntentMode.MoveTo, // Investigate sound
                InterruptType.EMSignalDetected => IntentMode.MoveTo, // Investigate EM signal
                InterruptType.CommsMessageReceived => IntentMode.ExecuteOrder,
                InterruptType.CommsAckReceived => IntentMode.Idle,

                // Group/Order interrupts
                InterruptType.NewOrder => IntentMode.ExecuteOrder,
                InterruptType.OrderCancelled => IntentMode.Idle,
                InterruptType.ObjectiveChanged => IntentMode.ExecuteOrder,
                InterruptType.GroupFormed => IntentMode.Follow, // Follow group leader
                InterruptType.GroupDisbanded => IntentMode.Idle,
                InterruptType.LeaderChanged => IntentMode.Follow,

                // State interrupts
                InterruptType.LowHealth => IntentMode.Flee,
                InterruptType.LowResources => IntentMode.Gather,
                InterruptType.StatusEffectApplied => IntentMode.Idle, // Context-dependent
                InterruptType.StatusEffectRemoved => IntentMode.Idle,
                InterruptType.AbilityReady => IntentMode.UseAbility,
                InterruptType.AbilityFailed => IntentMode.Idle,
                InterruptType.LieDetected => IntentMode.Custom0, // Game-specific
                InterruptType.IdentityExposed => IntentMode.Custom0, // Game-specific

                // Custom interrupts
                InterruptType.Custom0 => IntentMode.Custom0,
                InterruptType.Custom1 => IntentMode.Custom1,
                InterruptType.Custom2 => IntentMode.Custom2,
                InterruptType.Custom3 => IntentMode.Custom3,
                InterruptType.Custom4 => IntentMode.Custom0,
                InterruptType.Custom5 => IntentMode.Custom1,
                InterruptType.Custom6 => IntentMode.Custom2,
                InterruptType.Custom7 => IntentMode.Custom3,

                _ => IntentMode.Idle
            };
        }
    }
}


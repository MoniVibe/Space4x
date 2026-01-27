using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Effects;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Effects
{
    /// <summary>
    /// System that processes status effects: ticks durations, applies periodic effects, removes expired.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct StatusEffectSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            // Process all entities with status effects
            new TickStatusEffectsJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct TickStatusEffectsJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ActiveStatusEffect> effects,
                ref DynamicBuffer<StatusEffectTickEvent> tickEvents,
                ref DynamicBuffer<StatusEffectRemovedEvent> removedEvents)
            {
                // Process effects in reverse order for safe removal
                for (int i = effects.Length - 1; i >= 0; i--)
                {
                    var effect = effects[i];

                    // Skip permanent effects for duration check
                    bool isPermanent = effect.Duration < 0;

                    // Tick duration
                    if (!isPermanent)
                    {
                        effect.Duration -= DeltaTime;
                        
                        // Check if expired
                        if (effect.Duration <= 0)
                        {
                            // Emit removed event
                            removedEvents.Add(new StatusEffectRemovedEvent
                            {
                                Type = effect.Type,
                                WasExpired = true,
                                WasDispelled = false,
                                Tick = CurrentTick
                            });
                            
                            effects.RemoveAt(i);
                            continue;
                        }
                    }

                    // Process periodic effects (DoT/HoT)
                    if (effect.TickInterval > 0)
                    {
                        effect.TickTimer -= DeltaTime;
                        
                        while (effect.TickTimer <= 0)
                        {
                            // Emit tick event
                            tickEvents.Add(new StatusEffectTickEvent
                            {
                                Type = effect.Type,
                                Value = effect.Value * effect.Stacks,
                                Stacks = effect.Stacks,
                                Tick = CurrentTick
                            });
                            
                            effect.TickTimer += effect.TickInterval;
                        }
                    }

                    effects[i] = effect;
                }
            }
        }
    }

    /// <summary>
    /// System that processes status effect application requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(StatusEffectSystem))]
    [BurstCompile]
    public partial struct StatusEffectApplicationSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process apply requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<ApplyStatusEffectRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (SystemAPI.HasBuffer<ActiveStatusEffect>(req.TargetEntity))
                {
                    var effects = SystemAPI.GetBuffer<ActiveStatusEffect>(req.TargetEntity);
                    var immunities = SystemAPI.HasBuffer<StatusEffectImmunity>(req.TargetEntity)
                        ? SystemAPI.GetBuffer<StatusEffectImmunity>(req.TargetEntity)
                        : new DynamicBuffer<StatusEffectImmunity>();

                    byte maxEffects = 16; // Default
                    if (SystemAPI.HasComponent<StatusEffectConfig>(req.TargetEntity))
                    {
                        maxEffects = SystemAPI.GetComponent<StatusEffectConfig>(req.TargetEntity).MaxEffectsPerEntity;
                    }

                    bool applied = StatusEffectHelpers.TryApplyEffect(
                        ref effects,
                        immunities,
                        req.Type,
                        req.Category,
                        req.Behavior,
                        req.Duration,
                        req.Value,
                        req.TickInterval,
                        req.MaxStacks,
                        req.SourceEntity,
                        currentTick,
                        maxEffects);

                    if (applied && SystemAPI.HasBuffer<StatusEffectAppliedEvent>(req.TargetEntity))
                    {
                        var appliedEvents = SystemAPI.GetBuffer<StatusEffectAppliedEvent>(req.TargetEntity);
                        appliedEvents.Add(new StatusEffectAppliedEvent
                        {
                            Type = req.Type,
                            SourceEntity = req.SourceEntity,
                            Value = req.Value,
                            Stacks = 1,
                            Tick = currentTick
                        });
                    }
                }

                // Remove the request entity
                ecb.DestroyEntity(entity);
            }

            // Process remove requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<RemoveStatusEffectRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (SystemAPI.HasBuffer<ActiveStatusEffect>(req.TargetEntity))
                {
                    var effects = SystemAPI.GetBuffer<ActiveStatusEffect>(req.TargetEntity);
                    int removed = StatusEffectHelpers.RemoveEffect(ref effects, req.Type, req.RemoveAllStacks);

                    if (removed > 0 && SystemAPI.HasBuffer<StatusEffectRemovedEvent>(req.TargetEntity))
                    {
                        var removedEvents = SystemAPI.GetBuffer<StatusEffectRemovedEvent>(req.TargetEntity);
                        removedEvents.Add(new StatusEffectRemovedEvent
                        {
                            Type = req.Type,
                            WasDispelled = true,
                            WasExpired = false,
                            Tick = currentTick
                        });
                    }
                }

                // Remove the request entity
                ecb.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// System that processes immunity durations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StatusEffectSystem))]
    [BurstCompile]
    public partial struct StatusEffectImmunitySystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;

            new TickImmunitiesJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct TickImmunitiesJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref DynamicBuffer<StatusEffectImmunity> immunities)
            {
                for (int i = immunities.Length - 1; i >= 0; i--)
                {
                    var immunity = immunities[i];
                    
                    // Skip permanent immunities
                    if (immunity.Duration < 0)
                        continue;

                    immunity.Duration -= DeltaTime;
                    
                    if (immunity.Duration <= 0)
                    {
                        immunities.RemoveAt(i);
                    }
                    else
                    {
                        immunities[i] = immunity;
                    }
                }
            }
        }
    }
}


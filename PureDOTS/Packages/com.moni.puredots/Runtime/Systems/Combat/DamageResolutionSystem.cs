using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Consumes HitEvent and applies damage to Health/stats.
    /// Integrates with existing DamageEvent system - converts HitEvent to DamageEvent.
    /// Phase 1: Simple damage application.
    /// Phase 2: Extended with armor/shield calculations, resistances, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(FireControlSystem))]
    public partial struct DamageResolutionSystem : ISystem
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

            var hitEventBufferLookup = SystemAPI.GetBufferLookup<HitEvent>(false);
            hitEventBufferLookup.Update(ref state);

            // Process hit events and convert to damage events
            foreach (var (hitEventsReadOnly, entity) in
                SystemAPI.Query<DynamicBuffer<HitEvent>>()
                .WithEntityAccess())
            {
                // Get writable buffer via lookup
                if (!hitEventBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var hitEvents = hitEventBufferLookup[entity];
                
                if (hitEvents.Length == 0)
                {
                    continue;
                }

                // Ensure target has damage event buffer (for existing damage system)
                for (int i = 0; i < hitEvents.Length; i++)
                {
                    var hit = hitEvents[i];

                    // Skip if already processed
                    if (hit.HitTick == 0)
                    {
                        continue;
                    }

                    var targetEntity = hit.HitEntity;

                    if (!SystemAPI.Exists(targetEntity))
                    {
                        continue;
                    }

                    // Ensure target has DamageEvent buffer (for existing damage system)
                    if (!SystemAPI.HasBuffer<DamageEvent>(targetEntity))
                    {
                        state.EntityManager.AddBuffer<DamageEvent>(targetEntity);
                    }

                    var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(targetEntity);

                    // Convert HitEvent to DamageEvent
                    damageBuffer.Add(new DamageEvent
                    {
                        SourceEntity = hit.AttackerEntity,
                        TargetEntity = targetEntity,
                        RawDamage = hit.DamageAmount,
                        Type = hit.DamageType,
                        Tick = hit.HitTick,
                        Flags = DamageFlags.None // Phase 1: No special flags
                    });

                    // Apply damage directly to Health (Phase 1: simple application)
                    if (SystemAPI.HasComponent<Health>(targetEntity))
                    {
                        var health = SystemAPI.GetComponentRW<Health>(targetEntity);
                        health.ValueRW.Current = math.max(0f, health.ValueRO.Current - hit.DamageAmount);

                        // Emit interrupt for damage taken
                        if (SystemAPI.HasBuffer<Interrupt>(targetEntity))
                        {
                            var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(targetEntity);
                            InterruptUtils.EmitCombat(
                                ref interruptBuffer,
                                InterruptType.TookDamage,
                                hit.AttackerEntity,
                                targetEntity,
                                timeState.Tick,
                                InterruptPriority.High);
                        }
                    }

                    // Clear hit event (mark as processed)
                    hit.HitTick = 0;
                    hitEvents[i] = hit;
                }

                // Clean up processed hit events
                for (int i = hitEvents.Length - 1; i >= 0; i--)
                {
                    if (hitEvents[i].HitTick == 0)
                    {
                        hitEvents.RemoveAt(i);
                    }
                }
            }
        }
    }
}


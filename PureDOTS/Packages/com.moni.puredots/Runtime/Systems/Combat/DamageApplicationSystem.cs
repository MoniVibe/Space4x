using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Processes damage events and applies them to health/shield/armor.
    /// Integrates with buff system for damage modifiers.
    /// Runs after HitDetectionSystem and ProjectileDamageSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct DamageApplicationSystem : ISystem
    {
        private ComponentLookup<BuffStatCache> _buffLookup;
        private ComponentLookup<ArmorValue> _armorLookup;
        private ComponentLookup<Resistance> _resistanceLookup;
        private ComponentLookup<Shield> _shieldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _buffLookup = state.GetComponentLookup<BuffStatCache>(true);
            _armorLookup = state.GetComponentLookup<ArmorValue>(true);
            _resistanceLookup = state.GetComponentLookup<Resistance>(true);
            _shieldLookup = state.GetComponentLookup<Shield>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _buffLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _resistanceLookup.Update(ref state);
            _shieldLookup.Update(ref state);

            var damageHandle = new ProcessDamageEventsJob
            {
                CurrentTick = currentTick,
                Ecb = ecb,
                BuffLookup = _buffLookup,
                ArmorLookup = _armorLookup,
                ResistanceLookup = _resistanceLookup,
                ShieldLookup = _shieldLookup
            }.ScheduleParallel(state.Dependency);

            var healHandle = new ProcessHealEventsJob
            {
                CurrentTick = currentTick
            }.ScheduleParallel(damageHandle);

            state.Dependency = healHandle;
        }

        [BurstCompile]
        public partial struct ProcessDamageEventsJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<BuffStatCache> BuffLookup;
            [ReadOnly] public ComponentLookup<ArmorValue> ArmorLookup;
            [ReadOnly] public ComponentLookup<Resistance> ResistanceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Shield> ShieldLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                DynamicBuffer<DamageEvent> damageEvents,
                ref Health health,
                ref DynamicBuffer<DeathEvent> deathEvents)
            {
                // Skip if already dead
                if (health.Current <= 0f)
                {
                    damageEvents.Clear();
                    return;
                }

                float totalDamage = 0f;
                DamageType killingBlowType = DamageType.Physical;
                Entity killerEntity = Entity.Null;

                // Process all damage events
                for (int i = 0; i < damageEvents.Length; i++)
                {
                    var damageEvent = damageEvents[i];
                    float damage = damageEvent.RawDamage;

                    // Apply critical hit multiplier
                    if ((damageEvent.Flags & DamageFlags.Critical) != 0)
                    {
                        damage *= 1.5f; // 50% bonus for critical hits
                    }

                    // Apply buff modifiers (if BuffStatCache exists)
                    if (BuffLookup.HasComponent(entity))
                    {
                        var buffCache = BuffLookup[entity];
                        damage += buffCache.DamageFlat; // Add flat damage (usually 0, but can be negative for debuffs)
                        damage *= (1f + buffCache.DamagePercent); // Apply percent modifier
                    }

                    // Apply armor reduction (if ArmorValue exists and not piercing)
                    if ((damageEvent.Flags & DamageFlags.Pierce) == 0 && ArmorLookup.HasComponent(entity))
                    {
                        var armor = ArmorLookup[entity];
                        if (damageEvent.Type == DamageType.Physical)
                        {
                            // Flat armor reduction
                            damage = math.max(1f, damage - armor.Value);
                        }
                    }

                    // Apply resistance reduction (if Resistance exists)
                    if (ResistanceLookup.HasComponent(entity))
                    {
                        var resistance = ResistanceLookup[entity];
                        float resistanceValue = 0f;

                        switch (damageEvent.Type)
                        {
                            case DamageType.Physical:
                                resistanceValue = resistance.Physical;
                                break;
                            case DamageType.Fire:
                                resistanceValue = resistance.Fire;
                                break;
                            case DamageType.Cold:
                                resistanceValue = resistance.Cold;
                                break;
                            case DamageType.Lightning:
                                resistanceValue = resistance.Lightning;
                                break;
                            case DamageType.Poison:
                                resistanceValue = resistance.Poison;
                                break;
                            case DamageType.True:
                                // True damage bypasses resistances
                                break;
                        }

                        damage *= (1f - resistanceValue);
                    }

                    // Apply shield absorption (if Shield exists and not ignoring shield)
                    if ((damageEvent.Flags & DamageFlags.IgnoreShield) == 0 && ShieldLookup.HasComponent(entity))
                    {
                        var shield = ShieldLookup[entity];
                        if (shield.Current > 0f)
                        {
                            float shieldAbsorbed = math.min(shield.Current, damage);
                            shield.Current -= shieldAbsorbed;
                            shield.LastDamageTick = CurrentTick;
                            damage -= shieldAbsorbed;
                            ShieldLookup[entity] = shield;
                        }
                    }

                    // Apply minimum damage (lethal flag ensures at least 1 damage)
                    if ((damageEvent.Flags & DamageFlags.Lethal) != 0)
                    {
                        damage = math.max(1f, damage);
                    }

                    totalDamage += damage;
                    killingBlowType = damageEvent.Type;
                    if (damageEvent.SourceEntity != Entity.Null)
                    {
                        killerEntity = damageEvent.SourceEntity;
                    }
                }

                // Apply total damage to health
                health.Current -= totalDamage;
                health.LastDamageTick = CurrentTick;

                // Clamp health to 0
                if (health.Current < 0f)
                {
                    health.Current = 0f;
                }

                // Emit death event if health reached 0
                if (health.Current <= 0f && totalDamage > 0f)
                {
                    deathEvents.Add(new DeathEvent
                    {
                        DeadEntity = entity,
                        KillerEntity = killerEntity,
                        KillingBlowType = killingBlowType,
                        DeathTick = CurrentTick
                    });
                }

                // Clear processed damage events
                damageEvents.Clear();
            }
        }

        [BurstCompile]
        public partial struct ProcessHealEventsJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                DynamicBuffer<HealEvent> healEvents,
                ref Health health)
            {
                // Skip if dead
                if (health.Current <= 0f)
                {
                    healEvents.Clear();
                    return;
                }

                float totalHealing = 0f;

                // Process all heal events
                for (int i = 0; i < healEvents.Length; i++)
                {
                    totalHealing += healEvents[i].Amount;
                }

                // Apply healing
                health.Current += totalHealing;

                // Clamp to max health
                if (health.Current > health.Max)
                {
                    health.Current = health.Max;
                }

                // Clear processed heal events
                healEvents.Clear();
            }
        }
    }
}


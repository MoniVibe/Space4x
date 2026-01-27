using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Calculates hit chance and creates damage events for attacks.
    /// Uses deterministic RNG for hit/miss rolls.
    /// Runs before DamageApplicationSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct HitDetectionSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<CombatStats> _combatStatsLookup;
        private ComponentLookup<Weapon> _weaponLookup;
        private ComponentLookup<BuffStatCache> _buffLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _entityLookup = state.GetEntityStorageInfoLookup();
            _combatStatsLookup = state.GetComponentLookup<CombatStats>(true);
            _weaponLookup = state.GetComponentLookup<Weapon>(true);
            _buffLookup = state.GetComponentLookup<BuffStatCache>(true);
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

            _entityLookup.Update(ref state);
            _combatStatsLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _buffLookup.Update(ref state);

            var jobHandle = new ProcessAttacksJob
            {
                CurrentTick = currentTick,
                EntityLookup = _entityLookup,
                CombatStatsLookup = _combatStatsLookup,
                WeaponLookup = _weaponLookup,
                BuffLookup = _buffLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = jobHandle;
        }

        [BurstCompile]
        public partial struct ProcessAttacksJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public EntityStorageInfoLookup EntityLookup;
            [ReadOnly] public ComponentLookup<CombatStats> CombatStatsLookup;
            [ReadOnly] public ComponentLookup<Weapon> WeaponLookup;
            [ReadOnly] public ComponentLookup<BuffStatCache> BuffLookup;

            void Execute(
                Entity attackerEntity,
                [EntityIndexInQuery] int attackerIndex,
                in CombatStats attackerStats,
                in ActiveCombat combat,
                ref DynamicBuffer<DamageEvent> damageEvents)
            {
                // Determine target (combatant1 or combatant2)
                Entity targetEntity = Entity.Null;
                if (combat.Combatant1 == attackerEntity)
                {
                    targetEntity = combat.Combatant2;
                }
                else if (combat.Combatant2 == attackerEntity)
                {
                    targetEntity = combat.Combatant1;
                }

                if (targetEntity == Entity.Null || !EntityLookup.Exists(targetEntity))
                {
                    return; // Invalid target
                }

                // Get defender stats
                if (!CombatStatsLookup.HasComponent(targetEntity))
                {
                    return; // Target has no combat stats
                }

                var defenderStats = CombatStatsLookup[targetEntity];

                // Calculate hit chance
                float hitChance = CalculateHitChance(attackerEntity, attackerStats, targetEntity, defenderStats);

                // Roll for hit (deterministic RNG)
                float roll = DeterministicRandom(attackerEntity.Index, targetEntity.Index, CurrentTick);
                bool isHit = roll < hitChance;

                if (!isHit)
                {
                    return; // Miss - no damage event
                }

                // Calculate damage
                float rawDamage = attackerStats.AttackDamage;

                // Apply weapon damage if equipped
                if (attackerStats.EquippedWeapon != Entity.Null && EntityLookup.Exists(attackerStats.EquippedWeapon))
                {
                    if (WeaponLookup.HasComponent(attackerStats.EquippedWeapon))
                    {
                        var weapon = WeaponLookup[attackerStats.EquippedWeapon];
                        rawDamage += weapon.BaseDamage;
                    }
                }

                // Check for critical hit
                float critRoll = DeterministicRandom(attackerEntity.Index + 1000, targetEntity.Index, CurrentTick);
                float critChance = attackerStats.CriticalChance / 100f;
                bool isCritical = critRoll < critChance;

                // Create damage event
                var damageEvent = new DamageEvent
                {
                    SourceEntity = attackerEntity,
                    TargetEntity = targetEntity,
                    RawDamage = rawDamage,
                    Type = DamageType.Physical,
                    Tick = CurrentTick,
                    Flags = DamageFlags.None
                };

                if (isCritical)
                {
                    damageEvent.Flags |= DamageFlags.Critical;
                }

                damageEvents.Add(damageEvent);
            }

            [BurstCompile]
            private float CalculateHitChance(
                Entity attackerEntity,
                CombatStats attackerStats,
                Entity defenderEntity,
                CombatStats defenderStats)
            {
                // Base hit chance: (AttackerAccuracy - DefenderDefense) / 100
                float baseHitChance = (attackerStats.Accuracy - defenderStats.Defense) / 100f;

                // Apply buff modifiers if BuffStatCache exists
                if (BuffLookup.HasComponent(attackerEntity))
                {
                    var buffCache = BuffLookup[attackerEntity];
                    baseHitChance += buffCache.AccuracyFlat / 100f;
                    baseHitChance *= (1f + buffCache.AccuracyPercent);
                }

                // Clamp to 5-95% range (always some chance to hit/miss)
                return math.clamp(baseHitChance, 0.05f, 0.95f);
            }

            [BurstCompile]
            private static float DeterministicRandom(int seed1, int seed2, uint tick)
            {
                // Simple deterministic RNG using seeds and tick
                uint hash = (uint)(seed1 * 73856093) ^ (uint)(seed2 * 19349663) ^ tick;
                hash = hash * 1103515245 + 12345;
                return (hash & 0x7FFFFFFF) / (float)0x7FFFFFFF; // 0-1 range
            }
        }
    }
}


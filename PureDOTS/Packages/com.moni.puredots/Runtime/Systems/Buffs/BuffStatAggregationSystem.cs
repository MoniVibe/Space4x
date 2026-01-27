using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Buffs
{
    /// <summary>
    /// Aggregates stat modifiers from all active buffs into BuffStatCache.
    /// Runs after BuffTickSystem to update cache when buffs change.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(BuffTickSystem))]
    public partial struct BuffStatAggregationSystem : ISystem
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
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get buff catalog
            if (!SystemAPI.TryGetSingleton<BuffCatalogRef>(out var catalogRef) ||
                !catalogRef.Blob.IsCreated)
            {
                return;
            }

            ref var catalog = ref catalogRef.Blob.Value;

            // Process all entities with active buffs
            new AggregateBuffStatsJob
            {
                Catalog = catalogRef.Blob,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct AggregateBuffStatsJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<BuffDefinitionBlob> Catalog;

            public uint CurrentTick;

            void Execute(
                Entity entity,
                [ReadOnly] in DynamicBuffer<ActiveBuff> activeBuffs,
                ref BuffStatCache cache)
            {
                ref var catalog = ref Catalog.Value;

                // Reset cache
                cache = new BuffStatCache { LastUpdateTick = CurrentTick };

                // Aggregate modifiers from all active buffs
                for (int i = 0; i < activeBuffs.Length; i++)
                {
                    var buff = activeBuffs[i];

                    // Find buff definition
                    int buffIndex = -1;
                    for (int j = 0; j < catalog.Buffs.Length; j++)
                    {
                        if (catalog.Buffs[j].BuffId.Equals(buff.BuffId))
                        {
                            buffIndex = j;
                            break;
                        }
                    }

                    if (buffIndex < 0)
                        continue;

                    ref var buffDef = ref catalog.Buffs[buffIndex];
                    float stackMultiplier = GetStackMultiplier(buffDef.Stacking, buff.CurrentStacks);

                    // Apply stat modifiers
                    for (int m = 0; m < buffDef.StatModifiers.Length; m++)
                    {
                        var modifier = buffDef.StatModifiers[m];
                        float value = modifier.Value * stackMultiplier;

                        ApplyStatModifier(ref cache, modifier.Stat, modifier.Type, value);
                    }
                }
            }

            private static float GetStackMultiplier(StackBehavior stacking, byte stacks)
            {
                switch (stacking)
                {
                    case StackBehavior.Additive:
                        return stacks; // Linear scaling
                    case StackBehavior.Multiplicative:
                        return math.pow(1.1f, stacks - 1); // 1.1x per stack (example)
                    case StackBehavior.Refresh:
                    case StackBehavior.Replace:
                    default:
                        return 1f; // No stacking multiplier
                }
            }

            private static void ApplyStatModifier(ref BuffStatCache cache, StatTarget stat, ModifierType type, float value)
            {
                switch (stat)
                {
                    case StatTarget.Damage:
                        if (type == ModifierType.Flat)
                            cache.DamageFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.DamagePercent += value;
                        break;

                    case StatTarget.AttackSpeed:
                        if (type == ModifierType.Flat)
                            cache.AttackSpeedFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.AttackSpeedPercent += value;
                        break;

                    case StatTarget.Armor:
                        if (type == ModifierType.Flat)
                            cache.ArmorFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.ArmorPercent += value;
                        break;

                    case StatTarget.Health:
                        if (type == ModifierType.Flat)
                            cache.HealthFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.HealthPercent += value;
                        break;

                    case StatTarget.MaxHealth:
                        if (type == ModifierType.Flat)
                            cache.MaxHealthFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.MaxHealthPercent += value;
                        break;

                    case StatTarget.HealthRegen:
                        if (type == ModifierType.Flat)
                            cache.HealthRegenFlat += value;
                        break;

                    case StatTarget.Mana:
                        if (type == ModifierType.Flat)
                            cache.ManaFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.ManaPercent += value;
                        break;

                    case StatTarget.MaxMana:
                        if (type == ModifierType.Flat)
                            cache.MaxManaFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.MaxManaPercent += value;
                        break;

                    case StatTarget.ManaRegen:
                        if (type == ModifierType.Flat)
                            cache.ManaRegenFlat += value;
                        break;

                    case StatTarget.Stamina:
                        if (type == ModifierType.Flat)
                            cache.StaminaFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.StaminaPercent += value;
                        break;

                    case StatTarget.MaxStamina:
                        if (type == ModifierType.Flat)
                            cache.MaxStaminaFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.MaxStaminaPercent += value;
                        break;

                    case StatTarget.StaminaRegen:
                        if (type == ModifierType.Flat)
                            cache.StaminaRegenFlat += value;
                        break;

                    case StatTarget.Speed:
                        if (type == ModifierType.Flat)
                            cache.SpeedFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.SpeedPercent += value;
                        break;

                    case StatTarget.JumpHeight:
                        if (type == ModifierType.Flat)
                            cache.JumpHeightFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.JumpHeightPercent += value;
                        break;

                    case StatTarget.SkillGainRate:
                        if (type == ModifierType.Flat)
                            cache.SkillGainRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.SkillGainRatePercent += value;
                        break;

                    case StatTarget.XPGainRate:
                        if (type == ModifierType.Flat)
                            cache.XPGainRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.XPGainRatePercent += value;
                        break;

                    // Space4X specific
                    case StatTarget.PowerGeneration:
                        if (type == ModifierType.Flat)
                            cache.PowerGenerationFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.PowerGenerationPercent += value;
                        break;

                    case StatTarget.PowerDraw:
                        if (type == ModifierType.Flat)
                            cache.PowerDrawFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.PowerDrawPercent += value;
                        break;

                    case StatTarget.MiningRate:
                        if (type == ModifierType.Flat)
                            cache.MiningRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.MiningRatePercent += value;
                        break;

                    case StatTarget.RepairRate:
                        if (type == ModifierType.Flat)
                            cache.RepairRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.RepairRatePercent += value;
                        break;

                    case StatTarget.FireRate:
                        if (type == ModifierType.Flat)
                            cache.FireRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.FireRatePercent += value;
                        break;

                    case StatTarget.Accuracy:
                        if (type == ModifierType.Flat)
                            cache.AccuracyFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.AccuracyPercent += value;
                        break;

                    // Godgame specific
                    case StatTarget.Mood:
                        if (type == ModifierType.Flat)
                            cache.MoodFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.MoodPercent += value;
                        break;

                    case StatTarget.Faith:
                        if (type == ModifierType.Flat)
                            cache.FaithFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.FaithPercent += value;
                        break;

                    case StatTarget.WorshipRate:
                        if (type == ModifierType.Flat)
                            cache.WorshipRateFlat += value;
                        else if (type == ModifierType.Percent)
                            cache.WorshipRatePercent += value;
                        break;
                }
            }
        }
    }
}


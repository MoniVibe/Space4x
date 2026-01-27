using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Converts AggregateStats into AmbientGroupConditions using data-driven aggregation rules.
    /// Runs at configurable frequency (e.g., daily/weekly sim time).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateStatsRecalculationSystem))]
    public partial struct AmbientConditionsUpdateSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Get config state (optional - use defaults if missing)
            if (!SystemAPI.TryGetSingleton<AggregateConfigState>(out var configState))
            {
                return; // No config, skip ambient updates
            }

            var updateFrequency = configState.AmbientUpdateFrequency;
            if (updateFrequency == 0)
            {
                updateFrequency = 100u; // Default: update every 100 ticks
            }

            // Get catalog
            if (!configState.Catalog.IsCreated)
            {
                return; // No catalog, skip
            }

            ref var catalog = ref configState.Catalog.Value;

            // Process groups with AggregateStats
            foreach (var (identity, stats, ambient, entity) in SystemAPI.Query<
                RefRO<AggregateIdentity>,
                RefRO<AggregateStats>,
                RefRW<AmbientGroupConditions>>().WithEntityAccess())
            {
                var ambientValue = ambient.ValueRO;

                // Check update frequency
                if (ambientValue.LastUpdateTick > 0 && 
                    (currentTick - ambientValue.LastUpdateTick) < updateFrequency)
                {
                    continue; // Not time to update yet
                }

                // Find config for this aggregate type
                int configIndex = -1;
                for (int i = 0; i < catalog.TypeConfigs.Length; i++)
                {
                    if (catalog.TypeConfigs[i].TypeId == identity.ValueRO.TypeId)
                    {
                        configIndex = i;
                        break;
                    }
                }

                if (configIndex < 0)
                {
                    continue; // No config for this type
                }

                ref var config = ref catalog.TypeConfigs[configIndex];
                var statsValue = stats.ValueRO;

                // Initialize ambient metrics to zero
                float courage = 0f;
                float caution = 0f;
                float anger = 0f;
                float compassion = 0f;
                float drive = 0f;
                float loyalty = 0f;
                float conformity = 0f;
                float tolerance = 0f;

                // Apply aggregation rules
                for (int i = 0; i < config.Rules.Length; i++)
                {
                    var rule = config.Rules[i];
                    float sourceValue = GetSourceTraitValue(in statsValue, (AggregateSourceTrait)rule.SourceTrait);
                    float contribution = sourceValue * rule.Weight;

                    // Accumulate into target metric
                    switch ((AggregateTargetMetric)rule.TargetMetric)
                    {
                        case AggregateTargetMetric.AmbientCourage:
                            courage += contribution;
                            break;
                        case AggregateTargetMetric.AmbientCaution:
                            caution += contribution;
                            break;
                        case AggregateTargetMetric.AmbientAnger:
                            anger += contribution;
                            break;
                        case AggregateTargetMetric.AmbientCompassion:
                            compassion += contribution;
                            break;
                        case AggregateTargetMetric.AmbientDrive:
                            drive += contribution;
                            break;
                        case AggregateTargetMetric.ExpectationLoyalty:
                            loyalty += contribution;
                            break;
                        case AggregateTargetMetric.ExpectationConformity:
                            conformity += contribution;
                            break;
                        case AggregateTargetMetric.ToleranceForOutliers:
                            tolerance += contribution;
                            break;
                    }
                }

                // Normalize and clamp to 0-1 range
                ambientValue.AmbientCourage = math.clamp(courage, 0f, 1f);
                ambientValue.AmbientCaution = math.clamp(caution, 0f, 1f);
                ambientValue.AmbientAnger = math.clamp(anger, 0f, 1f);
                ambientValue.AmbientCompassion = math.clamp(compassion, 0f, 1f);
                ambientValue.AmbientDrive = math.clamp(drive, 0f, 1f);
                ambientValue.ExpectationLoyalty = math.clamp(loyalty, 0f, 1f);
                ambientValue.ExpectationConformity = math.clamp(conformity, 0f, 1f);
                ambientValue.ToleranceForOutliers = math.clamp(tolerance, 0f, 1f);
                ambientValue.LastUpdateTick = currentTick;

                ambient.ValueRW = ambientValue;
            }
        }

        [BurstCompile]
        private static float GetSourceTraitValue(in AggregateStats stats, AggregateSourceTrait trait)
        {
            switch (trait)
            {
                case AggregateSourceTrait.Initiative:
                    return stats.AvgInitiative / 100f; // Normalize 0-100 to 0-1
                case AggregateSourceTrait.VengefulForgiving:
                    return (stats.AvgVengefulForgiving + 100f) / 200f; // Normalize -100..+100 to 0-1
                case AggregateSourceTrait.BoldCraven:
                    return (stats.AvgBoldCraven + 100f) / 200f; // Normalize -100..+100 to 0-1
                case AggregateSourceTrait.CorruptPure:
                    return (stats.AvgCorruptPure + 100f) / 200f;
                case AggregateSourceTrait.ChaoticLawful:
                    return (stats.AvgChaoticLawful + 100f) / 200f;
                case AggregateSourceTrait.EvilGood:
                    return (stats.AvgEvilGood + 100f) / 200f;
                case AggregateSourceTrait.MightMagic:
                    return (stats.AvgMightMagic + 100f) / 200f;
                case AggregateSourceTrait.Ambition:
                    return stats.AvgAmbition / 100f; // Normalize 0-100 to 0-1
                case AggregateSourceTrait.DesireStatus:
                    return stats.StatusCoverage; // Already 0-1
                case AggregateSourceTrait.DesireWealth:
                    return stats.WealthCoverage;
                case AggregateSourceTrait.DesirePower:
                    return stats.PowerCoverage;
                case AggregateSourceTrait.DesireKnowledge:
                    return stats.KnowledgeCoverage;
                default:
                    return 0f;
            }
        }
    }
}


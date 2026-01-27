using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Telemetry
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BehaviorTelemetryAggregateSystem : ISystem
    {
        private ComponentLookup<BehaviorTelemetryConfig> _configLookup;
        private BufferLookup<BehaviorTelemetryRecord> _recordLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _configLookup = state.GetComponentLookup<BehaviorTelemetryConfig>(true);
            _recordLookup = state.GetBufferLookup<BehaviorTelemetryRecord>();

            state.RequireForUpdate<BehaviorTelemetryConfig>();
            state.RequireForUpdate<BehaviorTelemetryState>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _configLookup.Update(ref state);
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var configEntity = SystemAPI.GetSingletonEntity<BehaviorTelemetryConfig>();
            var config = _configLookup[configEntity];
            var cadence = math.max(1, config.AggregateCadenceTicks);
            if (timeState.Tick % (uint)cadence != 0)
            {
                return;
            }

            _recordLookup.Update(ref state);
            var telemetryEntity = SystemAPI.GetSingletonEntity<BehaviorTelemetryState>();
            var buffer = _recordLookup[telemetryEntity];

            AggregateHazard(ref state, buffer, timeState.Tick);
            AggregateGather(ref state, buffer, timeState.Tick);
            AggregateVillagerCore(ref state, buffer, timeState.Tick);
            AggregateFleetCore(ref state, buffer, timeState.Tick);
            AggregateModuleIntegrity(ref state, buffer, timeState.Tick);
            AggregateShieldEnvelope(ref state, buffer, timeState.Tick);
            AggregateLimbVitality(ref state, buffer, timeState.Tick);
            AggregateProjectileDamage(ref state, buffer, timeState.Tick);
            AggregateMorale(ref state, buffer, timeState.Tick);
        }

        private void AggregateHazard(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<HazardDodgeTelemetry>>())
            {
                var value = telemetry.ValueRW;
                if (value.RaycastHitsInterval > 0)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.HazardDodge,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.HazardRaycastHits,
                        ValueA = value.RaycastHitsInterval,
                        Passed = 1
                    });
                }

                if (value.AvoidanceTransitionsInterval > 0)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.HazardDodge,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.HazardAvoidanceTransitions,
                        ValueA = value.AvoidanceTransitionsInterval,
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.HazardDodge,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.HazardDodgeDistanceMm,
                    ValueA = value.DodgeDistanceMmInterval,
                    Passed = 1
                });

                telemetry.ValueRW = new HazardDodgeTelemetry
                {
                    WasAvoidingLastTick = value.WasAvoidingLastTick
                };
            }
        }

        private void AggregateGather(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<GatherDeliverTelemetry>>())
            {
                var value = telemetry.ValueRW;
                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.GatherDeliver,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.GatherMinedMilli,
                    ValueA = value.MinedAmountMilliInterval,
                    Passed = 1
                });

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.GatherDeliver,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.GatherDepositedMilli,
                    ValueA = value.DepositedAmountMilliInterval,
                    Passed = 1
                });

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.GatherDeliver,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.GatherCarrierCargoMilli,
                    ValueA = value.CarrierCargoMilliSnapshot,
                    Passed = 1
                });

                telemetry.ValueRW.MinedAmountMilliInterval = 0;
                telemetry.ValueRW.DepositedAmountMilliInterval = 0;
                telemetry.ValueRW.StuckTicksInterval = 0;
            }
        }

        private void AggregateVillagerCore(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<VillagerCoreTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.NeedSampleCount > 0)
                {
                    var avgNeed = value.NeedAccumulator / math.max(1u, value.NeedSampleCount);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.VillagerCore,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.VillagerNeedMaxMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgNeed),
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.VillagerCore,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.VillagerFocusCurrentMilli,
                    ValueA = BehaviorTelemetryMath.ToMilli(value.FocusSnapshot),
                    Passed = 1
                });

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.VillagerCore,
                    Kind = BehaviorTelemetryRecordKind.Metric,
                    MetricOrInvariantId = (ushort)BehaviorMetricId.VillagerFleeEvents,
                    ValueA = value.FleeEvents,
                    Passed = 1
                });

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.VillagerCore,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.VillagerFocusNonNegative,
                    ValueA = value.FocusNegativeDetected,
                    Passed = (byte)(value.FocusNegativeDetected == 0 ? 1 : 0)
                });

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.VillagerCore,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.VillagerNeedClamped,
                    ValueA = value.NeedExceededDetected,
                    Passed = (byte)(value.NeedExceededDetected == 0 ? 1 : 0)
                });

                telemetry.ValueRW = new VillagerCoreTelemetry
                {
                    FocusSnapshot = value.FocusSnapshot,
                    WasFleeingLastTick = value.WasFleeingLastTick
                };
            }
        }

        private void AggregateFleetCore(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<FleetCoreTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.CohesionSamples > 0)
                {
                    var avgCohesion = value.CohesionAccumulator / math.max(1u, value.CohesionSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.FleetCore,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.FleetCohesionMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgCohesion),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.FleetCore,
                        Kind = BehaviorTelemetryRecordKind.Invariant,
                        MetricOrInvariantId = (ushort)BehaviorInvariantId.FleetCohesionBounds,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgCohesion),
                        Passed = (byte)(avgCohesion >= 0f && avgCohesion <= 1.2f ? 1 : 0)
                    });
                }

                if (value.MoraleSamples > 0)
                {
                    var avgMorale = value.MoraleAccumulator / math.max(1u, value.MoraleSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.FleetCore,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.FleetMoraleMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgMorale),
                        Passed = 1
                    });
                }

                if (value.StrikeCraftSamples > 0)
                {
                    var avgLoad = value.StrikeCraftLoadAccumulator / math.max(1u, value.StrikeCraftSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.FleetCore,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.FleetStrikeCraftLoadMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgLoad),
                        Passed = 1
                    });
                }

                telemetry.ValueRW = default;
            }
        }

        private void AggregateModuleIntegrity(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<ModuleHealthTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.HullSamples > 0)
                {
                    var avgHull = value.HullAccumulator / math.max(1u, value.HullSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ModuleIntegrity,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ModuleHullMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgHull),
                        Passed = 1
                    });
                }

                if (value.ArmorSamples > 0)
                {
                    var avgArmor = value.ArmorAccumulator / math.max(1u, value.ArmorSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ModuleIntegrity,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ModuleArmorMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgArmor),
                        Passed = 1
                    });
                }

                if (value.EfficiencySamples > 0)
                {
                    var avgEff = value.EfficiencyAccumulator / math.max(1u, value.EfficiencySamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ModuleIntegrity,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ModuleEfficiencyMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avgEff),
                        Passed = 1
                    });
                }

                if (value.DamageEventsInterval > 0)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ModuleIntegrity,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ModuleDamageEvents,
                        ValueA = value.DamageEventsInterval,
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.ModuleIntegrity,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.ModuleHealthNonNegative,
                    ValueA = value.HullBelowZero | value.ArmorBelowZero,
                    Passed = (byte)((value.HullBelowZero | value.ArmorBelowZero) == 0 ? 1 : 0)
                });

                telemetry.ValueRW = default;
            }
        }

        private void AggregateShieldEnvelope(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<ShieldEnvelopeTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.Samples > 0)
                {
                    var denom = math.max(1u, value.Samples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ShieldEnvelope,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ShieldFrontLoadMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.FrontLoadAccumulator / denom),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ShieldEnvelope,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ShieldRearLoadMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.RearLoadAccumulator / denom),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ShieldEnvelope,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ShieldBubbleStrengthMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.BubbleStrengthAccumulator / denom),
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.ShieldEnvelope,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.ShieldLoadWithinBounds,
                    ValueA = value.OverCapacityDetected,
                    Passed = (byte)(value.OverCapacityDetected == 0 ? 1 : 0)
                });

                telemetry.ValueRW = default;
            }
        }

        private void AggregateLimbVitality(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<LimbVitalityTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.Samples > 0)
                {
                    var denom = math.max(1u, value.Samples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.LimbVitality,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.LimbHealthMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.HealthAccumulator / denom),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.LimbVitality,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.LimbStaminaMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.StaminaAccumulator / denom),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.LimbVitality,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.LimbManaMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.ManaAccumulator / denom),
                        Passed = 1
                    });
                }

                var negativeFlags = value.HealthNegativeDetected | value.StaminaNegativeDetected | value.ManaNegativeDetected;
                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.LimbVitality,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.LimbVitalityNonNegative,
                    ValueA = negativeFlags,
                    Passed = (byte)(negativeFlags == 0 ? 1 : 0)
                });

                telemetry.ValueRW = default;
            }
        }

        private void AggregateProjectileDamage(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<ProjectileDamageTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.ShotsFiredInterval > 0)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ProjectileDamage,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ProjectileShotsFired,
                        ValueA = value.ShotsFiredInterval,
                        Passed = 1
                    });
                }

                if (value.ShotsHitInterval > 0)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ProjectileDamage,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ProjectileShotsHit,
                        ValueA = value.ShotsHitInterval,
                        Passed = 1
                    });
                }

                if (math.abs(value.DamageMilliAccumulator) > 0f)
                {
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.ProjectileDamage,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.ProjectileDamageMilli,
                        ValueA = (long)math.round(value.DamageMilliAccumulator),
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.ProjectileDamage,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.ProjectileDamageNonNegative,
                    ValueA = value.NegativeDamageDetected,
                    Passed = (byte)(value.NegativeDamageDetected == 0 ? 1 : 0)
                });

                telemetry.ValueRW = default;
            }
        }

        private void AggregateMorale(ref SystemState state, DynamicBuffer<BehaviorTelemetryRecord> buffer, uint tick)
        {
            foreach (var telemetry in SystemAPI.Query<RefRW<MoraleTelemetry>>())
            {
                var value = telemetry.ValueRO;
                if (value.MoraleSamples > 0)
                {
                    var avg = value.MoraleAccumulator / math.max(1u, value.MoraleSamples);
                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.MoraleState,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.MoraleCurrentMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(avg),
                        Passed = 1
                    });

                    buffer.Add(new BehaviorTelemetryRecord
                    {
                        Tick = tick,
                        Behavior = BehaviorId.MoraleState,
                        Kind = BehaviorTelemetryRecordKind.Metric,
                        MetricOrInvariantId = (ushort)BehaviorMetricId.MoraleDeltaMilli,
                        ValueA = BehaviorTelemetryMath.ToMilli(value.DeltaAccumulator),
                        Passed = 1
                    });
                }

                buffer.Add(new BehaviorTelemetryRecord
                {
                    Tick = tick,
                    Behavior = BehaviorId.MoraleState,
                    Kind = BehaviorTelemetryRecordKind.Invariant,
                    MetricOrInvariantId = (ushort)BehaviorInvariantId.MoraleWithinBounds,
                    ValueA = value.OutOfBoundsDetected,
                    Passed = (byte)(value.OutOfBoundsDetected == 0 ? 1 : 0)
                });

                telemetry.ValueRW = default;
            }
        }
    }
}

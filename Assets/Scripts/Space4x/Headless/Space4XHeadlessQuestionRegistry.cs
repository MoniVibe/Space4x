using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Physics;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Headless
{
    internal enum Space4XQuestionStatus : byte
    {
        Pass = 0,
        Fail = 1,
        Unknown = 2
    }

    internal sealed class Space4XQuestionAnswer
    {
        public string Id;
        public Space4XQuestionStatus Status;
        public bool Required;
        public string Answer;
        public string UnknownReason;
        public uint StartTick;
        public uint EndTick;
        public Dictionary<string, float> Metrics;
        public List<Space4XQuestionEvidence> Evidence;
    }

    internal sealed class Space4XQuestionEvidence
    {
        public string BlackCatId;
        public Entity Primary;
        public Entity Secondary;
    }

    internal sealed class Space4XOperatorSignals
    {
        private readonly Dictionary<string, float> _metrics;
        private readonly List<Space4XOperatorBlackCat> _blackCats;

        public Space4XOperatorSignals(Dictionary<string, float> metrics, List<Space4XOperatorBlackCat> blackCats)
        {
            _metrics = metrics ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _blackCats = blackCats ?? new List<Space4XOperatorBlackCat>();
        }

        public bool TryGetMetric(string key, out float value)
        {
            if (_metrics == null)
            {
                value = 0f;
                return false;
            }

            return _metrics.TryGetValue(key, out value);
        }

        public float GetMetricOrDefault(string key, float fallback = 0f)
        {
            return TryGetMetric(key, out var value) ? value : fallback;
        }

        public bool HasBlackCat(string id)
        {
            return CountBlackCats(id) > 0;
        }

        public int CountBlackCats(string id)
        {
            if (_blackCats == null || _blackCats.Count == 0 || string.IsNullOrWhiteSpace(id))
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < _blackCats.Count; i++)
            {
                if (string.Equals(_blackCats[i].Id.ToString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public List<Space4XQuestionEvidence> CollectEvidence(string questionId)
        {
            var evidence = new List<Space4XQuestionEvidence>();
            if (_blackCats == null || _blackCats.Count == 0)
            {
                return evidence;
            }

            for (var i = 0; i < _blackCats.Count; i++)
            {
                var cat = _blackCats[i];
                var mapped = Space4XHeadlessQuestionIds.ResolveQuestionIdForBlackCatId(cat.Id.ToString());
                if (!string.Equals(mapped, questionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                evidence.Add(new Space4XQuestionEvidence
                {
                    BlackCatId = cat.Id.ToString(),
                    Primary = cat.Primary,
                    Secondary = cat.Secondary
                });
            }

            return evidence;
        }
    }

    internal struct Space4XOperatorRuntimeStats
    {
        public int MiningEntityCount;
        public int MiningYieldNonZeroCount;
        public float MiningYieldTotal;
        public int CollisionEventCount;
        public int CollisionProbeCount;
        public byte HasSteeringBeatConfig;
        public byte HasSensorsBeatConfig;
        public uint SteeringStartTick;
        public uint SteeringEndTick;

        public static Space4XOperatorRuntimeStats Collect(EntityManager entityManager)
        {
            var stats = new Space4XOperatorRuntimeStats();
            stats.MiningEntityCount = CountEntities(entityManager, ComponentType.ReadOnly<MiningState>());
            stats.CollisionProbeCount = CountEntities(entityManager, ComponentType.ReadOnly<Space4XCollisionProbeState>());
            stats.CollisionEventCount = CountCollisionEvents(entityManager);
            (stats.MiningYieldNonZeroCount, stats.MiningYieldTotal) = MeasureMiningYield(entityManager);
            (stats.HasSteeringBeatConfig, stats.SteeringStartTick, stats.SteeringEndTick) = ResolveSteeringWindow(entityManager);
            stats.HasSensorsBeatConfig = ResolveBeatPresence<Space4XSensorsBeatConfig>(entityManager);
            return stats;
        }

        private static int CountEntities(EntityManager entityManager, ComponentType type)
        {
            using var query = entityManager.CreateEntityQuery(type);
            return query.CalculateEntityCount();
        }

        private static int CountCollisionEvents(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsCollisionEventElement>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var count = 0;
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            var typeHandle = entityManager.GetBufferTypeHandle<PhysicsCollisionEventElement>(true);
            for (var i = 0; i < chunks.Length; i++)
            {
                var bufferAccessor = chunks[i].GetBufferAccessor(typeHandle);
                for (var j = 0; j < bufferAccessor.Length; j++)
                {
                    count += bufferAccessor[j].Length;
                }
            }

            return count;
        }

        private static (int nonZeroCount, float total) MeasureMiningYield(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiningYield>());
            if (query.IsEmptyIgnoreFilter)
            {
                return (0, 0f);
            }

            using var yields = query.ToComponentDataArray<MiningYield>(Allocator.Temp);
            var nonZero = 0;
            var total = 0f;
            for (var i = 0; i < yields.Length; i++)
            {
                var amount = yields[i].PendingAmount;
                total += amount;
                if (amount > 0f)
                {
                    nonZero++;
                }
            }

            return (nonZero, total);
        }

        private static (byte hasConfig, uint startTick, uint endTick) ResolveSteeringWindow(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XSteeringStabilityBeatConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                return (0, 0u, 0u);
            }

            var config = query.GetSingleton<Space4XSteeringStabilityBeatConfig>();
            var startTick = config.StartTick + config.SettleTicks;
            var endTick = startTick + config.MeasureTicks;
            return (1, startTick, endTick);
        }

        private static byte ResolveBeatPresence<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.IsEmptyIgnoreFilter ? (byte)0 : (byte)1;
        }
    }

    internal interface IHeadlessQuestion
    {
        string Id { get; }
        Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime);
    }

    internal static class Space4XHeadlessQuestionIds
    {
        public const string SteeringStable = "space4x.q.steering.stable_when_target_stable";
        public const string UndockRisk = "space4x.q.undock.risk_separation";
        public const string MiningProgress = "space4x.q.mining.progress";
        public const string CollisionResponse = "space4x.q.collision.response_present";
        public const string SensorsAcquireDrop = "space4x.q.sensors.acquire_drop";
        public const string Unknown = "space4x.q.unknown";

        public static string ResolveQuestionIdForBlackCatId(string blackCatId)
        {
            return blackCatId switch
            {
                "HEADING_OSCILLATION" => SteeringStable,
                "STEERING_BEAT_SKIPPED" => SteeringStable,
                "STEERING_BEAT_LOW_SPEED" => SteeringStable,
                "STEERING_WEAK_SIGNAL" => SteeringStable,
                "UNDOCK_BEAT_SKIPPED" => UndockRisk,
                "MINING_STALL" => MiningProgress,
                "COLLISION_PHASING" => CollisionResponse,
                "SENSORS_BEAT_SKIPPED" => SensorsAcquireDrop,
                "PERCEPTION_STALE" => SensorsAcquireDrop,
                "CONTACT_GHOST" => SensorsAcquireDrop,
                "CONTACT_THRASH" => SensorsAcquireDrop,
                _ => Unknown
            };
        }
    }

    internal static class Space4XHeadlessQuestionRegistry
    {
        private static readonly IHeadlessQuestion[] Questions =
        {
            new SteeringStableQuestion(),
            new UndockRiskQuestion(),
            new MiningProgressQuestion(),
            new CollisionResponseQuestion(),
            new SensorsAcquireDropQuestion()
        };

        private static readonly Dictionary<string, IHeadlessQuestion> QuestionMap;

        static Space4XHeadlessQuestionRegistry()
        {
            QuestionMap = new Dictionary<string, IHeadlessQuestion>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Questions.Length; i++)
            {
                var question = Questions[i];
                if (question == null || string.IsNullOrWhiteSpace(question.Id))
                {
                    continue;
                }

                QuestionMap[question.Id] = question;
            }
        }

        public static List<Space4XQuestionAnswer> BuildQuestions(
            Space4XOperatorSignals signals,
            Space4XOperatorRuntimeStats stats,
            in Space4XScenarioRuntime runtime,
            Dictionary<string, bool> questionPack)
        {
            var answers = new List<Space4XQuestionAnswer>();
            if (questionPack == null || questionPack.Count == 0)
            {
                answers.Capacity = Questions.Length;
                for (var i = 0; i < Questions.Length; i++)
                {
                    var question = Questions[i];
                    if (question == null)
                    {
                        continue;
                    }

                    var answer = question.Evaluate(signals, stats, runtime);
                    if (answer == null)
                    {
                        continue;
                    }

                    answer.Required = true;
                    if (answer.Status != Space4XQuestionStatus.Pass)
                    {
                        answer.Evidence = signals.CollectEvidence(answer.Id);
                    }
                    answers.Add(answer);
                }
            }
            else
            {
                var orderedIds = new List<string>(questionPack.Keys);
                orderedIds.Sort(StringComparer.OrdinalIgnoreCase);
                answers.Capacity = orderedIds.Count;
                for (var i = 0; i < orderedIds.Count; i++)
                {
                    var id = orderedIds[i];
                    var required = questionPack[id];
                    if (QuestionMap.TryGetValue(id, out var question))
                    {
                        var answer = question.Evaluate(signals, stats, runtime);
                        if (answer == null)
                        {
                            continue;
                        }

                        answer.Required = required;
                        if (answer.Status != Space4XQuestionStatus.Pass)
                        {
                            answer.Evidence = signals.CollectEvidence(answer.Id);
                        }
                        answers.Add(answer);
                    }
                    else
                    {
                        answers.Add(new Space4XQuestionAnswer
                        {
                            Id = id,
                            Required = required,
                            Status = Space4XQuestionStatus.Unknown,
                            UnknownReason = "unregistered_question",
                            Answer = "question not registered",
                            StartTick = runtime.StartTick,
                            EndTick = runtime.EndTick,
                            Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            answers.Sort((left, right) => string.CompareOrdinal(left?.Id, right?.Id));
            return answers;
        }

        private sealed class SteeringStableQuestion : IHeadlessQuestion
        {
            private const float HeadingErrorMax = 2f;
            private const float YawRateMax = 1f;
            private const float FlipsPer10sMax = 1f;

            public string Id => Space4XHeadlessQuestionIds.SteeringStable;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = stats.HasSteeringBeatConfig != 0 ? stats.SteeringStartTick : runtime.StartTick,
                    EndTick = stats.HasSteeringBeatConfig != 0 ? stats.SteeringEndTick : runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                var hasSamples = signals.TryGetMetric("space4x.steer.sample_count", out var sampleCount);
                var eligibleSamples = signals.GetMetricOrDefault("space4x.steer.eligible_samples");
                var speedGateCoverage = signals.GetMetricOrDefault("space4x.steer.speed_gate_coverage");
                var headingError = signals.GetMetricOrDefault("space4x.steer.heading_error_max_deg");
                var yawRate = signals.GetMetricOrDefault("space4x.steer.yaw_rate_max_deg_s");
                var flips = signals.GetMetricOrDefault("space4x.steer.sign_flips_per_10s");
                var retargets = signals.GetMetricOrDefault("space4x.steer.retarget_count");

                answer.Metrics["sample_count"] = sampleCount;
                answer.Metrics["eligible_samples"] = eligibleSamples;
                answer.Metrics["speed_gate_coverage"] = speedGateCoverage;
                answer.Metrics["heading_error_max_deg"] = headingError;
                answer.Metrics["yaw_rate_max_deg_s"] = yawRate;
                answer.Metrics["sign_flips_per_10s"] = flips;
                answer.Metrics["retarget_count"] = retargets;

                if (stats.HasSteeringBeatConfig == 0 || !hasSamples || sampleCount <= 0f)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = stats.HasSteeringBeatConfig == 0 ? "beat_absent" : "no_samples";
                    answer.Answer = "no steering samples";
                    return answer;
                }

                if (signals.HasBlackCat("STEERING_BEAT_SKIPPED") ||
                    signals.HasBlackCat("STEERING_BEAT_LOW_SPEED") ||
                    signals.HasBlackCat("STEERING_WEAK_SIGNAL"))
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "coverage_gap";
                    answer.Answer = "coverage gap during stability window";
                    return answer;
                }

                var pass = headingError <= HeadingErrorMax &&
                           yawRate <= YawRateMax &&
                           flips <= FlipsPer10sMax &&
                           retargets <= 0f;

                answer.Status = pass ? Space4XQuestionStatus.Pass : Space4XQuestionStatus.Fail;
                answer.Answer = $"heading_error_max={headingError:0.##} yaw_rate_max={yawRate:0.##} flips_per_10s={flips:0.##} retargets={retargets:0}";
                return answer;
            }
        }

        private sealed class UndockRiskQuestion : IHeadlessQuestion
        {
            private const float RiskGapMin = 0.00005f;
            private const float RiskEnforceMin = 0.01f;
            private const float RiskHardStop = 0.9f;

            public string Id => Space4XHeadlessQuestionIds.UndockRisk;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                var lawfulCount = signals.GetMetricOrDefault("space4x.undock.lawful.count");
                var chaoticCount = signals.GetMetricOrDefault("space4x.undock.chaotic.count");
                var maxRisk = signals.GetMetricOrDefault("space4x.undock.max_risk");
                var lawfulAvgRisk = signals.GetMetricOrDefault("space4x.undock.lawful.avg_risk");
                var chaoticAvgRisk = signals.GetMetricOrDefault("space4x.undock.chaotic.avg_risk");
                var lawfulWaitAvg = signals.GetMetricOrDefault("space4x.undock.lawful.wait_avg");
                var chaoticWaitAvg = signals.GetMetricOrDefault("space4x.undock.chaotic.wait_avg");
                var lawfulWaitOnly = signals.GetMetricOrDefault("space4x.undock.lawful.wait_only");
                var chaoticWaitOnly = signals.GetMetricOrDefault("space4x.undock.chaotic.wait_only");

                answer.Metrics["lawful_count"] = lawfulCount;
                answer.Metrics["chaotic_count"] = chaoticCount;
                answer.Metrics["max_risk"] = maxRisk;
                answer.Metrics["lawful_avg_risk"] = lawfulAvgRisk;
                answer.Metrics["chaotic_avg_risk"] = chaoticAvgRisk;
                answer.Metrics["lawful_wait_avg"] = lawfulWaitAvg;
                answer.Metrics["chaotic_wait_avg"] = chaoticWaitAvg;
                answer.Metrics["lawful_wait_only"] = lawfulWaitOnly;
                answer.Metrics["chaotic_wait_only"] = chaoticWaitOnly;

                var hasLawfulSignal = lawfulCount > 0f || lawfulWaitOnly > 0f;
                if (signals.HasBlackCat("UNDOCK_BEAT_SKIPPED") || !hasLawfulSignal || chaoticCount <= 0f || maxRisk < RiskEnforceMin)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "insufficient_samples";
                    answer.Answer = "undock samples missing for lawful/chaotic";
                    return answer;
                }

                var riskGapOk = chaoticAvgRisk >= lawfulAvgRisk + RiskGapMin;
                var waitOk = lawfulWaitAvg >= chaoticWaitAvg;
                var hardStopOk = maxRisk < RiskHardStop;
                var lawfulWaitOnlySeparation = lawfulCount <= 0f && lawfulWaitOnly > 0f;
                var pass = hardStopOk && (lawfulWaitOnlySeparation || (riskGapOk && waitOk));

                answer.Status = pass ? Space4XQuestionStatus.Pass : Space4XQuestionStatus.Fail;
                if (lawfulWaitOnlySeparation)
                {
                    answer.Answer = $"lawful_wait_only={lawfulWaitOnly:0} chaotic_count={chaoticCount:0} max_risk={maxRisk:0.##}";
                }
                else
                {
                    answer.Answer = $"lawful_count={lawfulCount:0} chaotic_count={chaoticCount:0} max_risk={maxRisk:0.##}";
                }
                return answer;
            }
        }

        private sealed class MiningProgressQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.MiningProgress;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                answer.Metrics["miners"] = stats.MiningEntityCount;
                answer.Metrics["yield_nonzero"] = stats.MiningYieldNonZeroCount;
                answer.Metrics["yield_total"] = stats.MiningYieldTotal;
                answer.Metrics["stall_count"] = signals.CountBlackCats("MINING_STALL");

                if (stats.MiningEntityCount == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "no_miners";
                    answer.Answer = "no mining entities present";
                    return answer;
                }

                if (signals.HasBlackCat("MINING_STALL"))
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = "mining stalled during active phase";
                    return answer;
                }

                if (stats.MiningYieldNonZeroCount == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "yield_not_observed";
                    answer.Answer = "no mining yield observed";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = $"miners={stats.MiningEntityCount} yield_total={stats.MiningYieldTotal:0.##}";
                return answer;
            }
        }

        private sealed class CollisionResponseQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.CollisionResponse;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                answer.Metrics["collision_events"] = stats.CollisionEventCount;
                answer.Metrics["collision_probes"] = stats.CollisionProbeCount;
                answer.Metrics["phasing_count"] = signals.CountBlackCats("COLLISION_PHASING");

                if (signals.HasBlackCat("COLLISION_PHASING"))
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = "overlap persisted without collision response";
                    return answer;
                }

                if (stats.CollisionEventCount == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "no_collision_events";
                    answer.Answer = "no collision events observed";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = $"collision_events={stats.CollisionEventCount}";
                return answer;
            }
        }

        private sealed class SensorsAcquireDropQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.SensorsAcquireDrop;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                var acquireRatio = signals.GetMetricOrDefault("space4x.sensor.acquire_detected_ratio");
                var dropRatio = signals.GetMetricOrDefault("space4x.sensor.drop_detected_ratio");
                var toggleCount = signals.GetMetricOrDefault("space4x.sensor.toggle_count");
                var staleSamples = signals.GetMetricOrDefault("space4x.sensor.stale_samples");
                var sampleCount = signals.GetMetricOrDefault("space4x.sensor.sample_count");

                answer.Metrics["sample_count"] = sampleCount;
                answer.Metrics["acquire_detected_ratio"] = acquireRatio;
                answer.Metrics["drop_detected_ratio"] = dropRatio;
                answer.Metrics["toggle_count"] = toggleCount;
                answer.Metrics["stale_samples"] = staleSamples;

                if (stats.HasSensorsBeatConfig == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "beat_absent";
                    answer.Answer = "sensors beat not configured";
                    return answer;
                }

                if (signals.HasBlackCat("SENSORS_BEAT_SKIPPED"))
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "coverage_gap";
                    answer.Answer = "sensors beat skipped";
                    return answer;
                }

                if (signals.HasBlackCat("PERCEPTION_STALE") ||
                    signals.HasBlackCat("CONTACT_GHOST") ||
                    signals.HasBlackCat("CONTACT_THRASH"))
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = "sensor acquisition/drop anomalies detected";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = $"acquire_ratio={acquireRatio:0.##} drop_ratio={dropRatio:0.##} toggles={toggleCount:0}";
                return answer;
            }
        }
    }
}

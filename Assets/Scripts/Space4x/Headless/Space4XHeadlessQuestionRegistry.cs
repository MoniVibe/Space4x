using System;
using System.Collections.Generic;
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

        public bool TryGetMetricKeySuffix(string prefix, out string suffix)
        {
            suffix = null;
            if (_metrics == null || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            foreach (var key in _metrics.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    suffix = key.Substring(prefix.Length);
                    return true;
                }
            }

            return false;
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
        public byte HasSensorsBeatConfig;
        public byte HasCommsBeatConfig;

        public static Space4XOperatorRuntimeStats Collect(EntityManager entityManager)
        {
            var stats = new Space4XOperatorRuntimeStats();
            stats.HasSensorsBeatConfig = ResolveBeatPresence<Space4XSensorsBeatConfig>(entityManager);
            stats.HasCommsBeatConfig = ResolveBeatPresence<Space4XCommsBeatConfig>(entityManager);
            return stats;
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
        public const string SensorsAcquireDrop = "space4x.q.sensors.acquire_drop";
        public const string CommsDelivery = "space4x.q.comms.delivery";
        public const string CommsDeliveryBlocked = "space4x.q.comms.delivery_blocked";
        public const string MovementTurnRateBounds = "space4x.q.movement.turnrate_bounds";
        public const string Unknown = "space4x.q.unknown";

        public static string ResolveQuestionIdForBlackCatId(string blackCatId)
        {
            return blackCatId switch
            {
                "SENSORS_BEAT_SKIPPED" => SensorsAcquireDrop,
                "SENSORS_DROP_NOT_EXERCISED" => SensorsAcquireDrop,
                "PERCEPTION_STALE" => SensorsAcquireDrop,
                "CONTACT_GHOST" => SensorsAcquireDrop,
                "CONTACT_THRASH" => SensorsAcquireDrop,
                "COMMS_BEAT_SKIPPED" => CommsDelivery,
                _ => Unknown
            };
        }
    }

    internal static class Space4XHeadlessQuestionRegistry
    {
        private static readonly IHeadlessQuestion[] Questions =
        {
            new SensorsAcquireDropQuestion(),
            new CommsDeliveryQuestion(),
            new CommsDeliveryBlockedQuestion(),
            new MovementTurnRateBoundsQuestion()
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

                if (signals.HasBlackCat("SENSORS_DROP_NOT_EXERCISED"))
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "coverage_gap";
                    answer.Answer = "drop window never left sensor range";
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

        private sealed class CommsDeliveryQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.CommsDelivery;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                var sent = signals.GetMetricOrDefault("space4x.comms.sent");
                var emitted = signals.GetMetricOrDefault("space4x.comms.emitted");
                var received = signals.GetMetricOrDefault("space4x.comms.received");
                var deliveryRatio = signals.GetMetricOrDefault("space4x.comms.delivery_ratio");
                var emitRatio = signals.GetMetricOrDefault("space4x.comms.emit_ratio");
                var firstLatency = signals.GetMetricOrDefault("space4x.comms.first_latency_ticks");
                var maxInbox = signals.GetMetricOrDefault("space4x.comms.max_inbox_depth");
                var maxOutbox = signals.GetMetricOrDefault("space4x.comms.max_outbox_depth");

                answer.Metrics["sent"] = sent;
                answer.Metrics["emitted"] = emitted;
                answer.Metrics["received"] = received;
                answer.Metrics["delivery_ratio"] = deliveryRatio;
                answer.Metrics["emit_ratio"] = emitRatio;
                answer.Metrics["first_latency_ticks"] = firstLatency;
                answer.Metrics["max_inbox_depth"] = maxInbox;
                answer.Metrics["max_outbox_depth"] = maxOutbox;

                if (stats.HasCommsBeatConfig == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "beat_absent";
                    answer.Answer = "comms beat not configured";
                    return answer;
                }

                if (signals.HasBlackCat("COMMS_BEAT_SKIPPED"))
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "coverage_gap";
                    answer.Answer = "comms beat skipped";
                    return answer;
                }

                if (sent <= 0f)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "no_messages_sent";
                    answer.Answer = "no comms requests sent";
                    return answer;
                }

                if (received <= 0f || deliveryRatio < 0.8f)
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = $"sent={sent:0} received={received:0} delivery_ratio={deliveryRatio:0.##}";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = $"sent={sent:0} received={received:0} delivery_ratio={deliveryRatio:0.##}";
                return answer;
            }
        }

        private sealed class CommsDeliveryBlockedQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.CommsDeliveryBlocked;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                var sent = signals.GetMetricOrDefault("space4x.comms.sent");
                var emitted = signals.GetMetricOrDefault("space4x.comms.emitted");
                var received = signals.GetMetricOrDefault("space4x.comms.received");
                var blockedReason = signals.GetMetricOrDefault("space4x.comms.blocked_reason");
                var wrongTransport = signals.GetMetricOrDefault("space4x.comms.diag.targeted_wrong_transport");

                answer.Metrics["sent"] = sent;
                answer.Metrics["emitted"] = emitted;
                answer.Metrics["received"] = received;
                answer.Metrics["blocked_reason"] = blockedReason;
                answer.Metrics["wrong_transport"] = wrongTransport;

                if (stats.HasCommsBeatConfig == 0)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "beat_absent";
                    answer.Answer = "comms beat not configured";
                    return answer;
                }

                if (signals.HasBlackCat("COMMS_BEAT_SKIPPED"))
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "coverage_gap";
                    answer.Answer = "comms beat skipped";
                    return answer;
                }

                if (sent <= 0f)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "no_messages_sent";
                    answer.Answer = "no comms requests sent";
                    return answer;
                }

                if (received > 0f)
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = $"delivery_succeeded sent={sent:0} received={received:0}";
                    return answer;
                }

                if (blockedReason <= 0f || blockedReason >= 7f)
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = $"delivery_not_blocked sent={sent:0} emitted={emitted:0} reason={blockedReason:0}";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = $"blocked_reason={blockedReason:0} sent={sent:0} emitted={emitted:0}";
                return answer;
            }
        }

        private sealed class MovementTurnRateBoundsQuestion : IHeadlessQuestion
        {
            public string Id => Space4XHeadlessQuestionIds.MovementTurnRateBounds;

            public Space4XQuestionAnswer Evaluate(Space4XOperatorSignals signals, Space4XOperatorRuntimeStats stats, in Space4XScenarioRuntime runtime)
            {
                var answer = new Space4XQuestionAnswer
                {
                    Id = Id,
                    StartTick = runtime.StartTick,
                    EndTick = runtime.EndTick,
                    Metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                };

                if (!Space4XHeadlessDiagnostics.Enabled)
                {
                    answer.Status = Space4XQuestionStatus.Unknown;
                    answer.UnknownReason = "diagnostics_disabled";
                    answer.Answer = "headless diagnostics disabled";
                    return answer;
                }

                var hasFailures = Space4XHeadlessDiagnostics.HasInvariantFailures;
                answer.Metrics["has_invariant_failures"] = hasFailures ? 1f : 0f;

                if (hasFailures)
                {
                    answer.Status = Space4XQuestionStatus.Fail;
                    answer.Answer = "movement invariant failures detected";
                    return answer;
                }

                answer.Status = Space4XQuestionStatus.Pass;
                answer.Answer = "no movement invariants";
                return answer;
            }
        }
    }
}

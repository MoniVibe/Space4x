using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Space4x.Scenario;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SystemEnvironment = System.Environment;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(Unity.Entities.LateSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XHeadlessUndockRiskGateSystem))]
    public partial struct Space4XHeadlessOperatorReportSystem : ISystem
    {
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string Space4xScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ReportFileName = "operator_report.json";
        private const int TraceEventLimit = 8;
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            WriteReport(ref state, runtime);
            _done = 1;
        }

        private static void WriteReport(ref SystemState state, in Space4XScenarioRuntime runtime)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var blackCats))
            {
                return;
            }

            var outputDir = ResolveOutputDirectory(state.EntityManager);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                return;
            }

            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, ReportFileName);

            var scenarioId = string.Empty;
            var seed = 0u;
            if (TryGetScenarioInfo(state.EntityManager, out var info))
            {
                scenarioId = info.ScenarioId.ToString();
                seed = info.Seed;
            }

            var metrics = CollectOperatorMetrics(state.EntityManager);
            var blackCatList = CopyBlackCats(blackCats);
            var signals = new Space4XOperatorSignals(metrics, blackCatList);
            var runtimeStats = Space4XOperatorRuntimeStats.Collect(state.EntityManager);
            var questionPack = CollectQuestionPack(state.EntityManager);
            var questions = Space4XHeadlessQuestionRegistry.BuildQuestions(signals, runtimeStats, runtime, questionPack);

            var sb = new StringBuilder(4096);
            var first = true;
            sb.Append('{');
            AppendString(ref first, sb, "scenarioId", scenarioId);
            AppendUInt(ref first, sb, "seed", seed);
            AppendUInt(ref first, sb, "startTick", runtime.StartTick);
            AppendUInt(ref first, sb, "endTick", runtime.EndTick);
            AppendSummary(ref first, sb, metrics);
            AppendQuestions(ref first, sb, questions);
            AppendBlackCats(ref first, sb, state.EntityManager, blackCatList);
            sb.Append('}');

            File.WriteAllText(outputPath, sb.ToString(), Encoding.ASCII);
        }

        private static void AppendSummary(ref bool first, StringBuilder sb, Dictionary<string, float> metrics)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"summary\":{");

            var innerFirst = true;
            if (metrics != null && metrics.Count > 0)
            {
                var keys = new List<string>(metrics.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    if (!key.StartsWith("space4x.steer.", StringComparison.OrdinalIgnoreCase) &&
                        !key.StartsWith("space4x.undock.", StringComparison.OrdinalIgnoreCase) &&
                        !key.StartsWith("space4x.sensor.", StringComparison.OrdinalIgnoreCase) &&
                        !key.StartsWith("space4x.comms.", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AppendFloat(ref innerFirst, sb, key, metrics[key]);
                }
            }

            sb.Append('}');
        }

        private static void AppendQuestions(ref bool first, StringBuilder sb, List<Space4XQuestionAnswer> questions)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"questions\":[");

            var innerFirst = true;
            if (questions != null)
            {
                for (var i = 0; i < questions.Count; i++)
                {
                    var question = questions[i];
                    AppendSeparator(ref innerFirst, sb);
                    sb.Append('{');
                    var qFirst = true;
                    AppendString(ref qFirst, sb, "id", question.Id ?? string.Empty);
                    AppendString(ref qFirst, sb, "status", ResolveQuestionStatus(question.Status));
                    AppendBool(ref qFirst, sb, "required", question.Required);
                    if (question.Status == Space4XQuestionStatus.Unknown)
                    {
                        AppendString(ref qFirst, sb, "unknown_reason", question.UnknownReason ?? string.Empty);
                    }
                    AppendString(ref qFirst, sb, "answer", question.Answer ?? string.Empty);
                    AppendWindow(ref qFirst, sb, question.StartTick, question.EndTick);
                    AppendQuestionMetrics(ref qFirst, sb, question.Metrics);
                    if (question.Status != Space4XQuestionStatus.Pass)
                    {
                        AppendQuestionEvidence(ref qFirst, sb, question.Evidence);
                    }
                    sb.Append('}');
                }
            }

            sb.Append(']');
        }

        private static void AppendBlackCats(
            ref bool first,
            StringBuilder sb,
            EntityManager entityManager,
            List<Space4XOperatorBlackCat> blackCats)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"blackCats\":[");

            var innerFirst = true;
            if (blackCats != null)
            {
                for (var i = 0; i < blackCats.Count; i++)
                {
                    var cat = blackCats[i];
                    AppendSeparator(ref innerFirst, sb);
                    sb.Append('{');
                    var catFirst = true;
                    var questionId = Space4XHeadlessQuestionIds.ResolveQuestionIdForBlackCatId(cat.Id.ToString());
                    AppendString(ref catFirst, sb, "id", cat.Id.ToString());
                    AppendString(ref catFirst, sb, "questionId", questionId);
                    AppendEntity(ref catFirst, sb, "primary", cat.Primary);
                    AppendEntity(ref catFirst, sb, "secondary", cat.Secondary);
                    AppendUInt(ref catFirst, sb, "startTick", cat.StartTick);
                    AppendUInt(ref catFirst, sb, "endTick", cat.EndTick);
                    AppendString(ref catFirst, sb, "classification", ResolveClassification(cat));
                    AppendMetrics(ref catFirst, sb, cat);
                    AppendDecisionTrace(ref catFirst, sb, entityManager, cat.Primary);
                    AppendMiningTrace(ref catFirst, sb, entityManager, cat.Primary);
                    AppendTraceTail(ref catFirst, sb, entityManager, cat.Primary, cat.Secondary);
                    sb.Append('}');
                }
            }

            sb.Append(']');
        }

        private static void AppendMetrics(ref bool first, StringBuilder sb, Space4XOperatorBlackCat cat)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"metrics\":{");
            var innerFirst = true;

            if (cat.Id.Equals(new FixedString64Bytes("HEADING_OSCILLATION")))
            {
                AppendFloat(ref innerFirst, sb, "flips_per_10s", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "yaw_rate_max_deg_s", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "retargets", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "heading_error_max_deg", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("STEERING_BEAT_SKIPPED")))
            {
                AppendFloat(ref innerFirst, sb, "sample_count", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "measure_seconds", cat.MetricB);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("STEERING_BEAT_LOW_SPEED")))
            {
                AppendFloat(ref innerFirst, sb, "sample_count", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "avg_speed", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "speed_gate_threshold", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "speed_gate_samples", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("SENSORS_BEAT_SKIPPED")))
            {
                AppendFloat(ref innerFirst, sb, "sample_count", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "acquire_samples", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "drop_samples", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "reason_code", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("PERCEPTION_STALE")))
            {
                AppendFloat(ref innerFirst, sb, "stale_samples", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "max_ticks_since_update", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "expected_update_ticks", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "acquire_detected_ratio", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("CONTACT_GHOST")))
            {
                AppendFloat(ref innerFirst, sb, "drop_detected", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "drop_samples", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "drop_detected_ratio", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "toggle_count", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("CONTACT_THRASH")))
            {
                AppendFloat(ref innerFirst, sb, "toggle_count", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "acquire_detected", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "drop_detected", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "sample_count", cat.MetricD);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("UNDOCK_BEAT_SKIPPED")))
            {
                AppendFloat(ref innerFirst, sb, "max_risk", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "lawful_undocks", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "chaotic_undocks", cat.MetricC);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("COLLISION_PHASING")))
            {
                AppendFloat(ref innerFirst, sb, "overlap_ticks", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "penetration", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "last_collision_tick", cat.MetricC);
            }
            else if (cat.Id.Equals(new FixedString64Bytes("MINING_STALL")))
            {
                AppendFloat(ref innerFirst, sb, "stall_ticks", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "distance_to_target", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "phase", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "dig_requests", cat.MetricD);
            }
            else
            {
                AppendFloat(ref innerFirst, sb, "metricA", cat.MetricA);
                AppendFloat(ref innerFirst, sb, "metricB", cat.MetricB);
                AppendFloat(ref innerFirst, sb, "metricC", cat.MetricC);
                AppendFloat(ref innerFirst, sb, "metricD", cat.MetricD);
            }

            sb.Append('}');
        }

        private static void AppendWindow(ref bool first, StringBuilder sb, uint startTick, uint endTick)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"window\":{");
            var innerFirst = true;
            AppendUInt(ref innerFirst, sb, "startTick", startTick);
            AppendUInt(ref innerFirst, sb, "endTick", endTick);
            sb.Append('}');
        }

        private static void AppendQuestionMetrics(ref bool first, StringBuilder sb, Dictionary<string, float> metrics)
        {
            if (metrics == null || metrics.Count == 0)
            {
                return;
            }

            AppendSeparator(ref first, sb);
            sb.Append("\"metrics\":{");
            var innerFirst = true;
            var keys = new List<string>(metrics.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                AppendFloat(ref innerFirst, sb, key, metrics[key]);
            }
            sb.Append('}');
        }

        private static void AppendQuestionEvidence(ref bool first, StringBuilder sb, List<Space4XQuestionEvidence> evidence)
        {
            if (evidence == null || evidence.Count == 0)
            {
                return;
            }

            evidence.Sort(CompareEvidence);
            AppendSeparator(ref first, sb);
            sb.Append("\"evidence\":[");
            var innerFirst = true;
            for (var i = 0; i < evidence.Count; i++)
            {
                var entry = evidence[i];
                AppendSeparator(ref innerFirst, sb);
                sb.Append('{');
                var evtFirst = true;
                AppendString(ref evtFirst, sb, "blackCatId", entry.BlackCatId ?? string.Empty);
                AppendEntity(ref evtFirst, sb, "primary", entry.Primary);
                AppendEntity(ref evtFirst, sb, "secondary", entry.Secondary);
                sb.Append('}');
            }
            sb.Append(']');
        }

        private static int CompareEvidence(Space4XQuestionEvidence left, Space4XQuestionEvidence right)
        {
            var leftPrimary = left?.Primary.Index ?? -1;
            var rightPrimary = right?.Primary.Index ?? -1;
            var cmp = leftPrimary.CompareTo(rightPrimary);
            if (cmp != 0)
            {
                return cmp;
            }

            var leftSecondary = left?.Secondary.Index ?? -1;
            var rightSecondary = right?.Secondary.Index ?? -1;
            cmp = leftSecondary.CompareTo(rightSecondary);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.CompareOrdinal(left?.BlackCatId, right?.BlackCatId);
        }

        private static string ResolveQuestionStatus(Space4XQuestionStatus status)
        {
            return status switch
            {
                Space4XQuestionStatus.Pass => "pass",
                Space4XQuestionStatus.Fail => "fail",
                Space4XQuestionStatus.Unknown => "unknown",
                _ => "unknown"
            };
        }

        private static Dictionary<string, float> CollectOperatorMetrics(EntityManager entityManager)
        {
            var metrics = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (TryGetOperatorMetrics(entityManager, out var operatorMetrics) && operatorMetrics.Length > 0)
            {
                for (var i = 0; i < operatorMetrics.Length; i++)
                {
                    var metric = operatorMetrics[i];
                    var key = metric.Key.ToString();
                    if (!metrics.ContainsKey(key))
                    {
                        metrics.Add(key, metric.Value);
                    }
                    else
                    {
                        metrics[key] = metric.Value;
                    }
                }
            }
            else if (TryGetTelemetryMetrics(entityManager, out var telemetryMetrics))
            {
                for (var i = 0; i < telemetryMetrics.Length; i++)
                {
                    var metric = telemetryMetrics[i];
                    var key = metric.Key.ToString();
                    if (!metrics.ContainsKey(key))
                    {
                        metrics.Add(key, metric.Value);
                    }
                    else
                    {
                        metrics[key] = metric.Value;
                    }
                }
            }

            return metrics;
        }

        private static List<Space4XOperatorBlackCat> CopyBlackCats(DynamicBuffer<Space4XOperatorBlackCat> blackCats)
        {
            var list = new List<Space4XOperatorBlackCat>(blackCats.Length);
            for (var i = 0; i < blackCats.Length; i++)
            {
                list.Add(blackCats[i]);
            }

            return list;
        }

        private static Dictionary<string, bool> CollectQuestionPack(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XHeadlessQuestionPackTag>());
            if (query.IsEmptyIgnoreFilter)
            {
                return null;
            }

            var entity = query.GetSingletonEntity();
            if (!entityManager.HasBuffer<Space4XHeadlessQuestionPackItem>(entity))
            {
                return null;
            }

            var buffer = entityManager.GetBuffer<Space4XHeadlessQuestionPackItem>(entity);
            if (buffer.Length == 0)
            {
                return null;
            }

            var pack = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < buffer.Length; i++)
            {
                var item = buffer[i];
                var id = item.Id.ToString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                pack[id] = item.Required != 0;
            }

            return pack;
        }

        private static void AppendDecisionTrace(ref bool first, StringBuilder sb, EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.HasComponent<DecisionTrace>(entity))
            {
                return;
            }

            var trace = entityManager.GetComponentData<DecisionTrace>(entity);
            AppendSeparator(ref first, sb);
            sb.Append("\"decision\":{");
            var innerFirst = true;
            AppendString(ref innerFirst, sb, "reason", ResolveDecisionReason(trace.ReasonCode));
            AppendFloat(ref innerFirst, sb, "score", trace.Score);
            AppendEntity(ref innerFirst, sb, "chosenTarget", trace.ChosenTarget);
            AppendEntity(ref innerFirst, sb, "blocker", trace.BlockerEntity);
            AppendUInt(ref innerFirst, sb, "sinceTick", trace.SinceTick);
            sb.Append('}');
        }

        private static void AppendMiningTrace(ref bool first, StringBuilder sb, EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null)
            {
                return;
            }

            var hasTrace = false;
            var trace = default(MiningDecisionTrace);
            if (entityManager.HasComponent<Space4XMiningStallState>(entity))
            {
                var stall = entityManager.GetComponentData<Space4XMiningStallState>(entity);
                if (stall.DecisionTick > 0)
                {
                    trace.Reason = stall.DecisionReason;
                    trace.Target = stall.DecisionTarget;
                    trace.DistanceToTarget = stall.DecisionDistance;
                    trace.RangeThreshold = stall.DecisionRangeThreshold;
                    trace.Standoff = stall.DecisionStandoff;
                    trace.ArrivalDistance = stall.DecisionArrivalDistance;
                    trace.ApproachDistance = stall.DecisionApproachDistance;
                    trace.Aligned = stall.DecisionAligned;
                    trace.Tick = stall.DecisionTick;
                    hasTrace = true;
                }
            }

            if (!hasTrace)
            {
                if (!entityManager.HasComponent<MiningDecisionTrace>(entity))
                {
                    return;
                }

                trace = entityManager.GetComponentData<MiningDecisionTrace>(entity);
            }

            AppendSeparator(ref first, sb);
            sb.Append("\"miningDecision\":{");
            var innerFirst = true;
            AppendString(ref innerFirst, sb, "reason", ResolveMiningDecisionReason(trace.Reason));
            AppendFloat(ref innerFirst, sb, "distance", trace.DistanceToTarget);
            AppendFloat(ref innerFirst, sb, "range_threshold", trace.RangeThreshold);
            AppendFloat(ref innerFirst, sb, "standoff", trace.Standoff);
            AppendFloat(ref innerFirst, sb, "arrival_distance", trace.ArrivalDistance);
            AppendFloat(ref innerFirst, sb, "approach_distance", trace.ApproachDistance);
            AppendFloat(ref innerFirst, sb, "aligned", trace.Aligned);
            AppendEntity(ref innerFirst, sb, "target", trace.Target);
            AppendUInt(ref innerFirst, sb, "tick", trace.Tick);
            sb.Append('}');
        }

        private static void AppendTraceTail(ref bool first, StringBuilder sb, EntityManager entityManager, Entity primary, Entity secondary)
        {
            AppendSeparator(ref first, sb);
            sb.Append("\"trace\":[");
            var innerFirst = true;

            AppendTraceEvents(ref innerFirst, sb, entityManager, primary);
            AppendTraceEvents(ref innerFirst, sb, entityManager, secondary);

            sb.Append(']');
        }




        private static void AppendTraceEvents(ref bool first, StringBuilder sb, EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.HasBuffer<MoveTraceEvent>(entity))
            {
                return;
            }

            var buffer = entityManager.GetBuffer<MoveTraceEvent>(entity);
            var start = math.max(0, buffer.Length - TraceEventLimit);

            AppendSeparator(ref first, sb);
            sb.Append('{');
            var innerFirst = true;
            AppendEntity(ref innerFirst, sb, "entity", entity);
            AppendSeparator(ref innerFirst, sb);
            sb.Append("\"events\":[");
            var eventFirst = true;

            for (var i = start; i < buffer.Length; i++)
            {
                var evt = buffer[i];
                AppendSeparator(ref eventFirst, sb);
                sb.Append('{');
                var evtFirst = true;
                AppendString(ref evtFirst, sb, "kind", ResolveTraceKind(evt.Kind));
                AppendUInt(ref evtFirst, sb, "tick", evt.Tick);
                AppendEntity(ref evtFirst, sb, "target", evt.Target);
                sb.Append('}');
            }

            sb.Append(']');
            sb.Append('}');
        }

        private static bool TryGetTelemetryMetrics(EntityManager entityManager, out DynamicBuffer<TelemetryMetric> metrics)
        {
            metrics = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var entity = query.GetSingletonEntity();
            if (!entityManager.HasBuffer<TelemetryMetric>(entity))
            {
                return false;
            }

            metrics = entityManager.GetBuffer<TelemetryMetric>(entity);
            return true;
        }

        private static bool TryGetOperatorMetrics(EntityManager entityManager, out DynamicBuffer<Space4XOperatorMetric> metrics)
        {
            metrics = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XOperatorReportTag>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var entity = query.GetSingletonEntity();
            if (!entityManager.HasBuffer<Space4XOperatorMetric>(entity))
            {
                return false;
            }

            metrics = entityManager.GetBuffer<Space4XOperatorMetric>(entity);
            return true;
        }

        private static bool TryGetScenarioInfo(EntityManager entityManager, out ScenarioInfo info)
        {
            info = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            info = query.GetSingleton<ScenarioInfo>();
            return true;
        }

        private static string ResolveOutputDirectory(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryExportConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var config = query.GetSingleton<TelemetryExportConfig>();
                if (config.Enabled != 0 && config.OutputPath.Length > 0)
                {
                    var path = config.OutputPath.ToString();
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }

            var envPath = SystemEnvironment.GetEnvironmentVariable(TelemetryPathEnv);
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var dir = Path.GetDirectoryName(envPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }

            var scenarioPath = SystemEnvironment.GetEnvironmentVariable(Space4xScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath))
            {
                var dir = Path.GetDirectoryName(scenarioPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }

            var fallback = Application.persistentDataPath;
            return string.IsNullOrWhiteSpace(fallback) ? "." : Path.Combine(fallback, "headless_reports");
        }

        private static string ResolveClassification(Space4XOperatorBlackCat cat)
        {
            if (cat.Id.Equals(new FixedString64Bytes("HEADING_OSCILLATION")))
            {
                return cat.Classification == 1 ? "decision_thrash" : "controller_oscillation";
            }

            if (cat.Id.Equals(new FixedString64Bytes("STEERING_BEAT_SKIPPED")))
            {
                return "no_samples";
            }

            if (cat.Id.Equals(new FixedString64Bytes("STEERING_BEAT_LOW_SPEED")))
            {
                return "predicate_not_applicable";
            }

            if (cat.Id.Equals(new FixedString64Bytes("SENSORS_BEAT_SKIPPED")))
            {
                return cat.Classification switch
                {
                    1 => "observer_missing",
                    2 => "target_missing",
                    3 => "sense_missing",
                    4 => "perceived_buffer_missing",
                    5 => "no_samples",
                    _ => "skipped"
                };
            }

            if (cat.Id.Equals(new FixedString64Bytes("PERCEPTION_STALE")))
            {
                return cat.Classification == 1 ? "update_lag" : "acquire_miss";
            }

            if (cat.Id.Equals(new FixedString64Bytes("CONTACT_GHOST")))
            {
                return "ghost_detected";
            }

            if (cat.Id.Equals(new FixedString64Bytes("CONTACT_THRASH")))
            {
                return "contact_thrash";
            }

            if (cat.Id.Equals(new FixedString64Bytes("COMMS_BEAT_SKIPPED")))
            {
                return cat.Classification switch
                {
                    1 => "sender_missing",
                    2 => "receiver_missing",
                    3 => "no_messages_sent",
                    4 => "no_messages_emitted",
                    _ => "skipped"
                };
            }

            if (cat.Id.Equals(new FixedString64Bytes("UNDOCK_BEAT_SKIPPED")))
            {
                return "no_undocks";
            }

            if (cat.Id.Equals(new FixedString64Bytes("COLLISION_PHASING")))
            {
                return "no_collision_response";
            }

            if (cat.Id.Equals(new FixedString64Bytes("MINING_STALL")))
            {
                return cat.Classification switch
                {
                    1 => "docked",
                    2 => "undock_wait",
                    3 => "approach",
                    4 => "latch_wait",
                    5 => "dig_zero",
                    6 => "terrain_no_effect",
                    7 => "return_missing",
                    8 => "docking_fail",
                    _ => "no_yield_progress"
                };
            }

            return "unknown";
        }

        private static string ResolveDecisionReason(DecisionReasonCode code)
        {
            return code switch
            {
                DecisionReasonCode.NoTarget => "no_target",
                DecisionReasonCode.MiningHold => "mining_hold",
                DecisionReasonCode.Arrived => "arrived",
                DecisionReasonCode.Moving => "moving",
                DecisionReasonCode.MiningUndockWait => "mining_undock_wait",
                DecisionReasonCode.MiningLatchWait => "mining_latch_wait",
                DecisionReasonCode.MiningDigging => "mining_digging",
                DecisionReasonCode.MiningReturnFull => "mining_return_full",
                _ => "none"
            };
        }

        private static string ResolveMiningDecisionReason(MiningDecisionReason reason)
        {
            return reason switch
            {
                MiningDecisionReason.NoTarget => "no_target",
                MiningDecisionReason.UndockWait => "undock_wait",
                MiningDecisionReason.NotInRange => "not_in_range",
                MiningDecisionReason.LatchNotReady => "latch_not_ready",
                MiningDecisionReason.Digging => "digging",
                MiningDecisionReason.DigZero => "dig_zero",
                MiningDecisionReason.ReturnFull => "return_full",
                MiningDecisionReason.DockingWait => "docking_wait",
                MiningDecisionReason.NotAligned => "not_aligned",
                _ => "none"
            };
        }

        private static string ResolveTraceKind(MoveTraceEventKind kind)
        {
            return kind switch
            {
                MoveTraceEventKind.IntentChanged => "intent_changed",
                MoveTraceEventKind.PlanChanged => "plan_changed",
                MoveTraceEventKind.DecisionChanged => "decision_changed",
                MoveTraceEventKind.SteeringFlip => "steering_flip",
                MoveTraceEventKind.UndockDecision => "undock_decision",
                _ => "unknown"
            };
        }

        private static void AppendEntity(ref bool first, StringBuilder sb, string key, Entity entity)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":");
            if (entity == Entity.Null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            var innerFirst = true;
            AppendInt(ref innerFirst, sb, "index", entity.Index);
            AppendInt(ref innerFirst, sb, "version", entity.Version);
            sb.Append('}');
        }

        private static void AppendString(ref bool first, StringBuilder sb, string key, string value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static void AppendUInt(ref bool first, StringBuilder sb, string key, uint value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendInt(ref bool first, StringBuilder sb, string key, int value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendBool(ref bool first, StringBuilder sb, string key, bool value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
        }

        private static void AppendFloat(ref bool first, StringBuilder sb, string key, float value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":");
            AppendFloatValue(sb, value);
        }

        private static void AppendSeparator(ref bool first, StringBuilder sb)
        {
            if (!first)
            {
                sb.Append(',');
                return;
            }

            first = false;
        }

        private static void AppendFloatValue(StringBuilder sb, float value)
        {
            if (float.IsNaN(value))
            {
                sb.Append("\"NaN\"");
                return;
            }

            if (float.IsInfinity(value))
            {
                sb.Append(value > 0f ? "\"Inf\"" : "\"-Inf\"");
                return;
            }

            sb.Append(value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

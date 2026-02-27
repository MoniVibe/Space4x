#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Headless;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Space4X.Tests.PlayMode
{
    [Ignore("WIP: use Space4XCapitalRangeBatchRunner for firing-range telemetry until this PlayMode harness is stabilized.")]
    public class Space4XCapitalRangeScenarioTelemetryTests
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string HeadlessScenePath = "Assets/Scenes/HeadlessBootstrap.unity";
        private const string ScenarioPath = "Assets/Scenarios/space4x_capital_shooting_range_micro.json";
        private const int MaxTicks = 1800; // 3x guard over expected 600-tick scenario runtime.

        private static readonly FixedString64Bytes MetricShotsFired = new FixedString64Bytes("space4x.gunnery.capital_range.shots.fired");
        private static readonly FixedString64Bytes MetricShotsHit = new FixedString64Bytes("space4x.gunnery.capital_range.shots.hit");
        private static readonly FixedString64Bytes MetricHitRate = new FixedString64Bytes("space4x.gunnery.capital_range.hit_rate");
        private static readonly FixedString64Bytes MetricReactionTime = new FixedString64Bytes("space4x.gunnery.capital_range.reaction_time_s");
        private static readonly FixedString64Bytes MetricScore = new FixedString64Bytes("space4x.gunnery.capital_range.score");

        [Test]
        public void CapitalRangeScenario_EmitsHitQualityTelemetry()
        {
            if (!Application.isBatchMode)
            {
                Assert.Ignore("Capital range telemetry test is intended for batchmode CLI lane runs.");
            }

            var runtimeErrors = new List<string>();
            var previousScenarioPath = global::System.Environment.GetEnvironmentVariable("SPACE4X_SCENARIO_PATH");
            var previousHeadless = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS");
            var previousForceRender = global::System.Environment.GetEnvironmentVariable("PUREDOTS_FORCE_RENDER");
            var previousRendering = global::System.Environment.GetEnvironmentVariable("PUREDOTS_RENDERING");
            var previousExitPolicy = global::System.Environment.GetEnvironmentVariable("PUREDOTS_EXIT_POLICY");
            var previousTimeProof = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF");
            var previousRewindProof = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF");
            var previousMiningProof = global::System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF");
            var previousMovementDiag = global::System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG");

            void CaptureErrorLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    runtimeErrors.Add($"{type}: {condition}");
                }
            }

            Application.logMessageReceived += CaptureErrorLog;
            try
            {
                global::System.Environment.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", ScenarioPath);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_RENDERING", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_EXIT_POLICY", "nevernonzero");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG", "0");
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();

#if UNITY_EDITOR
                var loadOp = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    HeadlessScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                while (loadOp != null && !loadOp.isDone)
                {
                    UpdateWorlds(runtimeErrors);
                }
#else
                SceneManager.LoadScene(SmokeSceneName, LoadSceneMode.Single);
#endif

                var observed = default(CapitalRangeMetricsSnapshot);
                var observedAnyMetrics = false;
                var reachedScenarioEnd = false;

                for (var tick = 0; tick < MaxTicks; tick++)
                {
                    UpdateWorlds(runtimeErrors);
                    if (!TryReadCapitalRangeMetrics(out var snapshot))
                    {
                        continue;
                    }

                    observed = snapshot;
                    observedAnyMetrics = snapshot.HasAnyMetric || observedAnyMetrics;
                    reachedScenarioEnd = snapshot.EndTick > 0 && snapshot.CurrentTick >= snapshot.EndTick;
                    if (reachedScenarioEnd && observedAnyMetrics)
                    {
                        break;
                    }
                }

                if (runtimeErrors.Count > 0)
                {
                    Assert.Fail($"Runtime errors detected while simulating capital range scenario:\n{string.Join("\n", runtimeErrors)}");
                }

                Assert.IsTrue(observed.IsScenarioWorld, "Did not observe a Space4X scenario runtime world while testing capital range telemetry.");
                Assert.IsTrue(reachedScenarioEnd, $"Scenario did not reach end tick. current={observed.CurrentTick} end={observed.EndTick} world='{observed.WorldName}'.");
                Assert.IsTrue(observedAnyMetrics, $"No capital-range metrics were emitted in world '{observed.WorldName}'.");
                Assert.Greater(observed.ShotsFired, 0f, $"No shots fired in capital range scenario (world='{observed.WorldName}', tick={observed.CurrentTick}, end={observed.EndTick}).");
                Assert.GreaterOrEqual(observed.ShotsHit, 0f, "Shots hit metric was not populated.");
                Assert.GreaterOrEqual(observed.HitRate, 0f, "Hit rate metric was not populated.");
                Assert.GreaterOrEqual(observed.Score, 0f, "Score metric was not populated.");
                Assert.GreaterOrEqual(observed.ReactionTimeSeconds, 0f, "Reaction time metric was not populated.");
            }
            finally
            {
                Application.logMessageReceived -= CaptureErrorLog;
                global::System.Environment.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", previousScenarioPath);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", previousHeadless);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", previousForceRender);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_RENDERING", previousRendering);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_EXIT_POLICY", previousExitPolicy);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", previousTimeProof);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", previousRewindProof);
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", previousMiningProof);
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG", previousMovementDiag);
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();
            }
        }

        private static void UpdateWorlds(List<string> runtimeErrors)
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                try
                {
                    world.EntityManager.CompleteAllTrackedJobs();
                    world.Update();
                    world.EntityManager.CompleteAllTrackedJobs();
                }
                catch (Exception ex)
                {
                    runtimeErrors.Add($"Exception updating world '{world.Name}': {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static bool TryReadCapitalRangeMetrics(out CapitalRangeMetricsSnapshot snapshot)
        {
            snapshot = default;
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var entityManager = world.EntityManager;
                using var runtimeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XScenarioRuntime>());
                if (runtimeQuery.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                var runtime = runtimeQuery.GetSingleton<Space4XScenarioRuntime>();
                snapshot.WorldName = world.Name;
                snapshot.EndTick = runtime.EndTick;
                snapshot.IsScenarioWorld = true;

                using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
                if (!timeQuery.IsEmptyIgnoreFilter)
                {
                    snapshot.CurrentTick = timeQuery.GetSingleton<TimeState>().Tick;
                }

                using var metricQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Space4XOperatorReportTag>(),
                    ComponentType.ReadOnly<Space4XOperatorMetric>());
                if (metricQuery.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                var reportEntity = metricQuery.GetSingletonEntity();
                var metrics = entityManager.GetBuffer<Space4XOperatorMetric>(reportEntity);
                snapshot.ShotsFired = TryGetMetric(metrics, MetricShotsFired, out var shotsFired) ? shotsFired : -1f;
                snapshot.ShotsHit = TryGetMetric(metrics, MetricShotsHit, out var shotsHit) ? shotsHit : -1f;
                snapshot.HitRate = TryGetMetric(metrics, MetricHitRate, out var hitRate) ? hitRate : -1f;
                snapshot.ReactionTimeSeconds = TryGetMetric(metrics, MetricReactionTime, out var reactionTime) ? reactionTime : -1f;
                snapshot.Score = TryGetMetric(metrics, MetricScore, out var score) ? score : -1f;
                snapshot.HasAnyMetric = snapshot.ShotsFired >= 0f ||
                                        snapshot.ShotsHit >= 0f ||
                                        snapshot.HitRate >= 0f ||
                                        snapshot.ReactionTimeSeconds >= 0f ||
                                        snapshot.Score >= 0f;
                return true;
            }

            return false;
        }

        private static bool TryGetMetric(DynamicBuffer<Space4XOperatorMetric> metrics, FixedString64Bytes key, out float value)
        {
            for (var i = 0; i < metrics.Length; i++)
            {
                if (!metrics[i].Key.Equals(key))
                {
                    continue;
                }

                value = metrics[i].Value;
                return true;
            }

            value = 0f;
            return false;
        }

        private struct CapitalRangeMetricsSnapshot
        {
            public string WorldName;
            public uint CurrentTick;
            public uint EndTick;
            public float ShotsFired;
            public float ShotsHit;
            public float HitRate;
            public float ReactionTimeSeconds;
            public float Score;
            public bool HasAnyMetric;
            public bool IsScenarioWorld;
        }
    }
}
#endif

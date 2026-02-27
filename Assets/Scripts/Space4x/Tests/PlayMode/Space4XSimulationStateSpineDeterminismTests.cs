#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Runtime;
using Space4X.Systems.StateSpine;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Space4X.Tests.PlayMode
{
    public class Space4XSimulationStateSpineDeterminismTests
    {
        [Test]
        public void SpineDigest_SameSeedAndReplayInput_IsStable()
        {
            var first = RunDigestScenario(seed: 424242u, ticks: 360);
            var second = RunDigestScenario(seed: 424242u, ticks: 360);

            Assert.AreEqual(first.RunningDigest, second.RunningDigest, "Spine running digest drifted for fixed seed/input.");
            Assert.AreEqual(first.LastTickDigest, second.LastTickDigest, "Spine tick digest drifted for fixed seed/input.");
            Assert.AreEqual(first.DroppedTotal, second.DroppedTotal, "Overflow totals drifted for fixed seed/input.");
            Assert.Greater(first.TicksDigested, 0u, "Digest probe did not run.");
        }

        [Test]
        public void SpineDigest_DifferentSeed_Diverges()
        {
            var first = RunDigestScenario(seed: 1001u, ticks: 360);
            var second = RunDigestScenario(seed: 2002u, ticks: 360);

            Assert.AreNotEqual(first.RunningDigest, second.RunningDigest, "Different seeds should diverge spine digest.");
        }

        [Test]
        public void SpineDigest_WorkerCountMatrix_IsStableForSameSeed()
        {
            var originalWorkers = math.max(1, JobsUtility.JobWorkerCount);
            var maxWorkers = math.max(1, JobsUtility.JobWorkerMaximumCount);
            var candidates = new List<int>
            {
                1,
                math.min(2, maxWorkers),
                math.min(4, maxWorkers),
                maxWorkers,
                originalWorkers
            };

            var seen = new HashSet<int>();
            var baselineDigest = 0u;
            var baselineSet = false;
            var evaluated = 0;

            try
            {
                foreach (var candidate in candidates)
                {
                    var workers = math.clamp(candidate, 1, maxWorkers);
                    if (!seen.Add(workers))
                    {
                        continue;
                    }

                    if (!TrySetWorkerCount(workers, out var setError))
                    {
                        TestContext.WriteLine($"[SpineDeterminism] skip workerCount={workers}: {setError}");
                        continue;
                    }

                    var first = RunDigestScenario(seed: 31337u, ticks: 420);
                    var second = RunDigestScenario(seed: 31337u, ticks: 420);

                    Assert.AreEqual(first.RunningDigest, second.RunningDigest, $"Digest drift for workerCount={workers}.");
                    Assert.AreEqual(first.LastTickDigest, second.LastTickDigest, $"Tick digest drift for workerCount={workers}.");

                    if (!baselineSet)
                    {
                        baselineDigest = first.RunningDigest;
                        baselineSet = true;
                    }
                    else
                    {
                        Assert.AreEqual(
                            baselineDigest,
                            first.RunningDigest,
                            $"Cross-worker digest mismatch at workerCount={workers}.");
                    }

                    evaluated++;
                }

                Assert.GreaterOrEqual(evaluated, 1, "No worker-count variant could be evaluated.");
            }
            finally
            {
                TrySetWorkerCount(originalWorkers, out _);
            }
        }

        private static SpineDigestSummary RunDigestScenario(uint seed, int ticks)
        {
            using var world = new World($"SpineDeterminism-{seed}-{ticks}");
            var entityManager = world.EntityManager;

            Space4X.Tests.CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);
            var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = 0u;
            time.FixedDeltaTime = 1f / 60f;
            time.DeltaTime = time.FixedDeltaTime;
            time.DeltaSeconds = time.FixedDeltaTime;
            time.ElapsedTime = 0f;
            time.WorldSeconds = 0f;
            time.CurrentSpeedMultiplier = 1f;
            time.IsPaused = false;
            entityManager.SetComponentData(timeEntity, time);

            var scenarioEntity = entityManager.CreateEntity(typeof(ScenarioInfo));
            entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes("space4x_spine_determinism_probe"),
                Seed = seed,
                RunTicks = ticks
            });

            var bootstrap = world.GetOrCreateSystem<Space4XSimulationStateSpineBootstrapSystem>();
            var tickSystem = world.GetOrCreateSystem<Space4XSimulationStateSpineTickSystem>();
            bootstrap.Update(world.Unmanaged);

            var rootQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XSimulationStateSpineRootTag>());
            var root = rootQuery.GetSingletonEntity();

            var config = entityManager.GetComponentData<Space4XSimulationStateSpineConfig>(root);
            config.RecordEventHistory = 1;
            config.MaxIntakePerTick = 3u;
            config.MaxRetainedEvents = 256u;
            config.MaxRetainedDeterminismProbes = 1024u;
            entityManager.SetComponentData(root, config);

            world.EntityManager.WorldUnmanaged.Time = new TimeData(0f, time.FixedDeltaTime);
            for (var i = 0; i < ticks; i++)
            {
                var tick = (uint)(i + 1);
                var tickTime = entityManager.GetComponentData<TimeState>(timeEntity);
                tickTime.Tick = tick;
                tickTime.ElapsedTime = tick * tickTime.FixedDeltaTime;
                tickTime.WorldSeconds = tickTime.ElapsedTime;
                tickTime.DeltaTime = tickTime.FixedDeltaTime;
                tickTime.DeltaSeconds = tickTime.FixedDeltaTime;
                entityManager.SetComponentData(timeEntity, tickTime);

                var inbox = entityManager.GetBuffer<Space4XSimulationStateSpineEventInbox>(root);
                EmitDeterministicEvents(inbox, seed, tick);

                world.EntityManager.WorldUnmanaged.Time = new TimeData(tickTime.ElapsedTime, tickTime.FixedDeltaTime);
                tickSystem.Update(world.Unmanaged);
            }

            var determinism = entityManager.GetComponentData<Space4XSimulationStateSpineDeterminism>(root);
            var overflow = entityManager.GetComponentData<Space4XSimulationStateSpineOverflow>(root);
            var probes = entityManager.GetBuffer<Space4XSimulationStateSpineDeterminismProbe>(root);

            return new SpineDigestSummary
            {
                RunningDigest = determinism.RunningDigest,
                LastTickDigest = determinism.LastTickDigest,
                TicksDigested = determinism.TicksDigested,
                DroppedTotal = overflow.DroppedTotal,
                ProbeCount = probes.Length
            };
        }

        private static void EmitDeterministicEvents(
            DynamicBuffer<Space4XSimulationStateSpineEventInbox> inbox,
            uint seed,
            uint tick)
        {
            var count = 1 + (int)(Hash(seed, tick, 0xACEDu) % 8u);
            for (var i = 0; i < count; i++)
            {
                var hash = Hash(seed, tick, (uint)(i + 1));
                inbox.Add(new Space4XSimulationStateSpineEventInbox
                {
                    DomainId = 1u + (hash % 11u),
                    FamilyId = 100u + ((hash >> 4) % 17u),
                    EventTypeId = 1000u + ((hash >> 9) % 31u),
                    Actor = Entity.Null,
                    Subject = Entity.Null,
                    Intensity = ((hash >> 12) & 0x3FFu) / 10f,
                    Quality = ((hash >> 22) & 0x3FFu) / 10f,
                    ContextFlags = hash & 0x3Fu
                });
            }
        }

        private static uint Hash(uint seed, uint tick, uint salt)
        {
            return math.hash(new uint4(
                seed ^ 0x9E3779B9u,
                tick + 0x85EBCA6Bu,
                salt ^ 0xC2B2AE35u,
                seed * 1664525u + salt + 1013904223u));
        }

        private static bool TrySetWorkerCount(int workerCount, out string error)
        {
            try
            {
                JobsUtility.JobWorkerCount = workerCount;
                var actual = JobsUtility.JobWorkerCount;
                if (actual != workerCount)
                {
                    error = $"requested={workerCount} actual={actual}";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private struct SpineDigestSummary
        {
            public uint RunningDigest;
            public uint LastTickDigest;
            public uint TicksDigested;
            public uint DroppedTotal;
            public int ProbeCount;
        }
    }
}
#endif

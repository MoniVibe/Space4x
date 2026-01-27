using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PureDOTS.Runtime;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Executes scenarios headlessly by spinning a DOTS world with the headless profile
    /// and driving ticks deterministically. Keeps allocations minimal to stay friendly to CI.
    /// </summary>
    public static class ScenarioRunnerExecutor
    {
        private const float FixedDeltaTime = 1f / 60f;
        private static readonly ProfilerMarker RunMarker = new("ScenarioRunner.Execute");
        private static readonly WorldSystemFilterFlags HeadlessFilterFlags = WorldSystemFilterFlags.Default
                                                                               | WorldSystemFilterFlags.Editor
                                                                               | WorldSystemFilterFlags.Streaming
                                                                               | WorldSystemFilterFlags.ProcessAfterLoad
                                                                               | WorldSystemFilterFlags.EntitySceneOptimizations;

        private static readonly string[] RootGroupTypeNames =
        {
            "PureDOTS.Systems.TimeSystemGroup, PureDOTS.Systems",
            "PureDOTS.Systems.EnvironmentSystemGroup, PureDOTS.Systems",
            "PureDOTS.Systems.SpatialSystemGroup, PureDOTS.Systems",
            "PureDOTS.Systems.GameplaySystemGroup, PureDOTS.Systems",
            "PureDOTS.Systems.HistorySystemGroup, PureDOTS.Systems"
        };

        private static ExitPolicy s_exitPolicy = ExitPolicy.InvariantsAndDeterminism;
        private static bool s_exitPolicyResolved;
        private static ExitPolicy GetExitPolicy()
        {
            if (!s_exitPolicyResolved)
            {
                s_exitPolicy = ScenarioExitUtility.ResolveExitPolicy();
                s_exitPolicyResolved = true;
                Debug.Log($"[ScenarioRunner] Exit policy set to {s_exitPolicy}");
            }

            return s_exitPolicy;
        }

        public static ScenarioRunResult RunFromFile(string scenarioPath, string reportPath = null)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath) || !File.Exists(scenarioPath))
            {
                throw new FileNotFoundException($"Scenario file not found: {scenarioPath}", scenarioPath);
            }

            var json = File.ReadAllText(scenarioPath);
            if (!ScenarioRunner.TryParse(json, out var data, out var parseError))
            {
                throw new InvalidOperationException($"Scenario parse failed: {parseError}");
            }

            return Run(data, scenarioPath, reportPath);
        }

        public static ScenarioRunResult Run(ScenarioDefinitionData data, string sourceLabel = "inline", string reportPath = null)
        {
            if (!ScenarioRunner.TryBuild(data, Allocator.Temp, out var scenario, out var buildError))
            {
                throw new InvalidOperationException($"Scenario build failed: {buildError}");
            }

            using (scenario)
            {
                ScenarioRunRecorder.Initialize(in scenario, sourceLabel, FixedDeltaTime);
                ScenarioRunResult result = default;
                try
                {
                    result = ExecuteScenario(in scenario);
                    WriteReport(reportPath, result);
                    Debug.Log($"ScenarioRunner: completed {scenario.ScenarioId} ({sourceLabel}) ticks={scenario.RunTicks} commands={scenario.InputCommands.Length} snapshots={result.SnapshotLogCount} frameBudgetExceeded={result.FrameTimingBudgetExceeded}");
                    return result;
                }
                finally
                {
                    ScenarioRunRecorder.CompleteRun(result);
                }
            }
        }

        private static ScenarioRunResult ExecuteScenario(in ResolvedScenario scenario)
        {
            ScenarioRunIssueReporter.BeginRun();
            var exitPolicy = GetExitPolicy();
            using var world = CreateWorld("ScenarioWorld");
            var result = new ScenarioRunResult
            {
                ScenarioId = scenario.ScenarioId.ToString(),
                RunTicks = scenario.RunTicks,
                Seed = scenario.Seed,
                EntityCountEntries = scenario.EntityCounts.Length,
                Metrics = new List<ScenarioMetric>(8),
                ExitPolicy = exitPolicy,
                HighestSeverity = ScenarioSeverity.Info
            };

            DefaultWorldInitializationInitializationHook(world);
            InjectScenarioMetadata(world.EntityManager, in scenario);
            EnsureScenarioState(world.EntityManager);
            var scenarioTickEntity = EnsureScenarioTick(world.EntityManager);

            var initGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simulationGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var fixedStepGroup = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            var lateSimulationGroup = world.GetOrCreateSystemManaged<LateSimulationSystemGroup>();

            var entityManager = world.EntityManager;

            // Warm-up initialization once to seed singletons (CoreSingletonBootstrapSystem runs here).
            world.Unmanaged.Time = new TimeData(FixedDeltaTime, 0);
            initGroup.Update();
            ScenarioRunRecorder.TryWriteRunHeader(entityManager);

            using (var commandQueue = BuildCommandLookup(in scenario))
            {
                var rewindEntity = ResolveRewindEntity(world.EntityManager);

                using (RunMarker.Auto())
                {
                    for (int i = 0; i < scenario.RunTicks; i++)
                    {
                        var elapsed = (i + 1) * FixedDeltaTime;
                        world.Unmanaged.Time = new TimeData(FixedDeltaTime, elapsed);
                        UpdateScenarioTick(world.EntityManager, scenarioTickEntity, (uint)(i + 1), elapsed);

                        if (rewindEntity == Entity.Null || !world.EntityManager.Exists(rewindEntity) ||
                            !world.EntityManager.HasComponent<RewindState>(rewindEntity))
                        {
                            rewindEntity = ResolveRewindEntity(world.EntityManager);
                        }

                        FlushCommandsForTick(world.EntityManager, rewindEntity, commandQueue, i);
                        EnsureScenarioRewindMode(world.EntityManager, rewindEntity);

                        initGroup.Update();
                        fixedStepGroup?.Update();
                        simulationGroup?.Update();
                        lateSimulationGroup?.Update();

                        ScenarioRunRecorder.RecordDigest(entityManager);
                    }
                }

                PopulateTelemetry(world.EntityManager, rewindEntity, ref result);
                ApplyScenarioMetricsAndAssertionsInternal(world.EntityManager, in scenario, ref result);
            }

            TryEmitBankResult(world.EntityManager, in scenario);
            ScenarioRunIssueReporter.FlushToResult(ref result);
            return result;
        }

        private static World CreateWorld(string name)
        {
            var world = new World(name, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;

            var systems = ResolveSystems();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

            // Ensure ECBs exist and groups sorted similarly to the bootstrap.
            world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            if (world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>() is { } fixedStepGroup)
            {
                fixedStepGroup.Timestep = FixedDeltaTime;
                fixedStepGroup.SortSystems();
            }

            world.GetOrCreateSystemManaged<SimulationSystemGroup>()?.SortSystems();
            world.GetOrCreateSystemManaged<InitializationSystemGroup>()?.SortSystems();
            world.GetOrCreateSystemManaged<LateSimulationSystemGroup>()?.SortSystems();

            return world;
        }

        private static void DefaultWorldInitializationInitializationHook(World world)
        {
            // Mirror bootstrap: ensure root groups are materialized for downstream lookups.
            foreach (var typeName in RootGroupTypeNames)
            {
                var type = Type.GetType(typeName);
                if (type != null && typeof(ComponentSystemGroup).IsAssignableFrom(type))
                {
                    world.GetOrCreateSystemManaged(type);
                }
            }
        }

        private static NativeParallelMultiHashMap<int, ScenarioInputCommand> BuildCommandLookup(in ResolvedScenario scenario)
        {
            var map = new NativeParallelMultiHashMap<int, ScenarioInputCommand>(scenario.InputCommands.Length, Allocator.Temp);
            for (int i = 0; i < scenario.InputCommands.Length; i++)
            {
                var command = scenario.InputCommands[i];
                map.Add(command.Tick, command);
            }
            return map;
        }

        private static Entity EnsureScenarioTick(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioRunnerTick>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity(typeof(ScenarioRunnerTick));
            entityManager.SetComponentData(entity, new ScenarioRunnerTick
            {
                Tick = 0,
                WorldSeconds = 0f
            });
            return entity;
        }

        private static void TryEmitBankResult(EntityManager entityManager, in ResolvedScenario scenario)
        {
            const string scenarioId = "scenario.time.rewind_short";
            const string bankId = "P0.TIME_REWIND_MICRO";

            if (!string.Equals(scenario.ScenarioId.ToString(), scenarioId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var timeResult = ResolveTimeControlResult(entityManager, out var timeTick);
            var rewindResult = ResolveRewindResult(entityManager);
            var scenarioTick = ResolveScenarioTick(entityManager);
            if (timeTick == 0)
            {
                timeTick = ResolveTimeTick(entityManager);
            }

            var delta = (int)timeTick - (int)scenarioTick;
            if (timeResult == 1 && rewindResult == 1)
            {
                Debug.Log($"BANK:{bankId}:PASS tickTime={timeTick} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            var reason = ResolveBankFailureReason(timeResult, rewindResult);
            Debug.Log($"BANK:{bankId}:FAIL reason={reason} tickTime={timeTick} scenarioTick={scenarioTick} delta={delta}");
        }

        private static byte ResolveTimeControlResult(EntityManager entityManager, out uint tick)
        {
            tick = 0;
            if (!TryGetSingleton(entityManager, out HeadlessTimeControlProofState proof))
            {
                return 0;
            }

            tick = proof.Tick;
            return proof.Result;
        }

        private static byte ResolveRewindResult(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out HeadlessRewindProofState proof))
            {
                return 0;
            }

            return proof.Result;
        }

        private static uint ResolveScenarioTick(EntityManager entityManager)
        {
            return TryGetSingleton(entityManager, out ScenarioRunnerTick tick) ? tick.Tick : 0u;
        }

        private static uint ResolveTimeTick(EntityManager entityManager)
        {
            if (TryGetSingleton(entityManager, out TickTimeState tickTime))
            {
                return tickTime.Tick;
            }

            return TryGetSingleton(entityManager, out TimeState timeState) ? timeState.Tick : 0u;
        }

        private static string ResolveBankFailureReason(byte timeResult, byte rewindResult)
        {
            if (timeResult == 2)
            {
                return "time_fail";
            }

            if (rewindResult == 2)
            {
                return "rewind_fail";
            }

            if (timeResult == 0)
            {
                return "time_missing";
            }

            if (rewindResult == 0)
            {
                return "rewind_missing";
            }

            return "unknown";
        }

        private static bool TryGetSingleton<T>(EntityManager entityManager, out T component)
            where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                component = default;
                return false;
            }

            component = query.GetSingleton<T>();
            return true;
        }

        private static void UpdateScenarioTick(EntityManager entityManager, Entity tickEntity, uint tick, float worldSeconds)
        {
            if (tickEntity == Entity.Null || !entityManager.Exists(tickEntity))
            {
                return;
            }

            entityManager.SetComponentData(tickEntity, new ScenarioRunnerTick
            {
                Tick = tick,
                WorldSeconds = worldSeconds
            });
        }

        private static Entity ResolveRewindEntity(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (query.IsEmpty)
            {
                throw new InvalidOperationException("Scenario runner expected RewindState singleton to exist after initialization.");
            }
            return query.GetSingletonEntity();
        }

        private static void FlushCommandsForTick(EntityManager entityManager, Entity rewindEntity, NativeParallelMultiHashMap<int, ScenarioInputCommand> commands, int currentTick)
        {
            if (!commands.TryGetFirstValue(currentTick, out var command, out var iterator))
            {
                return;
            }

            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var buffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            do
            {
                if (TryTranslateCommand(command, out var translated))
                {
                    buffer.Add(translated);
                }
            } while (commands.TryGetNextValue(out command, ref iterator));
        }

        private static void EnsureScenarioRewindMode(EntityManager entityManager, Entity rewindEntity)
        {
            if (rewindEntity == Entity.Null || !entityManager.Exists(rewindEntity) || !entityManager.HasComponent<RewindState>(rewindEntity))
            {
                return;
            }

            var rewindState = entityManager.GetComponentData<RewindState>(rewindEntity);
            if (rewindState.Mode == RewindMode.Paused)
            {
                // ScenarioRunner relies on TickTimeState for pause control; RewindState should stay in Record unless rewinding.
                rewindState.Mode = RewindMode.Record;
                entityManager.SetComponentData(rewindEntity, rewindState);
            }
        }

        private static bool TryTranslateCommand(in ScenarioInputCommand command, out TimeControlCommand timeCommand)
        {
            timeCommand = default;
            var id = command.CommandId.ToString().ToLowerInvariant();

            switch (id)
            {
                case "time.pause":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.Pause };
                    return true;
                case "time.play":
                case "time.resume":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.Resume };
                    return true;
                case "time.step":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.StepTicks, UintParam = ParseUInt(command.Payload, 1) };
                    return true;
                case "time.setspeed":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.SetSpeed, FloatParam = ParseFloat(command.Payload, 1f) };
                    return true;
                case "time.rewind":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.StartRewind, UintParam = ParseUInt(command.Payload, 0) };
                    return true;
                case "time.stoprewind":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.StopRewind };
                    return true;
                case "time.scrub":
                    timeCommand = new TimeControlCommand { Type = TimeControlCommandType.ScrubTo, UintParam = ParseUInt(command.Payload, 0) };
                    return true;
                default:
                    Debug.LogWarning($"ScenarioRunner: unknown command id {id}");
                    return false;
            }
        }

        private static uint ParseUInt(in FixedString64Bytes payload, uint fallback)
        {
            if (uint.TryParse(payload.ToString(), out var value))
            {
                return value;
            }
            return fallback;
        }

        private static float ParseFloat(in FixedString64Bytes payload, float fallback)
        {
            if (float.TryParse(payload.ToString(), out var value))
            {
                return value;
            }
            return fallback;
        }

        private static void PopulateTelemetry(EntityManager entityManager, Entity rewindEntity, ref ScenarioRunResult result)
        {
            using (var scenarioQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioState>()))
            {
                if (scenarioQuery.IsEmptyIgnoreFilter)
                {
                    ScenarioExitUtility.ReportScenarioContract("ScenarioStateMissing", "ScenarioState singleton missing after scenario run.");
                }
                else
                {
                    var scenarioState = scenarioQuery.GetSingleton<ScenarioState>();
                    if (!scenarioState.IsInitialized)
                    {
                        ScenarioExitUtility.ReportScenarioContract("ScenarioStateUninitialized", "Scenario never reached initialized state.");
                    }

                    if (scenarioState.BootPhase != ScenarioBootPhase.Done)
                    {
                        ScenarioRunIssueReporter.Report(ScenarioIssueKind.ScenarioContract, ScenarioSeverity.Warn, "ScenarioBootIncomplete", $"Boot phase = {scenarioState.BootPhase}");
                    }
                }
            }

            using (var tickQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>()))
            {
                if (tickQuery.TryGetSingleton(out TickTimeState tickTime))
                {
                    result.FinalTick = tickTime.Tick;
                }
                else
                {
                    Debug.LogWarning($"[ScenarioRunner] TickTimeState singleton missing or not unique (count={tickQuery.CalculateEntityCount()}). FinalTick left at {result.FinalTick}.");
                }
            }

            if (entityManager.HasComponent<TelemetryStream>(rewindEntity))
            {
                result.TelemetryVersion = entityManager.GetComponentData<TelemetryStream>(rewindEntity).Version;
            }

            if (entityManager.HasComponent<DebugDisplayData>(rewindEntity))
            {
                var debug = entityManager.GetComponentData<DebugDisplayData>(rewindEntity);
                result.CommandLogCount = debug.CommandLogCount;
                result.SnapshotLogCount = debug.SnapshotLogCount;
                result.FrameTimingBudgetExceeded = debug.FrameTimingBudgetExceeded;
                result.FrameTimingWorstMs = debug.FrameTimingWorstDurationMs;
                result.FrameTimingWorstGroup = debug.FrameTimingWorstGroup.ToString();
                result.RegistryContinuityFailures = debug.RegistryContinuityFailureCount;
                result.RegistryContinuityWarnings = debug.RegistryContinuityWarningCount;

                if (result.RegistryContinuityFailures > 0)
                {
                    ScenarioExitUtility.ReportScenarioContract("RegistryContinuityFailure", $"Registry continuity failures detected: {result.RegistryContinuityFailures}");
                }
                else if (result.RegistryContinuityWarnings > 0)
                {
                    ScenarioRunIssueReporter.Report(ScenarioIssueKind.ScenarioContract, ScenarioSeverity.Warn, "RegistryContinuityWarn", $"Registry continuity warnings detected: {result.RegistryContinuityWarnings}");
                }

                if (result.FrameTimingBudgetExceeded)
                {
                    ScenarioExitUtility.ReportPerformance("FrameTimingBudget", $"Frame budget exceeded ({result.FrameTimingWorstGroup}) worst={result.FrameTimingWorstMs:F2}ms");
                }
            }

            if (entityManager.HasComponent<InputCommandLogState>(rewindEntity) && entityManager.HasComponent<TickSnapshotLogState>(rewindEntity))
            {
                var commandState = entityManager.GetComponentData<InputCommandLogState>(rewindEntity);
                var snapshotState = entityManager.GetComponentData<TickSnapshotLogState>(rewindEntity);
                result.CommandCapacity = commandState.Capacity;
                result.SnapshotCapacity = snapshotState.Capacity;
                result.CommandBytes = commandState.Capacity * UnsafeUtility.SizeOf<InputCommandLogEntry>();
                result.SnapshotBytes = snapshotState.Capacity * UnsafeUtility.SizeOf<TickSnapshotLogEntry>();
                result.TotalLogBytes = result.CommandBytes + result.SnapshotBytes;
            }

            using (var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()))
            {
                if (timeQuery.TryGetSingletonEntity<TimeState>(out var timeEntity) &&
                    entityManager.HasComponent<PerformanceBudgetStatus>(timeEntity))
                {
                    var budgetStatus = entityManager.GetComponentData<PerformanceBudgetStatus>(timeEntity);
                    if (budgetStatus.HasFailure != 0)
                    {
                        result.PerformanceBudgetFailed = true;
                        result.PerformanceBudgetMetric = budgetStatus.Metric.ToString();
                        result.PerformanceBudgetValue = budgetStatus.ObservedValue;
                        result.PerformanceBudgetLimit = budgetStatus.BudgetValue;
                        result.PerformanceBudgetTick = budgetStatus.Tick;

                        var message = $"Performance budget failure ({result.PerformanceBudgetMetric}) at tick {result.PerformanceBudgetTick}: value={result.PerformanceBudgetValue:F2}, budget={result.PerformanceBudgetLimit:F2}";
                        ScenarioExitUtility.ReportPerformance("PerformanceBudget", message);
                    }
                }
            }

            TryCollectSpace4XMiningTelemetry(entityManager, ref result);
        }

        private static void ApplyScenarioMetricsAndAssertionsInternal(
            EntityManager entityManager,
            in ResolvedScenario scenario,
            ref ScenarioRunResult result)
        {
            if (!ScenarioMetricsUtility.TryGetMetricsBuffer(entityManager, out var metricBuffer))
            {
                if (scenario.Assertions.Length == 0)
                {
                    return;
                }
            }

            var metricMap = new NativeHashMap<FixedString64Bytes, double>(
                math.max(1, metricBuffer.IsCreated ? metricBuffer.Length : 0),
                Allocator.Temp);
            try
            {
                if (metricBuffer.IsCreated)
                {
                    for (int i = 0; i < metricBuffer.Length; i++)
                    {
                        var sample = metricBuffer[i];
                        metricMap[sample.Key] = sample.Value;
                        result.AddMetric(sample.Key.ToString(), sample.Value);
                    }

                    metricBuffer.Clear();
                }

                if (scenario.Assertions.Length == 0)
                {
                    return;
                }

                var assertionResults = new NativeList<ScenarioAssertionResult>(Allocator.Temp);
                try
                {
                    ValidateAssertions(in scenario.Assertions, in metricMap, ref assertionResults);
                    if (assertionResults.Length == 0)
                    {
                        return;
                    }

                    result.AssertionResults ??= new List<ScenarioAssertionReport>(assertionResults.Length);
                    for (int i = 0; i < assertionResults.Length; i++)
                    {
                        var nativeResult = assertionResults[i];
                        result.AssertionResults.Add(ScenarioAssertionReport.FromNative(nativeResult));

                        if (!nativeResult.Passed)
                        {
                            var message = nativeResult.FailureMessage.ToString();
                            if (string.IsNullOrWhiteSpace(message))
                            {
                                message = $"Assertion failed for metric {nativeResult.MetricId}";
                            }

                            ScenarioExitUtility.ReportScenarioContract("ScenarioAssertionFailed", message);
                        }
                    }
                }
                finally
                {
                    assertionResults.Dispose();
                }
            }
            finally
            {
                metricMap.Dispose();
            }
        }

        private static void ValidateAssertions(
            in NativeList<ScenarioAssertion> assertions,
            in NativeHashMap<FixedString64Bytes, double> metrics,
            ref NativeList<ScenarioAssertionResult> results)
        {
            results.Clear();

            for (int i = 0; i < assertions.Length; i++)
            {
                var assertion = assertions[i];
                var metricId = assertion.MetricId;

                if (!metrics.TryGetValue(metricId, out var actualValue))
                {
                    results.Add(new ScenarioAssertionResult
                    {
                        MetricId = metricId,
                        Passed = false,
                        ActualValue = 0.0,
                        ExpectedValue = assertion.ExpectedValue,
                        Operator = assertion.Operator,
                        FailureMessage = new FixedString128Bytes($"Metric '{metricId}' not found")
                    });
                    continue;
                }

                var result = ScenarioAssertionEvaluator.Validate(assertion, actualValue);
                results.Add(result);
            }
        }

        private static readonly string Space4XTelemetryTypeName = "Space4X.Registry.Space4XMiningTelemetry";

        private static Type s_space4xTelemetryType;
        private static FieldInfo s_space4xOreInHoldField;
        private static FieldInfo s_space4xOreDepositedField;
        private static FieldInfo s_space4xStorehouseInventoryField;
        private static MethodInfo s_getComponentDataMethod;

        private static void TryCollectSpace4XMiningTelemetry(EntityManager entityManager, ref ScenarioRunResult result)
        {
            EnsureSpace4XReflectionHandles();
            if (s_space4xTelemetryType == null || s_getComponentDataMethod == null)
            {
                Debug.LogWarning("[ScenarioRunner] Space4XMiningTelemetry type or accessor missing; skipping mining metrics.");
                return;
            }

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly(s_space4xTelemetryType));
            if (query.IsEmptyIgnoreFilter)
            {
                Debug.LogWarning("[ScenarioRunner] Space4XMiningTelemetry query returned no entities.");
                return;
            }

            var telemetryEntity = query.GetSingletonEntity();
            var telemetry = s_getComponentDataMethod
                .MakeGenericMethod(s_space4xTelemetryType)
                .Invoke(entityManager, new object[] { telemetryEntity });

            if (telemetry == null)
            {
                Debug.LogWarning("[ScenarioRunner] Space4XMiningTelemetry reflection returned null.");
                return;
            }

            var appended = false;
            if (s_space4xOreInHoldField != null)
            {
                var value = Convert.ToDouble(s_space4xOreInHoldField.GetValue(telemetry));
                result.AddMetric("space4x.mining.oreInHold", value);
                appended = true;
            }

            if (s_space4xOreDepositedField != null)
            {
                var value = Convert.ToDouble(s_space4xOreDepositedField.GetValue(telemetry));
                result.AddMetric("space4x.mining.oreDeposited", value);
                appended = true;
            }

            if (s_space4xStorehouseInventoryField != null)
            {
                var value = Convert.ToDouble(s_space4xStorehouseInventoryField.GetValue(telemetry));
                result.AddMetric("space4x.mining.storehouseInventory", value);
                appended = true;
            }

            if (!appended)
            {
                Debug.LogWarning("[ScenarioRunner] Space4XMiningTelemetry fields were missing; no mining metrics recorded.");
            }
            else
            {
                Debug.Log("[ScenarioRunner] Space4X mining metrics appended to scenario report.");
            }
        }

        private static void EnsureSpace4XReflectionHandles()
        {
            if (s_getComponentDataMethod == null)
            {
                s_getComponentDataMethod = typeof(EntityManager)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == nameof(EntityManager.GetComponentData)
                                         && m.IsGenericMethodDefinition
                                         && m.GetGenericArguments().Length == 1
                                         && m.GetParameters().Length == 1
                                         && m.GetParameters()[0].ParameterType == typeof(Entity));
            }

            if (s_space4xTelemetryType != null)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var resolved = assembly.GetType(Space4XTelemetryTypeName);
                if (resolved != null)
                {
                    s_space4xTelemetryType = resolved;
                    s_space4xOreInHoldField = resolved.GetField("OreInHold", BindingFlags.Public | BindingFlags.Instance);
                    s_space4xOreDepositedField = resolved.GetField("OreDeposited", BindingFlags.Public | BindingFlags.Instance);
                    s_space4xStorehouseInventoryField = resolved.GetField("StorehouseInventory", BindingFlags.Public | BindingFlags.Instance);
                    break;
                }
            }
        }

        private static void InjectScenarioMetadata(EntityManager entityManager, in ResolvedScenario scenario)
        {
            var entity = entityManager.CreateEntity(typeof(ScenarioInfo), typeof(ScenarioEntityCountElement));
            entityManager.SetComponentData(entity, new ScenarioInfo
            {
                ScenarioId = scenario.ScenarioId,
                Seed = scenario.Seed,
                RunTicks = scenario.RunTicks
            });

            var buffer = entityManager.GetBuffer<ScenarioEntityCountElement>(entity);
            for (int i = 0; i < scenario.EntityCounts.Length; i++)
            {
                buffer.Add(new ScenarioEntityCountElement
                {
                    RegistryId = scenario.EntityCounts[i].RegistryId,
                    Count = scenario.EntityCounts[i].Count
                });
            }

            if (scenario.HasBehaviorOverride)
            {
                var overrideEntity = entityManager.CreateEntity(typeof(BehaviorScenarioOverrideComponent));
                entityManager.SetComponentData(overrideEntity, new BehaviorScenarioOverrideComponent
                {
                    Value = scenario.BehaviorOverride
                });
            }

            if (scenario.HasTelemetryOverride)
            {
                var overrideEntity = entityManager.CreateEntity(typeof(TelemetryScenarioOverrideComponent));
                entityManager.SetComponentData(overrideEntity, new TelemetryScenarioOverrideComponent
                {
                    Value = scenario.TelemetryOverride
                });
            }
        }

        private static void EnsureScenarioState(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioState>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(ScenarioState));
                entityManager.SetComponentData(entity, new ScenarioState
                {
                    Current = ScenarioKind.AllSystemsShowcase,
                    IsInitialized = true,
                    BootPhase = ScenarioBootPhase.Done,
                    EnableGodgame = true,
                    EnableSpace4x = true,
                    EnableEconomy = false
                });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                var state = entityManager.GetComponentData<ScenarioState>(entity);
                state.IsInitialized = true;
                state.BootPhase = ScenarioBootPhase.Done;
                entityManager.SetComponentData(entity, state);
            }
        }

        private static void WriteReport(string reportPath, in ScenarioRunResult result)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                return;
            }

            var extension = Path.GetExtension(reportPath).ToLowerInvariant();
            if (extension == ".csv")
            {
                ScenarioRunResultCsv.Write(reportPath, result);
            }
            else
            {
                var serialized = ScenarioRunResultJson.Serialize(result);
                File.WriteAllText(reportPath, serialized);
            }
        }

        private static IReadOnlyList<Type> ResolveSystems()
        {
            if (TryResolveSystemsFromRegistry(out var systemsFromRegistry))
            {
                return systemsFromRegistry;
            }

            return DefaultWorldInitialization
                .GetAllSystems(HeadlessFilterFlags)
                .Where(t => t != typeof(Unity.Entities.PresentationSystemGroup))
                .ToArray();
        }

        private static bool TryResolveSystemsFromRegistry(out IReadOnlyList<Type> systems)
        {
            systems = null;

            var registryType = Type.GetType("PureDOTS.Systems.SystemRegistry, PureDOTS.Systems");
            if (registryType == null)
            {
                return false;
            }

            var builtinProfiles = registryType.GetNestedType("BuiltinProfiles", BindingFlags.Public | BindingFlags.Static);
            var headlessProperty = builtinProfiles?.GetProperty("Headless", BindingFlags.Public | BindingFlags.Static);
            var getSystems = registryType.GetMethod("GetSystems", BindingFlags.Public | BindingFlags.Static);

            var profile = headlessProperty?.GetValue(null);
            if (profile == null || getSystems == null)
            {
                return false;
            }

            if (getSystems.Invoke(null, new[] { profile }) is IReadOnlyList<Type> resolved)
            {
                systems = resolved;
                return true;
            }

            return false;
        }

        private static ComponentSystemGroup TryGetGroup(World world, string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null || !typeof(ComponentSystemGroup).IsAssignableFrom(type))
            {
                return null;
            }

            return world.GetOrCreateSystemManaged(type) as ComponentSystemGroup;
        }
    }

    public struct ScenarioRunResult
    {
        public string ScenarioId;
        public uint Seed;
        public int RunTicks;
        public uint FinalTick;
        public uint TelemetryVersion;
        public int CommandLogCount;
        public int SnapshotLogCount;
        public bool FrameTimingBudgetExceeded;
        public float FrameTimingWorstMs;
        public string FrameTimingWorstGroup;
        public int RegistryContinuityWarnings;
        public int RegistryContinuityFailures;
        public int EntityCountEntries;
        public int CommandCapacity;
        public int SnapshotCapacity;
        public int CommandBytes;
        public int SnapshotBytes;
        public int TotalLogBytes;
        public bool PerformanceBudgetFailed;
        public string PerformanceBudgetMetric;
        public float PerformanceBudgetValue;
        public float PerformanceBudgetLimit;
        public uint PerformanceBudgetTick;
        public List<ScenarioMetric> Metrics;
        public List<ScenarioRunIssue> Issues;
        public List<ScenarioAssertionReport> AssertionResults;
        public ScenarioSeverity HighestSeverity;
        public ExitPolicy ExitPolicy;

        public void AddMetric(string key, double value)
        {
            if (Metrics == null)
            {
                Metrics = new List<ScenarioMetric>();
            }

            Metrics.Add(new ScenarioMetric
            {
                Key = string.IsNullOrEmpty(key) ? string.Empty : key,
                Value = value
            });
        }

        public override string ToString()
        {
            return $"scenarioId={ScenarioId}, seed={Seed}, runTicks={RunTicks}, finalTick={FinalTick}, commands={CommandLogCount}, snapshots={SnapshotLogCount}, frameBudgetExceeded={FrameTimingBudgetExceeded}, perfBudgetFailed={PerformanceBudgetFailed}";
        }
    }

    public struct ScenarioMetric
    {
        public string Key;
        public double Value;
    }

    /// <summary>
    /// Managed-friendly assertion payload included in ScenarioRunResult output.
    /// </summary>
    public struct ScenarioAssertionReport
    {
        public string MetricId;
        public bool Passed;
        public double ActualValue;
        public double ExpectedValue;
        public ScenarioAssertionOperator Operator;
        public string FailureMessage;

        public static ScenarioAssertionReport FromNative(in ScenarioAssertionResult result)
        {
            return new ScenarioAssertionReport
            {
                MetricId = result.MetricId.ToString(),
                Passed = result.Passed,
                ActualValue = result.ActualValue,
                ExpectedValue = result.ExpectedValue,
                Operator = result.Operator,
                FailureMessage = result.FailureMessage.ToString()
            };
        }
    }
}

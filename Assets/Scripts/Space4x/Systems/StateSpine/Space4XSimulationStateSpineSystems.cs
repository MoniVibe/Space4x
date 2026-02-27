using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Space4X.Modes;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.StateSpine
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class Space4XSimulationStateSpineSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XSimulationStateSpineBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XSimulationStateSpineRootTag>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entityManager = state.EntityManager;
            var root = entityManager.CreateEntity(
                typeof(Space4XSimulationStateSpineRootTag),
                typeof(Space4XSimulationStateSpineMeta),
                typeof(Space4XSimulationStateSpineConfig),
                typeof(Space4XSimulationStateSpineState),
                typeof(Space4XSimulationStateSpineOverflow),
                typeof(Space4XSimulationStateSpineDeterminism),
                typeof(Space4XSimulationStateSpineTransitionGuard));

            entityManager.SetComponentData(root, new Space4XSimulationStateSpineMeta
            {
                SchemaVersion = Space4XSimulationStateSpineMeta.CurrentSchemaVersion,
                RegistryVersion = Space4XSimulationStateSpineMeta.CurrentRegistryVersion
            });
            entityManager.SetComponentData(root, Space4XSimulationStateSpineConfig.Default);
            entityManager.SetComponentData(root, new Space4XSimulationStateSpineState
            {
                Lifecycle = Space4XSimulationLifecycle.Boot,
                DeterministicLane = 1,
                Mode = byte.MaxValue,
                Reserved0 = 0,
                LastTick = 0u,
                ScenarioSeed = 0u,
                ScenarioHash32 = 0u,
                LastEventSerial = 0u,
                LastTransitionSerial = 0u
            });
            entityManager.SetComponentData(root, default(Space4XSimulationStateSpineOverflow));
            entityManager.SetComponentData(root, new Space4XSimulationStateSpineDeterminism
            {
                Version = Space4XSimulationStateSpineDeterminism.CurrentVersion,
                RunningDigest = 0u,
                LastTickDigest = 0u,
                LastTick = 0u,
                TicksDigested = 0u,
                EventsDigested = 0u
            });
            entityManager.SetComponentData(root, new Space4XSimulationStateSpineTransitionGuard
            {
                InvalidLifecycleTransitions = 0u,
                InvalidScenarioTransitions = 0u,
                InvalidModeTransitions = 0u,
                LastInvalidTick = 0u,
                BlockedScenarioSeed = 0u,
                BlockedScenarioHash32 = 0u,
                BlockedMode = uint.MaxValue
            });
            entityManager.AddBuffer<Space4XSimulationStateSpineEvent>(root);
            entityManager.AddBuffer<Space4XSimulationStateSpineEventInbox>(root);
            entityManager.AddBuffer<Space4XSimulationStateSpineDeterminismProbe>(root);
            entityManager.AddBuffer<Space4XSimulationStateSpineTransition>(root);

            state.Enabled = false;
        }
    }

    [UpdateInGroup(typeof(Space4XSimulationStateSpineSystemGroup))]
    public partial struct Space4XSimulationStateSpineTickSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XSimulationStateSpineRootTag>();
            state.RequireForUpdate<Space4XSimulationStateSpineState>();
            state.RequireForUpdate<Space4XSimulationStateSpineConfig>();
            state.RequireForUpdate<Space4XSimulationStateSpineOverflow>();
            state.RequireForUpdate<Space4XSimulationStateSpineDeterminism>();
            state.RequireForUpdate<Space4XSimulationStateSpineTransitionGuard>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var root = SystemAPI.GetSingletonEntity<Space4XSimulationStateSpineRootTag>();
            var config = SystemAPI.GetComponentRW<Space4XSimulationStateSpineConfig>(root);
            if (config.ValueRO.Enabled == 0)
            {
                return;
            }

            var runtime = SystemAPI.GetComponentRW<Space4XSimulationStateSpineState>(root);
            var overflow = SystemAPI.GetComponentRW<Space4XSimulationStateSpineOverflow>(root);
            var determinism = SystemAPI.GetComponentRW<Space4XSimulationStateSpineDeterminism>(root);
            var transitionGuard = SystemAPI.GetComponentRW<Space4XSimulationStateSpineTransitionGuard>(root);
            var inbox = SystemAPI.GetBuffer<Space4XSimulationStateSpineEventInbox>(root);
            var history = SystemAPI.GetBuffer<Space4XSimulationStateSpineEvent>(root);
            var probes = SystemAPI.GetBuffer<Space4XSimulationStateSpineDeterminismProbe>(root);
            var transitions = SystemAPI.GetBuffer<Space4XSimulationStateSpineTransition>(root);

            runtime.ValueRW.LastTick = timeState.Tick;
            overflow.ValueRW.DroppedThisTick = 0u;

            var lifecycleTarget = ResolveLifecycleTarget(runtime.ValueRO.Lifecycle, timeState.IsPaused);
            if (lifecycleTarget != runtime.ValueRO.Lifecycle)
            {
                TryApplyLifecycleTransition(
                    ref runtime.ValueRW,
                    ref transitionGuard.ValueRW,
                    transitions,
                    in config.ValueRO,
                    timeState.Tick,
                    lifecycleTarget,
                    runtime.ValueRO.Lifecycle == Space4XSimulationLifecycle.Boot
                        ? Space4XSimulationStateTransitionReason.BootAdvance
                        : Space4XSimulationStateTransitionReason.PauseState);
            }

            if (TryResolveCurrentMode(out var observedMode) && observedMode != runtime.ValueRO.Mode)
            {
                TryApplyModeTransition(
                    ref runtime.ValueRW,
                    ref transitionGuard.ValueRW,
                    transitions,
                    in config.ValueRO,
                    timeState.Tick,
                    observedMode,
                    Space4XSimulationStateTransitionReason.ModeObserved);
            }

            if (SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                var observedScenarioSeed = scenarioInfo.Seed;
                var observedScenarioHash = ComputeScenarioHash(in scenarioInfo);
                if (observedScenarioSeed != runtime.ValueRO.ScenarioSeed || observedScenarioHash != runtime.ValueRO.ScenarioHash32)
                {
                    TryApplyScenarioTransition(
                        ref runtime.ValueRW,
                        ref transitionGuard.ValueRW,
                        transitions,
                        in config.ValueRO,
                        timeState.Tick,
                        observedScenarioSeed,
                        observedScenarioHash,
                        Space4XSimulationStateTransitionReason.ScenarioObserved);
                }
            }

            var maxIntakePerTick = math.max(1u, config.ValueRO.MaxIntakePerTick);
            var consumed = math.min(inbox.Length, (int)maxIntakePerTick);
            var tickDigest = SeedTickDigest(timeState.Tick, runtime.ValueRO.ScenarioSeed, runtime.ValueRO.ScenarioHash32, runtime.ValueRO.Lifecycle);

            for (var i = 0; i < consumed; i++)
            {
                var evt = inbox[i];
                tickDigest = MixEventEnvelope(tickDigest, in evt);

                if (config.ValueRO.RecordEventHistory != 0)
                {
                    runtime.ValueRW.LastEventSerial += 1u;
                    history.Add(new Space4XSimulationStateSpineEvent
                    {
                        Serial = runtime.ValueRW.LastEventSerial,
                        Tick = timeState.Tick,
                        DomainId = evt.DomainId,
                        FamilyId = evt.FamilyId,
                        EventTypeId = evt.EventTypeId,
                        Actor = evt.Actor,
                        Subject = evt.Subject,
                        Intensity = evt.Intensity,
                        Quality = evt.Quality,
                        ContextFlags = evt.ContextFlags
                    });
                }
            }

            var dropped = 0u;
            if (consumed < inbox.Length)
            {
                dropped = (uint)(inbox.Length - consumed);
                overflow.ValueRW.DroppedThisTick = dropped;
                overflow.ValueRW.DroppedTotal += dropped;
                overflow.ValueRW.LastOverflowTick = timeState.Tick;
            }

            inbox.Clear();

            if (config.ValueRO.RecordEventHistory != 0)
            {
                var maxRetainedEvents = math.max(16u, config.ValueRO.MaxRetainedEvents);
                if (history.Length > maxRetainedEvents)
                {
                    var removeCount = history.Length - (int)maxRetainedEvents;
                    history.RemoveRange(0, removeCount);
                }
            }

            tickDigest = Mix(tickDigest, (uint)consumed);
            tickDigest = Mix(tickDigest, dropped);
            tickDigest = Mix(tickDigest, overflow.ValueRO.DroppedTotal);
            tickDigest = Mix(tickDigest, runtime.ValueRO.LastEventSerial);

            var runningDigest = determinism.ValueRO.RunningDigest;
            if (determinism.ValueRO.Version != Space4XSimulationStateSpineDeterminism.CurrentVersion || runningDigest == 0u)
            {
                runningDigest = Mix(0x811C9DC5u, runtime.ValueRO.ScenarioSeed ^ runtime.ValueRO.ScenarioHash32);
            }

            runningDigest = Mix(runningDigest, tickDigest);
            determinism.ValueRW.Version = Space4XSimulationStateSpineDeterminism.CurrentVersion;
            determinism.ValueRW.RunningDigest = runningDigest;
            determinism.ValueRW.LastTickDigest = tickDigest;
            determinism.ValueRW.LastTick = timeState.Tick;
            determinism.ValueRW.TicksDigested += 1u;
            determinism.ValueRW.EventsDigested += (uint)consumed;

            probes.Add(new Space4XSimulationStateSpineDeterminismProbe
            {
                Tick = timeState.Tick,
                TickDigest = tickDigest,
                RunningDigest = runningDigest,
                ConsumedEvents = (uint)consumed,
                DroppedEvents = dropped,
                ScenarioSeed = runtime.ValueRO.ScenarioSeed,
                ScenarioHash32 = runtime.ValueRO.ScenarioHash32
            });

            var maxRetainedProbes = math.max(16u, config.ValueRO.MaxRetainedDeterminismProbes);
            if (probes.Length > maxRetainedProbes)
            {
                var removeCount = probes.Length - (int)maxRetainedProbes;
                probes.RemoveRange(0, removeCount);
            }
        }

        private static uint SeedTickDigest(uint tick, uint scenarioSeed, uint scenarioHash32, Space4XSimulationLifecycle lifecycle)
        {
            var digest = 0x811C9DC5u;
            digest = Mix(digest, tick);
            digest = Mix(digest, scenarioSeed);
            digest = Mix(digest, scenarioHash32);
            digest = Mix(digest, (uint)lifecycle);
            return digest;
        }

        private static uint MixEventEnvelope(uint digest, in Space4XSimulationStateSpineEventInbox evt)
        {
            digest = Mix(digest, evt.DomainId);
            digest = Mix(digest, evt.FamilyId);
            digest = Mix(digest, evt.EventTypeId);
            digest = Mix(digest, Quantize(evt.Intensity));
            digest = Mix(digest, Quantize(evt.Quality));
            digest = Mix(digest, evt.ContextFlags);
            return digest;
        }

        private static uint Mix(uint digest, uint value)
        {
            return math.hash(new uint4(
                digest ^ 0x9E3779B9u,
                value + 0x85EBCA6Bu,
                digest * 1664525u + 1013904223u,
                value ^ 0xC2B2AE35u));
        }

        private static uint Quantize(float value)
        {
            if (!math.isfinite(value))
            {
                return 0u;
            }

            var rounded = (int)math.round(value * 1000f);
            return unchecked((uint)rounded);
        }

        private static Space4XSimulationLifecycle ResolveLifecycleTarget(
            Space4XSimulationLifecycle current,
            bool isPaused)
        {
            return current switch
            {
                Space4XSimulationLifecycle.Boot => Space4XSimulationLifecycle.Ready,
                Space4XSimulationLifecycle.Ready => isPaused
                    ? Space4XSimulationLifecycle.Paused
                    : Space4XSimulationLifecycle.Running,
                Space4XSimulationLifecycle.Running => isPaused
                    ? Space4XSimulationLifecycle.Paused
                    : Space4XSimulationLifecycle.Running,
                Space4XSimulationLifecycle.Paused => isPaused
                    ? Space4XSimulationLifecycle.Paused
                    : Space4XSimulationLifecycle.Running,
                _ => current
            };
        }

        private static bool TryResolveCurrentMode(out byte mode)
        {
            mode = (byte)Space4XModeSelectionState.CurrentMode;
            return true;
        }

        private static uint ComputeScenarioHash(in ScenarioInfo scenarioInfo)
        {
            return math.hash(new uint3(
                scenarioInfo.Seed,
                (uint)scenarioInfo.RunTicks,
                (uint)scenarioInfo.ScenarioId.GetHashCode()));
        }

        private static void TryApplyLifecycleTransition(
            ref Space4XSimulationStateSpineState runtime,
            ref Space4XSimulationStateSpineTransitionGuard guard,
            DynamicBuffer<Space4XSimulationStateSpineTransition> transitions,
            in Space4XSimulationStateSpineConfig config,
            uint tick,
            Space4XSimulationLifecycle target,
            Space4XSimulationStateTransitionReason reason)
        {
            var from = runtime.Lifecycle;
            if (from == target)
            {
                return;
            }

            var valid = IsLifecycleTransitionLegal(from, target);
            if (!valid)
            {
                guard.InvalidLifecycleTransitions += 1u;
                guard.LastInvalidTick = tick;
            }

            AppendTransition(
                ref runtime,
                transitions,
                in config,
                tick,
                Space4XSimulationStateTransitionKind.Lifecycle,
                valid,
                reason,
                (uint)from,
                (uint)target,
                runtime.ScenarioSeed,
                runtime.ScenarioHash32);

            if (valid)
            {
                runtime.Lifecycle = target;
            }
        }

        private static void TryApplyModeTransition(
            ref Space4XSimulationStateSpineState runtime,
            ref Space4XSimulationStateSpineTransitionGuard guard,
            DynamicBuffer<Space4XSimulationStateSpineTransition> transitions,
            in Space4XSimulationStateSpineConfig config,
            uint tick,
            byte targetMode,
            Space4XSimulationStateTransitionReason reason)
        {
            var fromMode = runtime.Mode;
            if (fromMode == targetMode)
            {
                return;
            }

            var valid = IsModeTransitionLegal(fromMode, targetMode, runtime.Lifecycle);
            if (!valid)
            {
                if (guard.BlockedMode == targetMode)
                {
                    return;
                }

                guard.BlockedMode = targetMode;
                guard.InvalidModeTransitions += 1u;
                guard.LastInvalidTick = tick;
            }
            else
            {
                guard.BlockedMode = uint.MaxValue;
            }

            AppendTransition(
                ref runtime,
                transitions,
                in config,
                tick,
                Space4XSimulationStateTransitionKind.Mode,
                valid,
                reason,
                fromMode,
                targetMode,
                runtime.ScenarioSeed,
                runtime.ScenarioHash32);

            if (valid)
            {
                runtime.Mode = targetMode;
            }
        }

        private static void TryApplyScenarioTransition(
            ref Space4XSimulationStateSpineState runtime,
            ref Space4XSimulationStateSpineTransitionGuard guard,
            DynamicBuffer<Space4XSimulationStateSpineTransition> transitions,
            in Space4XSimulationStateSpineConfig config,
            uint tick,
            uint targetSeed,
            uint targetHash32,
            Space4XSimulationStateTransitionReason reason)
        {
            if (runtime.ScenarioSeed == targetSeed && runtime.ScenarioHash32 == targetHash32)
            {
                return;
            }

            var valid = IsScenarioTransitionLegal(
                runtime.Lifecycle,
                runtime.ScenarioSeed,
                runtime.ScenarioHash32,
                targetSeed,
                targetHash32);

            if (!valid)
            {
                if (guard.BlockedScenarioSeed == targetSeed && guard.BlockedScenarioHash32 == targetHash32)
                {
                    return;
                }

                guard.BlockedScenarioSeed = targetSeed;
                guard.BlockedScenarioHash32 = targetHash32;
                guard.InvalidScenarioTransitions += 1u;
                guard.LastInvalidTick = tick;
            }
            else
            {
                guard.BlockedScenarioSeed = 0u;
                guard.BlockedScenarioHash32 = 0u;
            }

            AppendTransition(
                ref runtime,
                transitions,
                in config,
                tick,
                Space4XSimulationStateTransitionKind.Scenario,
                valid,
                reason,
                runtime.ScenarioHash32,
                targetHash32,
                runtime.ScenarioSeed,
                targetSeed);

            if (valid)
            {
                runtime.ScenarioSeed = targetSeed;
                runtime.ScenarioHash32 = targetHash32;
            }
        }

        private static bool IsLifecycleTransitionLegal(
            Space4XSimulationLifecycle from,
            Space4XSimulationLifecycle to)
        {
            return from switch
            {
                Space4XSimulationLifecycle.Boot => to == Space4XSimulationLifecycle.Ready ||
                                                   to == Space4XSimulationLifecycle.ShuttingDown,
                Space4XSimulationLifecycle.Ready => to == Space4XSimulationLifecycle.Running ||
                                                    to == Space4XSimulationLifecycle.Paused ||
                                                    to == Space4XSimulationLifecycle.ShuttingDown,
                Space4XSimulationLifecycle.Running => to == Space4XSimulationLifecycle.Paused ||
                                                      to == Space4XSimulationLifecycle.ShuttingDown,
                Space4XSimulationLifecycle.Paused => to == Space4XSimulationLifecycle.Running ||
                                                     to == Space4XSimulationLifecycle.ShuttingDown,
                Space4XSimulationLifecycle.ShuttingDown => false,
                _ => false
            };
        }

        private static bool IsModeTransitionLegal(
            byte fromMode,
            byte toMode,
            Space4XSimulationLifecycle lifecycle)
        {
            if (!IsKnownMode(toMode))
            {
                return false;
            }

            if (fromMode == byte.MaxValue)
            {
                return true;
            }

            if (!IsKnownMode(fromMode))
            {
                return false;
            }

            return lifecycle == Space4XSimulationLifecycle.Boot ||
                   lifecycle == Space4XSimulationLifecycle.Ready ||
                   lifecycle == Space4XSimulationLifecycle.Paused;
        }

        private static bool IsScenarioTransitionLegal(
            Space4XSimulationLifecycle lifecycle,
            uint fromSeed,
            uint fromHash32,
            uint toSeed,
            uint toHash32)
        {
            if (fromSeed == 0u && fromHash32 == 0u)
            {
                return true;
            }

            if (fromSeed == toSeed && fromHash32 == toHash32)
            {
                return true;
            }

            return lifecycle == Space4XSimulationLifecycle.Boot ||
                   lifecycle == Space4XSimulationLifecycle.Ready ||
                   lifecycle == Space4XSimulationLifecycle.Paused;
        }

        private static bool IsKnownMode(byte mode)
        {
            return mode == (byte)Space4XModeKind.Classic ||
                   mode == (byte)Space4XModeKind.FleetCrawl;
        }

        private static void AppendTransition(
            ref Space4XSimulationStateSpineState runtime,
            DynamicBuffer<Space4XSimulationStateSpineTransition> transitions,
            in Space4XSimulationStateSpineConfig config,
            uint tick,
            Space4XSimulationStateTransitionKind kind,
            bool valid,
            Space4XSimulationStateTransitionReason reason,
            uint fromValue,
            uint toValue,
            uint context0,
            uint context1)
        {
            runtime.LastTransitionSerial += 1u;
            transitions.Add(new Space4XSimulationStateSpineTransition
            {
                Serial = runtime.LastTransitionSerial,
                Tick = tick,
                Kind = kind,
                Valid = valid ? (byte)1 : (byte)0,
                Reason = reason,
                Reserved0 = 0,
                FromValue = fromValue,
                ToValue = toValue,
                Context0 = context0,
                Context1 = context1
            });

            var maxRetained = math.max(16u, config.MaxRetainedTransitions);
            if (transitions.Length > maxRetained)
            {
                var removeCount = transitions.Length - (int)maxRetained;
                transitions.RemoveRange(0, removeCount);
            }
        }
    }

    [UpdateInGroup(typeof(Space4XSimulationStateSpineSystemGroup))]
    [UpdateAfter(typeof(Space4XSimulationStateSpineTickSystem))]
    public partial struct Space4XSimulationStateSpineTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricRunningDigest =
            new FixedString64Bytes("space4x.spine.determinism.digest24");

        private static readonly FixedString64Bytes MetricTickDigest =
            new FixedString64Bytes("space4x.spine.determinism.tick_digest24");

        private static readonly FixedString64Bytes MetricDigestTick =
            new FixedString64Bytes("space4x.spine.determinism.tick");

        private static readonly FixedString64Bytes MetricDigestTicks =
            new FixedString64Bytes("space4x.spine.determinism.ticks");

        private static readonly FixedString64Bytes MetricDigestEvents =
            new FixedString64Bytes("space4x.spine.determinism.events");

        private static readonly FixedString64Bytes MetricOverflowDroppedTick =
            new FixedString64Bytes("space4x.spine.overflow.dropped_tick");

        private static readonly FixedString64Bytes MetricOverflowDroppedTotal =
            new FixedString64Bytes("space4x.spine.overflow.dropped_total");

        private static readonly FixedString64Bytes MetricTransitionSerial =
            new FixedString64Bytes("space4x.spine.transition.serial");

        private static readonly FixedString64Bytes MetricTransitionInvalidLifecycle =
            new FixedString64Bytes("space4x.spine.transition.invalid.lifecycle");

        private static readonly FixedString64Bytes MetricTransitionInvalidScenario =
            new FixedString64Bytes("space4x.spine.transition.invalid.scenario");

        private static readonly FixedString64Bytes MetricTransitionInvalidMode =
            new FixedString64Bytes("space4x.spine.transition.invalid.mode");

        private static readonly FixedString64Bytes MetricLifecycle =
            new FixedString64Bytes("space4x.spine.lifecycle");

        private static readonly FixedString64Bytes MetricMode =
            new FixedString64Bytes("space4x.spine.mode");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XSimulationStateSpineRootTag>();
            state.RequireForUpdate<Space4XSimulationStateSpineDeterminism>();
            state.RequireForUpdate<Space4XSimulationStateSpineTransitionGuard>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var metricBuffer))
            {
                return;
            }

            var root = SystemAPI.GetSingletonEntity<Space4XSimulationStateSpineRootTag>();
            var determinism = SystemAPI.GetComponent<Space4XSimulationStateSpineDeterminism>(root);
            var overflow = SystemAPI.GetComponent<Space4XSimulationStateSpineOverflow>(root);
            var runtime = SystemAPI.GetComponent<Space4XSimulationStateSpineState>(root);
            var transitionGuard = SystemAPI.GetComponent<Space4XSimulationStateSpineTransitionGuard>(root);

            metricBuffer.AddMetric(MetricRunningDigest, determinism.RunningDigest & 0x00FFFFFFu, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricTickDigest, determinism.LastTickDigest & 0x00FFFFFFu, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDigestTick, determinism.LastTick, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDigestTicks, determinism.TicksDigested, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDigestEvents, determinism.EventsDigested, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricOverflowDroppedTick, overflow.DroppedThisTick, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricOverflowDroppedTotal, overflow.DroppedTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricTransitionSerial, runtime.LastTransitionSerial, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricTransitionInvalidLifecycle, transitionGuard.InvalidLifecycleTransitions, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricTransitionInvalidScenario, transitionGuard.InvalidScenarioTransitions, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricTransitionInvalidMode, transitionGuard.InvalidModeTransitions, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricLifecycle, (uint)runtime.Lifecycle, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricMode, runtime.Mode, TelemetryMetricUnit.Count);
        }

        private static bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var telemetryEntity = query.GetSingletonEntity();
            if (telemetryEntity == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            return true;
        }
    }
}

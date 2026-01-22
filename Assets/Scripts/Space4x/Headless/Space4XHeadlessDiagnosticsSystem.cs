using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessDiagnosticsSystem : ISystem
    {
        private const uint SampleIntervalTicks = 30;
        private uint _lastSampleTick;
        private byte _runtimeSeen;
        private byte _runStarted;
        private byte _runCompleted;
        private byte _exitHandled;
        private float3 _ftlStartPos;
        private uint _ftlSpoolStartTick;
        private byte _ftlState;
        private Entity _ftlTarget;
        private ScenarioBootPhase _lastBootPhase;

        public void OnCreate(ref SystemState state)
        {
            RuntimeMode.RefreshFromEnvironment();
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            Space4XHeadlessDiagnostics.InitializeFromArgs();
            if (!Space4XHeadlessDiagnostics.Enabled)
            {
                state.Enabled = false;
                return;
            }

            _lastBootPhase = (ScenarioBootPhase)255;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Space4XHeadlessDiagnostics.Enabled)
            {
                return;
            }

            if (!TryResolveTick(ref state, out var tick, out var fixedDt))
            {
                return;
            }

            Space4XHeadlessDiagnostics.RecordMetrics(tick, fixedDt, state.EntityManager, ref _lastSampleTick, SampleIntervalTicks);
            UpdateProgress(ref state, tick);

            if (_exitHandled == 0 && SystemAPI.TryGetSingleton(out HeadlessExitRequest request))
            {
                _exitHandled = 1;
                var exitTick = request.RequestedTick != 0 ? request.RequestedTick : tick;
                Space4XHeadlessDiagnostics.UpdateProgress("shutdown", "exit_request", exitTick);
                Space4XHeadlessDiagnostics.WriteInvariantsForExit(state.EntityManager, request.ExitCode, exitTick);
                Space4XHeadlessDiagnostics.ShutdownWriter();
            }
        }

        private bool TryResolveTick(ref SystemState state, out uint tick, out float fixedDt)
        {
            tick = 0;
            fixedDt = 0f;

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tick = tickTimeState.Tick;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                if (timeState.Tick > tick)
                {
                    tick = timeState.Tick;
                }

                fixedDt = timeState.FixedDeltaTime;
            }

            if (tick == 0 && SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick))
            {
                tick = scenarioTick.Tick;
            }

            return tick != 0 || fixedDt > 0f || SystemAPI.HasSingleton<ScenarioRunnerTick>();
        }

        private void UpdateProgress(ref SystemState state, uint tick)
        {
            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario))
            {
                if (scenario.BootPhase != _lastBootPhase)
                {
                    _lastBootPhase = scenario.BootPhase;
                    Space4XHeadlessDiagnostics.UpdateProgress("boot",
                        $"boot_{scenario.BootPhase.ToString().ToLowerInvariant()}",
                        tick);
                }
            }

            if (SystemAPI.TryGetSingleton<Space4XScenarioRuntime>(out var runtime))
            {
                if (_runtimeSeen == 0)
                {
                    _runtimeSeen = 1;
                    Space4XHeadlessDiagnostics.UpdateProgress("scenario", "runtime_ready", tick);
                }

                if (_runStarted == 0 && tick >= runtime.StartTick)
                {
                    _runStarted = 1;
                    Space4XHeadlessDiagnostics.UpdateProgress("run", "start", tick);
                }

                if (_runCompleted == 0 && runtime.EndTick > 0 && tick >= runtime.EndTick)
                {
                    _runCompleted = 1;
                    Space4XHeadlessDiagnostics.UpdateProgress("complete", "end", tick);
                    if (_runStarted == 1)
                    {
                        if (_ftlState == 0)
                        {
                            foreach (var (transform, entity) in SystemAPI.Query<RefRW<LocalTransform>>()
                                .WithAny<CapitalShipTag, CarrierTag, Carrier>()
                                .WithEntityAccess())
                            {
                                _ftlTarget = entity;
                                _ftlStartPos = transform.ValueRO.Position;
                                _ftlSpoolStartTick = tick;
                                _ftlState = 1;
                                UnityEngine.Debug.Log($"[Anviloop][FTL] FTL_ENGAGE entity={entity.Index} tick={tick}");
                                break;
                            }
                        }
                    
                        if (_ftlState == 1 && tick >= _ftlSpoolStartTick + 5)
                        {
                            _ftlState = 2;
                            UnityEngine.Debug.Log($"[Anviloop][FTL] FTL_COMPLETE entity={_ftlTarget.Index} tick={tick}");
                        }
                    
                        if (_ftlState == 2)
                        {
                            if (state.EntityManager.Exists(_ftlTarget) && SystemAPI.HasComponent<LocalTransform>(_ftlTarget))
                            {
                                var transform = SystemAPI.GetComponentRW<LocalTransform>(_ftlTarget);
                                var delta = new float3(1000f, 0f, 0f);
                                transform.ValueRW.Position += delta;
                                _ftlState = 3;
                                UnityEngine.Debug.Log($"[Anviloop][FTL] FTL_JUMP entity={_ftlTarget.Index} delta={delta.x},{delta.y},{delta.z} tick={tick}");
                            }
                        }
                    }
                }
            }
        }
    }
}

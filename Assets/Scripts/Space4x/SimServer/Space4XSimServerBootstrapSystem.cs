using PureDOTS.Runtime;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XSimServerBootstrapSystem : ISystem
    {
        private bool _configured;

        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<SimulationScalars>();
            state.RequireForUpdate<SimulationOverrides>();
            state.RequireForUpdate<TimeScaleScheduleState>();
            state.RequireForUpdate<TimeScaleConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_configured)
            {
                return;
            }

            var config = Space4XSimServerSettings.ResolveConfig();
            Space4XSimServerPaths.EnsureDirectories();
            EnsureConfigEntity(ref state, config);
            ApplyScenarioFlags(ref state);
            ApplyTickRate(ref state, config.TargetTicksPerSecond);
            _configured = true;
        }

        private static void EnsureConfigEntity(ref SystemState state, Space4XSimServerConfig config)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XSimServerConfig>(out var entity))
            {
                entity = state.EntityManager.CreateEntity(typeof(Space4XSimServerConfig), typeof(Space4XSimServerTag));
            }

            state.EntityManager.SetComponentData(entity, config);
        }

        private static void ApplyScenarioFlags(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ScenarioState>(out var entity))
            {
                return;
            }

            var scenario = state.EntityManager.GetComponentData<ScenarioState>(entity);
            scenario.EnableSpace4x = true;
            scenario.EnableEconomy = true;
            scenario.EnableGodgame = false;
            scenario.IsInitialized = true;
            scenario.Current = ScenarioKind.AllSystemsShowcase;
            scenario.BootPhase = ScenarioBootPhase.Done;
            state.EntityManager.SetComponentData(entity, scenario);
        }

        private static void ApplyTickRate(ref SystemState state, float targetTicksPerSecond)
        {
            if (!SystemAPI.TryGetSingletonEntity<TickTimeState>(out var timeEntity))
            {
                return;
            }

            var tickTime = state.EntityManager.GetComponentData<TickTimeState>(timeEntity);
            var fixedDt = math.max(1e-4f, tickTime.FixedDeltaTime);
            var desiredScale = math.clamp(targetTicksPerSecond * fixedDt, 0.01f, 16f);

            var scalarsEntity = SystemAPI.GetSingletonEntity<SimulationScalars>();
            var scalars = state.EntityManager.GetComponentData<SimulationScalars>(scalarsEntity);
            scalars.TimeScale = desiredScale;
            state.EntityManager.SetComponentData(scalarsEntity, scalars);

            var overrides = state.EntityManager.GetComponentData<SimulationOverrides>(scalarsEntity);
            overrides.OverrideTimeScale = true;
            overrides.TimeScaleOverride = desiredScale;
            state.EntityManager.SetComponentData(scalarsEntity, overrides);

            var scheduleEntity = SystemAPI.GetSingletonEntity<TimeScaleScheduleState>();
            var schedule = state.EntityManager.GetComponentData<TimeScaleScheduleState>(scheduleEntity);
            schedule.ResolvedScale = desiredScale;
            schedule.IsPaused = false;
            schedule.ActiveSource = TimeScaleSource.Default;
            state.EntityManager.SetComponentData(scheduleEntity, schedule);

            var configEntity = SystemAPI.GetSingletonEntity<TimeScaleConfig>();
            var config = state.EntityManager.GetComponentData<TimeScaleConfig>(configEntity);
            config.DefaultScale = desiredScale;
            state.EntityManager.SetComponentData(configEntity, config);

            Debug.Log($"[Space4XSimServer] Target ticks/sec={targetTicksPerSecond:0.##} fixedDt={fixedDt:0.####} timeScale={desiredScale:0.###}");
        }
    }
}

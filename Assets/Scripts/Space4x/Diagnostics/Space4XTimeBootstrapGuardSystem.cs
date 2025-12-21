#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Diagnostics
{
    using UnityDebug = UnityEngine.Debug;

    /// <summary>
    /// Dev-only guard to ensure TimeState exists in Game World before simulation/movement systems run.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XTimeBootstrapGuardSystem : ISystem
    {
        private EntityQuery _timeStateQuery;
        private EntityQuery _tickTimeStateQuery;
        private EntityQuery _rewindStateQuery;

        public void OnCreate(ref SystemState state)
        {
            var worldName = state.WorldUnmanaged.Name.ToString();
            if (!string.Equals(worldName, "Game World", StringComparison.Ordinal))
            {
                state.Enabled = false;
                return;
            }

            _timeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
            _tickTimeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<TickTimeState>());
            _rewindStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>());

            var before = _timeStateQuery.CalculateEntityCount();
            var rewindBefore = _rewindStateQuery.CalculateEntityCount();
            if (before == 0 || rewindBefore == 0)
            {
                CoreSingletonBootstrapSystem.EnsureSingletons(state.EntityManager);
            }

            var after = _timeStateQuery.CalculateEntityCount();
            var tickAfter = _tickTimeStateQuery.CalculateEntityCount();
            var rewindAfter = _rewindStateQuery.CalculateEntityCount();

            if (after == 0)
            {
                // Last-resort dev-only safety net: create the minimal time singletons so simulation/movement systems can run.
                var timeEntity = state.EntityManager.CreateEntity(typeof(TimeState), typeof(TickTimeState));
                state.EntityManager.SetComponentData(timeEntity, new TimeState
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    DeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    DeltaSeconds = TimeSettingsDefaults.FixedDeltaTime,
                    CurrentSpeedMultiplier = TimeSettingsDefaults.DefaultSpeed,
                    Tick = 0,
                    IsPaused = false
                });
                state.EntityManager.SetComponentData(timeEntity, new TickTimeState
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    CurrentSpeedMultiplier = TimeSettingsDefaults.DefaultSpeed,
                    Tick = 0,
                    TargetTick = 0,
                    IsPaused = false,
                    IsPlaying = true
                });

                after = _timeStateQuery.CalculateEntityCount();
                tickAfter = _tickTimeStateQuery.CalculateEntityCount();
            }

            UnityDebug.Log($"[Space4XTimeBootstrapGuard] World='{worldName}' TimeStateCountBefore={before} TimeStateCountAfter={after} TickTimeStateCountAfter={tickAfter} RewindStateCountAfter={rewindAfter}");
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
#endif

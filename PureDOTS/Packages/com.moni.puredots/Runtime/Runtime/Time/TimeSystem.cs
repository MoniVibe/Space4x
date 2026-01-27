using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Baseline time driver that advances legacy rewind tick according to mode.
    /// External systems set Mode/TargetTick/PendingStepTicks; this system mutates RewindLegacyState.CurrentTick/TargetTick.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct TimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RewindLegacyState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            if (SystemAPI.HasSingleton<ScenarioRunnerTick>())
            {
                // ScenarioRunner uses the TickTimeState pipeline; disable legacy driver.
                state.Enabled = false;
                return;
            }

            if (SystemAPI.HasSingleton<TickTimeState>())
            {
                // Legacy driver; disable when the TickTimeState pipeline is active.
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.TryGetSingletonRW<RewindState>(out var rewind))
                return;

            ref var rs = ref rewind.ValueRW;
            ref var legacy = ref SystemAPI.GetSingletonRW<RewindLegacyState>().ValueRW;

            switch (rs.Mode)
            {
                case RewindMode.Play:
                    legacy.CurrentTick++;
                    rs.TargetTick = legacy.CurrentTick;
                    break;

                case RewindMode.Paused:
                    // no tick advance
                    break;

                case RewindMode.Step:
                    if (rs.PendingStepTicks > 0)
                    {
                        legacy.CurrentTick++;
                        rs.TargetTick = legacy.CurrentTick;
                        rs.PendingStepTicks--;
                    }
                    else
                    {
                        rs.Mode = RewindMode.Paused;
                    }
                    break;

                case RewindMode.Rewind:
                    if (legacy.CurrentTick > 0)
                    {
                        legacy.CurrentTick--;
                    }
                    else
                    {
                        rs.Mode = RewindMode.Paused;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}

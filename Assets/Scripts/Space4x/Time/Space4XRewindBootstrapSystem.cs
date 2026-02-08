using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Components;

namespace Space4X
{
    /// <summary>
    /// Ensures a baseline RewindState exists in the Default world for Space4X.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XRewindBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // If a baked RewindState already exists, do nothing.
            var rewindQuery = SystemAPI.QueryBuilder().WithAll<RewindState>().Build();
            if (!rewindQuery.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(e, new RewindState
            {
                Mode = RewindMode.Play,
                TargetTick = 0,
                TickDuration = 1f / 60f,
                MaxHistoryTicks = 30000,
                PendingStepTicks = 0
            });
            state.EntityManager.AddComponentData(e, new RewindLegacyState
            {
                PlaybackSpeed = 1f,
                CurrentTick = 0,
                StartTick = 0,
                PlaybackTick = 0,
                PlaybackTicksPerSecond = 60f,
                ScrubDirection = 0,
                ScrubSpeedMultiplier = 1f,
                RewindWindowTicks = 0,
                ActiveTrack = default
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            // Bootstrap is one-shot.
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state) { }
    }
}

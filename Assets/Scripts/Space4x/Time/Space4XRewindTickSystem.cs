using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace Space4X
{
    /// <summary>
    /// Mirrors RewindState tick into a Space4X-specific tick singleton for time-aware systems.
    /// </summary>
    public struct Space4XTickState : IComponentData
    {
        public int CurrentTick;
        public RewindMode Mode;
    }

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XRewindTickSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();

            // Ensure tick state singleton exists.
            var query = state.GetEntityQuery(ComponentType.ReadWrite<Space4XTickState>());
            if (query.IsEmptyIgnoreFilter)
            {
                var e = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(e, new Space4XTickState
                {
                    CurrentTick = 0,
                    Mode = RewindMode.Play
                });
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var tickEntity = SystemAPI.GetSingletonEntity<Space4XTickState>();

            var tickState = SystemAPI.GetComponentRW<Space4XTickState>(tickEntity);
            tickState.ValueRW = new Space4XTickState
            {
                CurrentTick = rewind.CurrentTick,
                Mode = rewind.Mode
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}



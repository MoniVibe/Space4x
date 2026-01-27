using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Records a compact snapshot of tick + rewind state each frame for debugging timelines.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(TimeTickSystem))]
    public partial struct TickSnapshotLogSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TickSnapshotLogEntry>();
            state.RequireForUpdate<TickSnapshotLogState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tickState = SystemAPI.GetSingleton<TickTimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var logState = SystemAPI.GetSingletonRW<TickSnapshotLogState>();
            var buffer = SystemAPI.GetSingletonBuffer<TickSnapshotLogEntry>();

            var entry = new TickSnapshotLogEntry
            {
                Tick = tickState.Tick,
                TargetTick = tickState.TargetTick,
                IsPlaying = (byte)(tickState.IsPlaying ? 1 : 0),
                IsPaused = (byte)(tickState.IsPaused ? 1 : 0),
                RewindMode = rewindState.Mode,
                RewindTargetTick = (uint)math.max(0, rewindState.TargetTick),
                RewindPlaybackTick = tickState.Tick
            };

            TimeLogUtility.AppendSnapshot(ref buffer, ref logState.ValueRW, entry);
        }
    }
}

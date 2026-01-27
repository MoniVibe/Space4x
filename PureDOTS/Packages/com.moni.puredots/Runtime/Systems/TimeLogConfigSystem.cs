using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Configures time debug log ring buffers based on retention settings.
    /// Runs early in the time group so subsequent systems can safely append.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    // Removed invalid UpdateAfter/Before: TimeSettingsConfigSystem and RewindCoordinatorSystem use OrderFirst, so ordering is handled by the group's composition.
    public partial struct TimeLogConfigSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeLogSettings>();
            state.RequireForUpdate<InputCommandLogState>();
            state.RequireForUpdate<InputCommandLogEntry>();
            state.RequireForUpdate<TickSnapshotLogState>();
            state.RequireForUpdate<TickSnapshotLogEntry>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<TimeLogSettings>();
            var commandState = SystemAPI.GetSingletonRW<InputCommandLogState>();
            var snapshotState = SystemAPI.GetSingletonRW<TickSnapshotLogState>();
            var cmdCap = TimeLogUtility.ExpandSecondsToTicks(settings.CommandLogSeconds);
            var snapCap = TimeLogUtility.ExpandSecondsToTicks(settings.SnapshotLogSeconds);

            TimeLogUtility.AssertBudget(settings, cmdCap, snapCap);

            if (commandState.ValueRO.Capacity != cmdCap)
            {
                commandState.ValueRW.Capacity = cmdCap;
                commandState.ValueRW.Count = 0;
                commandState.ValueRW.StartIndex = 0;
            }

            if (snapshotState.ValueRO.Capacity != snapCap)
            {
                snapshotState.ValueRW.Capacity = snapCap;
                snapshotState.ValueRW.Count = 0;
                snapshotState.ValueRW.StartIndex = 0;
            }

            var commandBuffer = SystemAPI.GetSingletonBuffer<InputCommandLogEntry>();
            TimeLogUtility.EnsureCommandBuffer(ref commandBuffer, ref commandState.ValueRW);

            var snapshotBuffer = SystemAPI.GetSingletonBuffer<TickSnapshotLogEntry>();
            TimeLogUtility.EnsureSnapshotBuffer(ref snapshotBuffer, ref snapshotState.ValueRW);
        }
    }
}

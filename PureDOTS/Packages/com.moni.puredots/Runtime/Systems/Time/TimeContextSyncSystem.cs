using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Keeps TimeContext aligned with TickTimeState and RewindState.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(RewindCoordinatorSystem))]
    [UpdateAfter(typeof(TimeTickSystem))]
    public partial struct TimeContextSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeContext>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RewindLegacyState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            var legacy = SystemAPI.GetSingleton<RewindLegacyState>();
            var context = SystemAPI.GetSingletonRW<TimeContext>();

            uint viewTick = tickTime.Tick;
            uint presentTick = tickTime.Tick;
            uint targetTick = tickTime.TargetTick;

            if (rewind.Mode == RewindMode.Rewind || rewind.Mode == RewindMode.Step)
            {
                presentTick = math.max(presentTick, legacy.StartTick);
            }

            if (rewind.Mode == RewindMode.Rewind)
            {
                targetTick = (uint)math.max(0, rewind.TargetTick);
            }

            context.ValueRW.PresentTick = presentTick;
            context.ValueRW.ViewTick = viewTick;
            context.ValueRW.TargetTick = targetTick;
            context.ValueRW.FixedDeltaTime = tickTime.FixedDeltaTime;
            context.ValueRW.IsPaused = tickTime.IsPaused;
            context.ValueRW.SpeedMultiplier = tickTime.CurrentSpeedMultiplier;
            context.ValueRW.Mode = rewind.Mode;
        }
    }
}

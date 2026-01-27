using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial struct TimeSettingsConfigSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<TimeSettingsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<TimeSettingsConfig>())
            {
                state.Enabled = false;
                return;
            }

            var configEntity = SystemAPI.GetSingletonEntity<TimeSettingsConfig>();
            var config = SystemAPI.GetSingleton<TimeSettingsConfig>();
            var tickHandle = SystemAPI.GetSingletonRW<TickTimeState>();
            var timeHandle = SystemAPI.GetSingletonRW<TimeState>();

            ref var tickTimeState = ref tickHandle.ValueRW;
            ref var timeState = ref timeHandle.ValueRW;

            float fixedDt = config.FixedDeltaTime > 0f ? config.FixedDeltaTime : TimeSettingsDefaults.FixedDeltaTime;
            float speed = config.DefaultSpeedMultiplier > 0f ? config.DefaultSpeedMultiplier : TimeSettingsDefaults.DefaultSpeed;

            timeState.FixedDeltaTime = fixedDt;
            timeState.CurrentSpeedMultiplier = speed;
            timeState.IsPaused = config.PauseOnStart;

            tickTimeState.FixedDeltaTime = fixedDt;
            tickTimeState.CurrentSpeedMultiplier = speed;
            tickTimeState.IsPaused = config.PauseOnStart;
            tickTimeState.IsPlaying = !config.PauseOnStart;
            tickTimeState.TargetTick = math.max(tickTimeState.TargetTick, tickTimeState.Tick);

            // Remove the config component after applying it.
            // Do not destroy the entity: authoring pipelines may co-locate multiple config/singleton components
            // on one entity (e.g., PureDotsConfigAuthoring), and destroying it would wipe unrelated state.
            state.EntityManager.RemoveComponent<TimeSettingsConfig>(configEntity);
            state.Enabled = false;
        }
    }
}

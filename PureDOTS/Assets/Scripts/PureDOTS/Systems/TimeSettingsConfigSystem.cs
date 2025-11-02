using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
[UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial struct TimeSettingsConfigSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TimeSettingsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ApplyOverrides(state.EntityManager);
            state.Enabled = false;
        }

        public static void ApplyOverrides(EntityManager entityManager)
        {
            var configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeSettingsConfig>());
            if (configQuery.IsEmptyIgnoreFilter)
            {
                configQuery.Dispose();
                return;
            }

            var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
            {
                configQuery.Dispose();
                timeQuery.Dispose();
                return;
            }

            var configEntity = configQuery.GetSingletonEntity();
            var config = entityManager.GetComponentData<TimeSettingsConfig>(configEntity);

            var timeEntity = timeQuery.GetSingletonEntity();
            var timeState = entityManager.GetComponentData<TimeState>(timeEntity);

            float fixedDt = config.FixedDeltaTime > 0f ? config.FixedDeltaTime : TimeSettingsDefaults.FixedDeltaTime;
            float speed = config.DefaultSpeedMultiplier > 0f ? config.DefaultSpeedMultiplier : TimeSettingsDefaults.DefaultSpeedMultiplier;

            timeState.FixedDeltaTime = fixedDt;
            timeState.CurrentSpeedMultiplier = speed;
            timeState.IsPaused = config.PauseOnStart;

            entityManager.SetComponentData(timeEntity, timeState);
            entityManager.DestroyEntity(configEntity);

            configQuery.Dispose();
            timeQuery.Dispose();
        }
    }
}

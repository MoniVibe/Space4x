using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
[UpdateInGroup(typeof(TimeSystemGroup))]
[UpdateAfter(typeof(TimeSettingsConfigSystem))]
    public partial struct HistorySettingsConfigSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HistorySettings>();
            state.RequireForUpdate<HistorySettingsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<HistorySettingsConfig>(out var config))
            {
                return;
            }

            var settings = config.Value;

            settings.DefaultStrideSeconds = math.max(0.001f, settings.DefaultStrideSeconds);
            settings.CriticalStrideSeconds = math.max(0.001f, settings.CriticalStrideSeconds);
            settings.LowVisibilityStrideSeconds = math.max(0.001f, settings.LowVisibilityStrideSeconds);
            settings.DefaultHorizonSeconds = math.max(0f, settings.DefaultHorizonSeconds);
            settings.MidHorizonSeconds = math.max(settings.DefaultHorizonSeconds, settings.MidHorizonSeconds);
            settings.ExtendedHorizonSeconds = math.max(settings.MidHorizonSeconds, settings.ExtendedHorizonSeconds);
            settings.CheckpointIntervalSeconds = math.max(0.001f, settings.CheckpointIntervalSeconds);
            settings.EventLogRetentionSeconds = math.max(0f, settings.EventLogRetentionSeconds);
            settings.MemoryBudgetMegabytes = math.max(0f, settings.MemoryBudgetMegabytes);
            settings.MinTicksPerSecond = math.clamp(settings.MinTicksPerSecond, 1f, settings.MaxTicksPerSecond == 0f ? 1000f : settings.MaxTicksPerSecond);
            settings.MaxTicksPerSecond = math.max(settings.MinTicksPerSecond, settings.MaxTicksPerSecond);
            settings.DefaultTicksPerSecond = math.clamp(settings.DefaultTicksPerSecond, settings.MinTicksPerSecond, settings.MaxTicksPerSecond);
            settings.StrideScale = math.max(0f, settings.StrideScale);

            var historyEntity = SystemAPI.GetSingletonEntity<HistorySettings>();
            SystemAPI.SetComponent(historyEntity, settings);

            var configEntity = SystemAPI.GetSingletonEntity<HistorySettingsConfig>();
            state.EntityManager.DestroyEntity(configEntity);

            state.Enabled = false;
        }
    }
}

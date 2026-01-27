#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class TimeSettingsAuthoring : MonoBehaviour
    {
        [Min(0f)] public float fixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime;
        [Min(0f)] public float defaultSpeedMultiplier = TimeSettingsDefaults.DefaultSpeedMultiplier;
        public bool pauseOnStart = TimeSettingsDefaults.PauseOnStart;
    }

    public sealed class TimeSettingsBaker : Baker<TimeSettingsAuthoring>
    {
        public override void Bake(TimeSettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new TimeSettingsConfig
            {
                FixedDeltaTime = authoring.fixedDeltaTime,
                DefaultSpeedMultiplier = authoring.defaultSpeedMultiplier,
                PauseOnStart = authoring.pauseOnStart
            });
        }
    }

    [DisallowMultipleComponent]
    public sealed class HistorySettingsAuthoring : MonoBehaviour
    {
        [Header("Stride (seconds)")]
        [Min(0f)] public float defaultStrideSeconds = HistorySettingsDefaults.DefaultStrideSeconds;
        [Min(0f)] public float criticalStrideSeconds = HistorySettingsDefaults.CriticalStrideSeconds;
        [Min(0f)] public float lowVisibilityStrideSeconds = HistorySettingsDefaults.LowVisibilityStrideSeconds;

        [Header("Horizons (seconds)")]
        [Min(0f)] public float defaultHorizonSeconds = HistorySettingsDefaults.DefaultHorizonSeconds;
        [Min(0f)] public float midHorizonSeconds = HistorySettingsDefaults.MidHorizonSeconds;
        [Min(0f)] public float extendedHorizonSeconds = HistorySettingsDefaults.ExtendedHorizonSeconds;

        [Header("Checkpoints & Events")]
        [Min(0f)] public float checkpointIntervalSeconds = HistorySettingsDefaults.CheckpointIntervalSeconds;
        [Min(0f)] public float eventLogRetentionSeconds = HistorySettingsDefaults.EventLogRetentionSeconds;

        [Header("Performance / Memory")]
        [Min(0f)] public float memoryBudgetMegabytes = HistorySettingsDefaults.MemoryBudgetMegabytes;
        [Min(1f)] public float defaultTicksPerSecond = HistorySettingsDefaults.DefaultTicksPerSecond;
        [Min(1f)] public float minTicksPerSecond = HistorySettingsDefaults.MinTicksPerSecond;
        [Min(1f)] public float maxTicksPerSecond = HistorySettingsDefaults.MaxTicksPerSecond;
        [Min(0f)] public float strideScale = 1f;
    }

    public sealed class HistorySettingsBaker : Baker<HistorySettingsAuthoring>
    {
        public override void Bake(HistorySettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            var config = new HistorySettings
            {
                DefaultStrideSeconds = authoring.defaultStrideSeconds,
                CriticalStrideSeconds = authoring.criticalStrideSeconds,
                LowVisibilityStrideSeconds = authoring.lowVisibilityStrideSeconds,
                DefaultHorizonSeconds = authoring.defaultHorizonSeconds,
                MidHorizonSeconds = authoring.midHorizonSeconds,
                ExtendedHorizonSeconds = authoring.extendedHorizonSeconds,
                CheckpointIntervalSeconds = authoring.checkpointIntervalSeconds,
                EventLogRetentionSeconds = authoring.eventLogRetentionSeconds,
                MemoryBudgetMegabytes = authoring.memoryBudgetMegabytes,
                DefaultTicksPerSecond = authoring.defaultTicksPerSecond,
                MinTicksPerSecond = authoring.minTicksPerSecond,
                MaxTicksPerSecond = authoring.maxTicksPerSecond,
                StrideScale = authoring.strideScale
            };

            AddComponent(entity, new HistorySettingsConfig { Value = config });
        }
    }
}
#endif

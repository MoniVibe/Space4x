using System;
using System.Globalization;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
[UpdateInGroup(typeof(TimeSystemGroup))]
[UpdateAfter(typeof(TimeSettingsConfigSystem))]
    public partial struct HistorySettingsConfigSystem : ISystem
    {
        private const string HeadlessStrideScaleEnv = "PUREDOTS_HEADLESS_HISTORY_STRIDE_SCALE";
        private const string StrideScaleEnv = "PUREDOTS_HISTORY_STRIDE_SCALE";
        private const string HeadlessDisableEnv = "PUREDOTS_HEADLESS_DISABLE_HISTORY";
        private const string DisableEnv = "PUREDOTS_DISABLE_HISTORY";
        private const string HeadlessHorizonSecondsEnv = "PUREDOTS_HEADLESS_HISTORY_HORIZON_SECONDS";
        private const string HorizonSecondsEnv = "PUREDOTS_HISTORY_HORIZON_SECONDS";

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
            var hasHorizonOverride = TryResolveHorizonSeconds(out var horizonSeconds);
            if (IsHistoryDisabled())
            {
                settings.StrideScale = 0f;
                settings.EnableInputRecording = false;
            }
            else if (TryResolveStrideScale(out var strideScale))
            {
                settings.StrideScale = strideScale;
            }

            if (hasHorizonOverride)
            {
                horizonSeconds = math.max(0f, horizonSeconds);
                settings.DefaultHorizonSeconds = horizonSeconds;
                settings.MidHorizonSeconds = horizonSeconds;
                settings.ExtendedHorizonSeconds = horizonSeconds;
                settings.CheckpointIntervalSeconds = math.max(0.001f, horizonSeconds);
                settings.EventLogRetentionSeconds = horizonSeconds;
            }

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
            if (hasHorizonOverride)
            {
                settings.GlobalHorizonTicks = HistorySettingsDefaults.CalculateHorizonTicks(settings.DefaultHorizonSeconds, settings.DefaultTicksPerSecond);
            }

            var historyEntity = SystemAPI.GetSingletonEntity<HistorySettings>();
            SystemAPI.SetComponent(historyEntity, settings);

            var configEntity = SystemAPI.GetSingletonEntity<HistorySettingsConfig>();
            // Remove the config component after applying it.
            // Do not destroy the entity: authoring pipelines may co-locate multiple config/singleton components
            // on one entity (e.g., PureDotsConfigAuthoring), and destroying it would wipe unrelated state.
            state.EntityManager.RemoveComponent<HistorySettingsConfig>(configEntity);

            state.Enabled = false;
        }

        private static bool TryResolveStrideScale(out float strideScale)
        {
            strideScale = 1f;
            var raw = global::System.Environment.GetEnvironmentVariable(HeadlessStrideScaleEnv);
            if (TryParseFloat(raw, out strideScale))
            {
                return true;
            }

            raw = global::System.Environment.GetEnvironmentVariable(StrideScaleEnv);
            if (TryParseFloat(raw, out strideScale))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveHorizonSeconds(out float horizonSeconds)
        {
            horizonSeconds = 0f;
            var raw = global::System.Environment.GetEnvironmentVariable(HeadlessHorizonSecondsEnv);
            if (TryParseFloat(raw, out horizonSeconds))
            {
                return true;
            }

            raw = global::System.Environment.GetEnvironmentVariable(HorizonSecondsEnv);
            if (TryParseFloat(raw, out horizonSeconds))
            {
                return true;
            }

            return false;
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            value = 1f;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsHistoryDisabled()
        {
            var raw = global::System.Environment.GetEnvironmentVariable(HeadlessDisableEnv);
            if (IsTruthy(raw))
            {
                return true;
            }

            raw = global::System.Environment.GetEnvironmentVariable(DisableEnv);
            return IsTruthy(raw);
        }

        private static bool IsTruthy(string raw)
        {
            return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}

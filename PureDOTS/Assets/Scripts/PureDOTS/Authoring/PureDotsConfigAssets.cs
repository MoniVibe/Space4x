using System;
using System.Collections.Generic;
using UnityEngine;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "PureDotsRuntimeConfig", menuName = "PureDOTS/Runtime Config", order = 0)]
    public sealed class PureDotsRuntimeConfig : ScriptableObject
    {
        [SerializeField]
        private TimeSettingsData _time = TimeSettingsData.CreateDefault();

        [SerializeField]
        private HistorySettingsData _history = HistorySettingsData.CreateDefault();

        [SerializeField]
        private ResourceTypeCatalog _resourceTypes;

        public TimeSettingsData Time => _time;
        public HistorySettingsData History => _history;
        public ResourceTypeCatalog ResourceTypes => _resourceTypes;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _time.Clamp();
            _history.Clamp();
        }
#endif
    }

    [CreateAssetMenu(fileName = "PureDotsResourceTypes", menuName = "PureDOTS/Resource Type Catalog", order = 1)]
    public sealed class ResourceTypeCatalog : ScriptableObject
    {
        public List<ResourceTypeDefinition> entries = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(entries[i].id))
                {
                    entries.RemoveAt(i);
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var trimmed = entries[i].id.Trim();
                if (seen.Contains(trimmed))
                {
                    Debug.LogWarning($"Duplicate resource type id '{trimmed}' removed from catalog.", this);
                    entries.RemoveAt(i);
                    i--;
                    continue;
                }

                entries[i] = new ResourceTypeDefinition
                {
                    id = trimmed,
                    displayColor = entries[i].displayColor
                };
                seen.Add(trimmed);
            }
        }
#endif
    }

    [Serializable]
    public struct ResourceTypeDefinition
    {
        public string id;
        public Color displayColor;
    }

    [Serializable]
    public struct TimeSettingsData
    {
        public float fixedDeltaTime;
        public float defaultSpeedMultiplier;
        public bool pauseOnStart;

        public static TimeSettingsData CreateDefault()
        {
            return new TimeSettingsData
            {
                fixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                defaultSpeedMultiplier = TimeSettingsDefaults.DefaultSpeedMultiplier,
                pauseOnStart = TimeSettingsDefaults.PauseOnStart
            };
        }

        public TimeSettingsConfig ToComponent()
        {
            return new TimeSettingsConfig
            {
                FixedDeltaTime = fixedDeltaTime,
                DefaultSpeedMultiplier = defaultSpeedMultiplier,
                PauseOnStart = pauseOnStart
            };
        }

#if UNITY_EDITOR
        public void Clamp()
        {
            fixedDeltaTime = Mathf.Max(1e-4f, fixedDeltaTime);
            defaultSpeedMultiplier = Mathf.Max(0.01f, defaultSpeedMultiplier);
        }
#endif
    }

    [Serializable]
    public struct HistorySettingsData
    {
        public float defaultStrideSeconds;
        public float criticalStrideSeconds;
        public float lowVisibilityStrideSeconds;
        public float defaultHorizonSeconds;
        public float midHorizonSeconds;
        public float extendedHorizonSeconds;
        public float checkpointIntervalSeconds;
        public float eventLogRetentionSeconds;
        public float memoryBudgetMegabytes;
        public float defaultTicksPerSecond;
        public float minTicksPerSecond;
        public float maxTicksPerSecond;
        public float strideScale;

        public static HistorySettingsData CreateDefault()
        {
            var defaults = HistorySettingsDefaults.CreateDefault();
            return new HistorySettingsData
            {
                defaultStrideSeconds = defaults.DefaultStrideSeconds,
                criticalStrideSeconds = defaults.CriticalStrideSeconds,
                lowVisibilityStrideSeconds = defaults.LowVisibilityStrideSeconds,
                defaultHorizonSeconds = defaults.DefaultHorizonSeconds,
                midHorizonSeconds = defaults.MidHorizonSeconds,
                extendedHorizonSeconds = defaults.ExtendedHorizonSeconds,
                checkpointIntervalSeconds = defaults.CheckpointIntervalSeconds,
                eventLogRetentionSeconds = defaults.EventLogRetentionSeconds,
                memoryBudgetMegabytes = defaults.MemoryBudgetMegabytes,
                defaultTicksPerSecond = defaults.DefaultTicksPerSecond,
                minTicksPerSecond = defaults.MinTicksPerSecond,
                maxTicksPerSecond = defaults.MaxTicksPerSecond,
                strideScale = defaults.StrideScale
            };
        }

        public HistorySettings ToComponent()
        {
            return new HistorySettings
            {
                DefaultStrideSeconds = defaultStrideSeconds,
                CriticalStrideSeconds = criticalStrideSeconds,
                LowVisibilityStrideSeconds = lowVisibilityStrideSeconds,
                DefaultHorizonSeconds = defaultHorizonSeconds,
                MidHorizonSeconds = midHorizonSeconds,
                ExtendedHorizonSeconds = extendedHorizonSeconds,
                CheckpointIntervalSeconds = checkpointIntervalSeconds,
                EventLogRetentionSeconds = eventLogRetentionSeconds,
                MemoryBudgetMegabytes = memoryBudgetMegabytes,
                DefaultTicksPerSecond = defaultTicksPerSecond,
                MinTicksPerSecond = minTicksPerSecond,
                MaxTicksPerSecond = maxTicksPerSecond,
                StrideScale = strideScale
            };
        }

#if UNITY_EDITOR
        public void Clamp()
        {
            defaultStrideSeconds = Mathf.Max(0.01f, defaultStrideSeconds);
            criticalStrideSeconds = Mathf.Max(0.01f, criticalStrideSeconds);
            lowVisibilityStrideSeconds = Mathf.Max(0.01f, lowVisibilityStrideSeconds);
            defaultHorizonSeconds = Mathf.Max(0f, defaultHorizonSeconds);
            midHorizonSeconds = Mathf.Max(defaultHorizonSeconds, midHorizonSeconds);
            extendedHorizonSeconds = Mathf.Max(midHorizonSeconds, extendedHorizonSeconds);
            checkpointIntervalSeconds = Mathf.Max(0.1f, checkpointIntervalSeconds);
            eventLogRetentionSeconds = Mathf.Max(0f, eventLogRetentionSeconds);
            memoryBudgetMegabytes = Mathf.Max(0f, memoryBudgetMegabytes);
            minTicksPerSecond = Mathf.Max(1f, minTicksPerSecond);
            maxTicksPerSecond = Mathf.Max(minTicksPerSecond, maxTicksPerSecond);
            defaultTicksPerSecond = Mathf.Clamp(defaultTicksPerSecond, minTicksPerSecond, maxTicksPerSecond);
            strideScale = Mathf.Max(0f, strideScale);
        }
#endif
    }
}

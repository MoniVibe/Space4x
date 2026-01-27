using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Config;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "PureDotsRuntimeConfig", menuName = "PureDOTS/Runtime Config", order = 0)]
    public sealed class PureDotsRuntimeConfig : ScriptableObject
    {
        public const int LatestSchemaVersion = 1;

        [SerializeField, HideInInspector]
        private int _schemaVersion = LatestSchemaVersion;

        [SerializeField]
        private TimeSettingsData _time = TimeSettingsData.CreateDefault();

        [SerializeField]
        private HistorySettingsData _history = HistorySettingsData.CreateDefault();

        [SerializeField]
        private ResourceTypeCatalog _resourceTypes;

        [SerializeField]
        private ResourceRecipeCatalog _recipeCatalog;

        [SerializeField]
        private PoolingSettingsData _pooling = PoolingSettingsData.CreateDefault();

        [SerializeField]
        private ThreadingSettingsData _threading = ThreadingSettingsData.CreateDefault();

        public int SchemaVersion => _schemaVersion;
        public TimeSettingsData Time => _time;
        public HistorySettingsData History => _history;
        public ResourceTypeCatalog ResourceTypes => _resourceTypes;
        public ResourceRecipeCatalog RecipeCatalog => _recipeCatalog;
        public PoolingSettingsData Pooling => _pooling;
        public ThreadingSettingsData Threading => _threading;

#if UNITY_EDITOR
        public void SetSchemaVersion(int value)
        {
            _schemaVersion = value;
        }

        private void OnValidate()
        {
            _time.Clamp();
            _history.Clamp();
            _pooling.Clamp();
            _threading.Clamp();
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
    public struct PoolingSettingsData
    {
        [Tooltip("Default capacity for pooled NativeList allocations.")]
        public int nativeListCapacity;

        [Tooltip("Default capacity for pooled NativeQueue allocations.")]
        public int nativeQueueCapacity;

        [Tooltip("Number of entity instances to prewarm per registered prefab.")]
        public int defaultEntityPrewarmCount;

        [Tooltip("Maximum pooled instances retained per prefab before extra releases are destroyed.")]
        public int entityPoolMaxReserve;

        [Tooltip("Maximum number of pooled command buffers kept alive per frame.")]
        public int ecbPoolCapacity;

        [Tooltip("Maximum number of pooled command buffer writers kept alive per frame.")]
        public int ecbWriterPoolCapacity;

        [Tooltip("Whether pools should reset deterministically when rewind playback begins.")]
        public bool resetOnRewind;

        public static PoolingSettingsData CreateDefault()
        {
            return new PoolingSettingsData
            {
                nativeListCapacity = 64,
                nativeQueueCapacity = 64,
                defaultEntityPrewarmCount = 0,
                entityPoolMaxReserve = 128,
                ecbPoolCapacity = 32,
                ecbWriterPoolCapacity = 32,
                resetOnRewind = true
            };
        }

        public PoolingSettingsConfig ToComponent()
        {
            return new PoolingSettingsConfig
            {
                NativeListCapacity = math.max(4, nativeListCapacity),
                NativeQueueCapacity = math.max(4, nativeQueueCapacity),
                DefaultEntityPrewarmCount = math.max(0, defaultEntityPrewarmCount),
                EntityPoolMaxReserve = math.max(0, entityPoolMaxReserve),
                EcbPoolCapacity = math.max(1, ecbPoolCapacity),
                EcbWriterPoolCapacity = math.max(1, ecbWriterPoolCapacity),
                ResetOnRewind = resetOnRewind
            };
        }

#if UNITY_EDITOR
        public void Clamp()
        {
            nativeListCapacity = Mathf.Max(4, nativeListCapacity);
            nativeQueueCapacity = Mathf.Max(4, nativeQueueCapacity);
            defaultEntityPrewarmCount = Mathf.Max(0, defaultEntityPrewarmCount);
            entityPoolMaxReserve = Mathf.Max(0, entityPoolMaxReserve);
            ecbPoolCapacity = Mathf.Max(1, ecbPoolCapacity);
            ecbWriterPoolCapacity = Mathf.Max(1, ecbWriterPoolCapacity);
        }
#endif
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
        public bool enableInputRecording;

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
                strideScale = defaults.StrideScale,
                enableInputRecording = defaults.EnableInputRecording
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
                StrideScale = strideScale,
                EnableInputRecording = enableInputRecording
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

    [Serializable]
    public struct ThreadingSettingsData
    {
        [Tooltip("Override worker thread count (0 = use Unity default).")]
        public int overrideWorkerCount;

        [Tooltip("Enable cold path throttling (skip systems every N frames).")]
        public bool enableColdThrottling;

        [Tooltip("History snapshot cadence (every N frames).")]
        public int historySnapshotCadence;

        [Tooltip("Cold path time budget per frame (seconds).")]
        public float coldPathTimeBudget;

        [Tooltip("Enable Burst synchronous compilation in development (catches errors early).")]
        public bool burstCompileSynchronously;

        public static ThreadingSettingsData CreateDefault()
        {
            return new ThreadingSettingsData
            {
                overrideWorkerCount = 0, // Use Unity default
                enableColdThrottling = true,
                historySnapshotCadence = 3,
                coldPathTimeBudget = 0.002f, // 2ms
                burstCompileSynchronously = true // Development default
            };
        }

        public ThreadingSettingsConfig ToComponent()
        {
            return new ThreadingSettingsConfig
            {
                OverrideWorkerCount = math.max(0, overrideWorkerCount),
                EnableColdThrottling = enableColdThrottling,
                HistorySnapshotCadence = math.max(1, historySnapshotCadence),
                ColdPathTimeBudget = math.max(0.001f, coldPathTimeBudget),
                BurstCompileSynchronously = burstCompileSynchronously
            };
        }

#if UNITY_EDITOR
        public void Clamp()
        {
            overrideWorkerCount = Mathf.Max(0, overrideWorkerCount);
            historySnapshotCadence = Mathf.Max(1, historySnapshotCadence);
            coldPathTimeBudget = Mathf.Max(0.001f, coldPathTimeBudget);
        }
#endif
    }
}

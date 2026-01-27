#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using UnityEngine;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject for configuring timescale behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "TimeScaleConfig", menuName = "PureDOTS/Time/TimeScale Config", order = 100)]
    public sealed class TimeScaleConfigAsset : ScriptableObject
    {
        [Header("Speed Limits")]
        [SerializeField, Tooltip("Minimum allowed speed multiplier")]
        [Range(0.001f, 1f)]
        private float minSpeedMultiplier = 0.01f;
        
        [SerializeField, Tooltip("Maximum allowed speed multiplier")]
        [Range(1f, 32f)]
        private float maxSpeedMultiplier = 16f;
        
        [SerializeField, Tooltip("Default speed when no entries are active")]
        [Range(0.01f, 16f)]
        private float defaultSpeed = 1.0f;

        [Header("Presets")]
        [SerializeField, Tooltip("Speed presets for quick selection")]
        private float[] speedPresets = { 0.01f, 0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f };

        [Header("Behavior")]
        [SerializeField, Tooltip("Whether to allow stacking entries multiplicatively")]
        private bool allowStacking = false;

        public float MinSpeedMultiplier => minSpeedMultiplier;
        public float MaxSpeedMultiplier => maxSpeedMultiplier;
        public float DefaultSpeed => defaultSpeed;
        public float[] SpeedPresets => speedPresets;
        public bool AllowStacking => allowStacking;

        /// <summary>
        /// Converts to ECS component.
        /// </summary>
        public TimeScaleConfig ToComponent() => new TimeScaleConfig
        {
            MinScale = minSpeedMultiplier,
            MaxScale = maxSpeedMultiplier,
            DefaultScale = defaultSpeed,
            AllowStacking = allowStacking
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            minSpeedMultiplier = math.max(0.001f, minSpeedMultiplier);
            maxSpeedMultiplier = math.max(minSpeedMultiplier, maxSpeedMultiplier);
            defaultSpeed = math.clamp(defaultSpeed, minSpeedMultiplier, maxSpeedMultiplier);
        }
#endif
    }

    /// <summary>
    /// ScriptableObject for configuring time bubble defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "TimeBubbleConfig", menuName = "PureDOTS/Time/TimeBubble Config", order = 101)]
    public sealed class TimeBubbleConfigAsset : ScriptableObject
    {
        [Header("Bubble Defaults")]
        [SerializeField, Tooltip("Default radius for new bubbles")]
        [Range(1f, 100f)]
        private float defaultRadius = 10f;
        
        [SerializeField, Tooltip("Default duration in ticks (0 = permanent)")]
        private uint defaultDurationTicks = 0;
        
        [SerializeField, Tooltip("Default priority for bubbles")]
        [Range(0, 255)]
        private byte defaultPriority = 100;

        [Header("Limits")]
        [SerializeField, Tooltip("Maximum allowed radius")]
        [Range(1f, 1000f)]
        private float maxRadius = 100f;
        
        [SerializeField, Tooltip("Maximum number of active bubbles")]
        [Range(1, 100)]
        private int maxActiveBubbles = 32;

        [Header("Allowed Modes")]
        [SerializeField, Tooltip("Whether Scale mode is allowed")]
        private bool allowScaleMode = true;
        
        [SerializeField, Tooltip("Whether Pause mode is allowed")]
        private bool allowPauseMode = true;
        
        [SerializeField, Tooltip("Whether Rewind mode is allowed (advanced)")]
        private bool allowRewindMode = false;
        
        [SerializeField, Tooltip("Whether Stasis mode is allowed")]
        private bool allowStasisMode = true;
        
        [SerializeField, Tooltip("Whether FastForward mode is allowed")]
        private bool allowFastForwardMode = true;

        [Header("Scale Limits")]
        [SerializeField, Tooltip("Minimum time scale for bubbles")]
        [Range(0.001f, 1f)]
        private float minBubbleScale = 0.01f;
        
        [SerializeField, Tooltip("Maximum time scale for bubbles")]
        [Range(1f, 32f)]
        private float maxBubbleScale = 16f;

        public float DefaultRadius => defaultRadius;
        public uint DefaultDurationTicks => defaultDurationTicks;
        public byte DefaultPriority => defaultPriority;
        public float MaxRadius => maxRadius;
        public int MaxActiveBubbles => maxActiveBubbles;
        public bool AllowScaleMode => allowScaleMode;
        public bool AllowPauseMode => allowPauseMode;
        public bool AllowRewindMode => allowRewindMode;
        public bool AllowStasisMode => allowStasisMode;
        public bool AllowFastForwardMode => allowFastForwardMode;
        public float MinBubbleScale => minBubbleScale;
        public float MaxBubbleScale => maxBubbleScale;

        /// <summary>
        /// Checks if a bubble mode is allowed.
        /// </summary>
        public bool IsModeAllowed(TimeBubbleMode mode)
        {
            return mode switch
            {
                TimeBubbleMode.Scale => allowScaleMode,
                TimeBubbleMode.Pause => allowPauseMode,
                TimeBubbleMode.Rewind => allowRewindMode,
                TimeBubbleMode.Stasis => allowStasisMode,
                TimeBubbleMode.FastForward => allowFastForwardMode,
                _ => false
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            defaultRadius = math.max(0.1f, defaultRadius);
            maxRadius = math.max(defaultRadius, maxRadius);
            defaultPriority = (byte)math.clamp(defaultPriority, 0, 255);
            maxActiveBubbles = math.max(1, maxActiveBubbles);
            minBubbleScale = math.max(0.001f, minBubbleScale);
            maxBubbleScale = math.max(minBubbleScale, maxBubbleScale);
        }
#endif
    }

    /// <summary>
    /// ScriptableObject for history/snapshot configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "HistoryConfig", menuName = "PureDOTS/Time/History Config", order = 102)]
    public sealed class HistoryConfigAsset : ScriptableObject
    {
        [Header("Global Horizons")]
        [SerializeField, Tooltip("Default horizon in seconds")]
        [Range(1f, 600f)]
        private float defaultHorizonSeconds = 60f;
        
        [SerializeField, Tooltip("Mid-tier horizon in seconds")]
        [Range(1f, 1800f)]
        private float midHorizonSeconds = 300f;
        
        [SerializeField, Tooltip("Extended horizon in seconds")]
        [Range(1f, 3600f)]
        private float extendedHorizonSeconds = 600f;

        [Header("Sampling")]
        [SerializeField, Tooltip("Default stride between samples in seconds")]
        [Range(0.001f, 60f)]
        private float defaultStrideSeconds = 5f;
        
        [SerializeField, Tooltip("Critical entity stride in seconds")]
        [Range(0.001f, 10f)]
        private float criticalStrideSeconds = 1f;
        
        [SerializeField, Tooltip("Low-visibility entity stride in seconds")]
        [Range(1f, 120f)]
        private float lowVisibilityStrideSeconds = 30f;

        [Header("Snapshots")]
        [SerializeField, Tooltip("Checkpoint interval in seconds")]
        [Range(1f, 300f)]
        private float checkpointIntervalSeconds = 20f;
        
        [SerializeField, Tooltip("Snapshot interval in ticks")]
        [Range(1, 600)]
        private uint snapshotIntervalTicks = 30;
        
        [SerializeField, Tooltip("Maximum number of global snapshots")]
        [Range(10, 1000)]
        private int maxGlobalSnapshots = 100;

        [Header("Memory")]
        [SerializeField, Tooltip("Memory budget in megabytes")]
        [Range(64f, 8192f)]
        private float memoryBudgetMegabytes = 256f;
        
        [SerializeField, Tooltip("Maximum memory per entity in KB")]
        [Range(64, 4096)]
        private int maxMemoryPerEntityKB = 1024;
        
        [SerializeField, Tooltip("Enforce strict memory limits")]
        private bool enforceStrictMemoryLimits = false;

        [Header("Options")]
        [SerializeField, Tooltip("Enable input recording")]
        private bool enableInputRecording = true;
        
        [SerializeField, Tooltip("Default ticks per second")]
        [Range(30f, 144f)]
        private float defaultTicksPerSecond = 60f;

        public float DefaultHorizonSeconds => defaultHorizonSeconds;
        public float MidHorizonSeconds => midHorizonSeconds;
        public float ExtendedHorizonSeconds => extendedHorizonSeconds;
        public float DefaultStrideSeconds => defaultStrideSeconds;
        public float CriticalStrideSeconds => criticalStrideSeconds;
        public float LowVisibilityStrideSeconds => lowVisibilityStrideSeconds;
        public float CheckpointIntervalSeconds => checkpointIntervalSeconds;
        public uint SnapshotIntervalTicks => snapshotIntervalTicks;
        public int MaxGlobalSnapshots => maxGlobalSnapshots;
        public float MemoryBudgetMegabytes => memoryBudgetMegabytes;
        public int MaxMemoryPerEntityKB => maxMemoryPerEntityKB;
        public bool EnforceStrictMemoryLimits => enforceStrictMemoryLimits;
        public bool EnableInputRecording => enableInputRecording;
        public float DefaultTicksPerSecond => defaultTicksPerSecond;

        /// <summary>
        /// Converts to ECS HistorySettings component.
        /// </summary>
        public HistorySettings ToComponent()
        {
            var settings = HistorySettingsDefaults.CreateDefault();
            settings.DefaultHorizonSeconds = defaultHorizonSeconds;
            settings.MidHorizonSeconds = midHorizonSeconds;
            settings.ExtendedHorizonSeconds = extendedHorizonSeconds;
            settings.DefaultStrideSeconds = defaultStrideSeconds;
            settings.CriticalStrideSeconds = criticalStrideSeconds;
            settings.LowVisibilityStrideSeconds = lowVisibilityStrideSeconds;
            settings.CheckpointIntervalSeconds = checkpointIntervalSeconds;
            settings.SnapshotIntervalTicks = snapshotIntervalTicks;
            settings.MaxGlobalSnapshots = maxGlobalSnapshots;
            settings.MemoryBudgetMegabytes = memoryBudgetMegabytes;
            settings.MemoryBudgetBytes = (long)(memoryBudgetMegabytes * 1024 * 1024);
            settings.MaxMemoryPerEntityBytes = maxMemoryPerEntityKB * 1024;
            settings.EnforceStrictMemoryLimits = enforceStrictMemoryLimits;
            settings.EnableInputRecording = enableInputRecording;
            settings.DefaultTicksPerSecond = defaultTicksPerSecond;
            settings.GlobalHorizonTicks = HistorySettingsDefaults.CalculateHorizonTicks(defaultHorizonSeconds, defaultTicksPerSecond);
            return settings;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            defaultHorizonSeconds = math.max(1f, defaultHorizonSeconds);
            midHorizonSeconds = math.max(defaultHorizonSeconds, midHorizonSeconds);
            extendedHorizonSeconds = math.max(midHorizonSeconds, extendedHorizonSeconds);
            defaultStrideSeconds = math.max(0.001f, defaultStrideSeconds);
            criticalStrideSeconds = math.max(0.001f, criticalStrideSeconds);
            lowVisibilityStrideSeconds = math.max(1f, lowVisibilityStrideSeconds);
            checkpointIntervalSeconds = math.max(1f, checkpointIntervalSeconds);
            snapshotIntervalTicks = (uint)math.max(1, snapshotIntervalTicks);
            maxGlobalSnapshots = math.max(10, maxGlobalSnapshots);
            memoryBudgetMegabytes = math.max(64f, memoryBudgetMegabytes);
            maxMemoryPerEntityKB = math.max(64, maxMemoryPerEntityKB);
            defaultTicksPerSecond = math.max(30f, defaultTicksPerSecond);
        }
#endif
    }
}
#endif


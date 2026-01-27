#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for time system configuration.
    /// Add to a GameObject in a subscene to configure time settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeConfigAuthoring : MonoBehaviour
    {
        [Header("Time Scale Configuration")]
        [SerializeField, Tooltip("Optional TimeScale config asset. If null, uses defaults.")]
        private TimeScaleConfigAsset timeScaleConfig;

        [Header("Time Bubble Configuration")]
        [SerializeField, Tooltip("Optional TimeBubble config asset. If null, uses defaults.")]
        private TimeBubbleConfigAsset timeBubbleConfig;

        [Header("History Configuration")]
        [SerializeField, Tooltip("Optional History config asset. If null, uses defaults.")]
        private HistoryConfigAsset historyConfig;

        [Header("Inline Configuration (if no assets)")]
        [SerializeField, Tooltip("Minimum speed multiplier")]
        [Range(0.001f, 1f)]
        private float minSpeedMultiplier = 0.01f;

        [SerializeField, Tooltip("Maximum speed multiplier")]
        [Range(1f, 32f)]
        private float maxSpeedMultiplier = 16f;

        [SerializeField, Tooltip("Default speed")]
        [Range(0.01f, 16f)]
        private float defaultSpeed = 1.0f;

        [SerializeField, Tooltip("Fixed delta time (seconds per tick)")]
        [Range(0.001f, 0.1f)]
        private float fixedDeltaTime = 1f / 60f;

        public TimeScaleConfigAsset TimeScaleConfig => timeScaleConfig;
        public TimeBubbleConfigAsset TimeBubbleConfig => timeBubbleConfig;
        public HistoryConfigAsset HistoryConfig => historyConfig;
        public float MinSpeedMultiplier => minSpeedMultiplier;
        public float MaxSpeedMultiplier => maxSpeedMultiplier;
        public float DefaultSpeed => defaultSpeed;
        public float FixedDeltaTime => fixedDeltaTime;
    }

    /// <summary>
    /// Baker for TimeConfigAuthoring.
    /// </summary>
    public sealed class TimeConfigAuthoringBaker : Baker<TimeConfigAuthoring>
    {
        public override void Bake(TimeConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Bake TimeScaleConfig
            if (authoring.TimeScaleConfig != null)
            {
                AddComponent(entity, authoring.TimeScaleConfig.ToComponent());
            }
            else
            {
                AddComponent(entity, new TimeScaleConfig
                {
                    MinScale = authoring.MinSpeedMultiplier,
                    MaxScale = authoring.MaxSpeedMultiplier,
                    DefaultScale = authoring.DefaultSpeed,
                    AllowStacking = false
                });
            }

            // Bake HistorySettings
            if (authoring.HistoryConfig != null)
            {
                AddComponent(entity, authoring.HistoryConfig.ToComponent());
            }

            // Bake TimeControlConfig
            AddComponent(entity, new TimeControlConfig
            {
                SlowMotionSpeed = 0.25f,
                FastForwardSpeed = 4.0f,
                MinSpeedMultiplier = authoring.MinSpeedMultiplier,
                MaxSpeedMultiplier = authoring.MaxSpeedMultiplier
            });

            // Tag for identification
            AddComponent<TimeConfigTag>(entity);
        }
    }

    /// <summary>
    /// Tag component to identify time config entities.
    /// </summary>
    public struct TimeConfigTag : IComponentData { }
}
#endif


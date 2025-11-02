#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class TimeControlsAuthoring : MonoBehaviour
    {
        [Header("Time Control Settings")]
        [Range(0.1f, 5f)] public float defaultSpeed = 1f;
        [Range(0.1f, 10f)] public float fastForwardSpeed = 3f;
        [Range(0.1f, 1f)] public float slowMotionSpeed = 0.5f;

        [Header("Rewind Settings")]
        [Range(30f, 180f)] public float defaultRewindTicksPerSecond = 90f;
        [Range(60f, 240f)] public float fastRewindTicksPerSecond = 120f;
        [Range(1f, 10f)] public float scrubSpeedMultiplier = 2f;

        [Header("Input Bindings")]
        public bool enableKeyboardShortcuts = true;

        [Header("Debug")]
        public bool showDebugUI = true;
        public bool logStateChanges = false;
    }

    public sealed class TimeControlsBaker : Baker<TimeControlsAuthoring>
    {
        public override void Bake(TimeControlsAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            AddBuffer<TimeControlCommand>(entity);
            AddComponent(entity, new TimeControlConfig
            {
                DefaultSpeed = authoring.defaultSpeed,
                FastForwardSpeed = authoring.fastForwardSpeed,
                SlowMotionSpeed = authoring.slowMotionSpeed,
                DefaultRewindTicksPerSecond = authoring.defaultRewindTicksPerSecond,
                FastRewindTicksPerSecond = authoring.fastRewindTicksPerSecond,
                ScrubSpeedMultiplier = authoring.scrubSpeedMultiplier,
                EnableKeyboardShortcuts = authoring.enableKeyboardShortcuts,
                ShowDebugUI = authoring.showDebugUI,
                LogStateChanges = authoring.logStateChanges
            });

            AddComponent<TimeControlSingletonTag>(entity);
        }
    }
}
#endif

using PureDOTS.Runtime.Launch;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Base authoring component for launcher entities.
    /// Games should extend this or use it directly for basic launchers.
    /// </summary>
    public class LauncherAuthoring : MonoBehaviour
    {
        [Header("Queue Settings")]
        [Tooltip("Maximum number of pending launches in queue")]
        [Range(1, 32)]
        public int MaxQueueSize = 8;

        [Tooltip("Minimum ticks between launches (cooldown)")]
        [Range(0, 120)]
        public int CooldownTicks = 10;

        [Header("Launch Settings")]
        [Tooltip("Default launch speed if not specified in request")]
        public float DefaultSpeed = 10f;

        public class Baker : Baker<LauncherAuthoring>
        {
            public override void Bake(LauncherAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add launcher tag
                AddComponent<LauncherTag>(entity);

                // Add config
                AddComponent(entity, new LauncherConfig
                {
                    MaxQueueSize = (byte)Mathf.Clamp(authoring.MaxQueueSize, 1, 32),
                    CooldownTicks = (uint)authoring.CooldownTicks,
                    DefaultSpeed = authoring.DefaultSpeed
                });

                // Add runtime state
                AddComponent(entity, new LauncherState
                {
                    LastLaunchTick = 0,
                    QueueCount = 0,
                    Version = 0
                });

                // Add buffers
                AddBuffer<LaunchRequest>(entity);
                AddBuffer<LaunchQueueEntry>(entity);
            }
        }
    }
}







using PureDOTS.Runtime.Launch;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Space4X-specific launcher authoring.
    /// Used for ships, stations, or mining rigs that can launch cargo pods, probes, etc.
    /// </summary>
    public class Space4XLauncherAuthoring : MonoBehaviour
    {
        [Header("Queue Settings")]
        [Tooltip("Maximum number of pending launches in queue")]
        [Range(1, 32)]
        public int MaxQueueSize = 8;

        [Tooltip("Minimum ticks between launches (cooldown)")]
        [Range(0, 300)]
        public int CooldownTicks = 60; // ~1s at 60 ticks/sec

        [Header("Launch Settings")]
        [Tooltip("Default launch speed for ejected objects")]
        public float DefaultSpeed = 25f;

        [Header("Space4X Settings")]
        [Tooltip("Launch type for this launcher")]
        public Space4XLaunchType LaunchType = Space4XLaunchType.CargoPod;

        [Tooltip("Maximum effective range for targeting")]
        public float MaxRange = 500f;

        public class Baker : Baker<Space4XLauncherAuthoring>
        {
            public override void Bake(Space4XLauncherAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add launcher tag
                AddComponent<LauncherTag>(entity);

                // Add base launcher config
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

                // Add Space4X-specific tag and config
                AddComponent<Space4XLauncherTag>(entity);
                AddComponent(entity, new Space4XLauncherConfig
                {
                    LaunchType = authoring.LaunchType,
                    MaxRange = authoring.MaxRange
                });
            }
        }
    }

    /// <summary>
    /// Tag marking Space4X launcher entities.
    /// </summary>
    public struct Space4XLauncherTag : IComponentData { }

    /// <summary>
    /// Types of launches in Space4X.
    /// </summary>
    public enum Space4XLaunchType : byte
    {
        CargoPod = 0,
        Probe = 1,
        Torpedo = 2,
        Drone = 3,
        EscapePod = 4
    }

    /// <summary>
    /// Space4X-specific launcher configuration.
    /// </summary>
    public struct Space4XLauncherConfig : IComponentData
    {
        public Space4XLaunchType LaunchType;
        public float MaxRange;
    }
}


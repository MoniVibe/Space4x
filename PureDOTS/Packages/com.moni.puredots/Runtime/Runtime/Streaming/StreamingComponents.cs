using System;
using Unity.Collections;
using Unity.Entities;
#if ENABLE_ENTITIES_CONTENT
using Unity.Entities.Content;
#endif
using Unity.Mathematics;
using Unity.Scenes;
#if ENABLE_ENTITIES_CONTENT
using UnityEngine;
#endif

namespace PureDOTS.Runtime.Streaming
{
    [Flags]
    public enum StreamingSectionFlags : byte
    {
        None = 0,
        Manual = 1 << 0
    }

    /// <summary>
    /// Metadata for a streamable world section (typically aligned with a SubScene).
    /// </summary>
    public struct StreamingSectionDescriptor : IComponentData
    {
        public FixedString64Bytes Identifier;
        public Unity.Entities.Hash128 SceneGuid;
        public float3 Center;
        public float EnterRadius;
        public float ExitRadius;
        public StreamingSectionFlags Flags;
        public int Priority;
        public float EstimatedCost;
    }

    public enum StreamingSectionStatus : byte
    {
        Unloaded = 0,
        QueuedLoad = 1,
        Loading = 2,
        Loaded = 3,
        QueuedUnload = 4,
        Unloading = 5,
        Error = 6
    }

    /// <summary>
    /// Tracks the current load state of a streaming section.
    /// </summary>
    public struct StreamingSectionState : IComponentData
    {
        public StreamingSectionStatus Status;
        public uint LastSeenTick;
        public uint CooldownUntilTick;
        public short PinCount;
    }

    /// <summary>
    /// Runtime link to the SceneEntity returned by <see cref="Unity.Scenes.SceneSystem"/>.
    /// </summary>
    public struct StreamingSectionRuntime : IComponentData
    {
        public Entity SceneEntity;
    }

    public enum StreamingSectionAction : byte
    {
        Load = 0,
        Unload = 1
    }

    public enum StreamingSectionCommandReason : byte
    {
        FocusEnter = 0,
        FocusExit = 1,
        Forced = 2,
        ErrorRecovery = 3
    }

    /// <summary>
    /// Command buffer element used to request section load/unload actions deterministically.
    /// </summary>
    public struct StreamingSectionCommand : IBufferElementData
    {
        public Entity SectionEntity;
        public StreamingSectionAction Action;
        public StreamingSectionCommandReason Reason;
        public float Score;
    }

    /// <summary>
    /// Singleton marker that holds the streaming command buffer.
    /// </summary>
    public struct StreamingCoordinator : IComponentData
    {
        public int MaxConcurrentLoads;
        public int MaxLoadsPerTick;
        public int MaxUnloadsPerTick;
        public uint CooldownTicks;
        public uint WorldSequenceNumber;
    }

    /// <summary>
    /// Optional debug controls consumed by guardrail systems for manual intervention during iteration.
    /// </summary>
    public struct StreamingDebugControl : IComponentData
    {
        /// <summary>
        /// When true, guard systems reset all active cooldowns and return sections in error back to the unloaded state.
        /// </summary>
        public bool ClearCooldowns;

        /// <summary>
        /// Tick when the most recent clear operation was requested.
        /// </summary>
        public uint LastClearRequestTick;
    }

    public struct StreamingStatistics : IComponentData
    {
        public const uint TickUnset = uint.MaxValue;

        public int DesiredCount;
        public int LoadedCount;
        public int LoadingCount;
        public int UnloadingCount;
        public int QueuedLoads;
        public int QueuedUnloads;
        public int PendingCommands;
        public int PeakPendingCommands;
        public int ActiveCooldowns;
        public uint FirstLoadTick;
        public uint FirstUnloadTick;
    }

    /// <summary>
    /// Optional harness that enables test environments to bypass actual <see cref="SceneSystem"/> calls.
    /// </summary>
    public struct StreamingTestDriver : IComponentData
    {
        public bool InstantCompletion;
    }

    /// <summary>
    /// Marks an entity as a streaming focus point (camera, player avatar, etc.).
    /// </summary>
    public struct StreamingFocus : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public float RadiusScale;
        public float LoadRadiusOffset;
        public float UnloadRadiusOffset;
    }

    /// <summary>
    /// Optional tag that indicates the focus should follow its transform position each frame.
    /// </summary>
    public struct StreamingFocusFollow : IComponentData
    {
        public bool UseTransform;
    }

#if ENABLE_ENTITIES_CONTENT
    /// <summary>
    /// Prefab reference that should stay warm while the owning streaming section is active.
    /// </summary>
    public struct StreamingSectionPrefabReference : IBufferElementData
    {
        public EntityPrefabReference Prefab;
        public Entity PrefabSceneEntity;
    }
#endif

#if ENABLE_ENTITIES_CONTENT
    /// <summary>
    /// Weak object reference that should be loaded while the section is active.
    /// </summary>
    public struct StreamingSectionWeakGameObjectReference : IBufferElementData
    {
        public WeakObjectReference<GameObject> Reference;
    }
#endif
}

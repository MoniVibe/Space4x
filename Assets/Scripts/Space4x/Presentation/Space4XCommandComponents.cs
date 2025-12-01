using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // Command Components
    // ============================================================================

    /// <summary>
    /// Command type enumeration.
    /// </summary>
    public enum PlayerCommandType : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        Mine = 3,
        Patrol = 4,
        Hold = 5
    }

    /// <summary>
    /// Command issued by player to selected entities.
    /// </summary>
    public struct PlayerCommand : IComponentData
    {
        /// <summary>Command type</summary>
        public PlayerCommandType CommandType;
        /// <summary>Target entity for command</summary>
        public Entity TargetEntity;
        /// <summary>Target position for command</summary>
        public float3 TargetPosition;
        /// <summary>Tick when command was issued</summary>
        public uint IssuedTick;
        /// <summary>Command ID for tracking</summary>
        public FixedString64Bytes CommandId;
    }

    /// <summary>
    /// Queue of pending commands for an entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CommandQueueEntry : IBufferElementData
    {
        public PlayerCommandType CommandType;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public uint IssuedTick;
        public FixedString64Bytes CommandId;
    }

    /// <summary>
    /// Visual feedback for issued commands.
    /// </summary>
    public struct CommandFeedback : IComponentData
    {
        /// <summary>Command type</summary>
        public PlayerCommandType CommandType;
        /// <summary>Target position for visual marker</summary>
        public float3 TargetPosition;
        /// <summary>Feedback timer</summary>
        public float FeedbackTimer;
        /// <summary>Feedback duration</summary>
        public float FeedbackDuration;
        /// <summary>Feedback color</summary>
        public float4 FeedbackColor;
    }

    /// <summary>
    /// Marker component for command feedback entities (visual-only).
    /// </summary>
    public struct CommandFeedbackTag : IComponentData { }
}


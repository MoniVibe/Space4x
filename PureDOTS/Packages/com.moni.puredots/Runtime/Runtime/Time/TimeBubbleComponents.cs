using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Mode of operation for a time bubble.
    /// </summary>
    public enum TimeBubbleMode : byte
    {
        /// <summary>Normal mode, applies scale factor to affected entities.</summary>
        Scale = 0,
        /// <summary>Pause mode, freezes affected entities.</summary>
        Pause = 1,
        /// <summary>Rewind mode, plays back history for affected entities.</summary>
        Rewind = 2,
        /// <summary>Stasis mode, completely freezes entities (no updates, no rendering changes).</summary>
        Stasis = 3,
        /// <summary>Fast-forward mode, accelerates time for affected entities.</summary>
        FastForward = 4
    }

    /// <summary>
    /// Volume type for time bubble spatial bounds.
    /// </summary>
    public enum TimeBubbleVolumeType : byte
    {
        /// <summary>Spherical volume defined by center and radius.</summary>
        Sphere = 0,
        /// <summary>Cylindrical volume (infinite height) defined by center and radius.</summary>
        Cylinder = 1,
        /// <summary>Box volume defined by bounds.</summary>
        Box = 2,
        /// <summary>Grid-based volume using spatial grid cells.</summary>
        Grid = 3
    }

    /// <summary>
    /// Unique identifier for a time bubble.
    /// </summary>
    public struct TimeBubbleId : IComponentData
    {
        /// <summary>Numeric identifier.</summary>
        public uint Id;
        /// <summary>Optional human-readable name for debugging.</summary>
        public FixedString32Bytes Name;

        /// <summary>
        /// Creates a new bubble ID with auto-generated name.
        /// </summary>
        public static TimeBubbleId Create(uint id)
        {
            return new TimeBubbleId
            {
                Id = id,
                Name = default
            };
        }

        /// <summary>
        /// Creates a new bubble ID with specified name.
        /// </summary>
        public static TimeBubbleId Create(uint id, FixedString32Bytes name)
        {
            return new TimeBubbleId
            {
                Id = id,
                Name = name
            };
        }
    }

    /// <summary>
    /// Authority policy for time bubbles in multiplayer.
    /// Determines how bubbles interact with player ownership.
    /// </summary>
    public enum TimeBubbleAuthorityPolicy : byte
    {
        /// <summary>Single-player only. Bubble is ignored in multiplayer.</summary>
        SinglePlayerOnly = 0,
        /// <summary>Local player only. In MP: only applies to the owner's presentation (client-side).</summary>
        LocalPlayerOnly,
        /// <summary>Authoritative shared. In MP: real simulation effect (future implementation).</summary>
        AuthoritativeShared
    }

    /// <summary>
    /// Parameters defining time bubble behavior.
    /// 
    /// SINGLE-PLAYER BEHAVIOR:
    /// - OwnerPlayerId = 0 (TimePlayerIds.SinglePlayer)
    /// - AffectsOwnedEntitiesOnly = false (affects all entities in volume)
    /// - AuthorityPolicy = SinglePlayerOnly
    /// 
    /// MULTIPLAYER SEMANTICS (future):
    /// - Bubbles will normally affect only entities whose owner matches OwnerPlayerId when AffectsOwnedEntitiesOnly == true.
    /// - When AffectsOwnedEntitiesOnly == false, bubbles are "shared" (e.g., global miracles, battlefield events) and affect all entities regardless of owner.
    /// - Non-owner entities may be excluded or treated differently (e.g., no rewind, only slow-time effects).
    /// - Cross-owner interaction rules (combat, trades, projectiles) will be defined in higher-level systems, not here.
    /// - AuthorityPolicy determines whether bubble effects are simulation (AuthoritativeShared) or presentation-only (LocalPlayerOnly).
    /// </summary>
    public struct TimeBubbleParams : IComponentData
    {
        /// <summary>ID of this bubble (matches TimeBubbleId.Id).</summary>
        public uint BubbleId;
        /// <summary>Mode of operation.</summary>
        public TimeBubbleMode Mode;
        /// <summary>Time scale factor (for Scale/FastForward modes).</summary>
        public float Scale;
        /// <summary>Offset in ticks for rewind mode.</summary>
        public int RewindOffsetTicks;
        /// <summary>Current playback tick for rewind mode.</summary>
        public uint PlaybackTick;
        /// <summary>Priority for overlap resolution (higher wins).</summary>
        public byte Priority;
        /// <summary>Owner player ID for multiplayer. In multiplayer, this determines which player's entities are affected. Single-player uses 0 (TimePlayerIds.SinglePlayer).</summary>
        public byte OwnerPlayerId;
        /// <summary>Whether this bubble affects only entities owned by OwnerPlayerId. Default false for single-player (affects all entities). In MP, true = player-specific bubble, false = shared bubble (global miracle, battlefield event).</summary>
        public bool AffectsOwnedEntitiesOnly;
        /// <summary>Source entity that created this bubble (miracle, tech, etc.).</summary>
        public Entity SourceEntity;
        /// <summary>Duration in ticks (0 = permanent until removed).</summary>
        public uint DurationTicks;
        /// <summary>Tick at which this bubble was created.</summary>
        public uint CreatedAtTick;
        /// <summary>Whether this bubble is currently active.</summary>
        public bool IsActive;
        /// <summary>Whether entities can enter/exit this bubble.</summary>
        public bool AllowMembershipChanges;
        /// <summary>Authority policy for multiplayer (determines if bubble affects simulation or presentation only).</summary>
        public TimeBubbleAuthorityPolicy AuthorityPolicy;

        /// <summary>
        /// Creates a scale bubble with specified parameters.
        /// </summary>
        public static TimeBubbleParams CreateScale(uint bubbleId, float scale, byte priority = 100)
        {
            return new TimeBubbleParams
            {
                BubbleId = bubbleId,
                Mode = TimeBubbleMode.Scale,
                Scale = scale,
                RewindOffsetTicks = 0,
                PlaybackTick = 0,
                Priority = priority,
                OwnerPlayerId = 0,
                AffectsOwnedEntitiesOnly = false,
                SourceEntity = Entity.Null,
                DurationTicks = 0,
                CreatedAtTick = 0,
                IsActive = true,
                AllowMembershipChanges = true,
                AuthorityPolicy = TimeBubbleAuthorityPolicy.SinglePlayerOnly
            };
        }

        /// <summary>
        /// Creates a pause bubble.
        /// </summary>
        public static TimeBubbleParams CreatePause(uint bubbleId, byte priority = 150)
        {
            return new TimeBubbleParams
            {
                BubbleId = bubbleId,
                Mode = TimeBubbleMode.Pause,
                Scale = 0f,
                RewindOffsetTicks = 0,
                PlaybackTick = 0,
                Priority = priority,
                OwnerPlayerId = 0,
                AffectsOwnedEntitiesOnly = false,
                SourceEntity = Entity.Null,
                DurationTicks = 0,
                CreatedAtTick = 0,
                IsActive = true,
                AllowMembershipChanges = true,
                AuthorityPolicy = TimeBubbleAuthorityPolicy.SinglePlayerOnly
            };
        }

        /// <summary>
        /// Creates a stasis bubble (complete freeze).
        /// </summary>
        public static TimeBubbleParams CreateStasis(uint bubbleId, byte priority = 200)
        {
            return new TimeBubbleParams
            {
                BubbleId = bubbleId,
                Mode = TimeBubbleMode.Stasis,
                Scale = 0f,
                RewindOffsetTicks = 0,
                PlaybackTick = 0,
                Priority = priority,
                OwnerPlayerId = 0,
                AffectsOwnedEntitiesOnly = false,
                SourceEntity = Entity.Null,
                DurationTicks = 0,
                CreatedAtTick = 0,
                IsActive = true,
                AllowMembershipChanges = false,
                AuthorityPolicy = TimeBubbleAuthorityPolicy.SinglePlayerOnly
            };
        }

        /// <summary>
        /// Creates a rewind bubble.
        /// </summary>
        public static TimeBubbleParams CreateRewind(uint bubbleId, int rewindOffsetTicks, byte priority = 175)
        {
            return new TimeBubbleParams
            {
                BubbleId = bubbleId,
                Mode = TimeBubbleMode.Rewind,
                Scale = -1f,
                RewindOffsetTicks = rewindOffsetTicks,
                PlaybackTick = 0,
                Priority = priority,
                OwnerPlayerId = 0,
                AffectsOwnedEntitiesOnly = false,
                SourceEntity = Entity.Null,
                DurationTicks = 0,
                CreatedAtTick = 0,
                IsActive = true,
                AllowMembershipChanges = false,
                AuthorityPolicy = TimeBubbleAuthorityPolicy.SinglePlayerOnly
            };
        }
    }

    /// <summary>
    /// Spatial volume definition for a time bubble.
    /// </summary>
    public struct TimeBubbleVolume : IComponentData
    {
        /// <summary>Center position of the volume.</summary>
        public float3 Center;
        /// <summary>Radius (for Sphere/Cylinder) or half-extents.x (for Box).</summary>
        public float Radius;
        /// <summary>Type of volume.</summary>
        public TimeBubbleVolumeType VolumeType;
        /// <summary>Half-extents for box volumes.</summary>
        public float3 HalfExtents;
        /// <summary>Height for cylinder (0 = infinite).</summary>
        public float Height;
        /// <summary>Whether to use Y coordinate in containment checks.</summary>
        public bool IgnoreY;

        /// <summary>
        /// Creates a spherical volume.
        /// </summary>
        public static TimeBubbleVolume CreateSphere(float3 center, float radius)
        {
            return new TimeBubbleVolume
            {
                Center = center,
                Radius = radius,
                VolumeType = TimeBubbleVolumeType.Sphere,
                HalfExtents = new float3(radius),
                Height = 0f,
                IgnoreY = false
            };
        }

        /// <summary>
        /// Creates a cylindrical volume (vertical axis).
        /// </summary>
        public static TimeBubbleVolume CreateCylinder(float3 center, float radius, float height = 0f)
        {
            return new TimeBubbleVolume
            {
                Center = center,
                Radius = radius,
                VolumeType = TimeBubbleVolumeType.Cylinder,
                HalfExtents = new float3(radius, height * 0.5f, radius),
                Height = height,
                IgnoreY = height <= 0f
            };
        }

        /// <summary>
        /// Creates a box volume.
        /// </summary>
        public static TimeBubbleVolume CreateBox(float3 center, float3 halfExtents)
        {
            return new TimeBubbleVolume
            {
                Center = center,
                Radius = math.cmax(halfExtents),
                VolumeType = TimeBubbleVolumeType.Box,
                HalfExtents = halfExtents,
                Height = halfExtents.y * 2f,
                IgnoreY = false
            };
        }

        /// <summary>
        /// Checks if a point is contained within this volume.
        /// </summary>
        public readonly bool Contains(float3 point)
        {
            float3 localPoint = point - Center;

            switch (VolumeType)
            {
                case TimeBubbleVolumeType.Sphere:
                    return math.lengthsq(localPoint) <= Radius * Radius;

                case TimeBubbleVolumeType.Cylinder:
                    float horizontalDistSq = localPoint.x * localPoint.x + localPoint.z * localPoint.z;
                    if (horizontalDistSq > Radius * Radius)
                        return false;
                    if (IgnoreY || Height <= 0f)
                        return true;
                    return math.abs(localPoint.y) <= Height * 0.5f;

                case TimeBubbleVolumeType.Box:
                    return math.abs(localPoint.x) <= HalfExtents.x &&
                           math.abs(localPoint.y) <= HalfExtents.y &&
                           math.abs(localPoint.z) <= HalfExtents.z;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Component attached to entities affected by a time bubble.
    /// Tracks the entity's current local time state.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct TimeBubbleMembership : IComponentData
    {
        /// <summary>ID of the bubble this entity belongs to.</summary>
        public uint BubbleId;
        /// <summary>Resolved local time mode.</summary>
        public TimeBubbleMode LocalMode;
        /// <summary>Resolved local time scale.</summary>
        public float LocalScale;
        /// <summary>Local playback tick (for rewind mode).</summary>
        public uint LocalPlaybackTick;
        /// <summary>Tick at which membership was established.</summary>
        public uint MemberSinceTick;
        /// <summary>Whether this entity was in a bubble last frame.</summary>
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
        public bool WasInBubblePreviousFrame;
        /// <summary>Priority of the bubble (cached for quick access).</summary>
        public byte BubblePriority;
    }

    /// <summary>
    /// Tag component indicating an entity can be affected by time bubbles.
    /// Entities without this tag ignore time bubbles.
    /// </summary>
    public struct TimeBubbleAffectableTag : IComponentData { }

    /// <summary>
    /// Tag component indicating an entity is currently in stasis.
    /// Systems should skip updates for entities with this tag.
    /// </summary>
    public struct StasisTag : IComponentData { }

    /// <summary>
    /// Buffer element for tracking which bubble IDs overlap at a position.
    /// </summary>
    public struct OverlappingBubble : IBufferElementData
    {
        public uint BubbleId;
        public byte Priority;
        public TimeBubbleMode Mode;
        public float Scale;
    }

    /// <summary>
    /// Singleton state for the time bubble system.
    /// </summary>
    public struct TimeBubbleSystemState : IComponentData
    {
        /// <summary>Next available bubble ID.</summary>
        public uint NextBubbleId;
        /// <summary>Current number of active bubbles.</summary>
        public int ActiveBubbleCount;
        /// <summary>Total entities currently affected by bubbles.</summary>
        public int AffectedEntityCount;
        /// <summary>Last tick at which bubble membership was updated.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Request to create a new time bubble.
    /// </summary>
    public struct TimeBubbleCreateRequest : IComponentData
    {
        public float3 Center;
        public float Radius;
        public TimeBubbleMode Mode;
        public float Scale;
        public byte Priority;
        public uint DurationTicks;
        public Entity SourceEntity;
        public bool IsPending;
    }

    /// <summary>
    /// Request to remove a time bubble.
    /// </summary>
    public struct TimeBubbleRemoveRequest : IComponentData
    {
        public uint BubbleId;
        public bool IsPending;
    }
}


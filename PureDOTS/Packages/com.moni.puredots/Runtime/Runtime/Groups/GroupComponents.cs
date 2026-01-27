using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Generic group member entry.
    /// Game-agnostic: can represent band members, guild members, fleet crews, etc.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct GroupMember : IBufferElementData
    {
        /// <summary>
        /// Entity that is a member of the group.
        /// </summary>
        public Entity MemberEntity;

        /// <summary>
        /// Compatibility alias for member entity access.
        /// </summary>
        public Entity Member
        {
            get => MemberEntity;
            set => MemberEntity = value;
        }

        /// <summary>
        /// Influence weight on group aggregates (0-1).
        /// Leaders typically have higher weight.
        /// </summary>
        public float Weight;

        /// <summary>
        /// Role within the group.
        /// </summary>
        public GroupRole Role;

        /// <summary>
        /// Tick when member joined.
        /// </summary>
        public uint JoinedTick;

        /// <summary>
        /// Member-specific flags.
        /// </summary>
        public GroupMemberFlags Flags;
    }

    /// <summary>
    /// Roles within a group.
    /// </summary>
    public enum GroupRole : byte
    {
        /// <summary>Regular member.</summary>
        Member = 0,
        /// <summary>Group leader.</summary>
        Leader = 1,
        /// <summary>Second in command.</summary>
        Lieutenant = 2,
        /// <summary>Specialist role.</summary>
        Specialist = 3,
        /// <summary>New/probationary member.</summary>
        Recruit = 4,
        /// <summary>Honorary/inactive member.</summary>
        Honorary = 5
    }

    /// <summary>
    /// Flags for group members.
    /// </summary>
    [System.Flags]
    public enum GroupMemberFlags : byte
    {
        None = 0,
        /// <summary>Member is currently active.</summary>
        Active = 1 << 0,
        /// <summary>Member is temporarily away.</summary>
        Away = 1 << 1,
        /// <summary>Member has voting rights.</summary>
        CanVote = 1 << 2,
        /// <summary>Member can invite others.</summary>
        CanInvite = 1 << 3,
        /// <summary>Member is marked for removal.</summary>
        PendingRemoval = 1 << 4
    }

    /// <summary>
    /// Configuration for a group entity.
    /// </summary>
    public struct GroupConfig : IComponentData
    {
        /// <summary>
        /// Maximum members allowed.
        /// </summary>
        public byte MaxMembers;

        /// <summary>
        /// Minimum members to remain viable.
        /// </summary>
        public byte MinMembers;

        /// <summary>
        /// Group type identifier.
        /// </summary>
        public GroupType Type;

        /// <summary>
        /// How often to recalculate aggregates (ticks).
        /// </summary>
        public ushort AggregationInterval;

        /// <summary>
        /// Configuration flags.
        /// </summary>
        public GroupConfigFlags Flags;

        /// <summary>
        /// Creates default config.
        /// </summary>
        public static GroupConfig Default => new GroupConfig
        {
            MaxMembers = 16,
            MinMembers = 1,
            Type = GroupType.Generic,
            AggregationInterval = 60,
            Flags = GroupConfigFlags.None
        };
    }

    /// <summary>
    /// Types of groups.
    /// </summary>
    public enum GroupType : byte
    {
        /// <summary>Generic group.</summary>
        Generic = 0,
        /// <summary>Military band/squad.</summary>
        Military = 1,
        /// <summary>Craft/profession guild.</summary>
        Guild = 2,
        /// <summary>Social/family group.</summary>
        Social = 3,
        /// <summary>Trading caravan.</summary>
        Caravan = 4,
        /// <summary>Ship crew.</summary>
        Crew = 5,
        /// <summary>Faction/political group.</summary>
        Faction = 6
    }

    /// <summary>
    /// Configuration flags for groups.
    /// </summary>
    [System.Flags]
    public enum GroupConfigFlags : byte
    {
        None = 0,
        /// <summary>Group persists if leader dies.</summary>
        PersistWithoutLeader = 1 << 0,
        /// <summary>Group can merge with similar groups.</summary>
        CanMerge = 1 << 1,
        /// <summary>Group can split into smaller groups.</summary>
        CanSplit = 1 << 2,
        /// <summary>New members need approval.</summary>
        RequiresApproval = 1 << 3,
        /// <summary>Members can leave freely.</summary>
        OpenMembership = 1 << 4
    }

    /// <summary>
    /// Aggregated statistics for a group.
    /// Computed from member data.
    /// </summary>
    public struct GroupAggregate : IComponentData
    {
        /// <summary>
        /// Number of active members.
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Center of mass of all members.
        /// </summary>
        public float3 CenterOfMass;

        /// <summary>
        /// Spread/dispersion of members.
        /// </summary>
        public float Dispersion;

        /// <summary>
        /// Average health across members.
        /// </summary>
        public float AverageHealth;

        /// <summary>
        /// Average morale across members.
        /// </summary>
        public float AverageMorale;

        /// <summary>
        /// Combined strength/power rating.
        /// </summary>
        public float TotalStrength;

        /// <summary>
        /// Group cohesion (0-1, how well members work together).
        /// </summary>
        public float Cohesion;

        /// <summary>
        /// Tick when aggregates were last computed.
        /// </summary>
        public uint LastComputeTick;
    }

    /// <summary>
    /// Group identity and naming.
    /// </summary>
    public struct GroupIdentity : IComponentData
    {
        /// <summary>
        /// Unique group ID.
        /// </summary>
        public int GroupId;

        /// <summary>
        /// Parent faction/organization.
        /// </summary>
        public Entity ParentEntity;

        /// <summary>
        /// Current leader entity.
        /// </summary>
        public Entity LeaderEntity;

        /// <summary>
        /// Tick when group was formed.
        /// </summary>
        public uint FormationTick;

        /// <summary>
        /// Group status.
        /// </summary>
        public GroupStatus Status;
    }

    /// <summary>
    /// Group status states.
    /// </summary>
    public enum GroupStatus : byte
    {
        /// <summary>Group is forming.</summary>
        Forming = 0,
        /// <summary>Group is active and operational.</summary>
        Active = 1,
        /// <summary>Group is disbanding.</summary>
        Disbanding = 2,
        /// <summary>Group is defunct/historical.</summary>
        Defunct = 3,
        /// <summary>Group is temporarily inactive.</summary>
        Suspended = 4
    }

    /// <summary>
    /// Command to add a member to a group.
    /// </summary>
    public struct AddGroupMemberCommand : IComponentData
    {
        public Entity GroupEntity;
        public Entity MemberEntity;
        public GroupRole Role;
        public float Weight;
    }

    /// <summary>
    /// Command to remove a member from a group.
    /// </summary>
    public struct RemoveGroupMemberCommand : IComponentData
    {
        public Entity GroupEntity;
        public Entity MemberEntity;
        public RemovalReason Reason;
    }

    /// <summary>
    /// Reasons for removing a group member.
    /// </summary>
    public enum RemovalReason : byte
    {
        /// <summary>Member left voluntarily.</summary>
        Left = 0,
        /// <summary>Member was expelled.</summary>
        Expelled = 1,
        /// <summary>Member died.</summary>
        Died = 2,
        /// <summary>Member was transferred to another group.</summary>
        Transferred = 3,
        /// <summary>Group disbanded.</summary>
        GroupDisbanded = 4
    }

    /// <summary>
    /// Tag for the group command queue entity.
    /// </summary>
    public struct GroupCommandQueue : IComponentData
    {
        public int PendingAdditions;
        public int PendingRemovals;
        public uint LastProcessTick;
    }

    /// <summary>
    /// Buffer of pending add commands.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PendingGroupAddition : IBufferElementData
    {
        public Entity GroupEntity;
        public Entity MemberEntity;
        public GroupRole Role;
        public float Weight;
        public uint RequestTick;
    }

    /// <summary>
    /// Buffer of pending remove commands.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PendingGroupRemoval : IBufferElementData
    {
        public Entity GroupEntity;
        public Entity MemberEntity;
        public RemovalReason Reason;
        public uint RequestTick;
    }

    // NOTE: GroupTag, GroupStance, and GroupStanceState are defined in Runtime/Groups/Components.cs
    // Duplicate definitions removed to avoid conflicts

    /// <summary>
    /// Helper methods for creating groups.
    /// </summary>
    public static class GroupHelpers
    {
        /// <summary>
        /// Creates a band/group entity with members.
        /// </summary>
        public static Entity CreateBand(
            EntityCommandBuffer ecb,
            Entity ownerOrg,
            NativeArray<Entity> members,
            int groupId,
            float3 position,
            uint currentTick = 0)
        {
            var groupEntity = ecb.CreateEntity();
            ecb.AddComponent(groupEntity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            ecb.AddComponent<GroupTag>(groupEntity);
            ecb.AddComponent(groupEntity, new GroupIdentity
            {
                GroupId = groupId,
                ParentEntity = ownerOrg,
                LeaderEntity = members.Length > 0 ? members[0] : Entity.Null,
                FormationTick = currentTick,
                Status = GroupStatus.Active
            });
            ecb.AddComponent(groupEntity, GroupConfig.Default);

            var groupMembers = ecb.AddBuffer<GroupMember>(groupEntity);
            for (int i = 0; i < members.Length; i++)
            {
                groupMembers.Add(new GroupMember
                {
                    MemberEntity = members[i],
                    Weight = i == 0 ? 1f : 0.5f, // First member is leader
                    Role = i == 0 ? GroupRole.Leader : GroupRole.Member,
                    JoinedTick = currentTick,
                    Flags = GroupMemberFlags.Active
                });
            }

            return groupEntity;
        }
    }
}


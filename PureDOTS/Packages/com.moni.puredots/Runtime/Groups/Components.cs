using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Marker component indicating this entity is a group container (band, wing, squadron, fleet).
    /// Enableable to allow deactivating groups without archetype changes.
    /// </summary>
    public struct GroupTag : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Unique identifier for a group, with faction association.
    /// </summary>
    public struct GroupId : IComponentData
    {
        public int Value;
        public int FactionId;
    }

    /// <summary>
    /// Type of group - determines behavior systems and formation rules.
    /// </summary>
    public enum GroupKind : byte
    {
        GroundBand,      // Godgame villagers
        StrikeWing,      // Space4x strike craft
        MiningWing,      // Space4x mining vessels
        FleetTaskUnit   // Space4x fleet groups
    }

    /// <summary>
    /// Metadata about a group: kind, leader, size limits.
    /// </summary>
    public struct GroupMeta : IComponentData
    {
        public GroupKind Kind;
        public Entity Leader;          // May be Entity.Null
        public byte MaxSize;
    }

    // NOTE: GroupMember is now defined in Runtime/Runtime/Groups/GroupComponents.cs
    // This duplicate definition has been removed to avoid conflicts
    // The canonical definition uses Entity MemberEntity, float Weight, GroupRole Role, etc.

    /// <summary>
    /// Formation type - spatial arrangement pattern.
    /// </summary>
    public enum FormationType : byte
    {
        Line,
        Wedge,
        Sphere,      // 3D for space
        Swarm,
        Column,
        Custom
    }

    /// <summary>
    /// Group stance - tactical posture and behavior mode.
    /// </summary>
    public enum GroupStance : byte
    {
        Hold,
        Attack,
        Skirmish,
        Retreat,
        Screen,
        IndependentHunt
    }

    /// <summary>
    /// Formation configuration for group members.
    /// </summary>
    public struct GroupFormation : IComponentData
    {
        public FormationType Type;
        public float Spacing;           // Distance between members
        public float Cohesion;          // 0..1 tolerance for deviation
        public float FacingWeight;       // How strongly members orient on group facing
    }

    /// <summary>
    /// Current stance state of the group.
    /// </summary>
    public struct GroupStanceState : IComponentData
    {
        public GroupStance Stance;
        public Entity PrimaryTarget;    // May be Entity.Null
        public float Aggression;        // -1..1 modifies stances
        public float Discipline;        // 0..1: obedience vs individuality
    }

    /// <summary>
    /// Aggregated morale state for the group.
    /// </summary>
    public struct GroupMoraleState : IComponentData
    {
        public float AverageMorale;     // Average of member morale [-1..+1]
        public float CasualtyRatio;     // 0..1: fraction of members lost
        public byte Routing;            // 0/1: group is routing/fleeing
    }

    /// <summary>
    /// Individual behavior parameters when part of a group.
    /// Derived from alignment, personality, and experience.
    /// </summary>
    public struct GroupBehaviorParams : IComponentData
    {
        public float Obedience;         // 0..1 (alignment & culture derived)
        public float Independence;      // 0..1 (chaos, ego, ace status)
        public float CohesionPreference; // How much they LIKE staying in formation
    }

    /// <summary>
    /// Individual tactical intent - what this member wants to do.
    /// </summary>
    public enum IndividualTacticalIntent : byte
    {
        FollowGroupOrder,
        AggressivePursuit,
        CautiousHold,
        Flee,
        Desert,
        Mutiny,
    }

    /// <summary>
    /// Individual combat intent - tactical decision for this member.
    /// </summary>
    public struct IndividualCombatIntent : IComponentData
    {
        public IndividualTacticalIntent Intent;
        public Entity TargetOverride;  // Optional personal target (may be Entity.Null)
    }
}


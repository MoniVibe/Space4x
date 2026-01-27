using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Group objective types (shared across games).
    /// Games can extend with custom objectives via Custom0-Custom15.
    /// </summary>
    public enum GroupObjectiveType : byte
    {
        None = 0,

        // Universal objectives
        Idle = 1,
        MoveTo = 2,
        Defend = 3,
        Patrol = 4,

        // Godgame objectives
        ExpandSettlement = 10,
        Forage = 11,
        Migrate = 12,
        Build = 13,
        Worship = 14,

        // Space4x objectives
        SecureSystem = 20,
        EscortConvoy = 21,
        Raid = 22,
        PatrolRoute = 23,
        Retreat = 24,
        Mining = 25,

        // Custom objectives (for game-specific extensions)
        Custom0 = 100,
        Custom1 = 101,
        Custom2 = 102,
        Custom3 = 103,
        Custom4 = 104,
        Custom5 = 105,
        Custom6 = 106,
        Custom7 = 107,
        Custom8 = 108,
        Custom9 = 109,
        Custom10 = 110,
        Custom11 = 111,
        Custom12 = 112,
        Custom13 = 113,
        Custom14 = 114,
        Custom15 = 115
    }

    /// <summary>
    /// Current group objective with optional parameters.
    /// Phase 1: Simple objective + target.
    /// Phase 2: Extended with conditions, success criteria, etc.
    /// </summary>
    public struct GroupObjective : IComponentData
    {
        /// <summary>
        /// Current objective type.
        /// </summary>
        public GroupObjectiveType ObjectiveType;

        /// <summary>
        /// Optional target entity (e.g., enemy to attack, resource to gather).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Optional target position (e.g., location to move to, defend, patrol).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Objective priority (0-255, higher = more important).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Tick when objective was set.
        /// </summary>
        public uint SetTick;

        /// <summary>
        /// Optional expiration tick (0 = no expiration).
        /// </summary>
        public uint ExpirationTick;

        /// <summary>
        /// Whether objective is currently active.
        /// </summary>
        public byte IsActive;
    }

    /// <summary>
    /// Group metrics (aggregated stats from members).
    /// Phase 1: Minimal metrics (member counts, basic resources, threat).
    /// Phase 2: Extended with detailed stats, cohesion, etc.
    /// </summary>
    public struct GroupMetrics : IComponentData
    {
        /// <summary>
        /// Total member count.
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Active member count (not away/disabled).
        /// </summary>
        public int ActiveMemberCount;

        /// <summary>
        /// Member counts per type/class (Phase 1: explicit fields).
        /// Fields 0-7: Reserved for common types.
        /// Fields 8-15: Game-specific types.
        /// </summary>
        public byte MemberCountType0;
        public byte MemberCountType1;
        public byte MemberCountType2;
        public byte MemberCountType3;
        public byte MemberCountType4;
        public byte MemberCountType5;
        public byte MemberCountType6;
        public byte MemberCountType7;
        public byte MemberCountType8;
        public byte MemberCountType9;
        public byte MemberCountType10;
        public byte MemberCountType11;
        public byte MemberCountType12;
        public byte MemberCountType13;
        public byte MemberCountType14;
        public byte MemberCountType15;

        /// <summary>
        /// Basic resource counts (Phase 1: explicit fields).
        /// Field 0: Food/Supplies
        /// Field 1: Fuel/Energy
        /// Field 2: Ammunition
        /// Fields 3-7: Game-specific resources
        /// </summary>
        public float ResourceCount0; // Food/Supplies
        public float ResourceCount1; // Fuel/Energy
        public float ResourceCount2; // Ammunition
        public float ResourceCount3;
        public float ResourceCount4;
        public float ResourceCount5;
        public float ResourceCount6;
        public float ResourceCount7;

        /// <summary>
        /// Estimated threat level (0-255).
        /// </summary>
        public byte ThreatLevel;

        /// <summary>
        /// Average health across members (0-1).
        /// </summary>
        public float AverageHealth;

        /// <summary>
        /// Average morale across members (0-1).
        /// </summary>
        public float AverageMorale;

        /// <summary>
        /// Tick when metrics were last computed.
        /// </summary>
        public uint LastComputedTick;
    }

    /// <summary>
    /// Resource budget for group (allocation limits).
    /// Phase 1: Simple per-resource limits.
    /// </summary>
    public struct GroupResourceBudget : IComponentData
    {
        /// <summary>
        /// Maximum resource counts (matches GroupMetrics resource fields).
        /// </summary>
        public float MaxResource0;
        public float MaxResource1;
        public float MaxResource2;
        public float MaxResource3;
        public float MaxResource4;
        public float MaxResource5;
        public float MaxResource6;
        public float MaxResource7;

        /// <summary>
        /// Current allocation (how much is currently assigned/committed).
        /// </summary>
        public float AllocatedResource0;
        public float AllocatedResource1;
        public float AllocatedResource2;
        public float AllocatedResource3;
        public float AllocatedResource4;
        public float AllocatedResource5;
        public float AllocatedResource6;
        public float AllocatedResource7;

        /// <summary>
        /// Whether budget is enforced (0 = advisory, 1 = hard limit).
        /// </summary>
        public byte IsEnforced;
    }
}


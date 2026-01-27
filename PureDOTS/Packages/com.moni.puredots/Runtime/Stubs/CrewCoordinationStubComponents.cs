// [TRI-STUB] Stub components for crew coordination
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Crew member - crew entity with role.
    /// </summary>
    public struct CrewMember : IComponentData
    {
        public Entity CrewEntity;
        public CrewRole Role;
        public Entity AssignedStation;
    }

    /// <summary>
    /// Crew roles.
    /// </summary>
    public enum CrewRole : byte
    {
        Pilot = 0,
        Operator = 1,
        Mechanic = 2,
        DeckChief = 3,
        Armorer = 4,
        Refueler = 5,
        Launcher = 6,
        Recovery = 7,
        Inspector = 8
    }

    /// <summary>
    /// Crew task - task assigned to crew member.
    /// </summary>
    public struct CrewTask : IComponentData
    {
        public Entity TaskEntity;
        public CrewTaskType TaskType;
        public float TaskProgress;
        public uint TaskStartTick;
    }

    /// <summary>
    /// Crew task types.
    /// </summary>
    public enum CrewTaskType : byte
    {
        PilotCraft = 0,
        OperateSensors = 1,
        MaintainEquipment = 2,
        LoadMunitions = 3,
        Refuel = 4,
        LaunchCraft = 5,
        RecoverCraft = 6,
        InspectCraft = 7
    }

    /// <summary>
    /// Operator-pilot link - coordination between operator and pilot.
    /// </summary>
    public struct OperatorPilotLink : IComponentData
    {
        public Entity Operator;
        public Entity Pilot;
        public float LinkQuality;
        public float SensorDataQuality;
        public float GuidanceBonus;
        public float ThreatAwarenessBonus;
        public float NavigationBonus;
        public float TargetingBonus;
    }

    /// <summary>
    /// Hangar operations - coordinated hangar operations.
    /// </summary>
    public struct HangarOperations : IComponentData
    {
        public Entity HangarBay;
        public float OperationalEfficiency;
        public float DeploymentSpeed;
        public float MaintenanceQuality;
        public byte CrewMemberCount;
    }

    /// <summary>
    /// Hangar crew member buffer.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct HangarCrewMember : IBufferElementData
    {
        public Entity CrewEntity;
        public CrewRole Role;
        public float RoleSkill;
        public float Fatigue;
        public byte IsOnDuty;
    }
}


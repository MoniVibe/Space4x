using Unity.Entities;

namespace Space4X.Registry
{
    public enum SkillDomain : byte
    {
        Mining = 0,
        Hauling = 1,
        Combat = 2,
        Repair = 3,
        Exploration = 4
    }

    public enum HazardTypeId : byte
    {
        Radiation = 0,
        Fauna = 1,
        Anomaly = 2,
        Thermal = 3,
        Void = 4
    }

    /// <summary>
    /// Per-crew skill multipliers used by mining, hauling, combat, refit, and exploration systems.
    /// Stored on the entity representing the active crew (often the vessel).
    /// </summary>
    public struct CrewSkills : IComponentData
    {
        public float MiningSkill;
        public float HaulingSkill;
        public float CombatSkill;
        public float RepairSkill;
        public float ExplorationSkill;
    }

    /// <summary>
    /// Accumulated experience counters for each skill domain. Skills derive from these values.
    /// </summary>
    public struct SkillExperienceGain : IComponentData
    {
        public float MiningXp;
        public float HaulingXp;
        public float CombatXp;
        public float RepairXp;
        public float ExplorationXp;
        public uint LastProcessedTick;
    }

    /// <summary>
    /// Hazard resistance entries keyed by hazard type with a multiplier in [0,1].
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HazardResistance : IBufferElementData
    {
        public HazardTypeId HazardType;
        public float ResistanceMultiplier;
    }

    /// <summary>
    /// Incoming hazard damage events before mitigation.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct HazardDamageEvent : IBufferElementData
    {
        public HazardTypeId HazardType;
        public float Amount;
    }

    /// <summary>
    /// Command-log style entry for skill gains to keep rewind/telemetry in sync.
    /// </summary>
    public struct SkillChangeLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity TargetEntity;
        public SkillDomain Domain;
        public float DeltaXp;
        public float NewSkill;
    }
}

using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Crew
{
    /// <summary>
    /// Crew role enumeration.
    /// </summary>
    public enum CrewRole : byte
    {
        Pilot = 0,
        Engineer = 1,
        Gunner = 2,
        Medic = 3
    }

    /// <summary>
    /// Crew specification data - defines crew role behavior and progression.
    /// </summary>
    public struct CrewSpec
    {
        public FixedString32Bytes Id;
        public byte Role; // CrewRole enum
        public float XpPerAction; // XP awarded per action
        public float FatiguePerHour; // Fatigue accumulated per hour
        public float RepairMultPerLvl; // Repair time multiplier per level (reduces time)
        public float AccuracyMultPerLvl; // Accuracy multiplier per level (increases accuracy)
    }

    /// <summary>
    /// Blob catalog for crew specifications.
    /// </summary>
    public struct CrewCatalogBlob
    {
        public BlobArray<CrewSpec> CrewSpecs;
    }

    /// <summary>
    /// Singleton component holding crew catalog reference.
    /// </summary>
    public struct CrewCatalog : IComponentData
    {
        public BlobAssetReference<CrewCatalogBlob> Catalog;
    }

    /// <summary>
    /// Crew state component - tracks XP, level, fatigue for individual crew members.
    /// </summary>
    public struct CrewState : IComponentData
    {
        public FixedString32Bytes CrewSpecId;
        public float Experience; // Current XP
        public byte Level; // Current level (derived from XP)
        public float Fatigue; // Current fatigue (0-1)
        public uint LastActionTick; // Last tick when XP was awarded
    }

    /// <summary>
    /// Component marking crew XP award events.
    /// </summary>
    public struct CrewXpAward : IComponentData
    {
        public Entity CrewEntity;
        public float XpAmount;
        public byte AwardReason; // 0=combat, 1=mining, 2=hauling, etc.
    }
}


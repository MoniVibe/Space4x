using System;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace Godgame.Registry
{
    /// <summary>
    /// Minimal villager summary data used by the Godgame registry bridge.
    /// </summary>
    public struct GodgameVillager : IComponentData
    {
        public FixedString64Bytes DisplayName;
        public int VillagerId;
        public int FactionId;
        public byte IsAvailable;
        public byte IsReserved;
        public float HealthPercent;
        public float MoralePercent;
        public float EnergyPercent;
        public VillagerJob.JobType JobType;
        public VillagerJob.JobPhase JobPhase;
        public VillagerDisciplineType Discipline;
        public byte DisciplineLevel;
        public byte IsCombatReady;
        public VillagerAIState.State AIState;
        public VillagerAIState.Goal AIGoal;
        public Entity CurrentTarget;
        public uint ActiveTicketId;
        public ushort CurrentResourceTypeIndex;
        public float Productivity;
    }

    /// <summary>
    /// Minimal storehouse summary data mirrored into the shared registry.
    /// </summary>
    public struct GodgameStorehouse : IComponentData
    {
        public FixedString64Bytes Label;
        public int StorehouseId;
        public float TotalCapacity;
        public float TotalStored;
        public float TotalReserved;
        public ushort PrimaryResourceTypeIndex;
        public uint LastMutationTick;
        public FixedList32Bytes<GodgameStorehouseResourceSummary> ResourceSummaries;
    }

    /// <summary>
    /// Per-resource capacity summary for a Godgame storehouse.
    /// </summary>
    public struct GodgameStorehouseResourceSummary
    {
        public ushort ResourceTypeIndex;
        public float Capacity;
        public float Stored;
        public float Reserved;
    }

    /// <summary>
    /// Snapshot cached by the registry bridge so presentation systems can publish telemetry.
    /// </summary>
    public struct GodgameRegistrySnapshot : IComponentData
    {
        public int VillagerCount;
        public int AvailableVillagers;
        public int IdleVillagers;
        public int ReservedVillagers;
        public int CombatReadyVillagers;
        public float AverageVillagerHealth;
        public float AverageVillagerMorale;
        public float AverageVillagerEnergy;
        public int StorehouseCount;
        public float TotalStorehouseCapacity;
        public float TotalStorehouseStored;
        public float TotalStorehouseReserved;
        public uint LastRegistryTick;
    }

    /// <summary>
    /// Canonical archetype ids reserved for Godgame specific registry metadata.
    /// Values chosen to avoid collisions with other projects.
    /// </summary>
    public static class GodgameRegistryIds
    {
        public const ushort VillagerArchetype = 0x4701;
        public const ushort StorehouseArchetype = 0x4702;
    }
}

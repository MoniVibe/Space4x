#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Village
{
    /// <summary>
    /// Legacy scenario components for the PureDOTS village slice.
    /// These components are used by scenario systems to create and manage simple village entities.
    /// </summary>

    /// <summary>
    /// World-level tag to enable village scenario presentation systems in host game worlds.
    /// Add this component to a world entity to enable VillageVisualSetupSystem.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillageWorldTag")]
    public struct VillageWorldTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a village entity in the scenario.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillageTag")]
    public struct VillageTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a villager entity in the scenario.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillagerTag")]
    public struct VillagerTag : IComponentData { }

    /// <summary>
    /// Component defining a home lot with its position.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "HomeLot")]
    public struct HomeLot : IComponentData
    {
        public float3 Position;
    }

    /// <summary>
    /// Component defining a work lot with its position.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "WorkLot")]
    public struct WorkLot : IComponentData
    {
        public float3 Position;
    }

    /// <summary>
    /// Component storing a villager's home position.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillagerHome")]
    public struct VillagerHome : IComponentData
    {
        public float3 Position;
    }

    /// <summary>
    /// Component storing a villager's work position.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillagerWork")]
    public struct VillagerWork : IComponentData
    {
        public float3 Position;
    }

    /// <summary>
    /// Component tracking a villager's current state in their work/home cycle.
    /// Phase 0 = going to work, Phase 1 = going home.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillagerState")]
    public struct VillagerState : IComponentData
    {
        public byte Phase;
    }
}
#endif

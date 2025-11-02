using System;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Custom bootstrap that creates the single simulation world we use for the pure DOTS run.
    /// Establishes the root system groups and appends the world to the player loop so no
    /// MonoBehaviour bootstrap is required.
    /// </summary>
    public sealed class PureDotsWorldBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            // Always run a single game world for now; presentation happens through system groups.
            var world = new World(defaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;

            // Pull every auto-created system (including editor/scene streaming helpers) into the world.
            var systems = DefaultWorldInitialization.GetAllSystems(
                WorldSystemFilterFlags.Default |
                WorldSystemFilterFlags.Editor |
                WorldSystemFilterFlags.Streaming |
                WorldSystemFilterFlags.ProcessAfterLoad |
                WorldSystemFilterFlags.EntitySceneOptimizations);

            var filteredSystems = new System.Collections.Generic.List<Type>(systems);
            RemoveSystemType(filteredSystems, "Unity.Rendering.FreezeStaticLODObjects, Unity.Entities.Graphics");
            RemoveSystemType(filteredSystems, "Unity.Rendering.UpdateSceneBoundingVolumeFromRendererBounds, Unity.Entities.Graphics");

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, filteredSystems);

            // Align fixed-step timing with the simulation tick assumptions.
            if (world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>() is { } fixedStepGroup)
            {
                fixedStepGroup.Timestep = 1f / 60f;
            }

            // Force creation of our custom groups early so ordering attributes are respected.
            ConfigureRootGroups(world);
            world.GetOrCreateSystemManaged<TimeSystemGroup>();
            world.GetOrCreateSystemManaged<VillagerSystemGroup>();
            world.GetOrCreateSystemManaged<ResourceSystemGroup>();
            world.GetOrCreateSystemManaged<CombatSystemGroup>();
            world.GetOrCreateSystemManaged<HandSystemGroup>();
            world.GetOrCreateSystemManaged<VegetationSystemGroup>();
            world.GetOrCreateSystemManaged<ConstructionSystemGroup>();
            world.GetOrCreateSystemManaged<HistorySystemGroup>();
            world.GetOrCreateSystemManaged<Unity.Rendering.StructuralChangePresentationSystemGroup>();

            AlignEntitiesGraphicsStructuralSystems(world);

            // Make sure everything ends up in the player loop.
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            Debug.Log("[PureDotsWorldBootstrap] Default DOTS world initialized.");
            return true;
        }

        private static void ConfigureRootGroups(World world)
        {
            var initializationGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            initializationGroup.SortSystems();

            if (world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>() is { } fixedStepGroup)
            {
                fixedStepGroup.SortSystems();
            }

            var simulationGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.SortSystems();

            var presentationGroup = world.GetOrCreateSystemManaged<PresentationSystemGroup>();
            presentationGroup.SortSystems();
        }

        private static void AlignEntitiesGraphicsStructuralSystems(World world)
        {
            var structuralGroup = world.GetExistingSystemManaged<Unity.Rendering.StructuralChangePresentationSystemGroup>();
            if (structuralGroup == null)
            {
                return;
            }

            var simulationGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();

            EnsureSystemInGroup(world, structuralGroup, "Unity.Rendering.FreezeStaticLODObjects, Unity.Entities.Graphics");
            EnsureSystemInGroup(world, structuralGroup, "Unity.Rendering.UpdateSceneBoundingVolumeFromRendererBounds, Unity.Entities.Graphics");
            EnsureSystemInGroup(world, structuralGroup, "Unity.Rendering.LODRequirementsUpdateSystem, Unity.Entities.Graphics");
            EnsureSystemInGroup(world, structuralGroup, "Unity.Rendering.RenderBoundsUpdateSystem, Unity.Entities.Graphics");

            structuralGroup.SortSystems();
            simulationGroup?.SortSystems();
            presentationGroup?.SortSystems();
        }

        private static void RemoveSystemType(System.Collections.Generic.List<Type> list, string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                return;
            }

            list.Remove(type);
        }

        private static void EnsureSystemInGroup(World world, ComponentSystemGroup group, string typeName)
        {
            var systemType = Type.GetType(typeName);
            if (systemType == null)
            {
                return;
            }

            if (world.GetOrCreateSystemManaged(systemType) is not ComponentSystemBase system)
            {
                return;
            }

            group.RemoveSystemFromUpdateList(system);
            group.AddSystemToUpdateList(system);
        }
    }
}

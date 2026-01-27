using PureDOTS.Runtime.Core;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Custom bootstrap that creates the single DOTS world for PureDOTS-based games.
    /// </summary>
    public sealed class PureDotsWorldBootstrap : ICustomBootstrap
    {
        private const string WorldName = "Game World";

        public bool Initialize(string defaultWorldName)
        {
            // Create the world that will host every system.
            var world = new World(WorldName);
            World.DefaultGameObjectInjectionWorld = world;

            // Gather the systems that should exist for this profile.
            // If specific filters are needed later, adjust the flags here.
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

            // Ensure the core root groups exist so Attributes can wire sub-groups correctly.
            world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<Unity.Entities.PresentationSystemGroup>();

            // Let Entities place every system into its declared group chain.
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

            // Ensure the canonical Game World tag exists so gameplay systems can gate themselves.
            var entityManager = world.EntityManager;
            using (var gameWorldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Core.GameWorldTag>()))
            {
                if (!gameWorldQuery.HasSingleton<PureDOTS.Runtime.Core.GameWorldTag>())
                {
                    entityManager.CreateEntity(typeof(PureDOTS.Runtime.Core.GameWorldTag));
                }
            }

            // Hook the world into the player loop so it actually updates each frame.
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[PureDotsWorldBootstrap] DOTS world initialized with profile '{WorldName}'.");
#endif

            // Returning true prevents Unity from creating a duplicate Default World.
            return true;
        }
    }
}

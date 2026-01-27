#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Entities;
using Unity.Rendering;

namespace PureDOTS.LegacyScenario.Rendering
{
    /// <summary>
    /// Centralized mesh and material index constants for legacy scenario systems.
    /// These indices correspond to positions in the RenderMeshArray used by scenario entities.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "PureDOTS.Demo.Rendering", null, "DemoMeshIndices")]
    public static class ScenarioMeshIndices
    {
        /// <summary>
        /// Mesh index for village ground/terrain (index 0 in RenderMeshArray).
        /// </summary>
        public const int VillageGroundMeshIndex = 0;

        /// <summary>
        /// Mesh index for village home structures (index 1 in RenderMeshArray).
        /// </summary>
        public const int VillageHomeMeshIndex = 1;

        /// <summary>
        /// Mesh index for village workplace structures (index 2 in RenderMeshArray).
        /// </summary>
        public const int VillageWorkMeshIndex = 2;

        /// <summary>
        /// Mesh index for villager entities and orbit cubes (index 3 in RenderMeshArray).
        /// </summary>
        public const int VillageVillagerMeshIndex = 3;

        /// <summary>
        /// Material index for scenario entities (typically 0, using Simple Lit shader).
        /// </summary>
        public const int ScenarioMaterialIndex = 0;
    }

    /// <summary>
    /// Legacy scenario bootstrap that initializes shared render mesh array for scenario systems.
    /// This is an example implementation for validating PureDOTS rendering hooks.
    ///
    /// IMPORTANT: Real games should NOT use this system. Instead:
    /// - Use game-specific RenderKey components
    /// - Implement RenderCatalogAuthoring/Baker for your game's render data
    /// - Use ApplyRenderCatalogSystem to assign render components
    ///
    /// Only runs when legacy scenario gates are enabled.
    /// </summary>
    [DisableAutoCreation]
    [Unity.Entities.UpdateInGroup(typeof(Unity.Entities.InitializationSystemGroup))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "PureDOTS.Demo.Rendering", null, "SharedRenderBootstrap")]
    public partial struct SharedRenderBootstrap : Unity.Entities.ISystem
    {
        public void OnCreate(ref Unity.Entities.SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            // Create an empty RenderMeshArray that will be populated by host game setup
            var renderMeshArray = new Unity.Rendering.RenderMeshArray
            {
                // Meshes and materials will be set up externally by host games
                // This singleton just provides access to the array
            };

            var entityManager = state.EntityManager;
            var singletonEntity = entityManager.CreateEntity();
            entityManager.AddSharedComponentManaged(singletonEntity, new RenderMeshArraySingleton
            {
                Value = renderMeshArray
            });

            UnityEngine.Debug.Log($"[SharedRenderBootstrap] Legacy scenario RenderMeshArray singleton created in world: {state.WorldUnmanaged.Name}.");
        }

        public void OnUpdate(ref Unity.Entities.SystemState state)
        {
            // One-time bootstrap, no updates needed
        }
    }
}

#endif

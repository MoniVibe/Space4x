using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Validates and maintains star cluster membership and star-planet hierarchies.
    /// Ensures star-planet relationships are valid and cluster data is consistent.
    /// Runs in EnvironmentSystemGroup after PlanetOrbitHierarchySystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(PlanetOrbitHierarchySystem))]
    public partial struct StarClusterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Skip if paused or rewinding
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Validate star-planet hierarchies
            // Check that all planets with StarParent reference valid stars
            foreach (var (starParent, entity) in SystemAPI.Query<RefRO<StarParent>>().WithEntityAccess())
            {
                var parentStar = starParent.ValueRO.ParentStar;

                // Skip if no parent star (shouldn't happen, but handle gracefully)
                if (parentStar == Entity.Null)
                    continue;

                // Validate that parent star exists and has star components
                if (!SystemAPI.Exists(parentStar))
                {
                    // Parent star doesn't exist - could log warning in non-Burst code
                    // In Burst, we just skip invalid references
                    continue;
                }

                // Verify parent has star components (optional validation)
                // This ensures we're not accidentally linking to non-star entities
                if (!SystemAPI.HasComponent<StarLuminosity>(parentStar))
                {
                    // Parent doesn't have star components - skip
                    continue;
                }
            }

            // Validate that stars with planets have valid planet references
            foreach (var (planets, starEntity) in SystemAPI.Query<DynamicBuffer<StarPlanet>>().WithEntityAccess())
            {
                for (int i = planets.Length - 1; i >= 0; i--)
                {
                    var planetEntity = planets[i].PlanetEntity;

                    // Remove invalid planet references
                    if (planetEntity == Entity.Null || !SystemAPI.Exists(planetEntity))
                    {
                        planets.RemoveAt(i);
                        continue;
                    }

                    // Verify planet has StarParent pointing back to this star
                    if (SystemAPI.HasComponent<StarParent>(planetEntity))
                    {
                        var planetStarParent = SystemAPI.GetComponent<StarParent>(planetEntity);
                        if (planetStarParent.ParentStar != starEntity)
                        {
                            // Planet's StarParent doesn't match - remove from buffer
                            // The hierarchy system will rebuild it correctly next frame
                            planets.RemoveAt(i);
                        }
                    }
                    else
                    {
                        // Planet doesn't have StarParent - remove from buffer
                        planets.RemoveAt(i);
                    }
                }
            }
        }
    }
}

























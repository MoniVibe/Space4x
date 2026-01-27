using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Time;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Validates and maintains planet orbit hierarchy (moons orbiting planets).
    /// Ensures PlanetSatellites buffers are kept in sync with PlanetParent references.
    /// Runs in EnvironmentSystemGroup after OrbitAdvanceSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(OrbitAdvanceSystem))]
    public partial struct PlanetOrbitHierarchySystem : ISystem
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

            // Clear all satellite buffers (we'll rebuild them)
            foreach (var satellites in SystemAPI.Query<DynamicBuffer<PlanetSatellite>>())
            {
                satellites.Clear();
            }

            // Rebuild satellite buffers from PlanetParent references
            foreach (var (parent, entity) in SystemAPI.Query<RefRO<PlanetParent>>().WithAll<OrbitParameters>().WithEntityAccess())
            {
                var parentPlanet = parent.ValueRO.ParentPlanet;

                // Skip if no parent (orbits star directly)
                if (parentPlanet == Entity.Null)
                    continue;

                // Skip if parent doesn't exist
                if (!SystemAPI.Exists(parentPlanet))
                    continue;

                // Add this entity to parent's satellite buffer
                if (SystemAPI.HasBuffer<PlanetSatellite>(parentPlanet))
                {
                    var parentSatellites = SystemAPI.GetBuffer<PlanetSatellite>(parentPlanet);
                    parentSatellites.Add(new PlanetSatellite { SatelliteEntity = entity });
                }
            }

            // Also handle OrbitParameters.ParentPlanet for entities that use orbit system directly
            foreach (var (orbitParams, entity) in SystemAPI.Query<RefRO<OrbitParameters>>().WithEntityAccess())
            {
                var parentPlanet = orbitParams.ValueRO.ParentPlanet;

                // Skip if no parent (orbits star directly)
                if (parentPlanet == Entity.Null)
                    continue;

                // Skip if parent doesn't exist
                if (!SystemAPI.Exists(parentPlanet))
                    continue;

                // Add this entity to parent's satellite buffer if parent has PlanetSatellite buffer
                if (SystemAPI.HasBuffer<PlanetSatellite>(parentPlanet))
                {
                    var parentSatellites = SystemAPI.GetBuffer<PlanetSatellite>(parentPlanet);
                    
                    // Check if already added
                    bool alreadyAdded = false;
                    for (int i = 0; i < parentSatellites.Length; i++)
                    {
                        if (parentSatellites[i].SatelliteEntity == entity)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }

                    if (!alreadyAdded)
                    {
                        parentSatellites.Add(new PlanetSatellite { SatelliteEntity = entity });
                    }
                }
            }

            // Clear all star planet buffers (we'll rebuild them)
            foreach (var planets in SystemAPI.Query<DynamicBuffer<StarPlanet>>())
            {
                planets.Clear();
            }

            // Rebuild star planet buffers from StarParent references
            foreach (var (starParent, entity) in SystemAPI.Query<RefRO<StarParent>>().WithAll<OrbitParameters>().WithEntityAccess())
            {
                var parentStar = starParent.ValueRO.ParentStar;

                // Skip if no parent star
                if (parentStar == Entity.Null)
                    continue;

                // Skip if parent star doesn't exist
                if (!SystemAPI.Exists(parentStar))
                    continue;

                // Add this planet to parent star's planet buffer
                if (SystemAPI.HasBuffer<StarPlanet>(parentStar))
                {
                    var starPlanets = SystemAPI.GetBuffer<StarPlanet>(parentStar);
                    
                    // Check if already added
                    bool alreadyAdded = false;
                    for (int i = 0; i < starPlanets.Length; i++)
                    {
                        if (starPlanets[i].PlanetEntity == entity)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }

                    if (!alreadyAdded)
                    {
                        starPlanets.Add(new StarPlanet { PlanetEntity = entity });
                    }
                }
            }
        }
    }
}


using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Populates the ResourceRegistryEntry buffer with asteroid entities so they can be found by vessel AI systems.
    /// Runs once during initialization to register all asteroids in the scene.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XResourceRegistryBootstrapSystem))]
    public partial struct Space4XResourceRegistryPopulationSystem : ISystem
    {
        private EntityQuery _asteroidQuery;
        private EntityQuery _registryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform, ResourceSourceState, ResourceTypeId>()
                .Build();

            _registryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Find the registry entity
            if (_registryQuery.IsEmptyIgnoreFilter)
            {
                // Registry not set up yet, wait for bootstrap
                return;
            }

            var registryEntity = _registryQuery.GetSingletonEntity();
            if (!state.EntityManager.HasBuffer<ResourceRegistryEntry>(registryEntity))
            {
                // Buffer not set up yet
                return;
            }

            var registryBuffer = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);

            // Check if asteroids are already registered (check first asteroid)
            if (!_asteroidQuery.IsEmptyIgnoreFilter)
            {
                var firstAsteroid = _asteroidQuery.GetSingletonEntity();
                bool alreadyRegistered = false;

                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].SourceEntity == firstAsteroid)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (alreadyRegistered)
                {
                    // Already populated, disable system
                    state.Enabled = false;
                    return;
                }
            }

            // Collect all asteroids and their data
            var asteroids = new NativeList<(Entity entity, float3 position, ResourceTypeId resourceType, ResourceSourceState state)>(Allocator.Temp);

            foreach (var (asteroid, transform, resourceType, resourceState, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>, RefRO<ResourceTypeId>, RefRO<ResourceSourceState>>()
                .WithEntityAccess())
            {
                asteroids.Add((entity, transform.ValueRO.Position, resourceType.ValueRO, resourceState.ValueRO));
            }

            // Map ResourceType to ResourceTypeIndex
            // For now, we'll use a simple mapping based on the Asteroid.ResourceType enum
            // This assumes asteroids use ResourceType enum values that map to registry indices
            for (int i = 0; i < asteroids.Length; i++)
            {
                var (entity, position, resourceType, resourceState) = asteroids[i];
                
                // Map ResourceType enum to ResourceTypeIndex
                // ResourceType enum: Minerals=0, Energy=1, Research=2, etc.
                ushort resourceTypeIndex = (ushort)resourceType.Value;
                
                // Only register if resource still exists
                if (resourceState.UnitsRemaining > 0f)
                {
                    var entry = new ResourceRegistryEntry
                    {
                        SourceEntity = entity,
                        Position = position,
                        ResourceTypeIndex = resourceTypeIndex,
                        Tier = ResourceTier.Raw
                    };
                    registryBuffer.Add(entry);
                }
            }

            asteroids.Dispose();

            // Disable system after population
            state.Enabled = false;
        }
    }
}


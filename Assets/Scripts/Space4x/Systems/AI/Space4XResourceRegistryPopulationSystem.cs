using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Space4X.Registry;
using ResourceRegistry = PureDOTS.Runtime.Components.ResourceRegistry;
using ResourceRegistryEntry = PureDOTS.Runtime.Components.ResourceRegistryEntry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Populates the ResourceRegistryEntry buffer with asteroid entities so they can be found by vessel AI systems.
    /// Runs reactively to handle both initial asteroids and dynamically spawned ones (e.g., from Scenario Runner).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XResourceRegistryBootstrapSystem))]
    public partial struct Space4XResourceRegistryPopulationSystem : ISystem
    {
        private EntityQuery _unregisteredAsteroidQuery;
        private EntityQuery _registryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _unregisteredAsteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4X.Registry.Asteroid, LocalTransform, Space4X.Registry.ResourceSourceState, Space4X.Registry.ResourceTypeId>()
                .WithNone<ResourceRegistryRegisteredTag>()
                .Build();

            _registryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Ensure a registry entity exists and is correctly configured
            Entity registryEntity;
            if (_registryQuery.IsEmptyIgnoreFilter)
            {
                registryEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<ResourceRegistry>(registryEntity);
            }
            else
            {
                registryEntity = _registryQuery.GetSingletonEntity();
            }

            if (!state.EntityManager.HasBuffer<ResourceRegistryEntry>(registryEntity))
            {
                state.EntityManager.AddBuffer<ResourceRegistryEntry>(registryEntity);
            }

            var registryBuffer = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);

            // Process unregistered asteroids
            if (_unregisteredAsteroidQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            // Collect unregistered asteroids and their data
            var asteroids = new NativeList<(Entity entity, float3 position, Space4X.Registry.ResourceTypeId resourceType, Space4X.Registry.ResourceSourceState state)>(Allocator.Temp);

            foreach (var (asteroid, transform, resourceType, resourceState, entity) in SystemAPI.Query<RefRO<Space4X.Registry.Asteroid>, RefRO<LocalTransform>, RefRO<Space4X.Registry.ResourceTypeId>, RefRO<Space4X.Registry.ResourceSourceState>>()
                .WithNone<ResourceRegistryRegisteredTag>()
                .WithEntityAccess())
            {
                asteroids.Add((entity, transform.ValueRO.Position, resourceType.ValueRO, resourceState.ValueRO));
            }

            // Register each asteroid
            for (int i = 0; i < asteroids.Length; i++)
            {
                var (entity, position, resourceType, resourceState) = asteroids[i];
                
                // Only register if resource still exists
                if (resourceState.UnitsRemaining <= 0f)
                {
                    continue;
                }

                // Convert ResourceTypeId (FixedString64Bytes) to ResourceType enum using utility
                // Note: Space4XMiningResourceUtility.MapToResourceType is not Burst-compatible
                // because it uses Append() calls. We'll use a Burst-compatible mapping instead.
                Space4X.Registry.ResourceType resourceTypeEnum = MapResourceTypeIdToEnum(in resourceType.Value);
                ushort resourceTypeIndex = (ushort)resourceTypeEnum;
                
                var entry = new ResourceRegistryEntry
                {
                    SourceEntity = entity,
                    Position = position,
                    ResourceTypeIndex = resourceTypeIndex,
                    Tier = ResourceTier.Raw
                };
                registryBuffer.Add(entry);

                // Mark as registered
                state.EntityManager.AddComponent<ResourceRegistryRegisteredTag>(entity);
            }

            asteroids.Dispose();
        }

        [BurstCompile]
        private static Space4X.Registry.ResourceType MapResourceTypeIdToEnum(in FixedString64Bytes resourceId)
        {
            // Map known resource ID patterns to ResourceType enum
            // This matches the IDs created by Space4XMiningResourceUtility
            // Use direct string comparison (FixedString64Bytes supports == operator)
            FixedString64Bytes mineralsId = "space4x.resource.minerals";
            FixedString64Bytes rareMetalsId1 = "space4x.resource.rare_metals";
            FixedString64Bytes rareMetalsId2 = "space4x.resource.rareMetals";
            FixedString64Bytes energyCrystalsId1 = "space4x.resource.energy_crystals";
            FixedString64Bytes energyCrystalsId2 = "space4x.resource.energyCrystals";
            FixedString64Bytes organicMatterId1 = "space4x.resource.organic_matter";
            FixedString64Bytes organicMatterId2 = "space4x.resource.organicMatter";
            
            if (resourceId == mineralsId)
            {
                return Space4X.Registry.ResourceType.Minerals;
            }
            if (resourceId == rareMetalsId1 || resourceId == rareMetalsId2)
            {
                return Space4X.Registry.ResourceType.RareMetals;
            }
            if (resourceId == energyCrystalsId1 || resourceId == energyCrystalsId2)
            {
                return Space4X.Registry.ResourceType.EnergyCrystals;
            }
            if (resourceId == organicMatterId1 || resourceId == organicMatterId2)
            {
                return Space4X.Registry.ResourceType.OrganicMatter;
            }
            
            // Default fallback
            return Space4X.Registry.ResourceType.Minerals;
        }
    }

    /// <summary>
    /// Tag component indicating an asteroid has been registered in the ResourceRegistry.
    /// Used to prevent duplicate registrations and enable reactive registration of dynamically spawned asteroids.
    /// </summary>
    public struct ResourceRegistryRegisteredTag : IComponentData { }
}


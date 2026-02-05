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
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PureDOTS.Systems.ResourceRegistrySystem))]
    public partial struct Space4XResourceRegistryPopulationSystem : ISystem
    {
        private EntityQuery _asteroidQuery;
        private EntityQuery _registryQuery;
        private static readonly FixedString64Bytes ResourceIdMinerals = "space4x.resource.minerals";
        private static readonly FixedString64Bytes ResourceIdRareMetalsA = "space4x.resource.rare_metals";
        private static readonly FixedString64Bytes ResourceIdRareMetalsB = "space4x.resource.rareMetals";
        private static readonly FixedString64Bytes ResourceIdEnergyCrystalsA = "space4x.resource.energy_crystals";
        private static readonly FixedString64Bytes ResourceIdEnergyCrystalsB = "space4x.resource.energyCrystals";
        private static readonly FixedString64Bytes ResourceIdOrganicMatterA = "space4x.resource.organic_matter";
        private static readonly FixedString64Bytes ResourceIdOrganicMatterB = "space4x.resource.organicMatter";

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4X.Registry.Asteroid, LocalTransform, Space4X.Registry.ResourceSourceState, Space4X.Registry.ResourceTypeId>()
                .Build();

            _registryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_registryQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var registryEntity = _registryQuery.GetSingletonEntity();
            var registryBuffer = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
            registryBuffer.Clear();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (_asteroidQuery.IsEmptyIgnoreFilter)
            {
                ecb.Dispose();
                return;
            }

            foreach (var (transform, resourceType, resourceState, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<Space4X.Registry.ResourceTypeId>, RefRO<Space4X.Registry.ResourceSourceState>>()
                         .WithAll<Space4X.Registry.Asteroid>()
                         .WithEntityAccess())
            {
                
                // Only register if resource still exists
                if (resourceState.ValueRO.UnitsRemaining <= 0f)
                {
                    continue;
                }

                // Convert ResourceTypeId (FixedString64Bytes) to ResourceType enum using utility
                // Note: Space4XMiningResourceUtility.MapToResourceType is not Burst-compatible
                // because it uses Append() calls. We'll use a Burst-compatible mapping instead.
                Space4X.Registry.ResourceType resourceTypeEnum = MapResourceTypeIdToEnum(in resourceType.ValueRO.Value);
                ushort resourceTypeIndex = (ushort)resourceTypeEnum;
                
                var entry = new ResourceRegistryEntry
                {
                    SourceEntity = entity,
                    Position = transform.ValueRO.Position,
                    ResourceTypeIndex = resourceTypeIndex,
                    Tier = ResourceTier.Raw
                };
                registryBuffer.Add(entry);

                if (!state.EntityManager.HasComponent<ResourceRegistryRegisteredTag>(entity))
                {
                    ecb.AddComponent<ResourceRegistryRegisteredTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static Space4X.Registry.ResourceType MapResourceTypeIdToEnum(in FixedString64Bytes resourceId)
        {
            // Map known resource ID patterns to ResourceType enum
            // This matches the IDs created by Space4XMiningResourceUtility
            // Use direct string comparison (FixedString64Bytes supports == operator)
            if (resourceId == ResourceIdMinerals)
            {
                return Space4X.Registry.ResourceType.Minerals;
            }
            if (resourceId == ResourceIdRareMetalsA || resourceId == ResourceIdRareMetalsB)
            {
                return Space4X.Registry.ResourceType.RareMetals;
            }
            if (resourceId == ResourceIdEnergyCrystalsA || resourceId == ResourceIdEnergyCrystalsB)
            {
                return Space4X.Registry.ResourceType.EnergyCrystals;
            }
            if (resourceId == ResourceIdOrganicMatterA || resourceId == ResourceIdOrganicMatterB)
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

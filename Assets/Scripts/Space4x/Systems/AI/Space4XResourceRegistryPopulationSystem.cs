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
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PureDOTS.Systems.ResourceRegistrySystem))]
    public partial struct Space4XResourceRegistryPopulationSystem : ISystem
    {
        private const string RegistryPopulationEnv = "SPACE4X_RESOURCE_REGISTRY_POPULATION";
        private EntityQuery _asteroidQuery;
        private EntityQuery _registryQuery;
        private static readonly FixedString64Bytes ResourceIdMinerals = "space4x.resource.minerals";
        private static readonly FixedString64Bytes ResourceIdRareMetalsA = "space4x.resource.rare_metals";
        private static readonly FixedString64Bytes ResourceIdRareMetalsB = "space4x.resource.rareMetals";
        private static readonly FixedString64Bytes ResourceIdEnergyCrystalsA = "space4x.resource.energy_crystals";
        private static readonly FixedString64Bytes ResourceIdEnergyCrystalsB = "space4x.resource.energyCrystals";
        private static readonly FixedString64Bytes ResourceIdOrganicMatterA = "space4x.resource.organic_matter";
        private static readonly FixedString64Bytes ResourceIdOrganicMatterB = "space4x.resource.organicMatter";

        public void OnCreate(ref SystemState state)
        {
            var enableValue = System.Environment.GetEnvironmentVariable(RegistryPopulationEnv);
            if (string.Equals(enableValue, "0", System.StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4X.Registry.Asteroid, LocalTransform, Space4X.Registry.ResourceSourceState, Space4X.Registry.ResourceTypeId>()
                .Build();

            _registryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_registryQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var registryEntity = _registryQuery.GetSingletonEntity();
            var registryBuffer = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
            registryBuffer.Clear();

            if (_asteroidQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var asteroidCount = _asteroidQuery.CalculateEntityCount();
            var entries = new NativeList<ResourceRegistryEntry>(math.max(1, asteroidCount), Allocator.TempJob);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var job = new PopulateRegistryJob
            {
                Entries = entries.AsParallelWriter(),
                RegisteredLookup = state.GetComponentLookup<ResourceRegistryRegisteredTag>(true),
                Ecb = ecb.AsParallelWriter()
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            if (entries.Length > 0)
            {
                registryBuffer.AddRange(entries.AsArray());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            entries.Dispose();
        }

        [BurstCompile]
        private partial struct PopulateRegistryJob : IJobEntity
        {
            public NativeList<ResourceRegistryEntry>.ParallelWriter Entries;
            [ReadOnly] public ComponentLookup<ResourceRegistryRegisteredTag> RegisteredLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([EntityIndexInQuery] int sortKey,
                in LocalTransform transform,
                in Space4X.Registry.ResourceTypeId resourceType,
                in Space4X.Registry.ResourceSourceState resourceState,
                Entity entity)
            {
                if (resourceState.UnitsRemaining <= 0f)
                {
                    return;
                }

                var resourceTypeEnum = MapResourceTypeIdToEnum(in resourceType.Value);
                ushort resourceTypeIndex = (ushort)resourceTypeEnum;

                Entries.AddNoResize(new ResourceRegistryEntry
                {
                    SourceEntity = entity,
                    Position = transform.Position,
                    ResourceTypeIndex = resourceTypeIndex,
                    Tier = ResourceTier.Raw
                });

                if (!RegisteredLookup.HasComponent(entity))
                {
                    Ecb.AddComponent<ResourceRegistryRegisteredTag>(sortKey, entity);
                }
            }
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

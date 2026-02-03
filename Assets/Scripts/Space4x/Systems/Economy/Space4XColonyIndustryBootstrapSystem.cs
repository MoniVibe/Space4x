using PureDOTS.Runtime;
using PureDOTS.Runtime.Economy.Resources;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Spawns minimal colony industry facilities (refinery, fabricator, shipyard, research)
    /// when economy is enabled so colonies can bootstrap production.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XColonyIndustryBootstrapSystem : ISystem
    {
        private EntityQuery _colonyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<Space4XColony>();
            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony>()
                .WithNone<ColonyIndustryBootstrapTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_colonyQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>()
                         .WithNone<ColonyIndustryBootstrapTag>()
                         .WithEntityAccess())
            {
                var position = float3.zero;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    position = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                }

                var tier = ResolveTier(colony.ValueRO.Population);

                if (!SystemAPI.HasComponent<ColonyIndustryInventory>(entity))
                {
                    var inventoryEntity = ecb.CreateEntity();
                    ecb.AddComponent(inventoryEntity, new Inventory
                    {
                        MaxMass = math.max(500f, colony.ValueRO.Population * 0.002f),
                        MaxVolume = 0f,
                        CurrentMass = 0f,
                        CurrentVolume = 0f,
                        LastUpdateTick = 0u
                    });
                    ecb.AddBuffer<InventoryItem>(inventoryEntity);
                    ecb.AddComponent(entity, new ColonyIndustryInventory
                    {
                        InventoryEntity = inventoryEntity
                    });
                }

                CreateFacility(ref ecb, entity, position, new float3(18f, 0f, 0f), FacilityArchetype.Refinery, FacilityBusinessClass.Refinery, tier);
                CreateFacility(ref ecb, entity, position, new float3(-18f, 0f, 0f), FacilityArchetype.Fabricator, FacilityBusinessClass.Production, tier);
                CreateFacility(ref ecb, entity, position, new float3(18f, 0f, 18f), FacilityArchetype.MobileFabricationBay, FacilityBusinessClass.ModuleFacility, tier);
                CreateFacility(ref ecb, entity, position, new float3(0f, 0f, 18f), FacilityArchetype.OrbitalDrydock, FacilityBusinessClass.Shipyard, tier);
                CreateFacility(ref ecb, entity, position, new float3(0f, 0f, -18f), FacilityArchetype.ResearchLab, FacilityBusinessClass.Research, tier);

                if (!SystemAPI.HasComponent<ColonyIndustryStock>(entity))
                {
                    var seed = math.max(0f, colony.ValueRO.StoredResources);
                    ecb.AddComponent(entity, new ColonyIndustryStock
                    {
                        OreReserve = seed * 0.25f,
                        SuppliesReserve = seed * 0.15f,
                        ResearchReserve = seed * 0.05f,
                        LastUpdateTick = 0u
                    });
                }

                if (!SystemAPI.HasComponent<TechResearchPool>(entity))
                {
                    ecb.AddComponent(entity, new TechResearchPool
                    {
                        Stored = 0f,
                        Threshold = 100f,
                        MaxTier = 4,
                        LastUpdateTick = 0u
                    });
                }

                if (!SystemAPI.HasComponent<TechLevel>(entity))
                {
                    ecb.AddComponent(entity, new TechLevel
                    {
                        MiningTech = 0,
                        CombatTech = 0,
                        HaulingTech = 0,
                        ProcessingTech = 0,
                        LastUpgradeTick = 0u
                    });
                }

                if (!SystemAPI.HasComponent<TechDiffusionState>(entity))
                {
                    ecb.AddComponent(entity, new TechDiffusionState
                    {
                        SourceEntity = Entity.Null,
                        DiffusionProgressSeconds = 0f,
                        DiffusionDurationSeconds = 0f,
                        TargetMiningTech = 0,
                        TargetCombatTech = 0,
                        TargetHaulingTech = 0,
                        TargetProcessingTech = 0,
                        Active = 0,
                        DiffusionStartTick = 0u
                    });
                }

                ecb.AddComponent<ColonyIndustryBootstrapTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void CreateFacility(
            ref EntityCommandBuffer ecb,
            Entity colony,
            float3 colonyPosition,
            float3 offset,
            FacilityArchetype archetype,
            FacilityBusinessClass role,
            FacilityTier tier)
        {
            var facility = ecb.CreateEntity();
            ecb.AddComponent(facility, LocalTransform.FromPositionRotationScale(colonyPosition + offset, quaternion.identity, 1f));
            ecb.AddComponent<SpatialIndexedTag>(facility);
            ecb.AddComponent(facility, new FacilityArchetypeComponent { Value = archetype });
            ecb.AddComponent(facility, new FacilityTierComponent { Value = tier });
            ecb.AddComponent(facility, new ColonyFacilityLink
            {
                Colony = colony,
                FacilityClass = role
            });
        }

        private static FacilityTier ResolveTier(float population)
        {
            if (population >= 5_000_000f)
            {
                return FacilityTier.Large;
            }

            if (population >= 1_000_000f)
            {
                return FacilityTier.Medium;
            }

            return FacilityTier.Small;
        }
    }
}

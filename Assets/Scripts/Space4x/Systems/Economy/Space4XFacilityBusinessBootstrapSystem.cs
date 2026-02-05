using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XFacilityBusinessBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FacilityArchetypeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (archetype, entity) in SystemAPI.Query<RefRO<FacilityArchetypeComponent>>().WithEntityAccess())
            {
                var role = ResolveBusinessClass(archetype.ValueRO.Value);
                if (role == FacilityBusinessClass.None)
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<FacilityBusinessClassComponent>(entity))
                {
                    ecb.AddComponent(entity, new FacilityBusinessClassComponent { Value = role });
                }

                EnsureBusiness(ref state, ref ecb, entity, role);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void EnsureBusiness(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, FacilityBusinessClass role)
        {
            var desiredType = ResolveBusinessType(role);
            var capacity = ResolveCapacity(state.EntityManager, entity);

            if (!state.EntityManager.HasComponent<BusinessProduction>(entity))
            {
                ecb.AddComponent(entity, new BusinessProduction
                {
                    Type = desiredType,
                    Capacity = capacity,
                    Throughput = 0f,
                    LastUpdateTick = 0
                });
            }
            else
            {
                var production = state.EntityManager.GetComponentData<BusinessProduction>(entity);
                production.Type = desiredType;
                production.Capacity = capacity;
                ecb.SetComponent(entity, production);
            }

            if (!state.EntityManager.HasComponent<BusinessInventory>(entity))
            {
                var inventoryEntity = ecb.CreateEntity();
                ecb.AddComponent(inventoryEntity, new Inventory
                {
                    MaxMass = capacity,
                    MaxVolume = 0f,
                    CurrentMass = 0f,
                    CurrentVolume = 0f,
                    LastUpdateTick = 0
                });
                ecb.AddBuffer<InventoryItem>(inventoryEntity);
                ecb.AddComponent(entity, new BusinessInventory { InventoryEntity = inventoryEntity });
            }
        }

        private static FacilityBusinessClass ResolveBusinessClass(FacilityArchetype archetype)
        {
            return archetype switch
            {
                FacilityArchetype.Refinery => FacilityBusinessClass.Refinery,
                FacilityArchetype.Foundry => FacilityBusinessClass.Refinery,
                FacilityArchetype.Bioprocessor => FacilityBusinessClass.Refinery,
                FacilityArchetype.ResearchLab => FacilityBusinessClass.Research,
                FacilityArchetype.ExpeditionLab => FacilityBusinessClass.Research,
                FacilityArchetype.Fabricator => FacilityBusinessClass.Production,
                FacilityArchetype.MobileFabricationBay => FacilityBusinessClass.ModuleFacility,
                FacilityArchetype.TitanForge => FacilityBusinessClass.ShipFabrication,
                FacilityArchetype.OrbitalDrydock => FacilityBusinessClass.Shipyard,
                FacilityArchetype.CivicWorks => FacilityBusinessClass.Construction,
                FacilityArchetype.TerraformingPlant => FacilityBusinessClass.Construction,
                _ => FacilityBusinessClass.None
            };
        }

        private static BusinessType ResolveBusinessType(FacilityBusinessClass role)
        {
            return role switch
            {
                FacilityBusinessClass.Refinery => BusinessType.Alchemist,
                FacilityBusinessClass.Research => BusinessType.Alchemist,
                FacilityBusinessClass.ModuleFacility => BusinessType.Blacksmith,
                FacilityBusinessClass.Production => BusinessType.Builder,
                FacilityBusinessClass.ShipFabrication => BusinessType.Builder,
                FacilityBusinessClass.Shipyard => BusinessType.Builder,
                FacilityBusinessClass.Construction => BusinessType.Builder,
                _ => BusinessType.Blacksmith
            };
        }

        private static float ResolveCapacity(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<FacilityTierComponent>(entity))
            {
                var tier = (int)entityManager.GetComponentData<FacilityTierComponent>(entity).Value;
                return 250f * (1 + math.max(0, tier));
            }

            return 250f;
        }
    }
}

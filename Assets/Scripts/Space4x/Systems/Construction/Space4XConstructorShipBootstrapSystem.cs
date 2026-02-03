using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems.Construction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XConstructorShipBootstrapSystem : ISystem
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
                if (!IsConstructorArchetype(archetype.ValueRO.Value))
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<Carrier>(entity))
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<ConstructorShipTag>(entity))
                {
                    ecb.AddComponent<ConstructorShipTag>(entity);
                }

                if (!state.EntityManager.HasComponent<ConstructionRig>(entity))
                {
                    ecb.AddComponent(entity, ConstructionRig.Default);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool IsConstructorArchetype(FacilityArchetype archetype)
        {
            return archetype == FacilityArchetype.MobileFabricationBay
                   || archetype == FacilityArchetype.CivicWorks
                   || archetype == FacilityArchetype.TerraformingPlant;
        }
    }
}

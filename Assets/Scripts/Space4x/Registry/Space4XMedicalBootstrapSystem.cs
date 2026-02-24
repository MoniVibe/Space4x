using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures medical facility state exists on entities that carry modules (ships, stations, colonies).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XMedicalBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PureDOTS.Runtime.Ships.CarrierModuleSlot>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<PureDOTS.Runtime.Ships.CarrierModuleSlot>>()
                         .WithNone<MedicalFacilityAggregate>()
                         .WithEntityAccess())
            {
                ecb.AddComponent<MedicalFacilityAggregate>(entity);

                if (!em.HasComponent<MedicalShiftPolicy>(entity))
                {
                    ecb.AddComponent(entity, MedicalShiftPolicy.Default);
                }

                if (!em.HasComponent<MedicalStaffingPolicy>(entity))
                {
                    ecb.AddComponent(entity, MedicalStaffingPolicy.Default);
                }

                if (!em.HasComponent<MedicalCarePolicy>(entity))
                {
                    ecb.AddComponent(entity, MedicalCarePolicy.Default);
                }

                if (!em.HasComponent<MedicalResearchState>(entity))
                {
                    ecb.AddComponent(entity, default(MedicalResearchState));
                }

                if (!em.HasBuffer<MedicalResearchUnlock>(entity))
                {
                    ecb.AddBuffer<MedicalResearchUnlock>(entity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Updates InRefitFacilityTag on carriers based on proximity to stations with RefitFacilityTag.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    // Removed invalid UpdateAfter: GameplayFixedStepSyncSystem runs in TimeSystemGroup.
    public partial struct FacilityProximitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<GameplayFixedStep>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (carrierTransform, carrierEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Carrier>()
                         .WithEntityAccess())
            {
                var carrierPos = carrierTransform.ValueRO.Position;
                var inFacility = false;

                foreach (var (facilityTransform, facilityZone) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<FacilityZone>>()
                             .WithAll<RefitFacilityTag>())
                {
                    var facilityPos = facilityTransform.ValueRO.Position;
                    var distanceSq = math.distancesq(carrierPos, facilityPos);
                    var radiusSq = facilityZone.ValueRO.RadiusMeters * facilityZone.ValueRO.RadiusMeters;

                    if (distanceSq <= radiusSq)
                    {
                        inFacility = true;
                        break;
                    }
                }

                var hasTag = state.EntityManager.HasComponent<InRefitFacilityTag>(carrierEntity);
                if (inFacility && !hasTag)
                {
                    ecb.AddComponent<InRefitFacilityTag>(carrierEntity);
                }
                else if (!inFacility && hasTag)
                {
                    ecb.RemoveComponent<InRefitFacilityTag>(carrierEntity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

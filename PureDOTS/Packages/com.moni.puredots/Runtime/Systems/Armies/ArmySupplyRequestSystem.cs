using PureDOTS.Runtime.Armies;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ArmySupplySystem))]
    public partial struct ArmySupplyRequestSystem : ISystem
    {
        private EntityQuery _depotQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _depotQuery = state.GetEntityQuery(ComponentType.ReadOnly<ArmySupplyDepot>());
            state.RequireForUpdate<ArmyStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var depots = _depotQuery.ToComponentDataArray<ArmySupplyDepot>(state.WorldUpdateAllocator);
            var depotEntities = _depotQuery.ToEntityArray(state.WorldUpdateAllocator);
            if (depots.Length == 0)
            {
                return;
            }

            foreach (var (stats, transform, entity) in SystemAPI
                         .Query<RefRO<ArmyStats>, RefRO<Unity.Transforms.LocalTransform>>()
                         .WithEntityAccess())
            {
                if (stats.ValueRO.SupplyLevel > 0.5f)
                {
                    continue;
                }

                var requestTarget = depots[0];
                var requestEntity = depotEntities[0];

                var requests = state.EntityManager.GetBuffer<ArmySupplyRequest>(requestEntity);
                requests.Add(new ArmySupplyRequest
                {
                    Army = entity,
                    SupplyNeeded = 1f - stats.ValueRO.SupplyLevel,
                    Destination = transform.ValueRO.Position,
                    Priority = 1
                });
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

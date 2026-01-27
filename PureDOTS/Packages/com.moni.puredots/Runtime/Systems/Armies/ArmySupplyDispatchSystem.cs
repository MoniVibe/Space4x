using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    // System struct cannot be Burst-compiled when OnCreate uses GetEntityQuery with ComponentType[] (creates managed arrays)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ArmySupplyRequestSystem))]
    public partial struct ArmySupplyDispatchSystem : ISystem
    {
        private EntityQuery _depotQuery;

        // Use QueryBuilder to avoid managed array allocations (Burst-safe).
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _depotQuery = SystemAPI.QueryBuilder()
                .WithAll<ArmySupplyDepot, LocalTransform>()
                .WithAllRW<ArmySupplyRequest>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            foreach (var (depot, transform, requests, entity) in SystemAPI.Query<RefRO<ArmySupplyDepot>, RefRO<LocalTransform>, DynamicBuffer<ArmySupplyRequest>>().WithEntityAccess())
            {
                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];
                    if (!state.EntityManager.Exists(request.Army))
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    if (math.lengthsq(request.Destination - transform.ValueRO.Position) < 1f)
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    var stats = state.EntityManager.GetComponentData<ArmyStats>(request.Army);
                    stats.SupplyLevel = math.saturate(stats.SupplyLevel + request.SupplyNeeded);
                    state.EntityManager.SetComponentData(request.Army, stats);
                    requests.RemoveAt(i);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

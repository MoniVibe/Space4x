using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Determines each villager's primary aggregate belonging based on membership loyalty.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerNeedsSystem))]
    public partial struct VillagerAggregateBelongingSystem : ISystem
    {
        private ComponentLookup<AggregateEntity> _aggregateLookup;
        private EntityQuery _villageQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _aggregateLookup = state.GetComponentLookup<AggregateEntity>(true);
            _villageQuery = state.GetEntityQuery(ComponentType.ReadOnly<VillageId>());
            state.RequireForUpdate<VillagerAggregateMembership>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _aggregateLookup.Update(ref state);

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var allocator = state.WorldUpdateAllocator;
            var villageLookup = BuildVillageLookup(ref state, allocator);

            foreach (var (villagerId, memberships, entity) in SystemAPI
                         .Query<RefRO<VillagerId>, DynamicBuffer<VillagerAggregateMembership>>()
                         .WithEntityAccess())
            {
                if (memberships.Length == 0)
                {
                    AssignVillageFallback(ref state, entity, villagerId.ValueRO, ref villageLookup, ecb);
                    continue;
                }

                var bestIndex = -1;
                var bestScore = -1f;
                for (int i = 0; i < memberships.Length; i++)
                {
                    var entry = memberships[i];
                    var score = entry.Loyalty * (0.5f + 0.5f * entry.Sympathy);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    AssignVillageFallback(ref state, entity, villagerId.ValueRO, ref villageLookup, ecb);
                    continue;
                }

                var best = memberships[bestIndex];
                if (!_aggregateLookup.HasComponent(best.Aggregate))
                {
                    AssignVillageFallback(ref state, entity, villagerId.ValueRO, ref villageLookup, ecb);
                    continue;
                }

                var belonging = new VillagerAggregateBelonging
                {
                    PrimaryAggregate = best.Aggregate,
                    Category = best.Category,
                    Loyalty = best.Loyalty,
                    Sympathy = best.Sympathy
                };

                if (SystemAPI.HasComponent<VillagerAggregateBelonging>(entity))
                {
                    SystemAPI.SetComponent(entity, belonging);
                }
                else
                {
                    ecb.AddComponent(entity, belonging);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private NativeParallelHashMap<int, Entity> BuildVillageLookup(ref SystemState state, Allocator allocator)
        {
            var villages = _villageQuery.ToEntityArray(allocator);
            var ids = _villageQuery.ToComponentDataArray<VillageId>(allocator);
            var map = new NativeParallelHashMap<int, Entity>(villages.Length, allocator);
            for (int i = 0; i < villages.Length; i++)
            {
                map[ids[i].Value] = villages[i];
            }
            return map;
        }

        private void AssignVillageFallback(ref SystemState state, Entity villager, VillagerId villagerId, ref NativeParallelHashMap<int, Entity> villageLookup, EntityCommandBuffer ecb)
        {
            if (!villageLookup.TryGetValue(villagerId.FactionId, out var villageEntity))
            {
                if (SystemAPI.HasComponent<VillagerAggregateBelonging>(villager))
                {
                    ecb.RemoveComponent<VillagerAggregateBelonging>(villager);
                }
                return;
            }

            var belonging = new VillagerAggregateBelonging
            {
                PrimaryAggregate = villageEntity,
                Category = AggregateCategory.Village,
                Loyalty = 0.5f,
                Sympathy = 0f
            };

            if (SystemAPI.HasComponent<VillagerAggregateBelonging>(villager))
            {
                SystemAPI.SetComponent(villager, belonging);
            }
            else
            {
                ecb.AddComponent(villager, belonging);
            }
        }
    }
}

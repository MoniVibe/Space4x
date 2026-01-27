using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Village;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ArmyIntentSystem))]
    public partial struct ArmyOrderAssignmentSystem : ISystem
    {
        private EntityQuery _villageQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villageQuery = SystemAPI.QueryBuilder()
                .WithAll<PureDOTS.Runtime.Village.VillageId>()
                .WithAll<LocalTransform>()
                .WithAll<VillageWorkforcePolicy>()
                .Build();
            state.RequireForUpdate<ArmyIntent>();
            state.RequireForUpdate(_villageQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var allocator = state.WorldUpdateAllocator;
            var villageEntities = _villageQuery.ToEntityArray(allocator);
            var villageIds = _villageQuery.ToComponentDataArray<PureDOTS.Runtime.Village.VillageId>(allocator);
            var villageTransforms = _villageQuery.ToComponentDataArray<LocalTransform>(allocator);
            var villagePolicies = _villageQuery.ToComponentDataArray<VillageWorkforcePolicy>(allocator);

            var villageLookup = new NativeParallelHashMap<int, VillageSnapshot>(villageEntities.Length, allocator);
            for (int i = 0; i < villageEntities.Length; i++)
            {
                villageLookup[villageIds[i].Value] = new VillageSnapshot
                {
                    Position = villageTransforms[i].Position,
                    Policy = villagePolicies[i],
                    Target = villageTransforms[i].Position
                };
            }

            foreach (var (intent, order, armyId, entity) in SystemAPI
                         .Query<RefRO<ArmyIntent>, RefRW<ArmyOrder>, RefRO<ArmyId>>()
                         .WithEntityAccess())
            {
                if (!villageLookup.TryGetValue(armyId.ValueRO.FactionId, out var village))
                {
                    order.ValueRW = new ArmyOrder
                    {
                        Type = ArmyOrder.OrderType.Idle,
                        TargetPosition = order.ValueRO.TargetPosition,
                        TargetEntity = Entity.Null,
                        IssuedTick = SystemAPI.GetSingleton<TimeState>().Tick
                    };
                    continue;
                }

                var desiredOrder = ArmyOrder.OrderType.Idle;
                float3 target = village.Position;

                if (village.Policy.DefenseUrgency > 0.25f)
                {
                    desiredOrder = ArmyOrder.OrderType.Defend;
                    target = village.Position;
                }
                else if (intent.ValueRO.DesiredOrder == ArmyOrder.OrderType.Raid)
                {
                    desiredOrder = ArmyOrder.OrderType.Raid;
                    target = GetRaidTarget(village.Position, intent.ValueRO.IntentWeight);
                }
                else if (intent.ValueRO.DesiredOrder == ArmyOrder.OrderType.Patrol)
                {
                    desiredOrder = ArmyOrder.OrderType.Patrol;
                    target = GetPatrolWaypoint(village.Position, intent.ValueRO.IntentWeight);
                }

                order.ValueRW = new ArmyOrder
                {
                    Type = desiredOrder,
                    TargetPosition = target,
                    TargetEntity = Entity.Null,
                    IssuedTick = SystemAPI.GetSingleton<TimeState>().Tick
                };
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private struct VillageSnapshot
        {
            public float3 Position;
            public VillageWorkforcePolicy Policy;
            public float3 Target;
        }

        private static float3 GetPatrolWaypoint(float3 center, float weight)
        {
            var radius = 25f + weight * 25f;
            var angle = math.radians(weight * 360f);
            return center + new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
        }

        private static float3 GetRaidTarget(float3 center, float weight)
        {
            var distance = 60f + weight * 60f;
            var angle = math.radians(90f + weight * 180f);
            return center + new float3(math.cos(angle) * distance, 0f, math.sin(angle) * distance);
        }
    }
}

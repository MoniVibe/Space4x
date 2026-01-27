using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ArmyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArmyOrder>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = time.FixedDeltaTime;

            foreach (var (order, transform, stats, entity) in SystemAPI
                         .Query<RefRO<ArmyOrder>, RefRW<LocalTransform>, RefRW<ArmyStats>>()
                         .WithEntityAccess())
            {
                if (order.ValueRO.Type == ArmyOrder.OrderType.Idle)
                {
                    stats.ValueRW.Fatigue = math.max(0f, stats.ValueRO.Fatigue - deltaTime * 0.1f);
                    continue;
                }

                var targetPos = order.ValueRO.TargetPosition;
                var direction = targetPos - transform.ValueRO.Position;
                var distance = math.length(direction);
                if (distance < 0.1f)
                {
                    stats.ValueRW.Fatigue = math.max(0f, stats.ValueRO.Fatigue - deltaTime * 0.2f);
                    continue;
                }

                var speed = math.max(0.5f, 5f - stats.ValueRO.Fatigue * 0.05f);
                var move = math.normalize(direction) * speed * deltaTime;
                var newPos = transform.ValueRO.Position + move;
                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPos, transform.ValueRO.Rotation, transform.ValueRO.Scale);

                stats.ValueRW.Fatigue = math.saturate(stats.ValueRO.Fatigue + deltaTime * 0.5f);
                stats.ValueRW.SupplyLevel = math.max(0f, stats.ValueRO.SupplyLevel - deltaTime * 0.1f);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

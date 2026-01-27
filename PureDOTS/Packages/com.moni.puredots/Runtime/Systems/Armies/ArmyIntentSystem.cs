using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ArmyIntentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (stats, intent, entity) in SystemAPI
                         .Query<RefRO<ArmyStats>, RefRW<ArmyIntent>>()
                         .WithEntityAccess())
            {
                if (stats.ValueRO.MemberCount <= 0)
                {
                    intent.ValueRW = new ArmyIntent { DesiredOrder = ArmyOrder.OrderType.Idle, IntentWeight = 0f };
                    continue;
                }

                var moraleFactor = math.saturate(stats.ValueRO.Morale / 100f);
                var fatiguePenalty = math.saturate(stats.ValueRO.Fatigue / 100f);
                var intentWeight = moraleFactor * (1f - fatiguePenalty);

                var desiredOrder = ArmyOrder.OrderType.Defend;
                if (intentWeight > 0.7f)
                {
                    desiredOrder = ArmyOrder.OrderType.Raid;
                }

                intent.ValueRW = new ArmyIntent
                {
                    DesiredOrder = desiredOrder,
                    IntentWeight = intentWeight
                };
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

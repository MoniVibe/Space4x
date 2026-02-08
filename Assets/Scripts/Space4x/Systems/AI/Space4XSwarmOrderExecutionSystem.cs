using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Swarms;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSwarmDemoSystem))]
    [BurstCompile]
    public partial struct Space4XSwarmOrderExecutionSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ControlOrderState>();
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _entityInfoLookup.Update(ref state);

            foreach (var (order, behavior) in SystemAPI.Query<RefRO<ControlOrderState>, RefRW<SwarmBehavior>>()
                         .WithAll<DroneTag>())
            {
                var updated = behavior.ValueRO;
                switch (order.ValueRO.Kind)
                {
                    case ControlOrderKind.Tow:
                        updated.Mode = SwarmMode.Tug;
                        updated.Target = Entity.Null;
                        break;
                    case ControlOrderKind.Attack:
                        updated.Mode = SwarmMode.Attack;
                        updated.Target = _entityInfoLookup.Exists(order.ValueRO.TargetEntity)
                            ? order.ValueRO.TargetEntity
                            : Entity.Null;
                        break;
                    case ControlOrderKind.Return:
                        updated.Mode = SwarmMode.Return;
                        updated.Target = Entity.Null;
                        break;
                    case ControlOrderKind.Screen:
                    case ControlOrderKind.Hold:
                    case ControlOrderKind.Idle:
                    default:
                        updated.Mode = SwarmMode.Screen;
                        updated.Target = Entity.Null;
                        break;
                }

                behavior.ValueRW = updated;
            }
        }
    }
}

using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TargetingPredictionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetingComputer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (computer, gunnery, projectile, loopState, transform) in SystemAPI
                         .Query<RefRO<TargetingComputer>, RefRO<GunnerySkill>, RefRO<ProjectileFlightSpec>, RefRO<CombatLoopState>, RefRO<LocalTransform>>())
            {
                if (loopState.ValueRO.Target == Entity.Null)
                {
                    continue;
                }

                var aim = new TargetingSolution
                {
                    Target = loopState.ValueRO.Target,
                    AimPosition = transform.ValueRO.Position,
                    TimeToImpact = 0f
                };

                if (SystemAPI.HasComponent<LocalTransform>(loopState.ValueRO.Target))
                {
                    var targetTransform = SystemAPI.GetComponent<LocalTransform>(loopState.ValueRO.Target);
                    var targetPosition = targetTransform.Position;
                    var targetVelocity = float3.zero;
                    if (SystemAPI.HasComponent<VelocitySample>(loopState.ValueRO.Target))
                    {
                        targetVelocity = SystemAPI.GetComponent<VelocitySample>(loopState.ValueRO.Target).Velocity;
                    }

                    var timeToImpact = NavHelpers.ComputeTravelTime(transform.ValueRO.Position, targetPosition, projectile.ValueRO.Speed);
                    var predictedPosition = targetPosition + targetVelocity * timeToImpact;
                    aim.AimPosition = predictedPosition;
                    aim.TimeToImpact = timeToImpact;
                }

                SystemAPI.SetComponent(loopState.ValueRO.Target, aim);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

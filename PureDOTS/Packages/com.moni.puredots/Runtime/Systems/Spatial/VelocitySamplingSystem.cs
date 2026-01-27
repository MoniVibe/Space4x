using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spatial
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct VelocitySamplingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            foreach (var (transform, velocity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<VelocitySample>>())
            {
                if (velocity.ValueRW.LastPosition.Equals(float3.zero) && velocity.ValueRW.Velocity.Equals(float3.zero))
                {
                    velocity.ValueRW.LastPosition = transform.ValueRO.Position;
                }

                var newVelocity = (transform.ValueRO.Position - velocity.ValueRW.LastPosition) / deltaTime;
                velocity.ValueRW.Velocity = newVelocity;
                velocity.ValueRW.LastPosition = transform.ValueRO.Position;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

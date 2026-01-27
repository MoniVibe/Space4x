using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResourcePileMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePileVelocity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (pile, velocity) in SystemAPI.Query<RefRW<ResourcePile>, RefRW<ResourcePileVelocity>>())
            {
                pile.ValueRW.Position += velocity.ValueRO.Velocity * deltaTime;
                var vel = velocity.ValueRW;
                vel.Velocity *= 0.99f; // slight damping
                velocity.ValueRW = vel;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

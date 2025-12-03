using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple debug movement system to test if rendering pipeline works.
    /// Moves any entity with a MiningOrder component in +X direction.
    /// This is temporary - remove after confirming rendering works.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DebugSimpleMinerMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Disable by default; turn on only when needed
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Move any entity that has a MiningOrder a little bit in +X
            foreach (var xf in SystemAPI.Query<RefRW<LocalTransform>>()
                                     .WithAll<MiningOrder>())
            {
                var t = xf.ValueRW;
                t.Position += new float3(1f, 0f, 0f) * dt;
                xf.ValueRW = t;
            }
        }
    }
}


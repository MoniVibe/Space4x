using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;

// Dev-only debug mover; editor only.
#if UNITY_EDITOR
namespace Space4X.Debug
{
    /// <summary>
    /// Simple debug movement system to test rendering. Editor-only.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DebugSimpleMinerMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Disabled unless explicitly enabled for debugging.
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

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
#endif


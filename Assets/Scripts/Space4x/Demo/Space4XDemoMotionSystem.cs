using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple deterministic motion for demo entities so smoke scenes show distinct movement.
    /// </summary>
    public struct Space4XDemoMotion : IComponentData
    {
        public float3 BasePosition;
        public float Amplitude;
        public float Speed;
        public float PhaseOffset;
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct Space4XDemoMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XDemoMotion>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var t = timeState.WorldSeconds;
            foreach (var (motion, transform) in SystemAPI.Query<RefRO<Space4XDemoMotion>, RefRW<LocalTransform>>())
            {
                var phase = t * motion.ValueRO.Speed + motion.ValueRO.PhaseOffset;
                var offset = new float3(math.cos(phase), 0f, math.sin(phase)) * motion.ValueRO.Amplitude;
                var updated = transform.ValueRO;
                updated.Position = motion.ValueRO.BasePosition + offset;
                transform.ValueRW = updated;
            }
        }
    }
}

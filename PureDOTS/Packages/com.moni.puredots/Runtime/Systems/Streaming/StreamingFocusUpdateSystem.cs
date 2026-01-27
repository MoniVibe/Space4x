using PureDOTS.Runtime.Streaming;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Writes the transform position into the StreamingFocus components that opt in.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateBefore(typeof(StreamingScannerSystem))]
    public partial struct StreamingFocusUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingFocus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            foreach (var (focus, follow, transform) in SystemAPI.Query<RefRW<StreamingFocus>, RefRO<StreamingFocusFollow>, RefRO<LocalTransform>>())
            {
                if (!follow.ValueRO.UseTransform)
                {
                    continue;
                }

                var currentPosition = transform.ValueRO.Position;
                var previousPosition = focus.ValueRO.Position;
                focus.ValueRW.Position = currentPosition;

                if (deltaTime > 0f)
                {
                    focus.ValueRW.Velocity = (currentPosition - previousPosition) / deltaTime;
                }
                else
                {
                    focus.ValueRW.Velocity = float3.zero;
                }
            }
        }
    }
}

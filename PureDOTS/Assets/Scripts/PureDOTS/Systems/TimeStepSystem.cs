using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    public partial struct TimeStepSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingletonRW<TimeState>();
            if (time.ValueRO.IsPaused)
            {
                return;
            }

            uint increment = 1u;
            if (time.ValueRO.CurrentSpeedMultiplier > 1f)
            {
                increment = (uint)math.max(1f, math.round(time.ValueRO.CurrentSpeedMultiplier));
            }

            time.ValueRW.Tick += increment;
        }
    }
}

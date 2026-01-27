using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(FuelConsumerSystem))]
    public partial struct FuelEffectivenessSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<PowerEffectiveness>();
            state.RequireForUpdate<FuelConsumerState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            foreach (var (effectiveness, fuelState) in SystemAPI
                         .Query<RefRW<PowerEffectiveness>, RefRO<FuelConsumerState>>())
            {
                var ratio = math.clamp(fuelState.ValueRO.FuelRatio, 0f, 1f);
                effectiveness.ValueRW.Value = math.max(0f, effectiveness.ValueRO.Value * ratio);
            }
        }
    }
}

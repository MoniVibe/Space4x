using PureDOTS.Runtime.Ships;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CrewContractSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrewContract>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var contract in SystemAPI.Query<RefRW<CrewContract>>())
            {
                contract.ValueRW.TimeServed += deltaTime / 60f;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

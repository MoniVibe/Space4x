using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DropOnlyHarvestDepositSystem))]
    public partial struct DropOnlyHarvestDropIntervalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceDropConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var dropConfig in SystemAPI.Query<RefRW<ResourceDropConfig>>())
            {
                dropConfig.ValueRW.TimeSinceLastDrop += deltaTime;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

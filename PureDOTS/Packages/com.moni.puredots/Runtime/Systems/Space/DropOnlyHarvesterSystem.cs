using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MiningLoopSystem))]
    public partial struct DropOnlyHarvesterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DropOnlyHarvesterTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (loopState, dropConfig) in SystemAPI.Query<RefRW<MiningLoopState>, RefRO<ResourceDropConfig>>().WithAll<DropOnlyHarvesterTag>())
            {
                if (loopState.ValueRO.Phase == MiningLoopPhase.TravellingToDropoff)
                {
                    // Force drop-off locally rather than returning to carrier
                    loopState.ValueRW.Phase = MiningLoopPhase.DroppingOff;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

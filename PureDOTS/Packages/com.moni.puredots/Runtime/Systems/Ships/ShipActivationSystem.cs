using PureDOTS.Runtime.Ships;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ShipActivationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipOwnership>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (ownership, crew) in SystemAPI.Query<RefRW<ShipOwnership>, RefRO<CrewAggregate>>())
            {
                if (ownership.ValueRO.Status == ShipStatus.ColdStorage && crew.ValueRO.MemberCount > 0)
                {
                    ownership.ValueRW.Status = ShipStatus.Crewed;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

// [TRI-STUB] Stub system for perception channel integration
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Perception
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PerceptionChannelStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


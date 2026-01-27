// [TRI-STUB] Stub system for sense organs
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Perception
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SenseOrganStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


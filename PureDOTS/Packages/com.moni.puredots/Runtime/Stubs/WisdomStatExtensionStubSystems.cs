// [TRI-STUB] Stub system for Wisdom stat
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Stats
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WisdomStatStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WisdomGainModifierStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


// [TRI-STUB] Stub system for extended personality axes
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Stats
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PersonalityAxesExtensionStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


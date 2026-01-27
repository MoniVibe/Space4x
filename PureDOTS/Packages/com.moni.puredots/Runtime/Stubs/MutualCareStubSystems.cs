// [TRI-STUB] Stub system for mutual care
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Cooperation
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MutualCareStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


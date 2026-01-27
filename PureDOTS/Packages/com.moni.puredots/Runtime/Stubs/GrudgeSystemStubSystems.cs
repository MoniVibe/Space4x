// [TRI-STUB] Stub system for grudge
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Relations
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GrudgeSystemStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


// [TRI-STUB] Stub system for relation decay
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Relations
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RelationDecayStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


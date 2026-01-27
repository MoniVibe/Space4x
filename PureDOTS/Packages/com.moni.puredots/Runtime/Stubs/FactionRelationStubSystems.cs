// [TRI-STUB] Stub system for faction relations
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Relations
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FactionRelationStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}


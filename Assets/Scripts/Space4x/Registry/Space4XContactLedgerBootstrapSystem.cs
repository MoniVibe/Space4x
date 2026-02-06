using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures factions carry contact ledgers and a shared tier config exists.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XOrganizationRelationSystem))]
    public partial struct Space4XContactLedgerBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EnsureConfig(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<Space4XContactStanding>(entity))
                {
                    ecb.AddBuffer<Space4XContactStanding>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XContactTierConfig>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XContactTierConfig));
            state.EntityManager.SetComponentData(entity, Space4XContactTierConfig.Default);
        }
    }
}

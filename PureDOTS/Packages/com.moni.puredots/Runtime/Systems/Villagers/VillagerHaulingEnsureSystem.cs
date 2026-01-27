using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Adds hauling state buffers/components required for opportunistic hauling.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct VillagerHaulingEnsureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (id, entity) in SystemAPI.Query<RefRO<VillagerId>>().WithNone<VillagerHaulingState>().WithEntityAccess())
            {
                ecb.AddComponent<VillagerHaulingState>(entity);
            }

            foreach (var (id, entity) in SystemAPI.Query<RefRO<VillagerId>>().WithNone<VillagerWithdrawRequest>().WithEntityAccess())
            {
                ecb.AddBuffer<VillagerWithdrawRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}






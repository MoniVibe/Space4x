using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Keeps villager faction assignments aligned with aggregate faction buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AggregateFactionMaintenanceSystem : ISystem
    {
        private ComponentLookup<AggregateFaction> _factionLookup;
        private BufferLookup<AggregateFactionMember> _memberLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _factionLookup = state.GetComponentLookup<AggregateFaction>(true);
            _memberLookup = state.GetBufferLookup<AggregateFactionMember>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factionLookup.Update(ref state);
            _memberLookup.Update(ref state);

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (belonging, entity) in SystemAPI.Query<RefRO<VillagerAggregateBelonging>>().WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<VillagerAggregateFaction>(entity))
                {
                    continue;
                }

                var factionComp = SystemAPI.GetComponent<VillagerAggregateFaction>(entity);
                if (!_factionLookup.HasComponent(factionComp.Faction))
                {
                    ecb.RemoveComponent<VillagerAggregateFaction>(entity);
                    continue;
                }

                if (!_memberLookup.HasBuffer(factionComp.Faction))
                {
                    continue;
                }

                var members = _memberLookup[factionComp.Faction];
                var found = false;
                for (int i = 0; i < members.Length; i++)
                {
                    if (members[i].Member == entity)
                    {
                        found = true;
                        factionComp.Loyalty = members[i].Loyalty;
                        SystemAPI.SetComponent(entity, factionComp);
                        break;
                    }
                }

                if (!found)
                {
                    ecb.RemoveComponent<VillagerAggregateFaction>(entity);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

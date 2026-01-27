using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Aggregate;
using AggregateEntity = PureDOTS.Runtime.Aggregate.AggregateEntity;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// Initializes organization entities from aggregate entities.
    /// Assigns OrgId and OrgKind based on AggregateType.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct OrgInitSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (aggregate, entity) in SystemAPI.Query<RefRO<AggregateEntity>>()
                .WithNone<OrgTag, OrgId>()
                .WithEntityAccess())
            {
                // Map AggregateType to OrgKind
                var orgKind = MapAggregateTypeToOrgKind(aggregate.ValueRO.Type);
                
                // Create OrgId
                var orgId = new OrgId
                {
                    Value = entity.Index, // Use entity index as ID for now
                    Kind = orgKind,
                    ParentOrgId = -1 // Will be set later if nested
                };

                ecb.AddComponent(entity, new OrgTag());
                ecb.AddComponent(entity, orgId);
            }
        }

        private static OrgKind MapAggregateTypeToOrgKind(AggregateType aggregateType)
        {
            return aggregateType switch
            {
                AggregateType.Family => OrgKind.Family,
                AggregateType.Dynasty => OrgKind.Family, // Dynasties are extended families
                AggregateType.Guild => OrgKind.Guild,
                AggregateType.Corporation => OrgKind.Company,
                AggregateType.Band => OrgKind.Other,
                AggregateType.Army => OrgKind.Faction,
                AggregateType.Fleet => OrgKind.Other,
                AggregateType.WorkCrew => OrgKind.Other,
                AggregateType.Expedition => OrgKind.Other,
                AggregateType.Cult => OrgKind.Church,
                _ => OrgKind.Other
            };
        }
    }
}


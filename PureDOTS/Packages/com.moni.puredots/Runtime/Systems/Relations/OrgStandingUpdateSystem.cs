using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// Updates OrgStandingSnapshot from OrgRelation edges (WARM path).
    /// Staggered updates (20-100 ticks per org), event-driven triggers for important orgs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct OrgStandingUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process orgs that need standing updates
            // Query orgs with OrgTag and check their update cadence
            foreach (var (orgTag, cadence, importance, orgEntity) in
                SystemAPI.Query<RefRO<OrgTag>, RefRO<UpdateCadence>, RefRO<AIImportance>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Find all OrgRelation edges for this org
                foreach (var (relation, relationEntity) in
                    SystemAPI.Query<RefRO<OrgRelation>>()
                    .WithAll<OrgRelationTag>()
                    .WithEntityAccess())
                {
                    Entity targetOrg = Entity.Null;
                    if (relation.ValueRO.OrgA == orgEntity)
                    {
                        targetOrg = relation.ValueRO.OrgB;
                    }
                    else if (relation.ValueRO.OrgB == orgEntity)
                    {
                        targetOrg = relation.ValueRO.OrgA;
                    }
                    else
                    {
                        continue;
                    }

                    // Ensure OrgStandingSnapshot exists for this org-target pair
                    // Use a buffer or component lookup - for now, create component on org entity
                    // In full implementation, would use a buffer indexed by target org
                    if (!SystemAPI.HasComponent<OrgStandingSnapshot>(orgEntity))
                    {
                        // For simplicity, we'll create one snapshot per org
                        // Full implementation would use a buffer for multiple targets
                        ecb.AddComponent<OrgStandingSnapshot>(orgEntity, new OrgStandingSnapshot
                        {
                            TargetOrg = targetOrg,
                            Attitude = relation.ValueRO.Attitude,
                            Trust = relation.ValueRO.Trust,
                            Fear = relation.ValueRO.Fear,
                            LastUpdateTick = timeState.Tick
                        });
                    }
                    else
                    {
                        var standing = SystemAPI.GetComponentRW<OrgStandingSnapshot>(orgEntity);
                        if (standing.ValueRO.TargetOrg == targetOrg)
                        {
                            // Update existing snapshot
                            standing.ValueRW.Attitude = relation.ValueRO.Attitude;
                            standing.ValueRW.Trust = relation.ValueRO.Trust;
                            standing.ValueRW.Fear = relation.ValueRO.Fear;
                            standing.ValueRW.LastUpdateTick = timeState.Tick;
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}


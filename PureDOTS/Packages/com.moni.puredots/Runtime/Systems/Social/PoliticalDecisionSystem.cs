using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems.Relations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Social
{
    /// <summary>
    /// Evaluates political events (alliances, sanctions) with budgets (COLD path).
    /// Processes few per tick with budget, updates OrgRelation + OrgTreatyFlags when decided.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: RelationPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct PoliticalDecisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RelationPerformanceBudget>();
            state.RequireForUpdate<RelationPerformanceCounters>();
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

            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<RelationPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.PoliticalDecisionsThisTick >= budget.MaxPoliticalDecisionsPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process alliance considerations
            foreach (var (allianceConsideration, entity) in
                SystemAPI.Query<RefRO<AllianceConsideration>>()
                .WithEntityAccess())
            {
                if (counters.ValueRO.PoliticalDecisionsThisTick >= budget.MaxPoliticalDecisionsPerTick)
                {
                    break;
                }

                // Evaluate alliance decision
                bool shouldAlliance = EvaluateAlliance(allianceConsideration.ValueRO);

                if (shouldAlliance)
                {
                    // Update OrgRelation
                    UpdateOrgRelationForAlliance(
                        ref state,
                        allianceConsideration.ValueRO.InitiatorOrg,
                        allianceConsideration.ValueRO.TargetOrg,
                        ecb);
                }

                // Remove consideration entity
                ecb.RemoveComponent<AllianceConsideration>(entity);
                counters.ValueRW.PoliticalDecisionsThisTick++;
            }

            // Process sanction considerations
            foreach (var (sanctionConsideration, entity) in
                SystemAPI.Query<RefRO<SanctionConsideration>>()
                .WithEntityAccess())
            {
                if (counters.ValueRO.PoliticalDecisionsThisTick >= budget.MaxPoliticalDecisionsPerTick)
                {
                    break;
                }

                // Evaluate sanction decision
                bool shouldSanction = EvaluateSanction(sanctionConsideration.ValueRO);

                if (shouldSanction)
                {
                    // Update OrgRelation
                    UpdateOrgRelationForSanction(
                        ref state,
                        sanctionConsideration.ValueRO.InitiatorOrg,
                        sanctionConsideration.ValueRO.TargetOrg,
                        sanctionConsideration.ValueRO.Reason,
                        ecb);
                }

                // Remove consideration entity
                ecb.RemoveComponent<SanctionConsideration>(entity);
                counters.ValueRW.PoliticalDecisionsThisTick++;
            }

            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        private static bool EvaluateAlliance(in AllianceConsideration consideration)
        {
            // Simple evaluation: alliance if attitude > 50 and threat > 0.3
            return consideration.CurrentAttitude > 50f && consideration.ThreatLevel > 0.3f;
        }

        [BurstCompile]
        private static bool EvaluateSanction(in SanctionConsideration consideration)
        {
            // Simple evaluation: sanction if attitude < -30 and offense severity > 0.5
            return consideration.CurrentAttitude < -30f && consideration.OffenseSeverity > 0.5f;
        }

        [BurstCompile]
        private void UpdateOrgRelationForAlliance(
            ref SystemState state,
            Entity orgA,
            Entity orgB,
            EntityCommandBuffer ecb)
        {
            // Find OrgRelation between these orgs
            foreach (var (relation, relationEntity) in
                SystemAPI.Query<RefRW<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                if ((relation.ValueRO.OrgA == orgA && relation.ValueRO.OrgB == orgB) ||
                    (relation.ValueRO.OrgA == orgB && relation.ValueRO.OrgB == orgA))
                {
                    // Update relation
                    relation.ValueRW.Kind = OrgRelationKind.Allied;
                    relation.ValueRW.Treaties |= OrgTreatyFlags.DefensivePact;
                    relation.ValueRW.Attitude = math.min(100f, relation.ValueRO.Attitude + 20f);
                    relation.ValueRW.Trust = math.min(1f, relation.ValueRO.Trust + 0.2f);
                    relation.ValueRW.LastUpdateTick = SystemAPI.GetSingleton<TimeState>().Tick;
                    break;
                }
            }
        }

        [BurstCompile]
        private void UpdateOrgRelationForSanction(
            ref SystemState state,
            Entity orgA,
            Entity orgB,
            SanctionReason reason,
            EntityCommandBuffer ecb)
        {
            // Find OrgRelation between these orgs
            foreach (var (relation, relationEntity) in
                SystemAPI.Query<RefRW<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                if ((relation.ValueRO.OrgA == orgA && relation.ValueRO.OrgB == orgB) ||
                    (relation.ValueRO.OrgA == orgB && relation.ValueRO.OrgB == orgA))
                {
                    // Update relation
                    relation.ValueRW.Kind = OrgRelationKind.Sanctioned;
                    relation.ValueRW.Treaties |= OrgTreatyFlags.Sanctions;
                    relation.ValueRW.Attitude = math.max(-100f, relation.ValueRO.Attitude - 30f);
                    relation.ValueRW.Trust = math.max(0f, relation.ValueRO.Trust - 0.3f);
                    relation.ValueRW.LastUpdateTick = SystemAPI.GetSingleton<TimeState>().Tick;
                    break;
                }
            }
        }
    }
}


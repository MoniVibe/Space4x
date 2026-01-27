using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Handles cult mass sacrifice operations with outcomes and relation impacts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct RitualSacrificeSystem : ISystem
    {
        private ComponentLookup<OrgRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();

            _relationLookup = state.GetComponentLookup<OrgRelation>(false); // Need write access for relation updates
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            _relationLookup.Update(ref state);

            // Process all active cult ritual operations
            foreach (var (operation, rules, progress, ritualParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<CultRitualParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.Kind != OperationKind.CultRitual)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - op.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Progress ritual completion
                ProgressRitual(ref ritualParams.ValueRW, progress.ValueRO, participants);

                // Check for discovery by outsiders
                CheckDiscovery(ref ritualParams.ValueRW, op, progress.ValueRO, currentTick);

                // Apply relation impacts when discovered
                if (ritualParams.ValueRO.IsDiscovered == 1)
                {
                    ApplyDiscoveryImpacts(ref state, op, ref progress.ValueRW, ritualParams.ValueRO);
                }

                // Complete ritual and grant outcomes
                if (ritualParams.ValueRO.CompletionProgress >= 1f)
                {
                    CompleteRitual(ref ritualParams.ValueRW, ref progress.ValueRW, participants);
                    operation.ValueRW.State = OperationState.Resolving;
                }

                // Update success metric
                UpdateRitualSuccessMetric(ref progress.ValueRW, ritualParams.ValueRO);

                // Check if operation should resolve
                if (OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void ProgressRitual(
            ref CultRitualParams ritualParams,
            OperationProgress progress,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Ritual completion increases with:
            // - Number of participants (cultists)
            // - Number of sacrifices
            // - Time elapsed

            float participantContribution = OperationHelpers.GetTotalContribution(participants) / 10f; // Normalize
            float sacrificeContribution = math.clamp(ritualParams.SacrificeCount / 20f, 0f, 1f);
            float timeContribution = math.clamp(progress.ElapsedTicks / 108000f, 0f, 1f); // 30 min = full completion

            ritualParams.CompletionProgress = (participantContribution * 0.4f + sacrificeContribution * 0.4f + timeContribution * 0.2f);
            ritualParams.CompletionProgress = math.clamp(ritualParams.CompletionProgress, 0f, 1f);
        }

        [BurstCompile]
        private void CheckDiscovery(
            ref CultRitualParams ritualParams,
            Operation operation,
            OperationProgress progress,
            uint currentTick)
        {
            if (ritualParams.IsDiscovered == 1)
                return; // Already discovered

            // Discovery probability increases with:
            // - Time elapsed (longer ritual = more likely to be discovered)
            // - Number of participants (more people = more leaks)
            // - Area taint (magical residue attracts attention)

            float timeRisk = math.clamp(progress.ElapsedTicks / 216000f, 0f, 1f); // 1 hour = high risk
            float taintRisk = ritualParams.AreaTaint;

            float discoveryProbability = (timeRisk * 0.6f + taintRisk * 0.4f) * 0.001f; // Per tick

            // Simple probability check (in production, use proper RNG)
            if (currentTick % 1000 == 0 && discoveryProbability > 0.0001f) // Check every ~4 seconds
            {
                // Discovered!
                ritualParams.IsDiscovered = 1;
            }
        }

        [BurstCompile]
        private void ApplyDiscoveryImpacts(
            ref SystemState state,
            Operation operation,
            ref OperationProgress progress,
            CultRitualParams ritualParams)
        {
            // When discovered, relations with other orgs take massive hit
            // Find all relations involving the cult org
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (relation, relationEntity) in SystemAPI.Query<RefRW<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                Entity otherOrg = Entity.Null;
                if (relation.ValueRO.OrgA == operation.InitiatorOrg)
                    otherOrg = relation.ValueRO.OrgB;
                else if (relation.ValueRO.OrgB == operation.InitiatorOrg)
                    otherOrg = relation.ValueRO.OrgA;

                if (otherOrg != Entity.Null && otherOrg != operation.TargetOrg)
                {
                    // Massive hit to attitude and trust
                    relation.ValueRW.Attitude -= 50f; // Huge penalty
                    relation.ValueRW.Attitude = math.clamp(relation.ValueRW.Attitude, -100f, 100f);

                    relation.ValueRW.Trust -= 0.5f; // Huge penalty
                    relation.ValueRW.Trust = math.clamp(relation.ValueRW.Trust, 0f, 1f);

                    relation.ValueRW.Fear += 0.3f; // Increase fear
                    relation.ValueRW.Fear = math.clamp(relation.ValueRW.Fear, 0f, 1f);

                    relation.ValueRW.Respect -= 0.4f; // Decrease respect
                    relation.ValueRW.Respect = math.clamp(relation.ValueRW.Respect, 0f, 1f);

                    relation.ValueRW.LastUpdateTick = SystemAPI.GetSingleton<TimeState>().Tick;
                }
            }

            // Increase unrest and casualties
            progress.Unrest += 0.2f; // Massive unrest spike
            progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);

            progress.Casualties += ritualParams.SacrificeCount; // All sacrifices count as casualties
        }

        [BurstCompile]
        private void CompleteRitual(
            ref CultRitualParams ritualParams,
            ref OperationProgress progress,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Grant mana/favor based on sacrifices
            float manaGain = ritualParams.SacrificeCount * 10f; // 10 mana per sacrifice
            ritualParams.ManaGained = manaGain;

            // Increase area taint
            ritualParams.AreaTaint += ritualParams.SacrificeCount * 0.05f;
            ritualParams.AreaTaint = math.clamp(ritualParams.AreaTaint, 0f, 1f);

            // Note: In production, grant actual mana to cult patron, spawn entities, etc.
            // For now, we just track it in the params

            // Update success metric
            progress.SuccessMetric = 1f; // Ritual completed successfully
        }

        [BurstCompile]
        private void UpdateRitualSuccessMetric(
            ref OperationProgress progress,
            CultRitualParams ritualParams)
        {
            // Success increases with:
            // - Completion progress
            // - Mana gained
            // - Area taint (successful ritual taints area)

            float completionScore = ritualParams.CompletionProgress * 0.5f;
            float manaScore = math.clamp(ritualParams.ManaGained / 200f, 0f, 1f) * 0.3f; // Normalize to 200 mana = full score
            float taintScore = ritualParams.AreaTaint * 0.2f;

            progress.SuccessMetric = completionScore + manaScore + taintScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






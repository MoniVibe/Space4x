using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Manages protests/riots with grievance tracking, scale, organization, and escalation thresholds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct ProtestRiotSystem : ISystem
    {
        private ComponentLookup<OrgRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();

            _relationLookup = state.GetComponentLookup<OrgRelation>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            _relationLookup.Update(ref state);

            // Process all active protest/riot operations
            foreach (var (operation, rules, progress, protestParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<ProtestRiotParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.Kind != OperationKind.Protest && op.Kind != OperationKind.Riot)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - op.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Update grievance level from relations
                UpdateGrievanceLevel(ref protestParams.ValueRW, op, ref state);

                // Grow crowd size over time
                GrowCrowdSize(ref protestParams.ValueRW, progress.ValueRO, participants);

                // Increase organization level over time
                IncreaseOrganization(ref protestParams.ValueRW, progress.ValueRO);

                // Check for escalation (protest → riot)
                CheckEscalation(ref operation.ValueRW, ref protestParams.ValueRW, progress.ValueRO, rules.ValueRO);

                // Apply effects based on whether it's a protest or riot
                if (protestParams.ValueRO.IsRiot == 1)
                {
                    ApplyRiotEffects(ref progress.ValueRW, protestParams.ValueRO, participants);
                }
                else
                {
                    ApplyProtestEffects(ref progress.ValueRW, protestParams.ValueRO, participants);
                }

                // Update success metric
                UpdateProtestRiotSuccessMetric(ref progress.ValueRW, protestParams.ValueRO, rules.ValueRO);

                // Check if operation should resolve
                if (OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void UpdateGrievanceLevel(
            ref ProtestRiotParams protestParams,
            Operation operation,
            ref SystemState state)
        {
            // Get relation between initiator and target
            OrgRelation relation = GetRelation(ref state, operation.InitiatorOrg, operation.TargetOrg);

            // Grievance = low attitude + low trust
            float attitudeGrievance = math.max(0f, -relation.Attitude / 100f);
            float trustGrievance = 1f - relation.Trust;

            protestParams.GrievanceLevel = (attitudeGrievance + trustGrievance) / 2f;
            protestParams.GrievanceLevel = math.clamp(protestParams.GrievanceLevel, 0f, 1f);
        }

        [BurstCompile]
        private OrgRelation GetRelation(ref SystemState state, Entity orgA, Entity orgB)
        {
            foreach (var relation in SystemAPI.Query<RefRO<OrgRelation>>()
                .WithAll<OrgRelationTag>())
            {
                if ((relation.ValueRO.OrgA == orgA && relation.ValueRO.OrgB == orgB) ||
                    (relation.ValueRO.OrgA == orgB && relation.ValueRO.OrgB == orgA))
                {
                    return relation.ValueRO;
                }
            }

            // Return neutral relation if not found
            return new OrgRelation
            {
                OrgA = orgA,
                OrgB = orgB,
                Attitude = 0f,
                Trust = 0.5f,
                Fear = 0f,
                Respect = 0.5f,
                Dependence = 0f
            };
        }

        [BurstCompile]
        private void GrowCrowdSize(
            ref ProtestRiotParams protestParams,
            OperationProgress progress,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Crowd size grows with:
            // - Grievance level
            // - Number of participants
            // - Time elapsed

            float participantSize = math.clamp(participants.Length / 50f, 0f, 1f);
            float timeGrowth = math.clamp(progress.ElapsedTicks / 108000f, 0f, 1f); // 30 min = full growth

            protestParams.CrowdSize = math.clamp(
                protestParams.GrievanceLevel * 0.4f + participantSize * 0.3f + timeGrowth * 0.3f,
                0f, 1f);
        }

        [BurstCompile]
        private void IncreaseOrganization(
            ref ProtestRiotParams protestParams,
            OperationProgress progress)
        {
            // Organization increases over time (spontaneous → organized)
            float timeOrganization = math.clamp(progress.ElapsedTicks / 216000f, 0f, 1f); // 1 hour = fully organized

            protestParams.OrganizationLevel = math.max(
                protestParams.OrganizationLevel,
                timeOrganization * 0.5f + protestParams.GrievanceLevel * 0.5f);
            protestParams.OrganizationLevel = math.clamp(protestParams.OrganizationLevel, 0f, 1f);
        }

        [BurstCompile]
        private void CheckEscalation(
            ref Operation operation,
            ref ProtestRiotParams protestParams,
            OperationProgress progress,
            OperationRules rules)
        {
            // Escalate to riot if grievance exceeds threshold
            if (protestParams.IsRiot == 0 && 
                protestParams.GrievanceLevel > protestParams.EscalationThreshold)
            {
                protestParams.IsRiot = 1;
                operation.Kind = OperationKind.Riot;
                
                // Increase severity when escalating
                // Note: Rules are readonly, so we can't modify them here
                // In production, create new operation or modify via ECB
            }
        }

        [BurstCompile]
        private void ApplyProtestEffects(
            ref OperationProgress progress,
            ProtestRiotParams protestParams,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Protests cause:
            // - Moderate unrest
            // - Visibility/pressure on target
            // - Low casualties

            float crowdEffect = protestParams.CrowdSize * protestParams.OrganizationLevel;
            
            progress.Unrest += crowdEffect * 0.0005f; // Per tick
            progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);

            // Casualties are minimal (occasional clashes)
            if (progress.ElapsedTicks % 20000 == 0) // Every ~80 seconds
            {
                progress.Casualties += (int)(crowdEffect * 2f);
            }
        }

        [BurstCompile]
        private void ApplyRiotEffects(
            ref OperationProgress progress,
            ProtestRiotParams protestParams,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Riots cause:
            // - High unrest
            // - Significant casualties
            // - Property damage (tracked as casualties)
            // - Morale damage

            float crowdEffect = protestParams.CrowdSize * protestParams.OrganizationLevel;
            
            progress.Unrest += crowdEffect * 0.002f; // Per tick, higher than protest
            progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);

            progress.TargetMorale -= crowdEffect * 0.0001f; // Per tick
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);

            // Casualties are significant (violent clashes)
            if (progress.ElapsedTicks % 5000 == 0) // Every ~20 seconds
            {
                progress.Casualties += (int)(crowdEffect * 10f);
            }
        }

        [BurstCompile]
        private void UpdateProtestRiotSuccessMetric(
            ref OperationProgress progress,
            ProtestRiotParams protestParams,
            OperationRules rules)
        {
            // Success increases with:
            // - High grievance level
            // - Large crowd size
            // - High organization
            // - High unrest
            // - Time elapsed

            float grievanceScore = protestParams.GrievanceLevel * 0.3f;
            float crowdScore = protestParams.CrowdSize * 0.2f;
            float organizationScore = protestParams.OrganizationLevel * 0.2f;
            float unrestScore = progress.Unrest * 0.2f;
            float timeScore = math.clamp(progress.ElapsedTicks / 216000f, 0f, 1f) * 0.1f; // 1 hour = full score

            progress.SuccessMetric = grievanceScore + crowdScore + organizationScore + unrestScore + timeScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






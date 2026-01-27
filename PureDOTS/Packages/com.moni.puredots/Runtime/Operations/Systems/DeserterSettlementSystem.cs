using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Converts deserter bands into new settlement Orgs with derived alignment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct DeserterSettlementSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<OrgPersona> _personaLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _personaLookup = state.GetComponentLookup<OrgPersona>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            _alignmentLookup.Update(ref state);
            _personaLookup.Update(ref state);

            // Process all active deserter settlement operations
            foreach (var (operation, rules, progress, deserterParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<DeserterSettlementParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.Kind != OperationKind.DeserterSettlement)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - op.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Check if settlement should be founded
                if (deserterParams.ValueRO.SettlementFounded == 0)
                {
                    // Settlement requires:
                    // - Minimum deserter count
                    // - Sufficient time elapsed
                    // - Suitable location

                    if (deserterParams.ValueRO.DeserterCount >= 5 && 
                        progress.ValueRO.ElapsedTicks >= 108000) // 30 min
                    {
                        FoundSettlement(ref state, op, ref deserterParams.ValueRW, ref progress.ValueRW, participants);
                    }
                }

                // Update success metric
                UpdateDeserterSuccessMetric(ref progress.ValueRW, deserterParams.ValueRO);

                // Check if operation should resolve
                if (deserterParams.ValueRO.SettlementFounded == 1 || 
                    OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void FoundSettlement(
            ref SystemState state,
            Operation operation,
            ref DeserterSettlementParams deserterParams,
            ref OperationProgress progress,
            DynamicBuffer<OperationParticipant> participants)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Create new organization entity for the settlement
            var newOrgEntity = ecb.CreateEntity();
            ecb.AddComponent(newOrgEntity, new OrgTag());
            ecb.AddComponent(newOrgEntity, new OrgId
            {
                Value = newOrgEntity.Index,
                Kind = OrgKind.Other, // Settlement/Colony
                ParentOrgId = -1 // Independent
            });

            // Derive alignment from deserter alignment and grievances
            AlignmentTriplet deserterAlignment = DeriveDeserterAlignment(ref state, operation, participants);

            // Add alignment component to new org
            if (!_alignmentLookup.HasComponent(newOrgEntity))
            {
                ecb.AddComponent(newOrgEntity, deserterAlignment);
            }

            // Derive persona from deserter persona
            OrgPersona deserterPersona = DeriveDeserterPersona(ref state, operation, participants);
            ecb.AddComponent(newOrgEntity, deserterPersona);

            // Mark settlement as founded
            deserterParams.SettlementFounded = 1;
            deserterParams.NewOrgEntity = newOrgEntity;

            // Update success metric
            progress.SuccessMetric = 1f; // Settlement founded successfully

            // Note: In production, also:
            // - Create relation with original org (hostile/traitor)
            // - Set up location/spatial components
            // - Initialize resources/infrastructure
        }

        [BurstCompile]
        private AlignmentTriplet DeriveDeserterAlignment(
            ref SystemState state,
            Operation operation,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Derive alignment from:
            // - Original org alignment (with shift toward chaos/distrust)
            // - Participant alignments (average)

            AlignmentTriplet originalAlignment = AlignmentTriplet.FromFloats(0f, 0f, 0f);
            Entity originalOrg = Entity.Null;
            
            // Get original org from operation target (simplified - in production, use deserterParams)
            // For now, use target org as original org
            if (operation.TargetOrg != Entity.Null && _alignmentLookup.HasComponent(operation.TargetOrg))
            {
                originalAlignment = _alignmentLookup[operation.TargetOrg];
                originalOrg = operation.TargetOrg;
            }

            // Deserters shift toward:
            // - More chaotic (distrust of authority)
            // - More neutral/corrupt (self-preservation)
            // - Slightly more evil (betrayal)

            float moral = originalAlignment.Moral - 0.2f; // Shift toward evil
            float order = originalAlignment.Order - 0.4f; // Shift toward chaos
            float purity = originalAlignment.Purity - 0.3f; // Shift toward corrupt

            // Average with participant alignments if available
            int participantCount = 0;
            float moralSum = moral;
            float orderSum = order;
            float puritySum = purity;

            for (int i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (_alignmentLookup.HasComponent(participant.ParticipantEntity))
                {
                    var partAlignment = _alignmentLookup[participant.ParticipantEntity];
                    moralSum += partAlignment.Moral;
                    orderSum += partAlignment.Order;
                    puritySum += partAlignment.Purity;
                    participantCount++;
                }
            }

            if (participantCount > 0)
            {
                moral = (moralSum + moral) / (participantCount + 1);
                order = (orderSum + order) / (participantCount + 1);
                purity = (puritySum + purity) / (participantCount + 1);
            }

            return AlignmentTriplet.FromFloats(moral, order, purity);
        }

        [BurstCompile]
        private OrgPersona DeriveDeserterPersona(
            ref SystemState state,
            Operation operation,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Derive persona from:
            // - Original org persona (with shift toward vengeful/distrustful)
            // - Participant personas (average)

            OrgPersona originalPersona = new OrgPersona
            {
                VengefulForgiving = 0.5f,
                CravenBold = 0.5f,
                Cohesion = 0.5f
            };

            // Get original org from operation target (simplified)
            Entity originalOrg = operation.TargetOrg;
            if (originalOrg != Entity.Null && _personaLookup.HasComponent(originalOrg))
            {
                originalPersona = _personaLookup[originalOrg];
            }

            // Deserters shift toward:
            // - More vengeful (grudge against original org)
            // - More bold (took risk to desert)
            // - Lower cohesion (fragmented group)

            float vengeful = math.min(1f, originalPersona.VengefulForgiving + 0.3f);
            float bold = math.min(1f, originalPersona.CravenBold + 0.2f);
            float cohesion = math.max(0f, originalPersona.Cohesion - 0.3f);

            // Average with participant personas if available
            int participantCount = 0;
            float vengefulSum = vengeful;
            float boldSum = bold;
            float cohesionSum = cohesion;

            for (int i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                // Note: Participants might not have OrgPersona (they're individuals)
                // In production, aggregate from individual PersonalityAxes
                participantCount++;
            }

            if (participantCount > 0)
            {
                vengeful = vengefulSum / (participantCount + 1);
                bold = boldSum / (participantCount + 1);
                cohesion = cohesionSum / (participantCount + 1);
            }

            return new OrgPersona
            {
                VengefulForgiving = math.clamp(vengeful, 0f, 1f),
                CravenBold = math.clamp(bold, 0f, 1f),
                Cohesion = math.clamp(cohesion, 0f, 1f),
                LastUpdateTick = SystemAPI.GetSingleton<TimeState>().Tick
            };
        }

        [BurstCompile]
        private void UpdateDeserterSuccessMetric(
            ref OperationProgress progress,
            DeserterSettlementParams deserterParams)
        {
            // Success increases with:
            // - Settlement founded
            // - Deserter count
            // - Time elapsed

            float foundedScore = deserterParams.SettlementFounded * 0.5f;
            float countScore = math.clamp(deserterParams.DeserterCount / 20f, 0f, 1f) * 0.3f;
            float timeScore = math.clamp(progress.ElapsedTicks / 216000f, 0f, 1f) * 0.2f; // 1 hour = full score

            progress.SuccessMetric = foundedScore + countScore + timeScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}


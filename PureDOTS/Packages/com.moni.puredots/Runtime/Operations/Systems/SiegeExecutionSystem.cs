using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Executes siege operations by managing encirclement, supply tracking, attrition, and rules of engagement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct SiegeExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            // Process all active siege operations
            foreach (var (operation, rules, progress, siegeParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<SiegeParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                if (operation.ValueRO.Kind != OperationKind.Siege)
                    continue;

                if (operation.ValueRO.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - operation.ValueRO.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Update encirclement level based on participants
                UpdateEncirclement(ref siegeParams.ValueRW, participants);

                // Check if encirclement is sufficient
                if (siegeParams.ValueRO.EncirclementLevel < siegeParams.ValueRO.MinEncirclementRequired)
                {
                    // Siege not fully established, reduce effectiveness
                    progress.ValueRW.SuccessMetric *= 0.95f; // Decay slowly
                    continue;
                }

                // Apply attrition to target
                ApplyAttrition(ref progress.ValueRW, siegeParams.ValueRO, rules.ValueRO);

                // Update supply levels
                UpdateSupplyLevels(ref progress.ValueRW, siegeParams.ValueRO, currentTick);

                // Apply famine if supply is low
                if (progress.ValueRO.TargetSupplyLevel < siegeParams.ValueRO.FamineThreshold)
                {
                    ApplyFamine(ref progress.ValueRW, siegeParams.ValueRO);
                }

                // Update morale based on supply and famine
                UpdateMorale(ref progress.ValueRW, siegeParams.ValueRO);

                // Update success metric
                UpdateSiegeSuccessMetric(ref progress.ValueRW, siegeParams.ValueRO, rules.ValueRO);

                // Check if siege should resolve
                if (OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void UpdateEncirclement(ref SiegeParams siegeParams, DynamicBuffer<OperationParticipant> participants)
        {
            // Encirclement = sum of participant contributions
            float totalContribution = 0f;
            for (int i = 0; i < participants.Length; i++)
            {
                totalContribution += participants[i].Contribution;
            }

            // Normalize to 0-1 (assuming 10 participants = full encirclement)
            siegeParams.EncirclementLevel = math.clamp(totalContribution / 10f, 0f, 1f);
        }

        [BurstCompile]
        private void ApplyAttrition(
            ref OperationProgress progress,
            SiegeParams siegeParams,
            OperationRules rules)
        {
            // Attrition reduces target supply
            float attritionAmount = siegeParams.AttritionRate * progress.ElapsedTicks;
            progress.TargetSupplyLevel = math.max(0f, progress.TargetSupplyLevel - attritionAmount);

            // Attrition also affects morale
            progress.TargetMorale -= siegeParams.AttritionRate * 0.1f;
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);
        }

        [BurstCompile]
        private void UpdateSupplyLevels(
            ref OperationProgress progress,
            SiegeParams siegeParams,
            uint currentTick)
        {
            // Siege side supply decays slowly (needs logistics support)
            progress.SiegeSupplyLevel -= 0.0001f; // Very slow decay
            progress.SiegeSupplyLevel = math.max(0f, progress.SiegeSupplyLevel);

            // Target supply is cut off (already handled by attrition)
            // But can be replenished if humanitarian corridors are allowed
            if (progress.TargetSupplyLevel < 1f && progress.SiegeSupplyLevel > 0.5f)
            {
                // Small replenishment if corridors allowed (simplified)
                // In production, check rules.AllowHumanitarianCorridors
                progress.TargetSupplyLevel += 0.00005f;
                progress.TargetSupplyLevel = math.min(1f, progress.TargetSupplyLevel);
            }
        }

        [BurstCompile]
        private void ApplyFamine(ref OperationProgress progress, SiegeParams siegeParams)
        {
            // Famine increases unrest and casualties
            float famineSeverity = 1f - (progress.TargetSupplyLevel / siegeParams.FamineThreshold);
            
            progress.Unrest += famineSeverity * 0.001f;
            progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);

            // Casualties increase with famine severity
            if (progress.ElapsedTicks % 1000 == 0) // Every ~4 seconds
            {
                int casualties = (int)(famineSeverity * 10f);
                progress.Casualties += casualties;
            }

            // Disease risk increases with famine
            float diseaseRisk = famineSeverity * siegeParams.DiseaseRiskMultiplier;
            if (diseaseRisk > 0.5f && progress.ElapsedTicks % 5000 == 0) // Disease outbreak
            {
                progress.Casualties += (int)(diseaseRisk * 20f);
            }
        }

        [BurstCompile]
        private void UpdateMorale(ref OperationProgress progress, SiegeParams siegeParams)
        {
            // Morale decreases with:
            // - Low supply
            // - High unrest
            // - Famine
            // - Time under siege

            float supplyMorale = progress.TargetSupplyLevel * 0.4f;
            float unrestMorale = (1f - progress.Unrest) * 0.3f;
            float timeMorale = math.max(0f, 1f - (progress.ElapsedTicks / 432000f)) * 0.3f; // Decay over 2 hours

            progress.TargetMorale = supplyMorale + unrestMorale + timeMorale;
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);
        }

        [BurstCompile]
        private void UpdateSiegeSuccessMetric(
            ref OperationProgress progress,
            SiegeParams siegeParams,
            OperationRules rules)
        {
            // Success increases with:
            // - High encirclement
            // - Low target supply
            // - Low target morale
            // - High unrest
            // - Time elapsed

            float encirclementScore = siegeParams.EncirclementLevel * 0.2f;
            float supplyScore = (1f - progress.TargetSupplyLevel) * 0.3f;
            float moraleScore = (1f - progress.TargetMorale) * 0.2f;
            float unrestScore = progress.Unrest * 0.2f;
            float timeScore = math.clamp(progress.ElapsedTicks / 432000f, 0f, 1f) * 0.1f; // 2 hours = full score

            progress.SuccessMetric = encirclementScore + supplyScore + moraleScore + unrestScore + timeScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






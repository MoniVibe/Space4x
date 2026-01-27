using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Manages funeral processions for renowned individuals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct FuneralProcessionSystem : ISystem
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

            // Process all active funeral operations
            foreach (var (operation, rules, progress, funeralParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<FuneralParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.Kind != OperationKind.Funeral)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - op.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Progress procession
                ProgressProcession(ref funeralParams.ValueRW, progress.ValueRO, participants);

                // Apply morale boost to aligned entities
                ApplyMoraleBoost(ref progress.ValueRW, funeralParams.ValueRO, participants);

                // Create legacy if renown is high enough
                if (funeralParams.ValueRO.RenownLevel > 0.7f && funeralParams.ValueRO.LegacyCreated == 0)
                {
                    CreateLegacy(ref funeralParams.ValueRW);
                }

                // Check if funeral should resolve
                if (funeralParams.ValueRO.ProcessionProgress >= 1f || progress.ValueRO.ElapsedTicks > 108000) // 30 min max
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }

                // Update success metric
                UpdateFuneralSuccessMetric(ref progress.ValueRW, funeralParams.ValueRO);
            }
        }

        [BurstCompile]
        private void ProgressProcession(
            ref FuneralParams funeralParams,
            OperationProgress progress,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Procession progress increases with:
            // - Number of participants (mourners)
            // - Time elapsed

            float participantContribution = OperationHelpers.GetTotalContribution(participants) / 20f; // Normalize
            float timeContribution = math.clamp(progress.ElapsedTicks / 108000f, 0f, 1f); // 30 min = full procession

            funeralParams.ProcessionProgress = (participantContribution * 0.5f + timeContribution * 0.5f);
            funeralParams.ProcessionProgress = math.clamp(funeralParams.ProcessionProgress, 0f, 1f);
        }

        [BurstCompile]
        private void ApplyMoraleBoost(
            ref OperationProgress progress,
            FuneralParams funeralParams,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Morale boost for aligned entities (those who respected the deceased)
            float moraleBoost = funeralParams.RenownLevel * funeralParams.ProcessionProgress * 0.1f;
            
            // Note: In production, apply to actual entity morale components
            // For now, track in progress
            progress.TargetMorale += moraleBoost * 0.001f; // Per tick
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);
        }

        [BurstCompile]
        private void CreateLegacy(ref FuneralParams funeralParams)
        {
            // Create legacy (holidays, memorials, festivals named after them)
            funeralParams.LegacyCreated = 1;
            
            // Note: In production, create actual holiday/festival entities or modify calendar
            // For now, just mark as created
        }

        [BurstCompile]
        private void UpdateFuneralSuccessMetric(
            ref OperationProgress progress,
            FuneralParams funeralParams)
        {
            // Success increases with:
            // - Procession progress
            // - Renown level
            // - Legacy created

            float processionScore = funeralParams.ProcessionProgress * 0.5f;
            float renownScore = funeralParams.RenownLevel * 0.3f;
            float legacyScore = funeralParams.LegacyCreated * 0.2f;

            progress.SuccessMetric = processionScore + renownScore + legacyScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






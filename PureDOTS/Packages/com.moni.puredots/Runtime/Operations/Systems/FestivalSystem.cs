using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Manages festivals, circuses, and pilgrimage markets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct FestivalSystem : ISystem
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

            // Process all active festival/circus operations
            foreach (var (operation, rules, progress, festivalParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<FestivalParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.Kind != OperationKind.Festival && op.Kind != OperationKind.Circus)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - op.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Apply festival effects
                ApplyFestivalEffects(ref progress.ValueRW, festivalParams.ValueRO, participants);

                // Handle recruitment
                ProcessRecruitment(ref progress.ValueRW, festivalParams.ValueRO, currentTick);

                // Check if festival should end
                if (progress.ValueRO.ElapsedTicks >= festivalParams.ValueRO.DurationTicks)
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }

                // Update success metric
                UpdateFestivalSuccessMetric(ref progress.ValueRW, festivalParams.ValueRO);
            }
        }

        [BurstCompile]
        private void ApplyFestivalEffects(
            ref OperationProgress progress,
            FestivalParams festivalParams,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Festivals provide:
            // - Increased trade (trade multiplier)
            // - Joy/happiness (joy modifier)
            // - Possible crime (crime probability multiplier)
            // - Information flow

            float participantEffect = OperationHelpers.GetTotalContribution(participants) / 10f;

            // Trade boost (note: in production, modify actual trade routes/markets)
            // For now, track as success metric component

            // Joy boost
            progress.TargetMorale += festivalParams.JoyModifier * participantEffect * 0.001f; // Per tick
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);

            // Crime probability (simplified - occasional crime events)
            if (progress.ElapsedTicks % 10000 == 0) // Every ~40 seconds
            {
                float crimeChance = festivalParams.CrimeProbabilityMultiplier * 0.1f;
                if (crimeChance > 0.05f)
                {
                    progress.Casualties += 1; // Crime incident
                    progress.Unrest += 0.01f; // Slight unrest
                    progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);
                }
            }
        }

        [BurstCompile]
        private void ProcessRecruitment(
            ref OperationProgress progress,
            FestivalParams festivalParams,
            uint currentTick)
        {
            // Recruitment probability per tick
            if (currentTick % 5000 == 0) // Check every ~20 seconds
            {
                float recruitmentChance = festivalParams.RecruitmentProbability * 1000f; // Scale up
                
                // Simplified: recruitment increases success metric
                if (recruitmentChance > 0.1f)
                {
                    progress.SuccessMetric += 0.01f; // Successful recruitment
                    progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
                }
            }
        }

        [BurstCompile]
        private void UpdateFestivalSuccessMetric(
            ref OperationProgress progress,
            FestivalParams festivalParams)
        {
            // Success increases with:
            // - Trade multiplier (economic success)
            // - Joy modifier (happiness)
            // - Duration completed

            float tradeScore = math.clamp((festivalParams.TradeMultiplier - 1f) / 1f, 0f, 1f) * 0.4f; // Normalize
            float joyScore = festivalParams.JoyModifier * 0.3f;
            float durationScore = math.clamp(progress.ElapsedTicks / (float)festivalParams.DurationTicks, 0f, 1f) * 0.3f;

            progress.SuccessMetric = tradeScore + joyScore + durationScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






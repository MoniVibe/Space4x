using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Executes occupation operations by enforcing light/heavy occupation stances and modifying local law/order/crime.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct OccupationExecutionSystem : ISystem
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

            // Process all active occupation operations
            foreach (var (operation, rules, progress, occupationParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRW<OccupationParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                if (operation.ValueRO.Kind != OperationKind.Occupation)
                    continue;

                if (operation.ValueRO.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - operation.ValueRO.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Update occupation stance based on rules
                occupationParams.ValueRW.Stance = rules.ValueRO.Stance;

                // Apply law/order/crime modifiers
                ApplyOccupationModifiers(ref progress.ValueRW, occupationParams.ValueRO, rules.ValueRO);

                // Spawn resistance cells
                SpawnResistanceCells(ref progress.ValueRW, occupationParams.ValueRO, currentTick);

                // Update success metric
                UpdateOccupationSuccessMetric(ref progress.ValueRW, occupationParams.ValueRO, rules.ValueRO);

                // Check if occupation should resolve
                if (OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void ApplyOccupationModifiers(
            ref OperationProgress progress,
            OccupationParams occupationParams,
            OperationRules rules)
        {
            // Heavy occupation increases law/order but also increases unrest
            // Light occupation decreases law/order but causes less unrest

            // Law/order modifier affects local stability
            // Positive modifier = more order, negative = less order
            float lawOrderEffect = occupationParams.LawOrderModifier;
            
            // Crime modifier affects local crime rate
            // Negative modifier = less crime, positive = more crime
            float crimeEffect = occupationParams.CrimeModifier;

            // Unrest modifier directly affects unrest
            float unrestChange = occupationParams.UnrestModifier * 0.001f; // Per tick
            progress.Unrest += unrestChange;
            progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);

            // Apply modifiers to target morale
            // Heavy occupation (high stance) reduces morale more
            float moralePenalty = occupationParams.Stance * 0.0001f; // Per tick
            progress.TargetMorale -= moralePenalty;
            progress.TargetMorale = math.clamp(progress.TargetMorale, 0f, 1f);

            // Note: In production, these modifiers would affect actual law/order/crime components
            // on the target location entity. For now, we track them in progress.
        }

        [BurstCompile]
        private void SpawnResistanceCells(
            ref OperationProgress progress,
            OccupationParams occupationParams,
            uint currentTick)
        {
            // Resistance spawns based on probability and unrest
            float spawnProbability = occupationParams.ResistanceSpawnProbability * progress.Unrest;

            // Simple probability check (in production, use proper RNG)
            // Check every 1000 ticks (~4 seconds)
            if (currentTick % 1000 == 0 && spawnProbability > 0.001f)
            {
                // Spawn resistance cell (simplified - just increment counter)
                // In production, create actual resistance entity
                progress.Casualties += (int)(spawnProbability * 100f); // Resistance activity causes casualties
            }
        }

        [BurstCompile]
        private void UpdateOccupationSuccessMetric(
            ref OperationProgress progress,
            OccupationParams occupationParams,
            OperationRules rules)
        {
            // Success increases with:
            // - Low unrest
            // - High law/order (positive modifier)
            // - Low crime (negative modifier)
            // - Time elapsed (longer occupation = more control)

            float unrestScore = (1f - progress.Unrest) * 0.3f;
            float lawOrderScore = math.max(0f, occupationParams.LawOrderModifier) * 0.3f;
            float crimeScore = math.max(0f, -occupationParams.CrimeModifier) * 0.2f;
            float timeScore = math.clamp(progress.ElapsedTicks / 864000f, 0f, 1f) * 0.2f; // 4 hours = full score

            progress.SuccessMetric = unrestScore + lawOrderScore + crimeScore + timeScore;
            progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
        }
    }
}






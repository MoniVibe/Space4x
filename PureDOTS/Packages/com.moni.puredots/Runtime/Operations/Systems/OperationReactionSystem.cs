using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Handles radical/extremist responses (hide, sacrifice, fight, escape) based on alignment and persona.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct OperationReactionSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<PersonalityAxes> _personalityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            _alignmentLookup.Update(ref state);
            _personalityLookup.Update(ref state);

            // Process all active operations that affect targets (siege, occupation, blockade)
            foreach (var (operation, progress, entity) in 
                SystemAPI.Query<RefRO<Operation>, RefRW<OperationProgress>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                // Only process siege and occupation operations
                if (op.Kind != OperationKind.Siege && op.Kind != OperationKind.Occupation)
                    continue;

                if (op.State != OperationState.Active)
                    continue;

                // Find radicals/extremists in the target location
                ProcessRadicalResponses(ref state, op, ref progress.ValueRW, currentTick);
            }
        }

        [BurstCompile]
        private void ProcessRadicalResponses(
            ref SystemState state,
            Operation operation,
            ref OperationProgress progress,
            uint currentTick)
        {
            // Find entities at target location (simplified - in production, use spatial queries)
            // For now, we'll process based on operation progress and update it

            // Calculate pressure on radicals based on operation severity
            float pressure = CalculatePressure(progress, operation.Kind);

            // Radical responses are determined by:
            // - Alignment (Good+Pure → negotiate/surrender, Evil+Corrupt → sacrifice civilians)
            // - Persona (Bold+Vengeful → violent resistance, Craven+Peaceful → sneak away)

            // Simplified: Update progress based on hypothetical radical responses
            // In production, iterate through actual radical entities at target location

            // Pure + Good entities more likely to surrender
            float surrenderProbability = CalculateSurrenderProbability(progress, pressure);
            if (surrenderProbability > 0.1f && currentTick % 10000 == 0) // Check every ~40 seconds
            {
                // Some radicals surrender
                progress.SuccessMetric += surrenderProbability * 0.1f;
                progress.Casualties -= (int)(surrenderProbability * 5f); // Fewer casualties
            }

            // Corrupt + Evil entities more likely to sacrifice civilians
            float sacrificeProbability = CalculateSacrificeProbability(progress, pressure);
            if (sacrificeProbability > 0.1f && currentTick % 5000 == 0) // Check every ~20 seconds
            {
                // Radicals sacrifice civilians, ignore suffering
                progress.Casualties += (int)(sacrificeProbability * 10f);
                progress.Unrest += sacrificeProbability * 0.05f;
                progress.Unrest = math.clamp(progress.Unrest, 0f, 1f);
            }

            // Bold + Vengeful entities more likely to fight back
            float fightBackProbability = CalculateFightBackProbability(progress, pressure);
            if (fightBackProbability > 0.1f && currentTick % 3000 == 0) // Check every ~12 seconds
            {
                // Radicals fight back, cause casualties to siege/occupation forces
                progress.Casualties += (int)(fightBackProbability * 15f);
                progress.SuccessMetric -= fightBackProbability * 0.05f; // Reduces success
                progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
            }

            // Craven + Peaceful entities more likely to escape
            float escapeProbability = CalculateEscapeProbability(progress, pressure);
            if (escapeProbability > 0.1f && currentTick % 8000 == 0) // Check every ~32 seconds
            {
                // Radicals escape, reducing pressure
                progress.SuccessMetric -= escapeProbability * 0.03f; // Reduces success slightly
                progress.SuccessMetric = math.clamp(progress.SuccessMetric, 0f, 1f);
            }
        }

        [BurstCompile]
        private float CalculatePressure(OperationProgress progress, OperationKind kind)
        {
            // Pressure increases with:
            // - Low supply (famine)
            // - High unrest
            // - Low morale
            // - Time elapsed

            float supplyPressure = 1f - progress.TargetSupplyLevel;
            float unrestPressure = progress.Unrest;
            float moralePressure = 1f - progress.TargetMorale;
            float timePressure = math.clamp(progress.ElapsedTicks / 432000f, 0f, 1f); // 2 hours = full pressure

            float basePressure = (supplyPressure * 0.3f + unrestPressure * 0.2f + moralePressure * 0.3f + timePressure * 0.2f);

            // Siege has more pressure than occupation
            if (kind == OperationKind.Siege)
                basePressure *= 1.2f;

            return math.clamp(basePressure, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateSurrenderProbability(OperationProgress progress, float pressure)
        {
            // Pure + Good entities more likely to surrender under pressure
            // High pressure + low morale = surrender
            float baseProbability = pressure * (1f - progress.TargetMorale) * 0.3f;

            // Pure entities feel guilt, more likely to surrender
            // Good entities care about civilians, more likely to surrender
            baseProbability *= 1.5f; // Boost for Pure+Good alignment

            return math.clamp(baseProbability, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateSacrificeProbability(OperationProgress progress, float pressure)
        {
            // Corrupt + Evil entities more likely to sacrifice civilians
            // High pressure + high unrest = sacrifice
            float baseProbability = pressure * progress.Unrest * 0.2f;

            // Corrupt entities don't care about civilians
            // Evil entities may use them as shields
            baseProbability *= 1.8f; // Boost for Corrupt+Evil alignment

            return math.clamp(baseProbability, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateFightBackProbability(OperationProgress progress, float pressure)
        {
            // Bold + Vengeful entities more likely to fight back
            // High pressure + high morale = fight back
            float baseProbability = pressure * progress.TargetMorale * 0.25f;

            // Bold entities take risks
            // Vengeful entities seek revenge
            baseProbability *= 1.6f; // Boost for Bold+Vengeful persona

            return math.clamp(baseProbability, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateEscapeProbability(OperationProgress progress, float pressure)
        {
            // Craven + Peaceful entities more likely to escape
            // High pressure + low morale = escape
            float baseProbability = pressure * (1f - progress.TargetMorale) * 0.2f;

            // Craven entities avoid conflict
            // Peaceful entities don't want to fight
            baseProbability *= 1.4f; // Boost for Craven+Peaceful persona

            return math.clamp(baseProbability, 0f, 1f);
        }
    }
}






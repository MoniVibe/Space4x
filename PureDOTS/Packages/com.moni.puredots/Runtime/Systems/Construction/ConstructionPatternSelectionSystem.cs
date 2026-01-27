using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Selects appropriate building patterns for each ConstructionIntent category.
    /// Scores candidate patterns and assigns the best one to each intent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConstructionDemandAggregationSystem))]
    public partial struct ConstructionPatternSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ConstructionConfigState>();
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

            var configState = SystemAPI.GetSingleton<ConstructionConfigState>();

            if (!configState.Catalog.IsCreated)
            {
                return;
            }

            ref var catalog = ref configState.Catalog.Value;

            // Process groups with ConstructionIntent buffers
            foreach (var (coordinator, entity) in SystemAPI.Query<
                RefRO<BuildCoordinator>>().WithEntityAccess())
            {
                if (coordinator.ValueRO.AutoBuildEnabled == 0)
                    continue;

                if (!SystemAPI.HasBuffer<ConstructionIntent>(entity))
                    continue;

                var intentsBuffer = SystemAPI.GetBuffer<ConstructionIntent>(entity);

                // Get group metrics for unlock checks
                var groupEntityRef = entity;
                GetGroupMetrics(ref state, ref groupEntityRef, out var groupMetrics);

                // Update intents that don't have a pattern selected yet
                for (int i = 0; i < intentsBuffer.Length; i++)
                {
                    var intent = intentsBuffer[i];
                    if (intent.PatternId >= 0 || intent.Status != 0) // Already has pattern or not planned
                        continue;

                    // Find best pattern for this category
                    int bestPatternId = -1;
                    float bestScore = 0f;

                    for (int j = 0; j < catalog.Specs.Length; j++)
                    {
                        ref var spec = ref catalog.Specs[j];

                        // Check category match
                        if (spec.Category != intent.Category)
                            continue;

                        // Check unlock conditions
                        if (!IsPatternUnlocked(in spec, in groupMetrics))
                            continue;

                        // Check auto-build eligibility
                        if (spec.IsAutoBuildEligible == 0)
                            continue;

                        // Score pattern
                        var score = intent.Urgency * spec.PatternUtility;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPatternId = spec.PatternId;
                        }
                    }

                    // Assign best pattern to intent
                    if (bestPatternId >= 0)
                    {
                        intent.PatternId = bestPatternId;
                        intentsBuffer[i] = intent;
                    }
                }
            }
        }

        [BurstCompile]
        private static bool IsPatternUnlocked(in BuildingPatternSpec spec, in GroupMetrics metrics)
        {
            if (metrics.Population < spec.MinPopulation)
                return false;

            if (metrics.FoodPerCapita < spec.MinFoodPerCapita)
                return false;

            // TODO: Check RequiresAdvancementLevel against group's advancement level
            // For now, stub

            return true;
        }

        [BurstCompile]
        private static void GetGroupMetrics(ref SystemState state, ref Entity groupEntity, out GroupMetrics metrics)
        {
            // Stub - game-specific systems can extend this
            // Would read from group components (population, food, etc.)
            metrics = new GroupMetrics
            {
                Population = 10f,
                FoodPerCapita = 1f,
                AdvancementLevel = 0
            };
        }

        private struct GroupMetrics
        {
            public float Population;
            public float FoodPerCapita;
            public byte AdvancementLevel;
        }
    }
}

























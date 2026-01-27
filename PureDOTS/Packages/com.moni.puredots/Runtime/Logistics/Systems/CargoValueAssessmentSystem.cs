using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Assesses cargo value and computes raid attractiveness and escort priority.
    /// Updates CargoValueState and creates SupplyRouteSummary for AI systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CargoValueAssessmentSystem : ISystem
    {
        private ComponentLookup<CargoLoadState> _loadStateLookup;
        private ComponentLookup<CargoValueState> _valueStateLookup;
        private ComponentLookup<RoutePlan> _routePlanLookup;
        private ComponentLookup<LogisticsJob> _jobLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            _loadStateLookup = state.GetComponentLookup<CargoLoadState>(false);
            _valueStateLookup = state.GetComponentLookup<CargoValueState>(false);
            _routePlanLookup = state.GetComponentLookup<RoutePlan>(false);
            _jobLookup = state.GetComponentLookup<LogisticsJob>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario)
                || !scenario.IsInitialized
                || !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _loadStateLookup.Update(ref state);
            _valueStateLookup.Update(ref state);
            _routePlanLookup.Update(ref state);
            _jobLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Update cargo value assessments for haulers with active assignments
            foreach (var (assignment, haulerEntity) in SystemAPI.Query<RefRO<HaulAssignment>>()
                .WithAll<HaulerTag>()
                .WithEntityAccess())
            {
                if (assignment.ValueRO.JobId == 0)
                {
                    continue;
                }

                // Get cargo load state
                if (!_loadStateLookup.TryGetComponent(haulerEntity, out var loadState))
                {
                    continue;
                }

                // Get or create value state
                if (!_valueStateLookup.HasComponent(haulerEntity))
                {
                    ecb.AddComponent(haulerEntity, new CargoValueState());
                }

                var valueStateRef = _valueStateLookup.GetRefRW(haulerEntity);

                // Compute route risk (simplified - would use actual route data)
                float routeRisk = 0.1f; // Default low risk
                if (_routePlanLookup.TryGetComponent(haulerEntity, out var routePlan))
                {
                    // Longer routes = higher risk
                    routeRisk = math.min(routePlan.EstimatedDistance / 1000f, 1.0f);
                }

                // Compute raid attractiveness
                // Higher value + higher risk = more attractive to raiders
                float baseAttractiveness = loadState.TotalValue * 0.001f;
                float riskMultiplier = 1.0f + routeRisk;
                float raidAttractiveness = math.min(baseAttractiveness * riskMultiplier, 1.0f);

                // Compute escort priority
                // Higher value + higher risk = higher priority for escorts
                float escortPriority = math.min(loadState.TotalValue * 0.001f * (1.0f + routeRisk), 1.0f);

                valueStateRef.ValueRW.TotalValue = loadState.TotalValue;
                valueStateRef.ValueRW.RaidAttractiveness = raidAttractiveness;
                valueStateRef.ValueRW.EscortPriority = escortPriority;

                // Create or update SupplyRouteSummary for AI systems
                if (!state.EntityManager.HasComponent<SupplyRouteSummary>(haulerEntity))
                {
                    ecb.AddComponent(haulerEntity, new SupplyRouteSummary
                    {
                        RouteEntity = haulerEntity
                    });
                }

                var summary = new SupplyRouteSummary
                {
                    Value = loadState.TotalValue,
                    Risk = routeRisk,
                    Distance = 0f,
                    EscortStrength = 0f, // Would be computed by escort system
                    RouteEntity = haulerEntity
                };

                if (_routePlanLookup.TryGetComponent(haulerEntity, out var plan))
                {
                    summary.Distance = plan.EstimatedDistance;
                }

                ecb.SetComponent(haulerEntity, summary);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


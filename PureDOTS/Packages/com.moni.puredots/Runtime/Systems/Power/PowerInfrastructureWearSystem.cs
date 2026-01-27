using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    /// <summary>
    /// Updates infrastructure wear based on utilization, quality, and environment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup), OrderLast = true)]
    public partial struct PowerInfrastructureWearSystem : ISystem
    {
        private ComponentLookup<PowerSourceState> _sourceStateLookup;
        private ComponentLookup<InfrastructureCondition> _conditionLookup;
        private ComponentLookup<InfrastructureManufacturer> _manufacturerLookup;
        private ComponentLookup<PowerNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sourceStateLookup = state.GetComponentLookup<PowerSourceState>(true);
            _conditionLookup = state.GetComponentLookup<InfrastructureCondition>(false);
            _manufacturerLookup = state.GetComponentLookup<InfrastructureManufacturer>(true);
            _nodeLookup = state.GetComponentLookup<PowerNode>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            // Update wear every 60 ticks (low frequency)
            if (timeState.Tick % 60 != 0)
            {
                return;
            }

            _sourceStateLookup.Update(ref state);
            _conditionLookup.Update(ref state);
            _manufacturerLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            var job = new PowerInfrastructureWearJob
            {
                SourceStateLookup = _sourceStateLookup,
                ConditionLookup = _conditionLookup,
                ManufacturerLookup = _manufacturerLookup,
                NodeLookup = _nodeLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(InfrastructureCondition))]
    [WithNone(typeof(PlaybackGuardTag))]
    public partial struct PowerInfrastructureWearJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<PowerSourceState> SourceStateLookup;
        [ReadOnly] public ComponentLookup<InfrastructureManufacturer> ManufacturerLookup;
        [ReadOnly] public ComponentLookup<PowerNode> NodeLookup;

        public ComponentLookup<InfrastructureCondition> ConditionLookup;

        [BurstCompile]
        private void Execute(Entity entity, ref InfrastructureCondition condition, in PowerNode node)
        {
            // Get quality modifier
            var qualityModifier = 1f;
            if (ManufacturerLookup.HasComponent(entity))
            {
                var manufacturer = ManufacturerLookup[entity];
                // Higher quality tier = slower wear (0.5x for tier 1, 0.25x for tier 2, etc.)
                qualityModifier = 1f / (1f + manufacturer.QualityTier);
            }

            // Compute utilization
            var utilization = 0f;
            var maxThroughput = 1000f; // Default

            if (SourceStateLookup.HasComponent(entity))
            {
                var sourceState = SourceStateLookup[entity];
                utilization = sourceState.MaxOutput > 0 ? sourceState.CurrentOutput / sourceState.MaxOutput : 0f;
                maxThroughput = sourceState.MaxOutput;
            }
            else
            {
                // For edges/substations, use node quality as proxy
                utilization = node.Quality;
            }

            // Wear delta = baseRate * (0.5 + utilization) * QualityModifier * EnvironmentModifier
            var baseRate = 0.0001f; // Per tick wear rate
            var environmentModifier = 1f; // Could factor in storms, cosmic rays, etc.
            var wearDelta = baseRate * (0.5f + utilization) * qualityModifier * environmentModifier;

            condition.Wear = math.min(1f, condition.Wear + wearDelta);

            // Derive fault risk
            condition.FaultRisk = condition.Wear * (1f + utilization * 0.5f);

            // Update state based on thresholds
            if (condition.FaultRisk > 0.8f || condition.Wear > 0.9f)
            {
                condition.State = InfrastructureState.Faulty;
            }
            else if (condition.FaultRisk > 0.5f || condition.Wear > 0.6f)
            {
                condition.State = InfrastructureState.Degraded;
            }
            else
            {
                condition.State = InfrastructureState.Normal;
            }

            // Reduce outputs when degraded/faulty
            if (SourceStateLookup.HasComponent(entity))
            {
                var sourceState = SourceStateLookup[entity];
                if (condition.State == InfrastructureState.Degraded)
                {
                    sourceState.MaxOutput *= 0.8f;
                }
                else if (condition.State == InfrastructureState.Faulty)
                {
                    sourceState.MaxOutput *= 0.5f;
                    sourceState.Online = 0; // Faulty sources go offline
                }
            }
        }
    }
}


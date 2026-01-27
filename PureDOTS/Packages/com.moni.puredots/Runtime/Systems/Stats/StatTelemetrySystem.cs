using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Stats
{
    /// <summary>
    /// System that publishes stat influence telemetry metrics.
    /// Tracks how stats affect gameplay outcomes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StatXPAccumulationSystem))]
    public partial struct StatTelemetrySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system will publish telemetry metrics for stat influences
            // Example keys:
            // - space4x.stats.commandInfluence.formationRadius
            // - space4x.stats.tacticsInfluence.targetingAccuracy
            // - space4x.stats.logisticsInfluence.transferSpeed
            // - space4x.stats.engineeringInfluence.repairSpeed
            // - space4x.stats.resolveInfluence.engagementTime
            
            // Implementation will be added when telemetry publishing is integrated
            // with gameplay systems that use stats
        }
    }

    /// <summary>
    /// Stat modifier telemetry entry for tracking modifier provenance.
    /// </summary>
    public struct StatModifierTelemetry : IBufferElementData
    {
        public Entity EntityId;
        public byte StatType;
        public half BaseValue;
        public half ModifiedValue;
        public FixedString64Bytes ModifierSource;
    }
}


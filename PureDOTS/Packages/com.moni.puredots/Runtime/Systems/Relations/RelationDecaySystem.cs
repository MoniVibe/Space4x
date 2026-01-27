using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// System that decays relations over time if entities don't interact.
    /// Throttled: runs periodically (every ~1000 ticks by default).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct RelationDecaySystem : ISystem
    {
        private uint _lastDecayTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Throttle decay checks (every ~1000 ticks by default)
            const uint DECAY_INTERVAL = 1000;
            if (currentTick - _lastDecayTick < DECAY_INTERVAL)
                return;

            _lastDecayTick = currentTick;

            // Assume ~60 ticks per second, 86400 seconds per day
            const float TICKS_PER_DAY = 60f * 86400f;

            // Process all entities with relations
            foreach (var (relations, config, entity) in 
                SystemAPI.Query<DynamicBuffer<EntityRelation>, RefRO<RelationConfig>>()
                    .WithEntityAccess())
            {
                var relationsBuffer = relations;
                var configValue = config.ValueRO;
                float decayCheckInterval = configValue.DecayCheckInterval > 0 
                    ? configValue.DecayCheckInterval 
                    : 1000f; // Default interval

                for (int i = 0; i < relationsBuffer.Length; i++)
                {
                    var relation = relationsBuffer[i];

                    // Skip family relations - they don't decay
                    if (RelationCalculator.IsFamilyRelation(relation.Type))
                        continue;

                    // Check if enough time has passed since last interaction
                    uint ticksSinceInteraction = currentTick - relation.LastInteractionTick;
                    if (!RelationDecayService.ShouldDecay(relation.LastInteractionTick, currentTick, decayCheckInterval))
                        continue;

                    // Calculate decay
                    sbyte newIntensity = RelationDecayService.CalculateDecayAmount(
                        relation.Intensity,
                        configValue.DecayRatePerDay,
                        ticksSinceInteraction,
                        TICKS_PER_DAY,
                        configValue.MinIntensity);

                    if (newIntensity != relation.Intensity)
                    {
                        relation.Intensity = newIntensity;

                        // Update type based on new intensity
                        var oldType = relation.Type;
                        relation.Type = RelationCalculator.DetermineRelationType(
                            relation.Intensity, relation.InteractionCount, relation.Type);

                        relationsBuffer[i] = relation;
                    }
                }
            }
        }
    }
}


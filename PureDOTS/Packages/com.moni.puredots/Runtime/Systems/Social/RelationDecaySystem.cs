using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// System that fades unused relationships over time.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [BurstCompile]
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

            // Only run decay periodically (every ~1000 ticks by default)
            const uint DECAY_INTERVAL = 1000;
            if (currentTick - _lastDecayTick < DECAY_INTERVAL)
                return;
            
            _lastDecayTick = currentTick;

            // Assume ~60 ticks per second, 86400 seconds per day
            const float TICKS_PER_DAY = 60f * 86400f;

            // Process all entities with relations
            foreach (var query in 
                SystemAPI.Query<DynamicBuffer<EntityRelation>, RefRO<RelationConfig>>()
                    .WithEntityAccess())
            {
                var relations = query.Item1;
                var config = query.Item2;

                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    
                    // Skip family relations - they don't decay
                    if (RelationCalculator.IsFamilyRelation(relation.Type))
                        continue;

                    // Calculate decay
                    uint ticksSinceInteraction = currentTick - relation.LastInteractionTick;
                    
                    sbyte newIntensity = RelationCalculator.CalculateDecay(
                        relation.Intensity,
                        ticksSinceInteraction,
                        config.ValueRO.DecayRatePerDay,
                        TICKS_PER_DAY,
                        config.ValueRO.MinIntensity);

                    if (newIntensity != relation.Intensity)
                    {
                        relation.Intensity = newIntensity;
                        
                        // Update type based on new intensity
                        var oldType = relation.Type;
                        relation.Type = RelationCalculator.DetermineRelationType(
                            relation.Intensity, relation.InteractionCount, relation.Type);

                        relations[i] = relation;
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that updates social standing based on relations.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(RelationDecaySystem))]
    [BurstCompile]
    public partial struct SocialStandingUpdateSystem : ISystem
    {
        private uint _lastUpdateTick;

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

            // Only update periodically
            const uint UPDATE_INTERVAL = 600;
            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                return;
            
            _lastUpdateTick = currentTick;

            // Update social standing for all entities with relations
            foreach (var (standing, relations, entity) in 
                SystemAPI.Query<RefRW<SocialStanding>, DynamicBuffer<EntityRelation>>()
                    .WithEntityAccess())
            {
                ushort total = (ushort)relations.Length;
                ushort positive = 0;
                ushort negative = 0;
                int reputationSum = 0;

                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    
                    if (RelationCalculator.IsPositiveRelation(relation.Type) || relation.Intensity > 20)
                        positive++;
                    else if (RelationCalculator.IsNegativeRelation(relation.Type) || relation.Intensity < -20)
                        negative++;

                    reputationSum += relation.Intensity;
                }

                standing.ValueRW.TotalRelations = total;
                standing.ValueRW.PositiveRelations = positive;
                standing.ValueRW.NegativeRelations = negative;

                // Calculate reputation as average intensity
                if (total > 0)
                {
                    standing.ValueRW.Reputation = (sbyte)math.clamp(reputationSum / total, -100, 100);
                }

                // Calculate influence from positive relations
                standing.ValueRW.Influence = (byte)math.clamp(positive * 5, 0, 100);

                // Calculate notoriety from total relations
                standing.ValueRW.Notoriety = (byte)math.clamp(total * 2, 0, 100);
            }
        }
    }
}


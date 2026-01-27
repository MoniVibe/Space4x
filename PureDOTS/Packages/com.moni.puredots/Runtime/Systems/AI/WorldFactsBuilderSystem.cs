using PureDOTS.Runtime.AI.WorldFacts;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// System that builds world facts from perception, memory, and registry.
    /// Updates facts each tick based on entity state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InterruptSystemGroup))]
    public partial struct WorldFactsBuilderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Build facts for entities with EntityIntent (agents)
            foreach (var (intent, facts, entity) in SystemAPI.Query<RefRO<EntityIntent>, DynamicBuffer<WorldFact>>()
                .WithEntityAccess())
            {
                var factsBuffer = facts;

                // Update facts based on EntityIntent
                if (intent.ValueRO.IsValid != 0)
                {
                    // Has target fact
                    WorldFactsAPI.SetFact(
                        ref factsBuffer,
                        WorldFactKey.HasTarget,
                        intent.ValueRO.TargetEntity != Entity.Null ? 1f : 0f,
                        WorldFactProvenance.Perception,
                        currentTick
                    );

                    // State facts based on intent mode
                    WorldFactsAPI.SetFact(
                        ref factsBuffer,
                        WorldFactKey.IsFleeing,
                        intent.ValueRO.Mode == IntentMode.Flee ? 1f : 0f,
                        WorldFactProvenance.Perception,
                        currentTick
                    );

                    WorldFactsAPI.SetFact(
                        ref factsBuffer,
                        WorldFactKey.IsCombat,
                        intent.ValueRO.Mode == IntentMode.Attack ? 1f : 0f,
                        WorldFactProvenance.Perception,
                        currentTick
                    );

                    WorldFactsAPI.SetFact(
                        ref factsBuffer,
                        WorldFactKey.IsWorking,
                        intent.ValueRO.Mode == IntentMode.Gather || intent.ValueRO.Mode == IntentMode.Deliver ? 1f : 0f,
                        WorldFactProvenance.Perception,
                        currentTick
                    );
                }

                // Phase 2: Will add more fact sources:
                // - Perception system (sees resource, sees enemy)
                // - Memory system (knows storehouse location)
                // - Registry queries (aggregate membership, resource availability)
            }
        }
    }
}




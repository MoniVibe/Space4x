using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// Applies importance-based update frequencies and detail levels for relations/econ systems.
    /// High-importance orgs: More frequent updates, detailed simulation.
    /// Low-importance: Coarse updates, approximate data, static/templated values.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct RelationLODSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
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

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Update UpdateCadence based on AIImportance for entities with relations
            foreach (var (importance, cadence, entity) in
                SystemAPI.Query<RefRO<AIImportance>, RefRW<UpdateCadence>>()
                .WithEntityAccess())
            {
                uint newCadence = cadence.ValueRO.UpdateCadenceValue;

                // Adjust cadence based on importance
                switch (importance.ValueRO.Level)
                {
                    case 0: // Cinematic/Hero - most frequent
                        newCadence = 1; // Every tick
                        break;
                    case 1: // Important - frequent
                        newCadence = 5; // Every 5 ticks
                        break;
                    case 2: // Normal - moderate
                        newCadence = 20; // Every 20 ticks
                        break;
                    case 3: // Background Noise - infrequent
                        newCadence = 100; // Every 100 ticks
                        break;
                }

                // Only update if changed
                if (cadence.ValueRO.UpdateCadenceValue != newCadence)
                {
                    cadence.ValueRW.UpdateCadenceValue = newCadence;
                    // Keep existing PhaseOffset
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}


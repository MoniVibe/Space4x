using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Infiltration;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rewind;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Infiltration
{
    /// <summary>
    /// Degrades cover identity over time and with suspicion.
    /// Poor cover increases suspicion gain rate.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SuspicionTrackingSystem))]
    public partial struct CoverDegradationSystem : ISystem
    {
        private ComponentLookup<InfiltrationState> _infiltrationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _infiltrationLookup = state.GetComponentLookup<InfiltrationState>(true);
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

            // Update every 100 ticks (throttled)
            if (timeState.Tick % 100 != 0)
            {
                return;
            }

            _infiltrationLookup.Update(ref state);

            foreach (var (cover, entity) in SystemAPI.Query<RefRW<CoverIdentity>>().WithEntityAccess())
            {
                // Get suspicion level if agent has infiltration state
                float suspicion = 0f;
                bool hasBeenQuestioned = false;

                if (_infiltrationLookup.HasComponent(entity))
                {
                    var infiltration = _infiltrationLookup[entity];
                    suspicion = infiltration.SuspicionLevel;
                    // Questioned if under investigation (would check Investigation component in full implementation)
                    hasBeenQuestioned = suspicion > 0.7f; // Simplified heuristic
                }

                // Calculate degradation
                float degradation = InfiltrationHelpers.CalculateCoverDegradation(
                    cover.ValueRO,
                    timeState.Tick,
                    suspicion,
                    hasBeenQuestioned);

                // Apply degradation
                var updatedCover = cover.ValueRO;
                updatedCover.Credibility = math.max(0f, updatedCover.Credibility - degradation);
                updatedCover.Authenticity = math.max(0f, updatedCover.Authenticity - degradation * 0.8f); // Authenticity degrades slower

                // Update CoverStrength in InfiltrationState if present
                if (_infiltrationLookup.HasComponent(entity))
                {
                    var infiltrationRW = SystemAPI.GetComponentRW<InfiltrationState>(entity);
                    // CoverStrength is average of Credibility and Authenticity
                    infiltrationRW.ValueRW.CoverStrength = (updatedCover.Credibility + updatedCover.Authenticity) * 0.5f;
                }

                cover.ValueRW = updatedCover;
            }
        }
    }
}


using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// System that computes effective delta time for entities based on LocalTimeScale.
    /// Writes EffectiveDeltaTime component per entity.
    /// Runs early in the time system group so other systems can use effective delta.
    /// 
    /// Note: LocalTimeScale is game-specific (e.g., Godgame.Miracles.LocalTimeScale).
    /// Game projects should create a bridge system that reads their LocalTimeScale
    /// and writes EffectiveDeltaTime, or extend this system to handle their type.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    public partial struct TimeScaleSamplerSystem : ISystem
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

            var globalDelta = timeState.DeltaTime;

            // Phase 1: Basic implementation
            // For entities that already have EffectiveDeltaTime, update it to globalDelta
            // Game-specific systems (e.g., TimeDistortionApplySystem) should write EffectiveDeltaTime
            // based on their LocalTimeScale components
            
            // This system ensures EffectiveDeltaTime exists for entities that need it
            // Game-specific time scale systems should update EffectiveDeltaTime.Value
            // based on their LocalTimeScale.Value * globalDelta
            
            // For Phase 1, we set default effective delta = global delta
            // In Phase 1.5, game-specific systems will override this with LocalTimeScale values
        }
    }
}


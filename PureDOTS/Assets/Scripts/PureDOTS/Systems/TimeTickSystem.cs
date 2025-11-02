using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages tick advancement and fixed timestep simulation.
    /// Replaces TimeMonolith tick logic.
    /// </summary>
[BurstCompile]
[UpdateInGroup(typeof(TimeSystemGroup))]
[UpdateAfter(typeof(HistorySettingsConfigSystem))]
    public partial struct TimeTickSystem : ISystem
    {
        private float _accumulator;
        private float _lastRealTime;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _accumulator = 0f;
            _lastRealTime = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingletonRW<TimeState>();

            // Skip if paused
            if (timeState.ValueRO.IsPaused)
            {
                _lastRealTime = (float)SystemAPI.Time.ElapsedTime;
                return;
            }

            float currentRealTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaRealTime = currentRealTime - _lastRealTime;
            _lastRealTime = currentRealTime;

            // Apply speed multiplier
            float scaledDelta = deltaRealTime * timeState.ValueRO.CurrentSpeedMultiplier;

            // Accumulate time for fixed timestep
            _accumulator += scaledDelta;

            var fixedDt = timeState.ValueRO.FixedDeltaTime;
            int maxStepsPerFrame = 4; // Prevent spiral of death
            int steps = 0;

            // Advance ticks based on accumulated time
            while (_accumulator >= fixedDt && steps < maxStepsPerFrame)
            {
                _accumulator -= fixedDt;
                timeState.ValueRW.Tick++;
                steps++;
            }

            // Clamp accumulator if we're falling too far behind
            if (_accumulator > fixedDt * maxStepsPerFrame)
            {
                _accumulator = fixedDt;
            }
        }
    }
}

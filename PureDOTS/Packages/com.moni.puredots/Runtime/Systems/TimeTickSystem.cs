using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;

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
        private double _accumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationScalars>();
            state.RequireForUpdate<SimulationOverrides>();
            _accumulator = 0d;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tickStateHandle = SystemAPI.GetSingletonRW<TickTimeState>();
            var timeStateHandle = SystemAPI.GetSingletonRW<TimeState>();
            var hasInterpolation = SystemAPI.TryGetSingletonRW<FixedStepInterpolationState>(out var interpolationHandle);
            ref var tickState = ref tickStateHandle.ValueRW;
            ref var timeState = ref timeStateHandle.ValueRW;
            var rewind = SystemAPI.GetSingleton<RewindState>();
            var scalars = SystemAPI.GetSingleton<SimulationScalars>();
            var overrides = SystemAPI.GetSingleton<SimulationOverrides>();

            // Get effective time scale
            float effectiveTimeScale = overrides.OverrideTimeScale
                ? overrides.TimeScaleOverride
                : scalars.TimeScale;

            if (rewind.Mode != RewindMode.Record)
            {
                tickState.TargetTick = Unity.Mathematics.math.max(tickState.TargetTick, tickState.Tick);
                _accumulator = 0d;
                SyncLegacyTime(ref tickState, ref timeState);
                if (hasInterpolation)
                {
                    interpolationHandle.ValueRW.Alpha = 0f;
                }
                return;
            }

            var playing = tickState.IsPlaying && !tickState.IsPaused;

            // Skip if paused
            if (!playing)
            {
                if (tickState.Tick < tickState.TargetTick)
                {
                    tickState.Tick++;
                }

                tickState.TargetTick = Unity.Mathematics.math.max(tickState.TargetTick, tickState.Tick);
                SyncLegacyTime(ref tickState, ref timeState);
                if (hasInterpolation)
                {
                    interpolationHandle.ValueRW.Alpha = 0f;
                }
                return;
            }

            var deltaRealTime = (double)SystemAPI.Time.DeltaTime;
            if (deltaRealTime < 0d)
            {
                deltaRealTime = 0d;
            }

            var fixedDt = (double)Unity.Mathematics.math.max(tickState.FixedDeltaTime, 1e-4f);
            var headlessTargetTps = SystemAPI.TryGetSingleton<HeadlessTpsCap>(out var cap) ? cap.TargetTps : 0f;
            var maxFrameTimeClamp = 0.25d;
            var maxStepsPerFrame = 0;
            var scaledDelta = 0d;

            if (headlessTargetTps > 0f)
            {
                var capMaxDelta = 1f / headlessTargetTps;
                maxFrameTimeClamp = Unity.Mathematics.math.min(maxFrameTimeClamp, capMaxDelta);
                if (deltaRealTime > maxFrameTimeClamp)
                {
                    deltaRealTime = maxFrameTimeClamp;
                }

                var targetScale = headlessTargetTps * (float)fixedDt;
                scaledDelta = deltaRealTime * targetScale;
                maxStepsPerFrame = 1;
            }
            else
            {
                // Clamp to avoid pathological catch-up after long stalls.
                if (deltaRealTime > maxFrameTimeClamp)
                {
                    deltaRealTime = maxFrameTimeClamp;
                }

                // Speed affects tick rate (accumulator), not per-tick dt.
                float baseSpeedMultiplier = Unity.Mathematics.math.max(0.01f, tickState.CurrentSpeedMultiplier);
                float timeScaleMultiplier = Unity.Mathematics.math.max(0.01f, effectiveTimeScale);
                var effectiveSpeed = baseSpeedMultiplier * timeScaleMultiplier;
                scaledDelta = deltaRealTime * (double)effectiveSpeed;
                maxStepsPerFrame = ResolveMaxSteps(effectiveSpeed);
            }

            // Accumulate time for fixed timestep
            _accumulator += scaledDelta;

            var steps = 0;

            // Advance ticks based on accumulated time
            while (_accumulator >= fixedDt && steps < maxStepsPerFrame)
            {
                _accumulator -= fixedDt;
                tickState.Tick++;
                steps++;
            }

            if (tickState.TargetTick < tickState.Tick)
            {
                tickState.TargetTick = tickState.Tick;
            }

            // Clamp accumulator if we're falling too far behind
            if (_accumulator > fixedDt * maxStepsPerFrame)
            {
                _accumulator = fixedDt;
            }

            // Update world seconds
            tickState.WorldSeconds = tickState.Tick * tickState.FixedDeltaTime;

            SyncLegacyTime(ref tickState, ref timeState);

            if (hasInterpolation)
            {
                var alpha = (float)Unity.Mathematics.math.saturate(_accumulator / fixedDt);
                interpolationHandle.ValueRW.Alpha = alpha;
            }
        }

        private static void SyncLegacyTime(ref TickTimeState tickState, ref TimeState legacy)
        {
            legacy.Tick = tickState.Tick;
            legacy.FixedDeltaTime = tickState.FixedDeltaTime;
            legacy.DeltaTime = tickState.FixedDeltaTime;
            legacy.DeltaSeconds = tickState.FixedDeltaTime;
            legacy.CurrentSpeedMultiplier = tickState.CurrentSpeedMultiplier;
            legacy.IsPaused = tickState.IsPaused;
            legacy.ElapsedTime = tickState.WorldSeconds;
            legacy.WorldSeconds = tickState.WorldSeconds;
        }

        private static int ResolveMaxSteps(float effectiveSpeed)
        {
            if (effectiveSpeed <= 2f)
            {
                return 8;
            }

            if (effectiveSpeed <= 4f)
            {
                return 16;
            }

            return 32;
        }
    }
}

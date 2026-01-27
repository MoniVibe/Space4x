using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Tech;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Tech
{
    /// <summary>
    /// Propagates tech levels from diffusion sources to nearby targets using distance and source-tier weighting.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TechDiffusionSystem : ISystem
    {
        private struct TechSourceInfo
        {
            public Entity Entity;
            public float Level;
            public float3 Position;
            public float SpreadMultiplier;
            public float MaxRange;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TechLevel>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || SystemAPI.GetSingleton<RewindState>().Mode != RewindMode.Record)
            {
                return;
            }

            var settings = SystemAPI.TryGetSingleton<TechDiffusionSettings>(out var cfg)
                ? cfg
                : TechDiffusionSettings.CreateDefault();
            var tickScale = math.max(0f, timeState.CurrentSpeedMultiplier);

            using var sources = new NativeList<TechSourceInfo>(state.WorldUpdateAllocator);
            foreach (var (level, source, entity) in SystemAPI.Query<RefRO<TechLevel>, RefRO<TechDiffusionSource>>().WithEntityAccess())
            {
                var pos = float3.zero;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    pos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                }

                sources.Add(new TechSourceInfo
                {
                    Entity = entity,
                    Level = math.max(0f, level.ValueRO.Value),
                    Position = pos,
                    SpreadMultiplier = math.max(0.01f, source.ValueRO.SpreadMultiplier),
                    MaxRange = source.ValueRO.MaxRange
                });
            }

            if (sources.Length == 0)
            {
                return;
            }

            foreach (var (level, diffusionState, entity) in SystemAPI.Query<RefRW<TechLevel>, RefRW<TechDiffusionState>>().WithNone<TechDiffusionSource>().WithEntityAccess())
            {
                var targetPos = float3.zero;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    targetPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                }

                if (!TrySelectSource(targetPos, sources, settings.DistanceFalloff, out var selected, out var distance))
                {
                    diffusionState.ValueRW.LastUpdateTick = timeState.Tick;
                    diffusionState.ValueRW.AppliedRate = 0f;
                    diffusionState.ValueRW.LastSource = Entity.Null;
                    diffusionState.ValueRW.IncomingLevel = level.ValueRO.Value;
                    diffusionState.ValueRW.Distance = 0f;
                    diffusionState.ValueRW.Progress = diffusionState.ValueRO.Progress;
                    continue;
                }

                var currentLevel = math.max(0f, level.ValueRO.Value);
                var levelGap = math.max(0f, selected.Level - currentLevel);
                if (levelGap <= 0f)
                {
                    diffusionState.ValueRW.LastSource = selected.Entity;
                    diffusionState.ValueRW.IncomingLevel = selected.Level;
                    diffusionState.ValueRW.Distance = distance;
                    diffusionState.ValueRW.AppliedRate = 0f;
                    diffusionState.ValueRW.Progress = 1f;
                    diffusionState.ValueRW.LastUpdateTick = timeState.Tick;
                    level.ValueRW.LastUpdateTick = timeState.Tick;
                    continue;
                }

                var distanceFactor = 1f / (1f + distance * math.max(0f, settings.DistanceFalloff));
                var rate = settings.BaseRatePerTick * tickScale * distanceFactor;
                rate *= (1f + settings.SourceLevelFactor * selected.Level) * selected.SpreadMultiplier;
                rate = math.max(settings.MinProgressPerTick, rate);

                var applied = math.min(levelGap, rate);
                level.ValueRW.Value = currentLevel + applied;
                level.ValueRW.LastUpdateTick = timeState.Tick;

                diffusionState.ValueRW.LastSource = selected.Entity;
                diffusionState.ValueRW.IncomingLevel = selected.Level;
                diffusionState.ValueRW.Distance = distance;
                diffusionState.ValueRW.AppliedRate = applied;
                diffusionState.ValueRW.Progress = math.saturate(level.ValueRW.Value / math.max(0.0001f, selected.Level));
                diffusionState.ValueRW.LastUpdateTick = timeState.Tick;
            }
        }

        private static bool TrySelectSource(
            float3 targetPos,
            NativeList<TechSourceInfo> sources,
            float distanceFalloff,
            out TechSourceInfo selected,
            out float distance)
        {
            selected = default;
            distance = 0f;

            if (sources.Length == 0)
            {
                return false;
            }

            var bestScore = float.MinValue;
            var bestDistance = 0f;
            var best = sources[0];

            for (int i = 0; i < sources.Length; i++)
            {
                var src = sources[i];
                var dist = math.distance(targetPos, src.Position);

                if (src.MaxRange > 0f && dist > src.MaxRange)
                {
                    continue;
                }

                var distanceFactor = 1f / (1f + dist * math.max(0f, distanceFalloff));
                var score = src.Level * distanceFactor * src.SpreadMultiplier;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDistance = dist;
                    best = src;
                }
            }

            if (bestScore <= float.MinValue)
            {
                return false;
            }

            selected = best;
            distance = bestDistance;
            return true;
        }
    }
}

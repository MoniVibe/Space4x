using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Applies deterministic evaporation to the moisture grid using climate profile data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(ClimateStateUpdateSystem))]
    public partial struct MoistureEvaporationSystem : ISystem
    {
        const uint kStrideTicks = 10u;
        private TimeAwareController _timeAware;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ClimateState>();
            state.RequireForUpdate<EnvironmentGridConfigData>();
            state.RequireForUpdate<MoistureGrid>();
            state.RequireForUpdate<MoistureGridSimulationState>();
            _timeAware = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (!_timeAware.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            var climate = SystemAPI.GetSingleton<ClimateState>();
            var gridConfig = SystemAPI.GetSingleton<EnvironmentGridConfigData>();

            ClimateProfileData profile;
            if (!SystemAPI.TryGetSingleton(out profile))
            {
                profile = ClimateProfileDefaults.Create(in gridConfig);
            }

            var gridState = SystemAPI.GetSingletonRW<MoistureGridSimulationState>();
            var currentTick = timeState.Tick;
            if (!EnvironmentEffectUtility.ShouldUpdate(currentTick, gridState.ValueRO.LastEvaporationTick, kStrideTicks))
            {
                return;
            }

            var tickDelta = gridState.ValueRO.LastEvaporationTick == uint.MaxValue
                ? 1u
                : EnvironmentEffectUtility.TickDelta(currentTick, gridState.ValueRO.LastEvaporationTick);

            var deltaSeconds = math.max(0f, timeState.FixedDeltaTime * tickDelta);
            if (deltaSeconds <= 0f)
            {
                gridState.ValueRW.LastEvaporationTick = currentTick;
                return;
            }

            var grid = SystemAPI.GetSingletonRW<MoistureGrid>();
            var gridEntity = SystemAPI.GetSingletonEntity<MoistureGrid>();
            
            // Check terrain version - if terrain changed, mark grid as needing update
            uint currentTerrainVersion = 0;
            if (SystemAPI.TryGetSingleton<PureDOTS.Environment.TerrainVersion>(out var terrainVersion))
            {
                currentTerrainVersion = terrainVersion.Value;
                if (currentTerrainVersion != grid.ValueRO.LastTerrainVersion)
                {
                    // Terrain changed, force update
                    grid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                    grid.ValueRW.LastUpdateTick = uint.MaxValue; // Force rebuild
                }
            }
            
            if (!SystemAPI.HasBuffer<MoistureGridRuntimeCell>(gridEntity))
            {
                gridState.ValueRW.LastEvaporationTick = currentTick;
                return;
            }

            var runtimeBuffer = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(gridEntity);
            if (runtimeBuffer.Length == 0)
            {
                gridState.ValueRW.LastEvaporationTick = currentTick;
                return;
            }

            NativeArray<SunlightSample> sunlightSamples = default;
            if (SystemAPI.TryGetSingletonEntity<SunlightGrid>(out var sunlightEntity) && SystemAPI.HasBuffer<SunlightGridRuntimeSample>(sunlightEntity))
            {
                var sunlightBuffer = SystemAPI.GetBuffer<SunlightGridRuntimeSample>(sunlightEntity);
                if (sunlightBuffer.Length == runtimeBuffer.Length)
                {
                    sunlightSamples = sunlightBuffer.Reinterpret<SunlightSample>().AsNativeArray();
                }
            }

            var job = new MoistureEvaporationJob
            {
                Cells = runtimeBuffer.AsNativeArray(),
                BaseRate = math.max(0f, profile.EvaporationBaseRate),
                GlobalTemperature = climate.GlobalTemperature,
                GlobalWindStrength = climate.GlobalWindStrength,
                AtmosphericMoisture = climate.AtmosphericMoisture,
                DeltaSeconds = deltaSeconds,
                Sunlight = sunlightSamples
            };

            state.Dependency = job.ScheduleParallel(runtimeBuffer.Length, 64, state.Dependency);
            gridState.ValueRW.LastEvaporationTick = currentTick;
        }

        [BurstCompile]
        private struct MoistureEvaporationJob : IJobFor
        {
            public NativeArray<MoistureGridRuntimeCell> Cells;
            public float BaseRate;
            public float GlobalTemperature;
            public float GlobalWindStrength;
            public float AtmosphericMoisture;
            public float DeltaSeconds;
            [ReadOnly] public NativeArray<SunlightSample> Sunlight;

            public void Execute(int index)
            {
                var cell = Cells[index];

                var tempFactor = math.exp((GlobalTemperature - 20f) * 0.05f);
                var windFactor = 1f + math.max(0f, GlobalWindStrength) * 0.1f;
                var humidityFactor = 1f - math.clamp(AtmosphericMoisture / 200f, 0f, 1f);
                var shadeFactor = 1f;
                if (Sunlight.IsCreated && index < Sunlight.Length)
                {
                    var sunlight = Sunlight[index];
                    var intensityFactor = math.saturate(sunlight.DirectLight / 100f);
                    var occlusionFactor = 1f - math.saturate(sunlight.OccluderCount * 0.08f);
                    shadeFactor = math.clamp(intensityFactor * occlusionFactor, 0.2f, 1f);
                }

                var evaporationPerSecond = BaseRate * tempFactor * windFactor * humidityFactor * shadeFactor;
                evaporationPerSecond = math.max(0f, evaporationPerSecond);

                var evaporation = evaporationPerSecond * DeltaSeconds;
                cell.Moisture = math.clamp(cell.Moisture - evaporation, 0f, 100f);
                cell.EvaporationRate = evaporationPerSecond;

                Cells[index] = cell;
            }
        }
    }

    /// <summary>
    /// Diffuses moisture between neighbouring cells and applies seepage to simulate drainage.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(MoistureEvaporationSystem))]
    public partial struct MoistureSeepageSystem : ISystem
    {
        const uint kStrideTicks = 10u;
        private TimeAwareController _timeAware;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MoistureGrid>();
            state.RequireForUpdate<MoistureGridSimulationState>();
            _timeAware = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (!_timeAware.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            timeState = context.Time;
            var gridState = SystemAPI.GetSingletonRW<MoistureGridSimulationState>();
            var currentTick = timeState.Tick;
            if (!EnvironmentEffectUtility.ShouldUpdate(currentTick, gridState.ValueRO.LastSeepageTick, kStrideTicks))
            {
                return;
            }

            var tickDelta = gridState.ValueRO.LastSeepageTick == uint.MaxValue
                ? 1u
                : EnvironmentEffectUtility.TickDelta(currentTick, gridState.ValueRO.LastSeepageTick);

            var deltaSeconds = math.max(0f, timeState.FixedDeltaTime * tickDelta);
            if (deltaSeconds <= 0f)
            {
                gridState.ValueRW.LastSeepageTick = currentTick;
                return;
            }

            var grid = SystemAPI.GetSingletonRW<MoistureGrid>();
            var gridEntity = SystemAPI.GetSingletonEntity<MoistureGrid>();
            
            // Check terrain version - if terrain changed, mark grid as needing update
            uint currentTerrainVersion = 0;
            if (SystemAPI.TryGetSingleton<PureDOTS.Environment.TerrainVersion>(out var terrainVersion))
            {
                currentTerrainVersion = terrainVersion.Value;
                if (currentTerrainVersion != grid.ValueRO.LastTerrainVersion)
                {
                    // Terrain changed, force update
                    grid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                    grid.ValueRW.LastUpdateTick = uint.MaxValue; // Force rebuild
                }
            }
            
            if (!SystemAPI.HasBuffer<MoistureGridRuntimeCell>(gridEntity))
            {
                gridState.ValueRW.LastSeepageTick = currentTick;
                return;
            }

            var runtimeBuffer = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(gridEntity);
            if (runtimeBuffer.Length == 0)
            {
                gridState.ValueRW.LastSeepageTick = currentTick;
                return;
            }

            var metadata = grid.ValueRO.Metadata;
            var diffusion = math.max(0f, grid.ValueRO.DiffusionCoefficient);
            var seepage = math.max(0f, grid.ValueRO.SeepageCoefficient);

            var nextValues = CollectionHelper.CreateNativeArray<float>(runtimeBuffer.Length, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            var computeJob = new MoistureSeepageComputeJob
            {
                Cells = runtimeBuffer.AsNativeArray(),
                NextValues = nextValues,
                Metadata = metadata,
                Diffusion = diffusion,
                Seepage = seepage,
                DeltaSeconds = deltaSeconds
            };

            state.Dependency = computeJob.ScheduleParallel(runtimeBuffer.Length, 64, state.Dependency);

            var applyJob = new MoistureSeepageApplyJob
            {
                Cells = runtimeBuffer.AsNativeArray(),
                NextValues = nextValues
            };

            state.Dependency = applyJob.ScheduleParallel(runtimeBuffer.Length, 64, state.Dependency);

            grid.ValueRW.LastUpdateTick = currentTick;
            gridState.ValueRW.LastSeepageTick = currentTick;
        }

        [BurstCompile]
        private struct MoistureSeepageComputeJob : IJobFor
        {
            [ReadOnly] public NativeArray<MoistureGridRuntimeCell> Cells;
            public NativeArray<float> NextValues;
            public EnvironmentGridMetadata Metadata;
            public float Diffusion;
            public float Seepage;
            public float DeltaSeconds;

            public void Execute(int index)
            {
                var current = math.clamp(Cells[index].Moisture, 0f, 100f);
                var neighbourDelta = 0f;

                neighbourDelta += SampleNeighbourDelta(index, new int2(1, 0), current);
                neighbourDelta += SampleNeighbourDelta(index, new int2(-1, 0), current);
                neighbourDelta += SampleNeighbourDelta(index, new int2(0, 1), current);
                neighbourDelta += SampleNeighbourDelta(index, new int2(0, -1), current);

                var diffusionTerm = Diffusion > 0f ? Diffusion * neighbourDelta : 0f;
                var seepageTerm = Seepage > 0f ? -Seepage * current : 0f;

                var delta = (diffusionTerm + seepageTerm) * DeltaSeconds;
                var nextValue = math.clamp(current + delta, 0f, 100f);
                NextValues[index] = nextValue;
            }

            float SampleNeighbourDelta(int index, int2 offset, float current)
            {
                if (!EnvironmentGridMath.TryGetNeighborIndex(Metadata, index, offset, out var neighbourIndex))
                {
                    return 0f;
                }

                var neighbour = math.clamp(Cells[neighbourIndex].Moisture, 0f, 100f);
                return neighbour - current;
            }
        }

        [BurstCompile]
        private struct MoistureSeepageApplyJob : IJobFor
        {
            public NativeArray<MoistureGridRuntimeCell> Cells;
            [ReadOnly] public NativeArray<float> NextValues;

            public void Execute(int index)
            {
                var cell = Cells[index];
                cell.Moisture = NextValues[index];
                Cells[index] = cell;
            }
        }
    }
}

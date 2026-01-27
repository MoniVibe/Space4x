using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Applies climate control sources to gradually pull local climate fields toward target values.
    /// Runs after moisture/temperature updates to allow controllers to override natural processes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(MoistureSeepageSystem))]
    public partial struct ClimateControlSystem : ISystem
    {
        const uint kStrideTicks = 5u;
        private TimeAwareController _timeAware;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MoistureGrid>();
            state.RequireForUpdate<TemperatureGrid>();

            _timeAware = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            if (!_timeAware.TryBegin(timeState, rewindState, out _))
            {
                return;
            }

            var currentTick = timeState.Tick;
            if (currentTick % kStrideTicks != 0)
            {
                return;
            }

            // Check if climate grid exists
            var moistureGrid = SystemAPI.GetSingleton<MoistureGrid>();
            var temperatureGrid = SystemAPI.GetSingleton<TemperatureGrid>();
            var moistureEntity = SystemAPI.GetSingletonEntity<MoistureGrid>();

            if (!SystemAPI.HasBuffer<ClimateGridRuntimeCell>(moistureEntity))
            {
                return;
            }

            var climateBuffer = SystemAPI.GetBuffer<ClimateGridRuntimeCell>(moistureEntity);
            if (climateBuffer.Length == 0)
            {
                return;
            }

            // Collect all climate control sources
            var sources = new NativeList<ClimateControlSource>(state.WorldUpdateAllocator);
            var sourcePositions = new NativeList<float3>(state.WorldUpdateAllocator);

            foreach (var (source, transform, _) in SystemAPI.Query<RefRO<ClimateControlSource>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                sources.Add(source.ValueRO);
                sourcePositions.Add(transform.ValueRO.Position);
            }

            if (sources.Length == 0)
            {
                return;
            }

            var deltaSeconds = timeState.FixedDeltaTime * kStrideTicks;
            var metadata = moistureGrid.Metadata;

            var job = new ClimateControlJob
            {
                ClimateCells = climateBuffer.AsNativeArray(),
                Sources = sources.AsArray(),
                SourcePositions = sourcePositions.AsArray(),
                Metadata = metadata,
                DeltaSeconds = deltaSeconds
            };

            state.Dependency = job.ScheduleParallel(climateBuffer.Length, 64, state.Dependency);
        }

        [BurstCompile]
        private struct ClimateControlJob : IJobFor
        {
            public NativeArray<ClimateGridRuntimeCell> ClimateCells;
            [ReadOnly] public NativeArray<ClimateControlSource> Sources;
            [ReadOnly] public NativeArray<float3> SourcePositions;
            public EnvironmentGridMetadata Metadata;
            public float DeltaSeconds;

            public void Execute(int index)
            {
                var cellCenter = EnvironmentGridMath.GetCellCenter(Metadata, index);
                var cell = ClimateCells[index];
                var currentClimate = cell.Climate;

                // Accumulate influence from all sources
                var totalInfluence = 0f;
                var weightedTarget = new ClimateVector();

                for (int i = 0; i < Sources.Length; i++)
                {
                    var source = Sources[i];
                    var sourcePos = SourcePositions[i];
                    var distance = math.distance(cellCenter, sourcePos);

                    if (distance > source.Radius)
                    {
                        continue;
                    }

                    // Falloff: linear from center to radius
                    var falloff = 1f - math.saturate(distance / source.Radius);
                    var influence = source.Strength * falloff * DeltaSeconds;
                    influence = math.clamp(influence, 0f, 1f);

                    if (influence > 0f)
                    {
                        totalInfluence += influence;
                        var contribution = ClimateVector.Lerp(currentClimate, source.TargetClimate, influence);
                        
                        // Weighted blend
                        weightedTarget.Temperature += contribution.Temperature * influence;
                        weightedTarget.Moisture += contribution.Moisture * influence;
                        weightedTarget.Fertility += contribution.Fertility * influence;
                        weightedTarget.WaterLevel += contribution.WaterLevel * influence;
                        weightedTarget.Ruggedness += contribution.Ruggedness * influence;
                    }
                }

                if (totalInfluence > 0f)
                {
                    // Normalize weighted target
                    var invTotal = 1f / totalInfluence;
                    weightedTarget.Temperature *= invTotal;
                    weightedTarget.Moisture *= invTotal;
                    weightedTarget.Fertility *= invTotal;
                    weightedTarget.WaterLevel *= invTotal;
                    weightedTarget.Ruggedness *= invTotal;

                    // Lerp toward weighted target
                    var blendFactor = math.saturate(totalInfluence);
                    cell.Climate = ClimateVector.Lerp(currentClimate, weightedTarget, blendFactor);
                }

                // Clamp values
                cell.Climate.Temperature = math.clamp(cell.Climate.Temperature, -1f, 1f);
                cell.Climate.Moisture = math.clamp(cell.Climate.Moisture, 0f, 1f);
                cell.Climate.Fertility = math.clamp(cell.Climate.Fertility, 0f, 1f);
                cell.Climate.WaterLevel = math.clamp(cell.Climate.WaterLevel, 0f, 1f);
                cell.Climate.Ruggedness = math.clamp(cell.Climate.Ruggedness, 0f, 1f);

                ClimateCells[index] = cell;
            }
        }
    }
}


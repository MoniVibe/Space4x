using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Creatures
{
    /// <summary>
    /// Evaluates environment comfort for species/cultures based on climate vectors.
    /// Applies bonuses/penalties based on comfort thresholds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    // Removed invalid UpdateAfter: ClimateControlSystem runs in EnvironmentSystemGroup.
    public partial struct SpeciesComfortSystem : ISystem
    {
        const uint kStrideTicks = 10u;
        private TimeAwareController _timeAware;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MoistureGrid>();

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

            // Get climate grid
            var moistureGrid = SystemAPI.GetSingleton<MoistureGrid>();
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

            var metadata = moistureGrid.Metadata;

            var job = new SpeciesComfortJob
            {
                ClimateCells = climateBuffer.AsNativeArray(),
                Metadata = metadata,
                CurrentTick = currentTick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct SpeciesComfortJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ClimateGridRuntimeCell> ClimateCells;
            [ReadOnly] public EnvironmentGridMetadata Metadata;
            public uint CurrentTick;

            public void Execute(
                ref EnvironmentComfort comfort,
                in SpeciesEnvironmentProfile profile,
                in LocalTransform transform)
            {
                // Sample climate at entity position
                if (!EnvironmentGridMath.TryWorldToCell(Metadata, transform.Position, out var cell, out _))
                {
                    comfort.ComfortScore = 0f;
                    comfort.LastUpdateTick = CurrentTick;
                    return;
                }

                var cellIndex = EnvironmentGridMath.GetCellIndex(Metadata, cell);
                if (cellIndex < 0 || cellIndex >= ClimateCells.Length)
                {
                    comfort.ComfortScore = 0f;
                    comfort.LastUpdateTick = CurrentTick;
                    return;
                }

                var climate = ClimateCells[cellIndex].Climate;
                var comfortScore = SpeciesComfortMath.Comfort(climate, profile.IdealBiome);

                comfort.ComfortScore = comfortScore;
                comfort.LastUpdateTick = CurrentTick;
            }
        }
    }
}


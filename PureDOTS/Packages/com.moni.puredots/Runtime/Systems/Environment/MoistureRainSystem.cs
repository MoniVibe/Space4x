using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
#if GODGAME
using Godgame.Runtime;
#endif
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Adds moisture to the moisture grid from rain clouds/miracles.
    /// Runs in EnvironmentSystemGroup to integrate with moisture grid cadence.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(MoistureSeepageSystem))]
    public partial struct MoistureRainSystem : ISystem
    {
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (!_timeAware.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<MoistureGrid>(out var moistureEntity))
            {
                return;
            }

            if (!SystemAPI.HasBuffer<MoistureGridRuntimeCell>(moistureEntity))
            {
                return;
            }

            var moistureGrid = SystemAPI.GetSingleton<MoistureGrid>();
            var runtimeBuffer = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(moistureEntity);
            if (runtimeBuffer.Length == 0)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var deltaSeconds = math.max(0f, timeState.FixedDeltaTime);

            // Collect rain cloud data
            var rainClouds = new NativeList<RainCloudData>(Allocator.Temp);
            foreach (var (cloudState, cloudConfig, transform, entity) in
                     SystemAPI.Query<RefRO<RainCloudState>, RefRO<RainCloudConfig>, RefRO<LocalTransform>>()
                         .WithEntityAccess()
                         .WithAll<RainCloudTag>())
            {
#if GODGAME
                // Skip held entities (game-specific: Divine Hand)
                if (SystemAPI.HasComponent<HandHeldTag>(entity))
                {
                    continue;
                }
#endif
                var position = transform.ValueRO.Position;
                var radius = math.max(cloudConfig.ValueRO.MinRadius,
                    cloudConfig.ValueRO.BaseRadius + position.y * cloudConfig.ValueRO.RadiusPerHeight);

                if (radius > 0.01f && cloudConfig.ValueRO.MoisturePerSecond > 0f)
                {
                    rainClouds.Add(new RainCloudData
                    {
                        Position = position,
                        Radius = radius,
                        MoisturePerSecond = cloudConfig.ValueRO.MoisturePerSecond,
                        Falloff = math.max(0.01f, cloudConfig.ValueRO.MoistureFalloff)
                    });
                }
            }

            if (rainClouds.Length == 0)
            {
                rainClouds.Dispose();
                return;
            }

            var job = new ApplyRainToMoistureGridJob
            {
                MoistureGrid = moistureGrid,
                RuntimeCells = runtimeBuffer.AsNativeArray(),
                RainClouds = rainClouds.AsArray(),
                DeltaSeconds = deltaSeconds,
                CurrentTick = currentTick
            };

            state.Dependency = job.ScheduleParallel(runtimeBuffer.Length, 64, state.Dependency);
            state.Dependency = rainClouds.Dispose(state.Dependency);

            // Update grid metadata
            var gridRW = SystemAPI.GetSingletonRW<MoistureGrid>();
            gridRW.ValueRW.LastUpdateTick = currentTick;
        }

        private struct RainCloudData
        {
            public float3 Position;
            public float Radius;
            public float MoisturePerSecond;
            public float Falloff;
        }

        [BurstCompile]
        private struct ApplyRainToMoistureGridJob : IJobFor
        {
            [ReadOnly] public MoistureGrid MoistureGrid;
            public NativeArray<MoistureGridRuntimeCell> RuntimeCells;
            [ReadOnly] public NativeArray<RainCloudData> RainClouds;
            public float DeltaSeconds;
            public uint CurrentTick;

            public void Execute(int index)
            {
                var cell = RuntimeCells[index];
                var cellCenter = EnvironmentGridMath.GetCellCenter(MoistureGrid.Metadata, index);
                var cellWorldPos = new float3(cellCenter.x, 0f, cellCenter.z);

                float totalMoistureAdded = 0f;

                for (int i = 0; i < RainClouds.Length; i++)
                {
                    var cloud = RainClouds[i];
                    var horizontal = new float2(
                        cellWorldPos.x - cloud.Position.x,
                        cellWorldPos.z - cloud.Position.z);
                    var distance = math.length(horizontal);

                    if (distance > cloud.Radius)
                    {
                        continue;
                    }

                    var t = distance / math.max(0.001f, cloud.Radius);
                    var weight = math.pow(1f - t, cloud.Falloff);
                    var moistureAdded = cloud.MoisturePerSecond * DeltaSeconds * weight;
                    totalMoistureAdded += moistureAdded;
                }

                if (totalMoistureAdded > 0f)
                {
                    cell.Moisture = math.clamp(cell.Moisture + totalMoistureAdded, 0f, 100f);
                    cell.LastRainTick = CurrentTick;
                    RuntimeCells[index] = cell;
                }
            }
        }
    }
}



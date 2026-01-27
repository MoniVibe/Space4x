using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.WorldGen;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Updates the sunlight grid based on climate time-of-day, seasonal variation, and simple vegetation occlusion.
    /// Produces deterministic direct/ambient light values that other environment systems can consume.
    /// </summary>
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(ClimateStateUpdateSystem))]
    public partial struct SunlightGridUpdateSystem : ISystem
    {
        const uint kUpdateStrideTicks = 5u;
        private TimeAwareController _timeAware;
        private EntityQuery _vegetationQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SunlightGrid>();
            state.RequireForUpdate<ClimateState>();

            _vegetationQuery = SystemAPI.QueryBuilder()
                .WithAll<VegetationLifecycle, LocalTransform>()
                .WithNone<VegetationDeadTag>()
                .Build();

            _timeAware = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            if (!_timeAware.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            var currentTick = context.Time.Tick;
            var sunlightEntity = SystemAPI.GetSingletonEntity<SunlightGrid>();
            var sunlightGrid = SystemAPI.GetComponent<SunlightGrid>(sunlightEntity);

            if (sunlightGrid.LastUpdateTick != uint.MaxValue)
            {
                var ticksSince = EnvironmentEffectUtility.TickDelta(currentTick, sunlightGrid.LastUpdateTick);
                if (ticksSince < kUpdateStrideTicks)
                {
                    return;
                }
            }

            if (!SystemAPI.HasBuffer<SunlightGridRuntimeSample>(sunlightEntity))
            {
                return;
            }

            var runtimeBuffer = SystemAPI.GetBuffer<SunlightGridRuntimeSample>(sunlightEntity);
            if (runtimeBuffer.Length == 0)
            {
                sunlightGrid.LastUpdateTick = currentTick;
                SystemAPI.SetComponent(sunlightEntity, sunlightGrid);
                return;
            }

            var climate = SystemAPI.GetSingleton<ClimateState>();
            CalculateSunlight(climate, out var sunDirection, out var directLight, out var ambientLight, out var sunScalar);

            var globalSunlight = 1f;
            if (SystemAPI.TryGetSingleton<SunlightState>(out var sunlightState))
            {
                globalSunlight = math.saturate(sunlightState.GlobalIntensity);
            }

            directLight *= globalSunlight;
            ambientLight *= globalSunlight;
            sunScalar = math.saturate(sunScalar * globalSunlight);

            var samples = runtimeBuffer.Reinterpret<SunlightSample>().AsNativeArray();

            var baselineJob = new SunlightBaselineJob
            {
                Samples = samples,
                DirectLight = directLight,
                AmbientLight = ambientLight
            };
            state.Dependency = baselineJob.ScheduleParallel(samples.Length, 128, state.Dependency);

            var occluderCounts = new NativeArray<int>(samples.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (!_vegetationQuery.IsEmptyIgnoreFilter)
            {
                foreach (var (lifecycle, transform) in SystemAPI.Query<RefRO<VegetationLifecycle>, RefRO<LocalTransform>>().WithNone<VegetationDeadTag>())
                {
                    var stage = lifecycle.ValueRO.CurrentStage;
                    if (stage < VegetationLifecycle.LifecycleStage.Mature)
                    {
                        continue;
                    }

                    if (!EnvironmentGridMath.TryWorldToCell(sunlightGrid.Metadata, transform.ValueRO.Position, out var cell, out _))
                    {
                        continue;
                    }

                    var index = EnvironmentGridMath.GetCellIndex(sunlightGrid.Metadata, cell);
                    occluderCounts[index] = math.min(occluderCounts[index] + 1, ushort.MaxValue);
                }
            }

            var occlusionJob = new ApplyOcclusionJob
            {
                Samples = samples,
                OccluderCounts = occluderCounts,
                DirectPenaltyPerOccluder = 0.08f,
                AmbientBoostPerOccluder = 1.5f
            };
            state.Dependency = occlusionJob.ScheduleParallel(samples.Length, 128, state.Dependency);
            occluderCounts.Dispose(state.Dependency);

            var terrainContext = BuildTerrainContext(ref state, out var chunkLookup);
            var shadowJob = new ApplyTerrainShadowJob
            {
                Samples = samples,
                Metadata = sunlightGrid.Metadata,
                TerrainContext = terrainContext,
                ProbeDepth = math.max(0.25f, terrainContext.WorldConfig.VoxelSize * 0.5f)
            };
            state.Dependency = shadowJob.ScheduleParallel(samples.Length, 128, state.Dependency);
            if (chunkLookup.IsCreated)
            {
                chunkLookup.Dispose(state.Dependency);
            }

            sunlightGrid.SunDirection = sunDirection;
            sunlightGrid.SunIntensity = sunScalar;
            sunlightGrid.LastUpdateTick = currentTick;
            SystemAPI.SetComponent(sunlightEntity, sunlightGrid);
        }

        private static TerrainQueryContext BuildTerrainContext(ref SystemState state, out NativeParallelHashMap<TerrainChunkKey, Entity> chunkLookup)
        {
            var moistureGrid = default(MoistureGrid);
            state.GetEntityQuery(ComponentType.ReadOnly<MoistureGrid>())
                .TryGetSingleton(out moistureGrid);

            var terrainPlane = default(TerrainHeightPlane);
            state.GetEntityQuery(ComponentType.ReadOnly<TerrainHeightPlane>())
                .TryGetSingleton(out terrainPlane);

            var flatSurface = default(TerrainFlatSurface);
            state.GetEntityQuery(ComponentType.ReadOnly<TerrainFlatSurface>())
                .TryGetSingleton(out flatSurface);

            var solidSphere = default(TerrainSolidSphere);
            state.GetEntityQuery(ComponentType.ReadOnly<TerrainSolidSphere>())
                .TryGetSingleton(out solidSphere);

            var terrainConfig = TerrainWorldConfig.Default;
            state.GetEntityQuery(ComponentType.ReadOnly<TerrainWorldConfig>())
                .TryGetSingleton(out terrainConfig);

            var surfaceDomain = default(SurfaceFieldsDomainConfig);
            state.GetEntityQuery(ComponentType.ReadOnly<SurfaceFieldsDomainConfig>())
                .TryGetSingleton(out surfaceDomain);

            var globalTerrainVersion = 0u;
            if (state.GetEntityQuery(ComponentType.ReadOnly<TerrainVersion>())
                .TryGetSingleton(out TerrainVersion terrainVersion))
            {
                globalTerrainVersion = terrainVersion.Value;
            }

            var surfaceChunks = default(NativeArray<SurfaceFieldsChunkRef>);
            var surfaceCacheQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SurfaceFieldsChunkRefCache>(),
                ComponentType.ReadOnly<SurfaceFieldsChunkRef>());
            if (surfaceCacheQuery.TryGetSingletonEntity<SurfaceFieldsChunkRefCache>(out var surfaceCacheEntity))
            {
                surfaceChunks = state.EntityManager
                    .GetBuffer<SurfaceFieldsChunkRef>(surfaceCacheEntity)
                    .AsNativeArray();
            }

            var volumeEntity = Entity.Null;
            var volumeOrigin = terrainConfig.VolumeWorldOrigin;
            var volumeWorldToLocal = float4x4.identity;
            byte volumeEnabled = 0;

            var volumeQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainVolume>());
            if (!volumeQuery.IsEmptyIgnoreFilter)
            {
                using var volumeEntities = volumeQuery.ToEntityArray(Allocator.Temp);
                if (volumeEntities.Length > 0)
                {
                    volumeEntity = volumeEntities[0];
                    var volume = state.EntityManager.GetComponentData<TerrainVolume>(volumeEntity);
                    volumeOrigin = volume.LocalOrigin;
                    volumeEnabled = 1;
                }
            }

            if (volumeEnabled != 0 && state.EntityManager.HasComponent<LocalTransform>(volumeEntity))
            {
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(volumeEntity);
                var volumeLocalToWorld = float4x4.TRS(localTransform.Position, localTransform.Rotation, new float3(localTransform.Scale));
                volumeWorldToLocal = math.inverse(volumeLocalToWorld);
            }

            chunkLookup = BuildChunkLookup(ref state);
            var chunkComponentLookup = state.GetComponentLookup<TerrainChunk>(true);
            chunkComponentLookup.Update(ref state);
            var voxelRuntimeLookup = state.GetBufferLookup<TerrainVoxelRuntime>(true);
            voxelRuntimeLookup.Update(ref state);
            var voxelAccessor = new TerrainVoxelAccessor
            {
                ChunkLookup = chunkLookup,
                Chunks = chunkComponentLookup,
                RuntimeVoxels = voxelRuntimeLookup,
                WorldConfig = terrainConfig
            };

            return new TerrainQueryContext
            {
                MoistureGrid = moistureGrid,
                HeightPlane = terrainPlane,
                FlatSurface = flatSurface,
                SolidSphere = solidSphere,
                WorldConfig = terrainConfig,
                GlobalTerrainVersion = globalTerrainVersion,
                SurfaceFieldsDomain = surfaceDomain,
                SurfaceFieldsChunks = surfaceChunks,
                VoxelAccessor = voxelAccessor,
                VolumeEntity = volumeEntity,
                VolumeOrigin = volumeOrigin,
                VolumeWorldToLocal = volumeWorldToLocal,
                VolumeEnabled = volumeEnabled
            };
        }

        private static NativeParallelHashMap<TerrainChunkKey, Entity> BuildChunkLookup(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            var chunkCount = query.CalculateEntityCount();
            if (chunkCount <= 0)
            {
                return default;
            }

            var map = new NativeParallelHashMap<TerrainChunkKey, Entity>(chunkCount, Allocator.TempJob);
            using var chunkData = query.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            using var chunkEntities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < chunkData.Length; i++)
            {
                var chunk = chunkData[i];
                var entity = chunkEntities[i];
                map.TryAdd(new TerrainChunkKey
                {
                    VolumeEntity = chunk.VolumeEntity,
                    ChunkCoord = chunk.ChunkCoord
                }, entity);
            }

            return map;
        }

        private static void CalculateSunlight(in PureDOTS.Environment.ClimateState climate, out float3 direction, out float direct, out float ambient, out float scalar)
        {
            var hours = math.clamp(climate.TimeOfDayHours, 0f, 24f);
            var dayProgress = hours / 24f;
            var dayAngle = dayProgress * math.PI * 2f;

            var sinElevation = math.sin(dayAngle - math.PI / 2f);
            var clampedSin = math.clamp(sinElevation, -1f, 1f);
            var sunHeight = math.max(0f, clampedSin);
            var elevationAngle = math.asin(clampedSin);
            var cosElevation = math.cos(elevationAngle);

            if (sunHeight <= 0.0001f)
            {
                direction = new float3(0f, -1f, 0f);
            }
            else
            {
                var azimuth = dayAngle + math.PI;
                var dir = new float3(
                    math.cos(azimuth) * cosElevation,
                    -sunHeight,
                    math.sin(azimuth) * cosElevation);
                direction = math.normalizesafe(dir, new float3(0f, -1f, 0f));
            }

            var cloudFactor = math.saturate(climate.CloudCover / 100f);
            var seasonScale = climate.CurrentSeason switch
            {
                Season.Summer => 1.1f,
                Season.Winter => 0.7f,
                _ => 0.9f
            };

            var baseDirect = math.pow(sunHeight, 0.65f) * 100f * seasonScale;
            var directLight = math.clamp(baseDirect * (1f - 0.7f * cloudFactor), 0f, 100f);

            var ambientBase = math.lerp(8f, 35f, cloudFactor);
            var ambientLight = math.clamp(ambientBase + directLight * 0.2f, 5f, 60f);
            if (sunHeight <= 0.0001f)
            {
                ambientLight = math.clamp(ambientBase * 0.5f, 5f, 25f);
            }

            direct = directLight;
            ambient = ambientLight;
            scalar = math.saturate(directLight / 100f);
        }

        [BurstCompile]
        private struct SunlightBaselineJob : IJobFor
        {
            public NativeArray<SunlightSample> Samples;
            public float DirectLight;
            public float AmbientLight;

            public void Execute(int index)
            {
                Samples[index] = new SunlightSample
                {
                    DirectLight = DirectLight,
                    AmbientLight = AmbientLight,
                    OccluderCount = 0
                };
            }
        }

        [BurstCompile]
        private struct ApplyOcclusionJob : IJobFor
        {
            public NativeArray<SunlightSample> Samples;
            [ReadOnly] public NativeArray<int> OccluderCounts;
            public float DirectPenaltyPerOccluder;
            public float AmbientBoostPerOccluder;

            public void Execute(int index)
            {
                var sample = Samples[index];
                var count = math.max(0, OccluderCounts[index]);
                sample.OccluderCount = (ushort)math.min(count, ushort.MaxValue);

                var directPenalty = 1f - math.saturate(count * DirectPenaltyPerOccluder);
                sample.DirectLight = math.max(0f, sample.DirectLight * directPenalty);

                var ambient = sample.AmbientLight + count * AmbientBoostPerOccluder;
                sample.AmbientLight = math.clamp(ambient, 5f, 100f);

                Samples[index] = sample;
            }
        }

        [BurstCompile]
        private struct ApplyTerrainShadowJob : IJobFor
        {
            public NativeArray<SunlightSample> Samples;
            public EnvironmentGridMetadata Metadata;
            public TerrainQueryContext TerrainContext;
            public float ProbeDepth;

            public void Execute(int index)
            {
                var sample = Samples[index];
                var probe = EnvironmentGridMath.GetCellCenter(Metadata, index);

                if (TerrainQueryFacade.TrySampleHeight(TerrainContext, probe, out var height))
                {
                    probe.y = height - ProbeDepth;
                }

                if (TerrainQueryFacade.IsSolid(TerrainContext, probe))
                {
                    sample.DirectLight = 0f;
                    sample.AmbientLight = 0f;
                }

                Samples[index] = sample;
            }
        }
    }
}

using PureDOTS.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Builds the runtime environment singleton using the authored configuration.
    /// Creates grid blob assets with deterministic defaults so simulation systems
    /// can update them during the environment phase each record tick.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct EnvironmentGridBootstrapSystem : ISystem
    {
        private Entity _configEntity;
        private BlobAssetReference<MoistureGridBlob> _moistureBlob;
        private BlobAssetReference<TemperatureGridBlob> _temperatureBlob;
        private BlobAssetReference<SunlightGridBlob> _sunlightBlob;
        private BlobAssetReference<WindFieldBlob> _windBlob;
        private BlobAssetReference<BiomeGridBlob> _biomeBlob;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnvironmentGridConfigData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var configEntity = SystemAPI.GetSingletonEntity<EnvironmentGridConfigData>();
            var config = SystemAPI.GetSingleton<EnvironmentGridConfigData>();

            _configEntity = configEntity;

            var entityManager = state.EntityManager;
            if (!entityManager.HasComponent<ClimateState>(configEntity))
            {
                entityManager.AddComponentData(configEntity, CreateDefaultClimateState());
            }

            if (!entityManager.HasComponent<MoistureGrid>(configEntity))
            {
                var grid = CreateMoistureGrid(config);
                entityManager.AddComponentData(configEntity, grid);
                _moistureBlob = grid.Blob;
            }
            else
            {
                _moistureBlob = entityManager.GetComponentData<MoistureGrid>(configEntity).Blob;
            }

            EnsureMoistureRuntimeBuffers(ref state, configEntity);
            EnsureClimateRuntimeBuffers(ref state, configEntity);

            if (!entityManager.HasComponent<TemperatureGrid>(configEntity))
            {
                var grid = CreateTemperatureGrid(config);
                entityManager.AddComponentData(configEntity, grid);
                _temperatureBlob = grid.Blob;
            }
            else
            {
                _temperatureBlob = entityManager.GetComponentData<TemperatureGrid>(configEntity).Blob;
            }

            if (!entityManager.HasComponent<SunlightGrid>(configEntity))
            {
                var grid = CreateSunlightGrid(config);
                entityManager.AddComponentData(configEntity, grid);
                _sunlightBlob = grid.Blob;
            }
            else
            {
                _sunlightBlob = entityManager.GetComponentData<SunlightGrid>(configEntity).Blob;
            }

            EnsureSunlightRuntimeBuffer(ref state, configEntity);

            if (!entityManager.HasComponent<WindField>(configEntity))
            {
                var grid = CreateWindField(config);
                entityManager.AddComponentData(configEntity, grid);
                _windBlob = grid.Blob;
            }
            else
            {
                _windBlob = entityManager.GetComponentData<WindField>(configEntity).Blob;
            }

            if (config.BiomeEnabled != 0 && !entityManager.HasComponent<BiomeGrid>(configEntity))
            {
                var grid = CreateBiomeGrid(config);
                entityManager.AddComponentData(configEntity, grid);
                _biomeBlob = grid.Blob;
            }
            else if (entityManager.HasComponent<BiomeGrid>(configEntity))
            {
                _biomeBlob = entityManager.GetComponentData<BiomeGrid>(configEntity).Blob;
            }

            if (entityManager.HasComponent<BiomeGrid>(configEntity))
            {
                EnsureBiomeRuntimeBuffer(ref state, configEntity);
            }

            EnsureClimateRuntimeBuffers(ref state, configEntity);

            if (!entityManager.HasComponent<MoistureGridSimulationState>(configEntity))
            {
                entityManager.AddComponentData(configEntity, new MoistureGridSimulationState
                {
                    LastEvaporationTick = uint.MaxValue,
                    LastSeepageTick = uint.MaxValue
                });
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            if (_configEntity != Entity.Null && entityManager.Exists(_configEntity))
            {
                RemoveComponentIfPresent<MoistureGridSimulationState>(entityManager, _configEntity);
                RemoveComponentIfPresent<ClimateState>(entityManager, _configEntity);
                RemoveComponentIfPresent<MoistureGrid>(entityManager, _configEntity);
                RemoveComponentIfPresent<TemperatureGrid>(entityManager, _configEntity);
                RemoveComponentIfPresent<SunlightGrid>(entityManager, _configEntity);
                RemoveComponentIfPresent<WindField>(entityManager, _configEntity);
                RemoveComponentIfPresent<BiomeGrid>(entityManager, _configEntity);

                RemoveBufferIfPresent<MoistureGridRuntimeCell>(entityManager, _configEntity);
                RemoveBufferIfPresent<SunlightGridRuntimeSample>(entityManager, _configEntity);
                RemoveBufferIfPresent<BiomeGridRuntimeCell>(entityManager, _configEntity);
                RemoveBufferIfPresent<ClimateGridRuntimeCell>(entityManager, _configEntity);
            }

            DisposeBlob(ref _moistureBlob);
            DisposeBlob(ref _temperatureBlob);
            DisposeBlob(ref _sunlightBlob);
            DisposeBlob(ref _windBlob);
            DisposeBlob(ref _biomeBlob);

            _configEntity = Entity.Null;
        }

        private static PureDOTS.Environment.ClimateState CreateDefaultClimateState()
        {
            return new ClimateState
            {
                CurrentSeason = Season.Spring,
                SeasonProgress = 0f,
                TimeOfDayHours = 6f,
                DayNightProgress = 6f / 24f,
                GlobalTemperature = 18f,
                GlobalWindDirection = math.normalize(new float2(0.7f, 0.5f)),
                GlobalWindStrength = 8f,
                AtmosphericMoisture = 55f,
                CloudCover = 20f,
                LastUpdateTick = uint.MaxValue
            };
        }

        private static MoistureGrid CreateMoistureGrid(in EnvironmentGridConfigData config)
        {
            var blob = CreateMoistureBlob(config.Moisture);
            return new MoistureGrid
            {
                Metadata = config.Moisture,
                Blob = blob,
                ChannelId = config.MoistureChannelId,
                DiffusionCoefficient = math.max(0f, config.MoistureDiffusion),
                SeepageCoefficient = math.max(0f, config.MoistureSeepage),
                LastUpdateTick = uint.MaxValue,
                LastTerrainVersion = 0u
            };
        }

        private static TemperatureGrid CreateTemperatureGrid(in EnvironmentGridConfigData config)
        {
            var blob = CreateTemperatureBlob(config.Temperature, config.BaseSeasonTemperature);
            return new TemperatureGrid
            {
                Metadata = config.Temperature,
                Blob = blob,
                ChannelId = config.TemperatureChannelId,
                BaseSeasonTemperature = config.BaseSeasonTemperature,
                TimeOfDaySwing = config.TimeOfDaySwing,
                SeasonalSwing = config.SeasonalSwing,
                LastUpdateTick = uint.MaxValue,
                LastTerrainVersion = 0u
            };
        }

        private static SunlightGrid CreateSunlightGrid(in EnvironmentGridConfigData config)
        {
            var blob = CreateSunlightBlob(config.Sunlight, config.DefaultSunIntensity);
            return new SunlightGrid
            {
                Metadata = config.Sunlight,
                Blob = blob,
                ChannelId = config.SunlightChannelId,
                SunDirection = math.lengthsq(config.DefaultSunDirection) > 0f
                    ? math.normalize(config.DefaultSunDirection)
                    : new float3(0f, -1f, 0f),
                SunIntensity = config.DefaultSunIntensity,
                LastUpdateTick = uint.MaxValue,
                LastTerrainVersion = 0u
            };
        }

        private static WindField CreateWindField(in EnvironmentGridConfigData config)
        {
            var blob = CreateWindBlob(config.Wind, config.DefaultWindDirection, config.DefaultWindStrength);
            return new WindField
            {
                Metadata = config.Wind,
                Blob = blob,
                ChannelId = config.WindChannelId,
                GlobalWindDirection = math.lengthsq(config.DefaultWindDirection) > 0f
                    ? math.normalize(config.DefaultWindDirection)
                    : new float2(0f, 1f),
                GlobalWindStrength = config.DefaultWindStrength,
                LastUpdateTick = uint.MaxValue,
                LastTerrainVersion = 0u
            };
        }

        private static BiomeGrid CreateBiomeGrid(in EnvironmentGridConfigData config)
        {
            var blob = CreateBiomeBlob(config.Biome);
            return new BiomeGrid
            {
                Metadata = config.Biome,
                Blob = blob,
                ChannelId = config.BiomeChannelId,
                LastUpdateTick = uint.MaxValue,
                LastTerrainVersion = 0u
            };
        }

        private static void EnsureMoistureRuntimeBuffers(ref SystemState state, Entity configEntity)
        {
            var entityManager = state.EntityManager;
            if (!entityManager.HasComponent<MoistureGrid>(configEntity))
            {
                return;
            }

            var moistureGrid = entityManager.GetComponentData<MoistureGrid>(configEntity);
            var cellCount = math.max(1, moistureGrid.Metadata.CellCount);
            if (!entityManager.HasBuffer<MoistureGridRuntimeCell>(configEntity))
            {
                InitialiseMoistureRuntimeBuffer(entityManager, configEntity, in moistureGrid);
            }
            else
            {
                var buffer = entityManager.GetBuffer<MoistureGridRuntimeCell>(configEntity);
                if (buffer.Length != cellCount)
                {
                    buffer.Clear();
                    buffer.ResizeUninitialized(cellCount);
                    PopulateMoistureRuntimeBuffer(buffer, in moistureGrid);
                }
            }
        }

        private static void EnsureSunlightRuntimeBuffer(ref SystemState state, Entity configEntity)
        {
            var entityManager = state.EntityManager;
            if (!entityManager.HasComponent<SunlightGrid>(configEntity))
            {
                return;
            }

            var sunlightGrid = entityManager.GetComponentData<SunlightGrid>(configEntity);
            var cellCount = math.max(1, sunlightGrid.Metadata.CellCount);

            DynamicBuffer<SunlightGridRuntimeSample> buffer;
            if (!entityManager.HasBuffer<SunlightGridRuntimeSample>(configEntity))
            {
                buffer = entityManager.AddBuffer<SunlightGridRuntimeSample>(configEntity);
                buffer.ResizeUninitialized(cellCount);
            }
            else
            {
                buffer = entityManager.GetBuffer<SunlightGridRuntimeSample>(configEntity);
                if (buffer.Length != cellCount)
                {
                    buffer.Clear();
                    buffer.ResizeUninitialized(cellCount);
                }
            }

            PopulateSunlightRuntimeBuffer(buffer, in sunlightGrid);
        }

        private static void EnsureBiomeRuntimeBuffer(ref SystemState state, Entity configEntity)
        {
            var entityManager = state.EntityManager;
            var biomeGrid = entityManager.GetComponentData<BiomeGrid>(configEntity);
            var cellCount = math.max(1, biomeGrid.Metadata.CellCount);

            DynamicBuffer<BiomeGridRuntimeCell> buffer;
            if (!entityManager.HasBuffer<BiomeGridRuntimeCell>(configEntity))
            {
                buffer = entityManager.AddBuffer<BiomeGridRuntimeCell>(configEntity);
                buffer.ResizeUninitialized(cellCount);
            }
            else
            {
                buffer = entityManager.GetBuffer<BiomeGridRuntimeCell>(configEntity);
                if (buffer.Length != cellCount)
                {
                    buffer.Clear();
                    buffer.ResizeUninitialized(cellCount);
                }
            }

            PopulateBiomeRuntimeBuffer(buffer, in biomeGrid);
        }

        private static void InitialiseMoistureRuntimeBuffer(EntityManager entityManager, Entity entity, in MoistureGrid grid)
        {
            if (!grid.IsCreated)
            {
                return;
            }

            var cellCount = math.max(1, grid.Metadata.CellCount);
            var buffer = entityManager.AddBuffer<MoistureGridRuntimeCell>(entity);
            buffer.ResizeUninitialized(cellCount);
            PopulateMoistureRuntimeBuffer(buffer, in grid);
        }

        private static void PopulateMoistureRuntimeBuffer(DynamicBuffer<MoistureGridRuntimeCell> buffer, in MoistureGrid grid)
        {
            if (!grid.IsCreated)
            {
                return;
            }

            ref var moist = ref grid.Blob.Value.Moisture;
            ref var evaporation = ref grid.Blob.Value.EvaporationRate;
            ref var lastRain = ref grid.Blob.Value.LastRainTick;

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new MoistureGridRuntimeCell
                {
                    Moisture = i < moist.Length ? moist[i] : 0f,
                    EvaporationRate = i < evaporation.Length ? evaporation[i] : 0f,
                    LastRainTick = i < lastRain.Length ? lastRain[i] : 0u
                };
            }
        }

        private static void PopulateSunlightRuntimeBuffer(DynamicBuffer<SunlightGridRuntimeSample> buffer, in SunlightGrid grid)
        {
            if (!grid.IsCreated)
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = new SunlightGridRuntimeSample { Value = default };
                }
                return;
            }

            ref var samples = ref grid.Blob.Value.Samples;
            for (var i = 0; i < buffer.Length; i++)
            {
                var value = i < samples.Length ? samples[i] : default;
                buffer[i] = new SunlightGridRuntimeSample { Value = value };
            }
        }

        private static void PopulateBiomeRuntimeBuffer(DynamicBuffer<BiomeGridRuntimeCell> buffer, in BiomeGrid grid)
        {
            if (!grid.IsCreated)
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = new BiomeGridRuntimeCell { Value = BiomeType.Unknown };
                }
                return;
            }

            ref var biomes = ref grid.Blob.Value.Biomes;
            for (var i = 0; i < buffer.Length; i++)
            {
                var value = i < biomes.Length ? biomes[i] : BiomeType.Unknown;
                buffer[i] = new BiomeGridRuntimeCell { Value = value };
            }
        }

        private static void EnsureClimateRuntimeBuffers(ref SystemState state, Entity configEntity)
        {
            var entityManager = state.EntityManager;
            if (!entityManager.HasComponent<MoistureGrid>(configEntity))
            {
                return;
            }

            var moistureGrid = entityManager.GetComponentData<MoistureGrid>(configEntity);
            var cellCount = math.max(1, moistureGrid.Metadata.CellCount);

            DynamicBuffer<ClimateGridRuntimeCell> buffer;
            if (!entityManager.HasBuffer<ClimateGridRuntimeCell>(configEntity))
            {
                buffer = entityManager.AddBuffer<ClimateGridRuntimeCell>(configEntity);
                buffer.ResizeUninitialized(cellCount);
            }
            else
            {
                buffer = entityManager.GetBuffer<ClimateGridRuntimeCell>(configEntity);
                if (buffer.Length != cellCount)
                {
                    buffer.Clear();
                    buffer.ResizeUninitialized(cellCount);
                }
            }

            PopulateClimateRuntimeBuffer(buffer, moistureGrid, entityManager.GetComponentData<TemperatureGrid>(configEntity));
        }

        private static void PopulateClimateRuntimeBuffer(DynamicBuffer<ClimateGridRuntimeCell> buffer, in MoistureGrid moistureGrid, in TemperatureGrid temperatureGrid)
        {
            var cellCount = buffer.Length;
            var moistureBlob = moistureGrid.Blob;
            var temperatureBlob = temperatureGrid.Blob;

            for (var i = 0; i < cellCount; i++)
            {
                var climate = new ClimateVector();

                // Convert moisture from 0-100 to 0-1
                if (moistureBlob.IsCreated && i < moistureBlob.Value.Moisture.Length)
                {
                    climate.Moisture = math.clamp(moistureBlob.Value.Moisture[i] / 100f, 0f, 1f);
                }

                // Convert temperature from Celsius to normalized -1..+1 (using 0-40°C range)
                if (temperatureBlob.IsCreated && i < temperatureBlob.Value.TemperatureCelsius.Length)
                {
                    var tempC = temperatureBlob.Value.TemperatureCelsius[i];
                    climate.Temperature = math.clamp((tempC - 20f) / 20f, -1f, 1f);
                }

                // Defaults for other values
                climate.Fertility = 0.5f;
                climate.WaterLevel = 0f;
                climate.Ruggedness = 0f;

                buffer[i] = new ClimateGridRuntimeCell { Climate = climate };
            }
        }

        private static BlobAssetReference<MoistureGridBlob> CreateMoistureBlob(in EnvironmentGridMetadata metadata)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MoistureGridBlob>();

            var cellCount = math.max(1, metadata.CellCount);

            var moisture = builder.Allocate(ref root.Moisture, cellCount);
            var drainage = builder.Allocate(ref root.DrainageRate, cellCount);
            var terrain = builder.Allocate(ref root.TerrainHeight, cellCount);
            var lastRain = builder.Allocate(ref root.LastRainTick, cellCount);
            var evaporation = builder.Allocate(ref root.EvaporationRate, cellCount);

            for (var i = 0; i < cellCount; i++)
            {
                moisture[i] = 50f;
                drainage[i] = 0.05f;
                terrain[i] = 0f;
                lastRain[i] = 0u;
                evaporation[i] = 1f;
            }

            var blob = builder.CreateBlobAssetReference<MoistureGridBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static BlobAssetReference<TemperatureGridBlob> CreateTemperatureBlob(in EnvironmentGridMetadata metadata, float baseTemperature)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TemperatureGridBlob>();

            var cellCount = math.max(1, metadata.CellCount);
            var temperature = builder.Allocate(ref root.TemperatureCelsius, cellCount);
            var altitude = builder.Allocate(ref root.AltitudeMeters, cellCount);

            for (var i = 0; i < cellCount; i++)
            {
                temperature[i] = baseTemperature;
                altitude[i] = 0f;
            }

            var blob = builder.CreateBlobAssetReference<TemperatureGridBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static BlobAssetReference<SunlightGridBlob> CreateSunlightBlob(in EnvironmentGridMetadata metadata, float defaultIntensity)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SunlightGridBlob>();

            var cellCount = math.max(1, metadata.CellCount);
            var samples = builder.Allocate(ref root.Samples, cellCount);

            for (var i = 0; i < cellCount; i++)
            {
                samples[i] = new SunlightSample
                {
                    DirectLight = defaultIntensity,
                    AmbientLight = defaultIntensity * 0.25f,
                    OccluderCount = 0
                };
            }

            var blob = builder.CreateBlobAssetReference<SunlightGridBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static BlobAssetReference<WindFieldBlob> CreateWindBlob(in EnvironmentGridMetadata metadata, float2 defaultDirection, float defaultStrength)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WindFieldBlob>();

            var cellCount = math.max(1, metadata.CellCount);
            var samples = builder.Allocate(ref root.Samples, cellCount);
            var direction = math.lengthsq(defaultDirection) > 0f
                ? math.normalize(defaultDirection)
                : new float2(0f, 1f);

            for (var i = 0; i < cellCount; i++)
            {
                samples[i] = new WindSample
                {
                    Direction = direction,
                    Strength = defaultStrength
                };
            }

            var blob = builder.CreateBlobAssetReference<WindFieldBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static BlobAssetReference<BiomeGridBlob> CreateBiomeBlob(in EnvironmentGridMetadata metadata)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BiomeGridBlob>();

            var cellCount = math.max(1, metadata.CellCount);
            var biomes = builder.Allocate(ref root.Biomes, cellCount);

            for (var i = 0; i < cellCount; i++)
            {
                biomes[i] = BiomeType.Unknown;
            }

            var blob = builder.CreateBlobAssetReference<BiomeGridBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static void DisposeBlob<T>(ref BlobAssetReference<T> blob) where T : unmanaged
        {
            if (blob.IsCreated)
            {
                blob.Dispose();
                blob = default;
            }
        }

        private static void RemoveComponentIfPresent<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
            {
                entityManager.RemoveComponent<T>(entity);
            }
        }

        private static void RemoveBufferIfPresent<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (entityManager.HasBuffer<T>(entity))
            {
                entityManager.RemoveComponent<T>(entity);
            }
        }
    }
}

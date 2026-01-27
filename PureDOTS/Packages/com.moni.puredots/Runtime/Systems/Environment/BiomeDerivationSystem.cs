using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Derives biome classifications per cell using current moisture and temperature fields.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(MoistureSeepageSystem))]
    public partial struct BiomeDerivationSystem : ISystem
    {
        private TimeAwareController _timeAware;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<BiomeGrid>();
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

            var biomeEntity = SystemAPI.GetSingletonEntity<BiomeGrid>();
            if (!SystemAPI.HasBuffer<BiomeGridRuntimeCell>(biomeEntity))
            {
                return;
            }

            var biomeBuffer = SystemAPI.GetBuffer<BiomeGridRuntimeCell>(biomeEntity);
            if (biomeBuffer.Length == 0)
            {
                return;
            }

            var biomeGrid = SystemAPI.GetSingletonRW<BiomeGrid>();
            var moistureGrid = SystemAPI.GetSingleton<MoistureGrid>();
            var temperatureGrid = SystemAPI.GetSingleton<TemperatureGrid>();
            
            // Check terrain version - if terrain changed, force biome recalculation
            uint currentTerrainVersion = 0;
            if (SystemAPI.TryGetSingleton<PureDOTS.Environment.TerrainVersion>(out var terrainVersion))
            {
                currentTerrainVersion = terrainVersion.Value;
                if (currentTerrainVersion != biomeGrid.ValueRO.LastTerrainVersion)
                {
                    // Terrain changed, force biome update
                    biomeGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                    biomeGrid.ValueRW.LastUpdateTick = uint.MaxValue; // Force rebuild
                }
            }

            NativeArray<MoistureGridRuntimeCell> moistureRuntime = default;
            var hasMoistureRuntime = false;
            if (SystemAPI.TryGetSingletonEntity<MoistureGrid>(out var moistureEntity) &&
                SystemAPI.HasBuffer<MoistureGridRuntimeCell>(moistureEntity))
            {
                var buffer = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(moistureEntity);
                if (buffer.Length == biomeBuffer.Length)
                {
                    moistureRuntime = buffer.AsNativeArray();
                    hasMoistureRuntime = true;
                }
            }

            NativeArray<ClimateGridRuntimeCell> climateRuntime = default;
            var hasClimateRuntime = false;
            if (SystemAPI.TryGetSingletonEntity<MoistureGrid>(out var climateEntity) &&
                SystemAPI.HasBuffer<ClimateGridRuntimeCell>(climateEntity))
            {
                var buffer = SystemAPI.GetBuffer<ClimateGridRuntimeCell>(climateEntity);
                if (buffer.Length == biomeBuffer.Length)
                {
                    climateRuntime = buffer.AsNativeArray();
                    hasClimateRuntime = true;
                }
            }

            var job = new BiomeDerivationJob
            {
                Biomes = biomeBuffer.AsNativeArray(),
                MoistureRuntime = moistureRuntime,
                MoistureBlob = moistureGrid.Blob,
                HasMoistureRuntime = hasMoistureRuntime,
                TemperatureBlob = temperatureGrid.Blob,
                ClimateRuntime = climateRuntime,
                HasClimateRuntime = hasClimateRuntime
            };

            state.Dependency = job.ScheduleParallel(biomeBuffer.Length, 64, state.Dependency);
            biomeGrid.ValueRW.LastUpdateTick = timeState.Tick;
        }

        [BurstCompile]
        private struct BiomeDerivationJob : IJobFor
        {
            public NativeArray<BiomeGridRuntimeCell> Biomes;

            [ReadOnly] public NativeArray<MoistureGridRuntimeCell> MoistureRuntime;
            [ReadOnly] public BlobAssetReference<MoistureGridBlob> MoistureBlob;
            public bool HasMoistureRuntime;

            [ReadOnly] public BlobAssetReference<TemperatureGridBlob> TemperatureBlob;

            [ReadOnly] public NativeArray<ClimateGridRuntimeCell> ClimateRuntime;
            public bool HasClimateRuntime;

            public void Execute(int index)
            {
                BiomeType biome;
                
                if (HasClimateRuntime && ClimateRuntime.IsCreated && index < ClimateRuntime.Length)
                {
                    // Use climate vector for classification
                    var climate = ClimateRuntime[index].Climate;
                    biome = ClassifyBiomeFromClimate(climate);
                }
                else
                {
                    // Fallback to temperature/moisture
                    var moisture = SampleMoisture(index);
                    var temperature = SampleTemperature(index);
                    biome = ClassifyBiome(temperature, moisture);
                }

                Biomes[index] = new BiomeGridRuntimeCell
                {
                    Value = biome
                };
            }

            private float SampleMoisture(int index)
            {
                if (HasMoistureRuntime && MoistureRuntime.IsCreated && index < MoistureRuntime.Length)
                {
                    return MoistureRuntime[index].Moisture;
                }

                if (MoistureBlob.IsCreated)
                {
                    ref var moisture = ref MoistureBlob.Value.Moisture;
                    if (index >= 0 && index < moisture.Length)
                    {
                        return moisture[index];
                    }
                }

                return 0f;
            }

            private float SampleTemperature(int index)
            {
                if (TemperatureBlob.IsCreated)
                {
                    ref var temperatures = ref TemperatureBlob.Value.TemperatureCelsius;
                    if (index >= 0 && index < temperatures.Length)
                    {
                        return temperatures[index];
                    }
                }

                return 0f;
            }

            private static BiomeType ClassifyBiome(float temperature, float moisture)
            {
                if (temperature <= -10f)
                {
                    return BiomeType.Tundra;
                }

                if (temperature <= 2f)
                {
                    return moisture >= 55f ? BiomeType.Swamp : BiomeType.Taiga;
                }

                if (temperature <= 18f)
                {
                    if (moisture >= 70f)
                    {
                        return BiomeType.Forest;
                    }

                    if (moisture >= 45f)
                    {
                        return BiomeType.Grassland;
                    }

                    return BiomeType.Savanna;
                }

                if (temperature <= 30f)
                {
                    if (moisture >= 75f)
                    {
                        return BiomeType.Rainforest;
                    }

                    if (moisture >= 35f)
                    {
                        return BiomeType.Grassland;
                    }

                    return BiomeType.Savanna;
                }

                return moisture >= 30f ? BiomeType.Savanna : BiomeType.Desert;
            }

            private static BiomeType ClassifyBiomeFromClimate(in ClimateVector climate)
            {
                // Convert normalized temperature back to Celsius for compatibility
                var temperature = 20f + climate.Temperature * 20f;
                var moisture = climate.Moisture * 100f;

                // Water level overrides: ocean/swamp
                if (climate.WaterLevel > 0.7f)
                {
                    return BiomeType.Swamp;
                }

                // Use temperature/moisture classification
                return ClassifyBiome(temperature, moisture);
            }
        }
    }
}


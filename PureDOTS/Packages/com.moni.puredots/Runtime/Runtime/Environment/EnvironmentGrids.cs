using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Seasons referenced by the shared climate state and environment grids.
    /// </summary>
    public enum Season : byte
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    /// <summary>
    /// Shared biome identifiers consumed across vegetation, climate, and resource systems.
    /// </summary>
    public enum BiomeType : byte
    {
        Unknown = 0,
        Tundra = 1,
        Taiga = 2,
        Grassland = 3,
        Forest = 4,
        Desert = 5,
        Rainforest = 6,
        Savanna = 7,
        Swamp = 8
    }

    /// <summary>
    /// Global climate singleton used to coordinate seasonal and atmospheric state.
    /// </summary>
    public struct ClimateState : IComponentData
    {
        public Season CurrentSeason;
        public float SeasonProgress;        // 0-1 across the current season
        public float TimeOfDayHours;        // 0-24 across the current day
        public float DayNightProgress;      // 0-1 over the current day-night cycle
        public float GlobalTemperature;     // Degrees Celsius
        public float2 GlobalWindDirection;  // Normalised XZ vector
        public float GlobalWindStrength;    // m/s
        public float AtmosphericMoisture;   // 0-100 humidity
        public float CloudCover;            // 0-100 percentage
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Shared metadata container describing a 2D environment grid laid across XZ-plane terrain.
    /// </summary>
    public struct EnvironmentGridMetadata
    {
        public float3 WorldMin;
        public float3 WorldMax;
        public float CellSize;
        public int2 Resolution;
        public float InverseCellSize;

        public static EnvironmentGridMetadata Create(float3 worldMin, float3 worldMax, float cellSize, int2 resolution)
        {
            var safeCellSize = math.max(0.1f, cellSize);
            var safeResolution = math.max(resolution, new int2(1, 1));

            return new EnvironmentGridMetadata
            {
                WorldMin = worldMin,
                WorldMax = worldMax,
                CellSize = safeCellSize,
                Resolution = safeResolution,
                InverseCellSize = math.rcp(safeCellSize)
            };
        }

        public readonly int CellCount => math.max(Resolution.x * Resolution.y, 1);

        public readonly int2 CellCounts => Resolution;

        public readonly int2 MaxCellIndex => math.max(Resolution - 1, new int2(0, 0));

        public readonly float2 WorldSizeXZ => new float2(WorldMax.x - WorldMin.x, WorldMax.z - WorldMin.z);

        public readonly bool Contains(float3 worldPosition)
        {
            return worldPosition.x >= WorldMin.x && worldPosition.x <= WorldMax.x &&
                   worldPosition.z >= WorldMin.z && worldPosition.z <= WorldMax.z;
        }
    }

    /// <summary>
    /// Aggregated configuration for authoring and runtime bootstrap of environment grids.
    /// </summary>
    public struct EnvironmentGridConfigData : IComponentData
    {
        public EnvironmentGridMetadata Moisture;
        public EnvironmentGridMetadata Temperature;
        public EnvironmentGridMetadata Sunlight;
        public EnvironmentGridMetadata Wind;
        public EnvironmentGridMetadata Biome;
        public byte BiomeEnabled; // 0 = false, 1 = true
        public FixedString64Bytes MoistureChannelId;
        public FixedString64Bytes TemperatureChannelId;
        public FixedString64Bytes SunlightChannelId;
        public FixedString64Bytes WindChannelId;
        public FixedString64Bytes BiomeChannelId;

        public float MoistureDiffusion;
        public float MoistureSeepage;

        public float BaseSeasonTemperature;
        public float TimeOfDaySwing;
        public float SeasonalSwing;

        public float3 DefaultSunDirection;
        public float DefaultSunIntensity;

        public float2 DefaultWindDirection;
        public float DefaultWindStrength;
    }

    /// <summary>
    /// Blob payload describing per-cell moisture values and auxiliary data.
    /// </summary>
    public struct MoistureGridBlob
    {
        public BlobArray<float> Moisture;        // 0-100 moisture content
        public BlobArray<float> DrainageRate;    // Units per second drained from the cell
        public BlobArray<float> TerrainHeight;   // Terrain height cached for seepage
        public BlobArray<uint> LastRainTick;     // Tick when rainfall last occurred
        public BlobArray<float> EvaporationRate; // Per-cell evaporation scale
    }

    /// <summary>
    /// Runtime per-cell data for the moisture grid. Stored in a dynamic buffer so systems can mutate
    /// moisture values deterministically without reallocating blob data each frame.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct MoistureGridRuntimeCell : IBufferElementData
    {
        public float Moisture;
        public float EvaporationRate;
        public uint LastRainTick;
    }

    /// <summary>
    /// Tracks per-system cadence for the moisture simulation.
    /// </summary>
    public struct MoistureGridSimulationState : IComponentData
    {
        public uint LastEvaporationTick;
        public uint LastSeepageTick;
    }

    /// <summary>
    /// Singleton component providing access to the world moisture map.
    /// </summary>
    public struct MoistureGrid : IComponentData
    {
        public EnvironmentGridMetadata Metadata;
        public BlobAssetReference<MoistureGridBlob> Blob;
        public FixedString64Bytes ChannelId;
        public float DiffusionCoefficient;
        public float SeepageCoefficient;
        public uint LastUpdateTick;
        public uint LastTerrainVersion;

        public readonly bool IsCreated => Blob.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCellIndex(int2 cell) => EnvironmentGridMath.GetCellIndex(Metadata, cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float SampleBilinear(float3 worldPosition, float defaultValue = 0f)
        {
            if (!IsCreated)
            {
                return defaultValue;
            }

            ref var moisture = ref Blob.Value.Moisture;
            return EnvironmentGridMath.SampleBilinear(Metadata, ref moisture, worldPosition, defaultValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void WriteCell(NativeArray<float> values, int2 cell, float value)
        {
            EnvironmentGridMath.WriteCell(values, Metadata, cell, value);
        }
    }

    /// <summary>
    /// Blob payload describing per-cell temperature data.
    /// </summary>
    public struct TemperatureGridBlob
    {
        public BlobArray<float> TemperatureCelsius;
        public BlobArray<float> AltitudeMeters;
    }

    /// <summary>
    /// Singleton component providing the temperature field across the world.
    /// </summary>
    public struct TemperatureGrid : IComponentData
    {
        public EnvironmentGridMetadata Metadata;
        public BlobAssetReference<TemperatureGridBlob> Blob;
        public FixedString64Bytes ChannelId;
        public float BaseSeasonTemperature;
        public float TimeOfDaySwing;
        public float SeasonalSwing;
        public uint LastUpdateTick;
        public uint LastTerrainVersion;

        public readonly bool IsCreated => Blob.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCellIndex(int2 cell) => EnvironmentGridMath.GetCellIndex(Metadata, cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float SampleBilinear(float3 worldPosition, float defaultValue = 0f)
        {
            if (!IsCreated)
            {
                return defaultValue;
            }

            ref var temperature = ref Blob.Value.TemperatureCelsius;
            return EnvironmentGridMath.SampleBilinear(Metadata, ref temperature, worldPosition, defaultValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void WriteCell(NativeArray<float> values, int2 cell, float value)
        {
            EnvironmentGridMath.WriteCell(values, Metadata, cell, value);
        }
    }

    /// <summary>
    /// Sample describing the light contributions within the sunlight grid.
    /// </summary>
    public struct SunlightSample
    {
        public float DirectLight;  // 0-100
        public float AmbientLight; // 0-100
        public ushort OccluderCount;

        public static SunlightSample Lerp(in SunlightSample a, in SunlightSample b, float t)
        {
            var direct = math.lerp(a.DirectLight, b.DirectLight, t);
            var ambient = math.lerp(a.AmbientLight, b.AmbientLight, t);
            var occluders = math.lerp((float)a.OccluderCount, (float)b.OccluderCount, t);

            return new SunlightSample
            {
                DirectLight = direct,
                AmbientLight = ambient,
                OccluderCount = (ushort)math.clamp(math.round(occluders), 0f, ushort.MaxValue)
            };
        }
    }

    /// <summary>
    /// Blob payload describing sunlight samples.
    /// </summary>
    public struct SunlightGridBlob
    {
        public BlobArray<SunlightSample> Samples;
    }

    /// <summary>
    /// Mutable runtime buffer for sunlight samples so environment systems can update light values deterministically.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SunlightGridRuntimeSample : IBufferElementData
    {
        public SunlightSample Value;
    }

    /// <summary>
    /// Singleton component representing sunlight intensity over the terrain.
    /// </summary>
    public struct SunlightGrid : IComponentData
    {
        public EnvironmentGridMetadata Metadata;
        public BlobAssetReference<SunlightGridBlob> Blob;
        public FixedString64Bytes ChannelId;
        public float3 SunDirection; // Normalised world direction
        public float SunIntensity;  // Lux or relative units
        public uint LastUpdateTick;
        public uint LastTerrainVersion;

        public readonly bool IsCreated => Blob.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCellIndex(int2 cell) => EnvironmentGridMath.GetCellIndex(Metadata, cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SunlightSample SampleBilinear(float3 worldPosition, SunlightSample defaultValue = default)
        {
            if (!IsCreated)
            {
                return defaultValue;
            }

            ref var samples = ref Blob.Value.Samples;
            return EnvironmentGridMath.SampleBilinear(Metadata, ref samples, worldPosition, defaultValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void WriteCell(NativeArray<SunlightSample> values, int2 cell, SunlightSample value)
        {
            EnvironmentGridMath.WriteCell(values, Metadata, cell, value);
        }
    }

    /// <summary>
    /// Per-cell wind vector sample storing direction (XZ) and strength.
    /// </summary>
    public struct WindSample
    {
        public float2 Direction; // Normalised XZ
        public float Strength;   // m/s

        public static WindSample Lerp(in WindSample a, in WindSample b, float t)
        {
            var blendedDir = math.lerp(a.Direction, b.Direction, t);
            if (math.lengthsq(blendedDir) > 1e-6f)
            {
                blendedDir = math.normalize(blendedDir);
            }

            return new WindSample
            {
                Direction = blendedDir,
                Strength = math.lerp(a.Strength, b.Strength, t)
            };
        }
    }

    /// <summary>
    /// Blob payload for the wind field.
    /// </summary>
    public struct WindFieldBlob
    {
        public BlobArray<WindSample> Samples;
    }

    /// <summary>
    /// Singleton component representing prevailing and local wind.
    /// </summary>
    public struct WindField : IComponentData
    {
        public EnvironmentGridMetadata Metadata;
        public BlobAssetReference<WindFieldBlob> Blob;
        public FixedString64Bytes ChannelId;
        public float2 GlobalWindDirection; // Normalised XZ
        public float GlobalWindStrength;   // m/s
        public uint LastUpdateTick;
        public uint LastTerrainVersion;

        public readonly bool IsCreated => Blob.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCellIndex(int2 cell) => EnvironmentGridMath.GetCellIndex(Metadata, cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly WindSample SampleBilinear(float3 worldPosition, WindSample defaultValue = default)
        {
            if (!IsCreated)
            {
                return defaultValue;
            }

            ref var samples = ref Blob.Value.Samples;
            return EnvironmentGridMath.SampleBilinear(Metadata, ref samples, worldPosition, defaultValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void WriteCell(NativeArray<WindSample> values, int2 cell, WindSample value)
        {
            EnvironmentGridMath.WriteCell(values, Metadata, cell, value);
        }
    }

    /// <summary>
    /// Blob payload storing biome assignments per cell.
    /// </summary>
    public struct BiomeGridBlob
    {
        public BlobArray<BiomeType> Biomes;
    }

    /// <summary>
    /// Optional biome grid derived from temperature + moisture.
    /// </summary>
    public struct BiomeGrid : IComponentData
    {
        public EnvironmentGridMetadata Metadata;
        public BlobAssetReference<BiomeGridBlob> Blob;
        public FixedString64Bytes ChannelId;
        public uint LastUpdateTick;
        public uint LastTerrainVersion;

        public readonly bool IsCreated => Blob.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCellIndex(int2 cell) => EnvironmentGridMath.GetCellIndex(Metadata, cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BiomeType SampleNearest(float3 worldPosition, BiomeType defaultValue = BiomeType.Unknown)
        {
            if (!IsCreated)
            {
                return defaultValue;
            }

            if (!EnvironmentGridMath.TryWorldToCell(Metadata, worldPosition, out var cell, out _))
            {
                return defaultValue;
            }

            var index = EnvironmentGridMath.GetCellIndex(Metadata, cell);
            return Blob.Value.Biomes[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void WriteCell(NativeArray<BiomeType> values, int2 cell, BiomeType value)
        {
            EnvironmentGridMath.WriteCell(values, Metadata, cell, value);
        }
    }

    /// <summary>
    /// Mutable runtime representation of Biome assignments per cell.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BiomeGridRuntimeCell : IBufferElementData
    {
        public BiomeType Value;
    }

    /// <summary>
    /// Utility math helpers shared by all environment grids.
    /// </summary>
    public static class EnvironmentGridMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCellIndex(in EnvironmentGridMetadata metadata, int2 cell)
        {
            var max = metadata.MaxCellIndex;
            var clamped = math.clamp(cell, new int2(0, 0), max);
            return clamped.y * metadata.Resolution.x + clamped.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetCellCoordinates(in EnvironmentGridMetadata metadata, int index)
        {
            var width = math.max(1, metadata.Resolution.x);
            var y = index / width;
            var x = index - y * width;
            return new int2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetNeighborIndex(in EnvironmentGridMetadata metadata, int index, int2 offset, out int neighborIndex)
        {
            var coords = GetCellCoordinates(metadata, index);
            coords += offset;

            if (coords.x < 0 || coords.y < 0 || coords.x > metadata.MaxCellIndex.x || coords.y > metadata.MaxCellIndex.y)
            {
                neighborIndex = -1;
                return false;
            }

            neighborIndex = GetCellIndex(metadata, coords);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetCellCenter(in EnvironmentGridMetadata metadata, int index)
        {
            var coords = GetCellCoordinates(metadata, index);
            var x = metadata.WorldMin.x + (coords.x + 0.5f) * metadata.CellSize;
            var z = metadata.WorldMin.z + (coords.y + 0.5f) * metadata.CellSize;
            return new float3(x, 0f, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWorldToCell(in EnvironmentGridMetadata metadata, float3 worldPosition, out int2 baseCell, out float2 fractional)
        {
            var local = new float2(
                (worldPosition.x - metadata.WorldMin.x) * metadata.InverseCellSize,
                (worldPosition.z - metadata.WorldMin.z) * metadata.InverseCellSize);

            var maxCoord = new float2(
                math.max(metadata.Resolution.x - math.EPSILON, 0f),
                math.max(metadata.Resolution.y - math.EPSILON, 0f));

            var clampedLocal = math.clamp(local, float2.zero, maxCoord);
            var floor = (int2)math.floor(clampedLocal);
            fractional = clampedLocal - floor;

            baseCell = math.clamp(floor, new int2(0, 0), metadata.MaxCellIndex);
            return metadata.Contains(worldPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleBilinear(in EnvironmentGridMetadata metadata, ref BlobArray<float> values, float3 worldPosition, float defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = math.lerp(c00, c10, frac.x);
            var x1 = math.lerp(c01, c11, frac.x);
            return math.lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleBilinear(in EnvironmentGridMetadata metadata, NativeArray<float> values, float3 worldPosition, float defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = math.lerp(c00, c10, frac.x);
            var x1 = math.lerp(c01, c11, frac.x);
            return math.lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleBilinear(in EnvironmentGridMetadata metadata, NativeArray<MoistureGridRuntimeCell> values, float3 worldPosition, float defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)].Moisture;
            var c10 = values[GetCellIndex(metadata, right)].Moisture;
            var c01 = values[GetCellIndex(metadata, up)].Moisture;
            var c11 = values[GetCellIndex(metadata, upRight)].Moisture;

            var x0 = math.lerp(c00, c10, frac.x);
            var x1 = math.lerp(c01, c11, frac.x);
            return math.lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SunlightSample SampleBilinear(in EnvironmentGridMetadata metadata, ref BlobArray<SunlightSample> values, float3 worldPosition, SunlightSample defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = SunlightSample.Lerp(c00, c10, frac.x);
            var x1 = SunlightSample.Lerp(c01, c11, frac.x);
            return SunlightSample.Lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SunlightSample SampleBilinear(in EnvironmentGridMetadata metadata, NativeArray<SunlightSample> values, float3 worldPosition, SunlightSample defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = SunlightSample.Lerp(c00, c10, frac.x);
            var x1 = SunlightSample.Lerp(c01, c11, frac.x);
            return SunlightSample.Lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SampleBilinearVector(in EnvironmentGridMetadata metadata, NativeArray<float3> values, float3 worldPosition, float3 defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = math.lerp(c00, c10, frac.x);
            var x1 = math.lerp(c01, c11, frac.x);
            return math.lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WindSample SampleBilinear(in EnvironmentGridMetadata metadata, ref BlobArray<WindSample> values, float3 worldPosition, WindSample defaultValue)
        {
            if (!TryWorldToCell(metadata, worldPosition, out var baseCell, out var frac))
            {
                return defaultValue;
            }

            var right = baseCell + new int2(1, 0);
            var up = baseCell + new int2(0, 1);
            var upRight = baseCell + new int2(1, 1);

            var c00 = values[GetCellIndex(metadata, baseCell)];
            var c10 = values[GetCellIndex(metadata, right)];
            var c01 = values[GetCellIndex(metadata, up)];
            var c11 = values[GetCellIndex(metadata, upRight)];

            var x0 = WindSample.Lerp(c00, c10, frac.x);
            var x1 = WindSample.Lerp(c01, c11, frac.x);
            return WindSample.Lerp(x0, x1, frac.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCell(NativeArray<float> values, in EnvironmentGridMetadata metadata, int2 cell, float value)
        {
            values[GetCellIndex(metadata, cell)] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCell(NativeArray<SunlightSample> values, in EnvironmentGridMetadata metadata, int2 cell, SunlightSample value)
        {
            values[GetCellIndex(metadata, cell)] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCell(NativeArray<WindSample> values, in EnvironmentGridMetadata metadata, int2 cell, WindSample value)
        {
            values[GetCellIndex(metadata, cell)] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCell(NativeArray<BiomeType> values, in EnvironmentGridMetadata metadata, int2 cell, BiomeType value)
        {
            values[GetCellIndex(metadata, cell)] = value;
        }
    }

    /// <summary>
    /// Accumulated additive contribution for scalar environment channels.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentScalarContribution : IBufferElementData
    {
        public float Value;
    }

    /// <summary>
    /// Accumulated additive contribution for vector environment channels.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentVectorContribution : IBufferElementData
    {
        public float3 Value;
    }

    /// <summary>
    /// Runtime event pulse emitted by environment effects (e.g., storms, radiation bursts).
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentEventPulse : IBufferElementData
    {
        public FixedString64Bytes EffectId;
        public FixedString64Bytes ChannelId;
        public float Intensity;
        public uint Tick;
    }
}

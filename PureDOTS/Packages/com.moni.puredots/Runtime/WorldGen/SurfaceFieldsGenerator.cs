using PureDOTS.Runtime.WorldGen.Domain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace PureDOTS.Runtime.WorldGen
{
    public static class SurfaceFieldsGenerator
    {
        public struct Settings
        {
            public float SeaLevel01;

            public float HeightFrequency;
            public float HeightWarpFrequency;
            public float HeightWarpAmplitude;
            public float RidgeFrequency;
            public float RidgeStrength;
            public float ConstraintHeightStrength;
            public float ConstraintOceanStrength;
            public float ConstraintRidgeStrength;

            public float TempNoiseFrequency;
            public float TempNoiseStrength;
            public float ElevationTempLapse;

            public float MoistureNoiseFrequency;
            public float MoistureNoiseStrength;
            public float MoistureFromWaterStrength;
        }

        public static Settings DefaultSettings => new()
        {
            SeaLevel01 = 0.5f,
            HeightFrequency = 0.01f,
            HeightWarpFrequency = 0.02f,
            HeightWarpAmplitude = 2f,
            RidgeFrequency = 0.04f,
            RidgeStrength = 0.35f,
            ConstraintHeightStrength = 0f,
            ConstraintOceanStrength = 0f,
            ConstraintRidgeStrength = 0f,
            TempNoiseFrequency = 0.01f,
            TempNoiseStrength = 0.15f,
            ElevationTempLapse = 0.5f,
            MoistureNoiseFrequency = 0.01f,
            MoistureNoiseStrength = 0.2f,
            MoistureFromWaterStrength = 0.35f
        };

        public static Settings SettingsFromStage(ref WorldGenStageBlob stage)
        {
            var settings = DefaultSettings;
            settings.SeaLevel01 = GetFloat(ref stage, "sea_level", settings.SeaLevel01);
            settings.HeightFrequency = GetFloat(ref stage, "height_frequency", settings.HeightFrequency);
            settings.HeightWarpFrequency = GetFloat(ref stage, "height_warp_frequency", settings.HeightWarpFrequency);
            settings.HeightWarpAmplitude = GetFloat(ref stage, "height_warp_amplitude", settings.HeightWarpAmplitude);
            settings.RidgeFrequency = GetFloat(ref stage, "ridge_frequency", settings.RidgeFrequency);
            settings.RidgeStrength = GetFloat(ref stage, "ridge_strength", settings.RidgeStrength);
            settings.ConstraintHeightStrength = GetFloat(ref stage, "constraint_height_strength", settings.ConstraintHeightStrength);
            settings.ConstraintOceanStrength = GetFloat(ref stage, "constraint_ocean_strength", settings.ConstraintOceanStrength);
            settings.ConstraintRidgeStrength = GetFloat(ref stage, "constraint_ridge_strength", settings.ConstraintRidgeStrength);
            settings.TempNoiseFrequency = GetFloat(ref stage, "temp_noise_frequency", settings.TempNoiseFrequency);
            settings.TempNoiseStrength = GetFloat(ref stage, "temp_noise_strength", settings.TempNoiseStrength);
            settings.ElevationTempLapse = GetFloat(ref stage, "elevation_temp_lapse", settings.ElevationTempLapse);
            settings.MoistureNoiseFrequency = GetFloat(ref stage, "moisture_noise_frequency", settings.MoistureNoiseFrequency);
            settings.MoistureNoiseStrength = GetFloat(ref stage, "moisture_noise_strength", settings.MoistureNoiseStrength);
            settings.MoistureFromWaterStrength = GetFloat(ref stage, "moisture_from_water_strength", settings.MoistureFromWaterStrength);
            return settings;
        }

        public static BlobAssetReference<SurfaceFieldsChunkBlob> GenerateChunk<TDomain>(
            ref WorldRecipeBlob recipe,
            ref WorldGenStageBlob stage,
            uint stageIndex,
            ref WorldGenDefinitionsBlob definitions,
            in TDomain domain,
            int3 chunkCoord,
            Allocator allocator)
            where TDomain : struct, IWorldDomainProvider
        {
            var settings = SettingsFromStage(ref stage);
            return GenerateChunk(ref recipe, ref stage, stageIndex, ref definitions, in domain, in settings, default, chunkCoord, allocator);
        }

        public static BlobAssetReference<SurfaceFieldsChunkBlob> GenerateChunk<TDomain>(
            ref WorldRecipeBlob recipe,
            ref WorldGenStageBlob stage,
            uint stageIndex,
            ref WorldGenDefinitionsBlob definitions,
            in TDomain domain,
            in SurfaceConstraintMapSampler constraints,
            int3 chunkCoord,
            Allocator allocator)
            where TDomain : struct, IWorldDomainProvider
        {
            var settings = SettingsFromStage(ref stage);
            return GenerateChunk(ref recipe, ref stage, stageIndex, ref definitions, in domain, in settings, constraints, chunkCoord, allocator);
        }

        public static BlobAssetReference<SurfaceFieldsChunkBlob> GenerateChunk<TDomain>(
            ref WorldRecipeBlob recipe,
            ref WorldGenStageBlob stage,
            uint stageIndex,
            ref WorldGenDefinitionsBlob definitions,
            in TDomain domain,
            in Settings settings,
            in SurfaceConstraintMapSampler constraints,
            int3 chunkCoord,
            Allocator allocator)
            where TDomain : struct, IWorldDomainProvider
        {
            var cells = domain.CellsPerChunk;
            var vertexStride = cells.x + 1;
            var vertexCount = (cells.x + 1) * (cells.y + 1);
            var cellCount = cells.x * cells.y;

            var stageSeed = WorldGenRng.ComputeStageSeed(recipe.WorldSeed, stage.Kind, stageIndex, stage.SeedSalt, 17);
            var rnd = WorldGenRng.CreateRandom(stageSeed);
            var heightOffset = rnd.NextFloat2(-10000f, 10000f);
            var warpOffsetA = rnd.NextFloat2(-10000f, 10000f);
            var warpOffsetB = rnd.NextFloat2(-10000f, 10000f);
            var ridgeOffset = rnd.NextFloat2(-10000f, 10000f);
            var tempOffset = rnd.NextFloat2(-10000f, 10000f);
            var moistureOffset = rnd.NextFloat2(-10000f, 10000f);

            var oceanBiomeIndex = FindBiomeIndex(ref definitions, "ocean");
            if (oceanBiomeIndex == ushort.MaxValue)
            {
                oceanBiomeIndex = FindBiomeIndex(ref definitions, "water");
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SurfaceFieldsChunkBlob>();
            root.SchemaVersion = WorldGenSchema.SurfaceFieldsChunkSchemaVersion;
            root.ChunkCoord = chunkCoord;
            root.CellsPerChunk = cells;
            root.CellSize = domain.CellSize;

            var heightQ = builder.Allocate(ref root.HeightQ, vertexCount);
            var tempQ = builder.Allocate(ref root.TempQ, vertexCount);
            var moistureQ = builder.Allocate(ref root.MoistureQ, vertexCount);
            var cellData = builder.Allocate(ref root.Cells, cellCount);

            ulong quantHash = SurfaceFieldsHash.Begin();

            ushort heightMin = ushort.MaxValue;
            ushort heightMax = 0;
            ulong heightSum = 0;

            byte tempMin = byte.MaxValue;
            byte tempMax = 0;
            ulong tempSum = 0;

            byte moistureMin = byte.MaxValue;
            byte moistureMax = 0;
            ulong moistureSum = 0;

            for (int vz = 0; vz <= cells.y; vz++)
            {
                for (int vx = 0; vx <= cells.x; vx++)
                {
                    var vIndex = vx + vz * vertexStride;
                    var wpos = domain.ToWorld(chunkCoord, new int2(vx, vz));

                    var height01 = SampleHeight01(settings, domain, constraints, wpos, heightOffset, warpOffsetA, warpOffsetB, ridgeOffset);
                    var heightQuant = SurfaceFieldsQuantization.QuantizeU16(height01);
                    heightQ[vIndex] = heightQuant;

                    var temp01 = SampleTemp01(settings, domain, wpos, height01, tempOffset);
                    var tempQuant = SurfaceFieldsQuantization.QuantizeU8(temp01);
                    tempQ[vIndex] = tempQuant;

                    var moisture01 = SampleMoisture01(settings, domain, wpos, height01, settings.SeaLevel01, moistureOffset);
                    var moistureQuant = SurfaceFieldsQuantization.QuantizeU8(moisture01);
                    moistureQ[vIndex] = moistureQuant;

                    quantHash = SurfaceFieldsHash.HashU16(quantHash, heightQuant);
                    quantHash = SurfaceFieldsHash.HashByte(quantHash, tempQuant);
                    quantHash = SurfaceFieldsHash.HashByte(quantHash, moistureQuant);

                    heightMin = heightQuant < heightMin ? heightQuant : heightMin;
                    heightMax = heightQuant > heightMax ? heightQuant : heightMax;
                    heightSum += heightQuant;

                    tempMin = tempQuant < tempMin ? tempQuant : tempMin;
                    tempMax = tempQuant > tempMax ? tempQuant : tempMax;
                    tempSum += tempQuant;

                    moistureMin = moistureQuant < moistureMin ? moistureQuant : moistureMin;
                    moistureMax = moistureQuant > moistureMax ? moistureQuant : moistureMax;
                    moistureSum += moistureQuant;
                }
            }

            uint landCells = 0;
            uint waterCells = 0;

            for (int cz = 0; cz < cells.y; cz++)
            {
                for (int cx = 0; cx < cells.x; cx++)
                {
                    var cIndex = cx + cz * cells.x;
                    var h00 = heightQ[cx + cz * vertexStride];
                    var h10 = heightQ[(cx + 1) + cz * vertexStride];
                    var h01 = heightQ[cx + (cz + 1) * vertexStride];
                    var h11 = heightQ[(cx + 1) + (cz + 1) * vertexStride];
                    var cellHeightQ = (ushort)((h00 + h10 + h01 + h11 + 2) >> 2);

                    var t00 = tempQ[cx + cz * vertexStride];
                    var t10 = tempQ[(cx + 1) + cz * vertexStride];
                    var t01 = tempQ[cx + (cz + 1) * vertexStride];
                    var t11 = tempQ[(cx + 1) + (cz + 1) * vertexStride];
                    var cellTempQ = (byte)((t00 + t10 + t01 + t11 + 2) >> 2);

                    var m00 = moistureQ[cx + cz * vertexStride];
                    var m10 = moistureQ[(cx + 1) + cz * vertexStride];
                    var m01 = moistureQ[cx + (cz + 1) * vertexStride];
                    var m11 = moistureQ[(cx + 1) + (cz + 1) * vertexStride];
                    var cellMoistureQ = (byte)((m00 + m10 + m01 + m11 + 2) >> 2);

                    var height01 = SurfaceFieldsQuantization.DequantizeU16(cellHeightQ);
                    var isWater = height01 < settings.SeaLevel01;

                    var waterQ = (byte)0;
                    if (isWater)
                    {
                        var depth01 = math.saturate((settings.SeaLevel01 - height01) / math.max(1e-5f, (1f - settings.SeaLevel01)));
                        waterQ = SurfaceFieldsQuantization.QuantizeU8(depth01);
                        waterCells++;
                    }
                    else
                    {
                        landCells++;
                    }

                    var biomeId = ResolveBiome(ref definitions, cellTempQ, cellMoistureQ);
                    if (isWater && oceanBiomeIndex != ushort.MaxValue)
                    {
                        biomeId = oceanBiomeIndex;
                    }

                    var resourcePotentialQ = (byte)0;
                    if (!isWater)
                    {
                        resourcePotentialQ = SurfaceFieldsQuantization.QuantizeU8(ComputeResourcePotential01(cellTempQ, cellMoistureQ, height01));
                    }

                    cellData[cIndex] = new SurfaceFieldsCellBlob
                    {
                        WaterQ = waterQ,
                        ResourcePotentialQ = resourcePotentialQ,
                        BiomeId = biomeId
                    };

                    quantHash = SurfaceFieldsHash.HashByte(quantHash, waterQ);
                    quantHash = SurfaceFieldsHash.HashByte(quantHash, resourcePotentialQ);
                    quantHash = SurfaceFieldsHash.HashU16(quantHash, biomeId);
                }
            }

            root.Summary = new SurfaceFieldsSummaryBlob
            {
                HeightMinQ = heightMin,
                HeightMaxQ = heightMax,
                HeightMeanQ = (ushort)(heightSum / (ulong)vertexCount),
                TempMinQ = tempMin,
                TempMaxQ = tempMax,
                TempMeanQ = (byte)(tempSum / (ulong)vertexCount),
                MoistureMinQ = moistureMin,
                MoistureMaxQ = moistureMax,
                MoistureMeanQ = (byte)(moistureSum / (ulong)vertexCount),
                LandCellCount = landCells,
                WaterCellCount = waterCells
            };

            root.QuantizedHash = quantHash;
            return builder.CreateBlobAssetReference<SurfaceFieldsChunkBlob>(allocator);
        }

        private static float SampleHeight01<TDomain>(
            in Settings settings,
            in TDomain domain,
            in SurfaceConstraintMapSampler constraints,
            float3 worldPos,
            float2 heightOffset,
            float2 warpOffsetA,
            float2 warpOffsetB,
            float2 ridgeOffset)
            where TDomain : struct, IWorldDomainProvider
        {
            var p = worldPos.xz * settings.HeightFrequency + heightOffset;
            var warpSampleA = snoise(p * settings.HeightWarpFrequency + warpOffsetA);
            var warpSampleB = snoise(p * settings.HeightWarpFrequency + warpOffsetB);
            p += new float2(warpSampleA, warpSampleB) * settings.HeightWarpAmplitude;

            var baseNoise = 0.5f + 0.5f * snoise(p);
            var ridge = 1f - math.abs(snoise(p * settings.RidgeFrequency + ridgeOffset));
            var height01 = baseNoise + ridge * settings.RidgeStrength;

            if (constraints.IsCreated && (settings.ConstraintHeightStrength != 0f || settings.ConstraintOceanStrength != 0f || settings.ConstraintRidgeStrength != 0f))
            {
                var heightBias = constraints.SampleHeightBiasSigned01(worldPos.xz);
                var oceanMask = constraints.SampleOceanMask01(worldPos.xz);
                var ridgeMask = constraints.SampleRidgeMask01(worldPos.xz);

                height01 += heightBias * settings.ConstraintHeightStrength;
                height01 -= oceanMask * settings.ConstraintOceanStrength;
                height01 += ridge * ridgeMask * settings.ConstraintRidgeStrength;
            }

            height01 = math.saturate(height01);
            return height01;
        }

        private static float SampleTemp01<TDomain>(
            in Settings settings,
            in TDomain domain,
            float3 worldPos,
            float height01,
            float2 tempOffset)
            where TDomain : struct, IWorldDomainProvider
        {
            var lat01 = domain.Latitude01(worldPos);
            var latDist = math.abs(lat01 - 0.5f) * 2f;
            var baseTemp = 1f - latDist;
            var noiseTerm = snoise(worldPos.xz * settings.TempNoiseFrequency + tempOffset);
            var temp01 = baseTemp + (noiseTerm * settings.TempNoiseStrength);
            temp01 -= math.max(0f, height01 - settings.SeaLevel01) * settings.ElevationTempLapse;
            return math.saturate(temp01);
        }

        private static float SampleMoisture01<TDomain>(
            in Settings settings,
            in TDomain domain,
            float3 worldPos,
            float height01,
            float seaLevel01,
            float2 moistureOffset)
            where TDomain : struct, IWorldDomainProvider
        {
            var noiseTerm = snoise(worldPos.xz * settings.MoistureNoiseFrequency + moistureOffset);
            var baseMoisture = 0.5f + 0.5f * noiseTerm;

            var waterFactor = math.saturate((seaLevel01 - height01) * 8f);
            var moisture01 = baseMoisture + (waterFactor * settings.MoistureFromWaterStrength);
            return math.saturate(moisture01);
        }

        private static float ComputeResourcePotential01(byte tempQ, byte moistureQ, float height01)
        {
            var temp01 = SurfaceFieldsQuantization.DequantizeU8(tempQ);
            var moisture01 = SurfaceFieldsQuantization.DequantizeU8(moistureQ);
            var tempComfort = 1f - math.abs(temp01 - 0.5f) * 2f;
            var fertility = (0.6f * moisture01) + (0.4f * tempComfort);
            var altitudePenalty = 1f - math.saturate(height01);
            return math.saturate(fertility * (0.7f + 0.3f * altitudePenalty));
        }

        private static ushort ResolveBiome(ref WorldGenDefinitionsBlob definitions, byte tempQ, byte moistureQ)
        {
            if (definitions.Biomes.Length == 0)
            {
                return 0;
            }

            var temp01 = SurfaceFieldsQuantization.DequantizeU8(tempQ);
            var moisture01 = SurfaceFieldsQuantization.DequantizeU8(moistureQ);

            float bestScore = -1f;
            ushort bestIndex = 0;
            for (int i = 0; i < definitions.Biomes.Length; i++)
            {
                ref var biome = ref definitions.Biomes[i];
                if (temp01 < biome.TemperatureMin || temp01 > biome.TemperatureMax) continue;
                if (moisture01 < biome.MoistureMin || moisture01 > biome.MoistureMax) continue;
                var score = biome.Weight;
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = (ushort)i;
            }

            return bestScore < 0f ? (ushort)0 : bestIndex;
        }

        private static ushort FindBiomeIndex(ref WorldGenDefinitionsBlob definitions, string id)
        {
            if (definitions.Biomes.Length == 0 || string.IsNullOrWhiteSpace(id))
            {
                return ushort.MaxValue;
            }

            var key = new FixedString64Bytes(id.Trim().ToLowerInvariant());
            for (int i = 0; i < definitions.Biomes.Length; i++)
            {
                if (definitions.Biomes[i].Id.Equals(key))
                {
                    return (ushort)i;
                }
            }

            return ushort.MaxValue;
        }

        private static float GetFloat(ref WorldGenStageBlob stage, string key, float fallback)
        {
            var keyFs = new FixedString64Bytes(key);
            for (int i = 0; i < stage.Parameters.Length; i++)
            {
                ref var p = ref stage.Parameters[i];
                if (!p.Key.Equals(keyFs)) continue;

                if (p.Type == WorldGenParamType.Float)
                {
                    return p.FloatValue;
                }

                if (p.Type == WorldGenParamType.Int)
                {
                    return p.IntValue;
                }

                if (p.Type == WorldGenParamType.Bool)
                {
                    return p.BoolValue != 0 ? 1f : 0f;
                }
            }

            return fallback;
        }
    }
}

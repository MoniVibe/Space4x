using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.GameplaySystemGroup))]
    public partial struct Space4XAsteroidVolumeGenerateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TerrainWorldConfig>();
            state.RequireForUpdate<Space4XAsteroidVolumeConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var terrainConfig = SystemAPI.GetSingleton<TerrainWorldConfig>();
            if (terrainConfig.VoxelSize <= 0f ||
                terrainConfig.VoxelsPerChunk.x <= 0 ||
                terrainConfig.VoxelsPerChunk.y <= 0 ||
                terrainConfig.VoxelsPerChunk.z <= 0)
            {
                return;
            }

            var chunkSize = new float3(terrainConfig.VoxelsPerChunk) * terrainConfig.VoxelSize;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (volumeConfig, _, entity) in SystemAPI.Query<RefRO<Space4XAsteroidVolumeConfig>, RefRO<LocalTransform>>()
                         .WithAll<Asteroid>()
                         .WithNone<Space4XAsteroidVolumeState>()
                         .WithEntityAccess())
            {
                var radius = math.max(0.1f, volumeConfig.ValueRO.Radius);
                var volumeOrigin = new float3(-radius, -radius, -radius);

                if (SystemAPI.HasComponent<TerrainVolume>(entity))
                {
                    ecb.SetComponent(entity, new TerrainVolume { LocalOrigin = volumeOrigin });
                }
                else
                {
                    ecb.AddComponent(entity, new TerrainVolume { LocalOrigin = volumeOrigin });
                }

                ecb.AddComponent(entity, new Space4XAsteroidVolumeState { Initialized = 1 });

                var diameter = radius * 2f;
                var chunkCounts = new int3(
                    math.max(1, (int)math.ceil(diameter / math.max(1e-4f, chunkSize.x))),
                    math.max(1, (int)math.ceil(diameter / math.max(1e-4f, chunkSize.y))),
                    math.max(1, (int)math.ceil(diameter / math.max(1e-4f, chunkSize.z))));
                var maxCoord = chunkCounts - 1;

                var coreRadius = radius * math.saturate(volumeConfig.ValueRO.CoreRadiusRatio);
                var mantleRadius = radius * math.saturate(volumeConfig.ValueRO.MantleRadiusRatio);
                if (mantleRadius < coreRadius)
                {
                    mantleRadius = coreRadius;
                }

                for (int z = 0; z <= maxCoord.z; z++)
                {
                    for (int y = 0; y <= maxCoord.y; y++)
                    {
                        for (int x = 0; x <= maxCoord.x; x++)
                        {
                            var chunkCoord = new int3(x, y, z);
                            var chunkOrigin = volumeOrigin + new float3(chunkCoord) * chunkSize;
                            if (!SphereIntersectsAabb(radius, chunkOrigin, chunkOrigin + chunkSize))
                            {
                                continue;
                            }
                            var blob = BuildChunkBlob(volumeConfig.ValueRO, radius, coreRadius, mantleRadius,
                                volumeOrigin, chunkCoord, terrainConfig.VoxelsPerChunk, terrainConfig.VoxelSize);

                            var chunkEntity = ecb.CreateEntity();
                            ecb.AddComponent(chunkEntity, new TerrainChunk
                            {
                                ChunkCoord = chunkCoord,
                                VoxelsPerChunk = terrainConfig.VoxelsPerChunk,
                                VolumeEntity = entity,
                                BaseBlob = blob
                            });
                            ecb.AddComponent(chunkEntity, new Parent { Value = entity });
                            ecb.AddComponent(chunkEntity, LocalTransform.FromPositionRotationScale(chunkOrigin, quaternion.identity, 1f));
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static BlobAssetReference<TerrainChunkBlob> BuildChunkBlob(
            in Space4XAsteroidVolumeConfig config,
            float radius,
            float coreRadius,
            float mantleRadius,
            float3 volumeOrigin,
            int3 chunkCoord,
            int3 voxelsPerChunk,
            float voxelSize)
        {
            var voxelCount = voxelsPerChunk.x * voxelsPerChunk.y * voxelsPerChunk.z;
            var chunkSize = new float3(voxelsPerChunk) * voxelSize;
            var chunkOrigin = volumeOrigin + new float3(chunkCoord) * chunkSize;
            var radiusSq = radius * radius;

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkBlob>();
            var solidMask = builder.Allocate(ref root.SolidMask, voxelCount);
            var materialIds = builder.Allocate(ref root.MaterialId, voxelCount);
            var hardness = builder.Allocate(ref root.Hardness, voxelCount);
            var depositIds = builder.Allocate(ref root.DepositId, voxelCount);
            var oreGrades = builder.Allocate(ref root.OreGrade, voxelCount);

            var coreDepositId = config.CoreDepositId;
            var coreOreGrade = config.CoreOreGrade;
            var exponent = math.max(0.1f, config.OreGradeExponent);

            var index = 0;
            for (int z = 0; z < voxelsPerChunk.z; z++)
            {
                for (int y = 0; y < voxelsPerChunk.y; y++)
                {
                    for (int x = 0; x < voxelsPerChunk.x; x++)
                    {
                        var localPos = chunkOrigin + new float3((x + 0.5f) * voxelSize, (y + 0.5f) * voxelSize, (z + 0.5f) * voxelSize);
                        var distSq = math.lengthsq(localPos);
                        if (distSq <= radiusSq)
                        {
                            solidMask[index] = 1;
                            var dist = math.sqrt(distSq);
                            byte materialId;
                            if (dist <= coreRadius)
                            {
                                materialId = config.CoreMaterialId;
                            }
                            else if (dist <= mantleRadius)
                            {
                                materialId = config.MantleMaterialId;
                            }
                            else
                            {
                                materialId = config.CrustMaterialId;
                            }

                            materialIds[index] = materialId;
                            hardness[index] = materialId;

                            if (coreDepositId != 0 && dist <= coreRadius)
                            {
                                var depthRatio = math.saturate(1f - (dist / math.max(0.001f, radius)));
                                var oreFactor = math.pow(depthRatio, exponent);
                                var noise = Hash01(chunkCoord, voxelsPerChunk, x, y, z, config.Seed);
                                var grade = coreOreGrade * oreFactor * math.lerp(0.7f, 1.2f, noise);
                                oreGrades[index] = (byte)math.clamp((int)math.round(grade), 0, 255);
                                depositIds[index] = coreDepositId;
                            }
                            else
                            {
                                oreGrades[index] = 0;
                                depositIds[index] = 0;
                            }
                        }
                        else
                        {
                            solidMask[index] = 0;
                            materialIds[index] = 0;
                            hardness[index] = 0;
                            depositIds[index] = 0;
                            oreGrades[index] = 0;
                        }

                        index++;
                    }
                }
            }

            return builder.CreateBlobAssetReference<TerrainChunkBlob>(Allocator.Persistent);
        }

        private static bool SphereIntersectsAabb(float radius, float3 min, float3 max)
        {
            var clamped = math.clamp(float3.zero, min, max);
            return math.lengthsq(clamped) <= radius * radius;
        }

        private static float Hash01(int3 chunkCoord, int3 voxelsPerChunk, int x, int y, int z, uint seed)
        {
            var global = new int3(
                x + chunkCoord.x * voxelsPerChunk.x,
                y + chunkCoord.y * voxelsPerChunk.y,
                z + chunkCoord.z * voxelsPerChunk.z);
            var hash = math.hash(new int4(global.x, global.y, global.z, (int)seed));
            return (hash & 1023u) / 1023f;
        }
    }
}

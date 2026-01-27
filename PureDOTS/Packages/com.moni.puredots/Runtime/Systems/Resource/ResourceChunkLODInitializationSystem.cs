using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.WorldGen;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Systems.Resource
{
    /// <summary>
    /// System that adds LOD components to resource chunks that don't have them.
    /// Runs during initialization to ensure all resource chunks have LOD support.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ResourceChunkLODInitializationSystem : ISystem
    {
        private EntityQuery _lodQuery;

        public void OnCreate(ref SystemState state)
        {
            // Query for resource chunks without LOD components
            _lodQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ResourceChunkState>()
                .WithNone<PureDOTS.Runtime.Rendering.RenderLODData>());

            state.RequireForUpdate(_lodQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_lodQuery.IsEmpty)
            {
                return;
            }

            int addedCount = 0;

            foreach (var (chunkState, entity) in SystemAPI.Query<RefRO<ResourceChunkState>>()
                .WithNone<PureDOTS.Runtime.Rendering.RenderLODData>()
                .WithEntityAccess())
            {
                ResourceChunkLODHelpers.AddLODComponents(state.EntityManager, entity);
                addedCount++;
            }

            if (addedCount > 0)
            {
                Debug.Log($"[ResourceChunkLODInit] Added LOD components to {addedCount} resource chunks");
            }
        }
    }

    /// <summary>
    /// System that processes thrown resource chunks with ballistic motion.
    /// Updates position based on velocity and gravity, checks for ground collision.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct ResourceChunkBallisticSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;

            var moistureGrid = default(MoistureGrid);
            SystemAPI.TryGetSingleton(out moistureGrid);
            var terrainPlane = default(TerrainHeightPlane);
            SystemAPI.TryGetSingleton(out terrainPlane);
            var flatSurface = default(TerrainFlatSurface);
            SystemAPI.TryGetSingleton(out flatSurface);
            var solidSphere = default(TerrainSolidSphere);
            SystemAPI.TryGetSingleton(out solidSphere);
            var terrainConfig = TerrainWorldConfig.Default;
            SystemAPI.TryGetSingleton(out terrainConfig);
            var surfaceDomain = default(SurfaceFieldsDomainConfig);
            SystemAPI.TryGetSingleton(out surfaceDomain);
            var globalTerrainVersion = 0u;
            if (SystemAPI.TryGetSingleton<TerrainVersion>(out var terrainVersion))
            {
                globalTerrainVersion = terrainVersion.Value;
            }

            var surfaceChunks = default(NativeArray<SurfaceFieldsChunkRef>);
            if (SystemAPI.TryGetSingletonEntity<SurfaceFieldsChunkRefCache>(out var surfaceCacheEntity))
            {
                surfaceChunks = SystemAPI.GetBuffer<SurfaceFieldsChunkRef>(surfaceCacheEntity).AsNativeArray();
            }

            var terrainContext = new TerrainQueryContext
            {
                MoistureGrid = moistureGrid,
                HeightPlane = terrainPlane,
                FlatSurface = flatSurface,
                SolidSphere = solidSphere,
                WorldConfig = terrainConfig,
                GlobalTerrainVersion = globalTerrainVersion,
                SurfaceFieldsDomain = surfaceDomain,
                SurfaceFieldsChunks = surfaceChunks,
                VoxelAccessor = default,
                VolumeEntity = Entity.Null,
                VolumeOrigin = terrainConfig.VolumeWorldOrigin,
                VolumeWorldToLocal = float4x4.identity,
                VolumeEnabled = 0
            };

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (motion, collision, chunkState, transform, entity) in
                SystemAPI.Query<RefRW<PureDOTS.Runtime.Physics.BallisticMotion>,
                                RefRW<PureDOTS.Runtime.Physics.GroundCollisionCheck>,
                                RefRW<ResourceChunkState>,
                                RefRW<Unity.Transforms.LocalTransform>>()
                    .WithEntityAccess())
            {
                // Skip if not active
                if ((motion.ValueRO.Flags & PureDOTS.Runtime.Physics.BallisticMotionFlags.Active) == 0)
                {
                    continue;
                }

                // Update position
                var position = transform.ValueRO.Position;
                var velocity = motion.ValueRO.Velocity;

                // Apply gravity
                if ((motion.ValueRO.Flags & PureDOTS.Runtime.Physics.BallisticMotionFlags.UseGravity) != 0)
                {
                    velocity += motion.ValueRO.Gravity * deltaTime;
                }

                position += velocity * deltaTime;
                motion.ValueRW.Velocity = velocity;
                motion.ValueRW.FlightTime += deltaTime;

                // Update chunk velocity for compatibility
                chunkState.ValueRW.Velocity = velocity;

                // Check for ground collision (simple height check)
                float groundHeight = TerrainQueryFacade.SampleHeight(terrainContext, position);
                if (position.y <= groundHeight + collision.ValueRO.HeightOffset)
                {
                    // Landed
                    position.y = groundHeight + collision.ValueRO.HeightOffset;
                    collision.ValueRW.Flags |= PureDOTS.Runtime.Physics.GroundCollisionCheck.FlagHasCollided;

                    // Check if should break
                    float impactSpeed = Unity.Mathematics.math.length(velocity);
                    if (impactSpeed > collision.ValueRO.BreakVelocityThreshold)
                    {
                        collision.ValueRW.Flags |= PureDOTS.Runtime.Physics.GroundCollisionCheck.FlagShouldBreak;
                    }

                    // Stop motion
                    motion.ValueRW.Flags &= ~PureDOTS.Runtime.Physics.BallisticMotionFlags.Active;
                    motion.ValueRW.Velocity = Unity.Mathematics.float3.zero;
                    chunkState.ValueRW.Velocity = Unity.Mathematics.float3.zero;
                    chunkState.ValueRW.Flags &= ~ResourceChunkFlags.Thrown;

                    // Remove ballistic components
                    ecb.RemoveComponent<PureDOTS.Runtime.Physics.BallisticMotion>(entity);
                    ecb.RemoveComponent<PureDOTS.Runtime.Physics.GroundCollisionCheck>(entity);
                }
                else if (motion.ValueRO.FlightTime >= motion.ValueRO.MaxFlightTime)
                {
                    // Exceeded max flight time, force land
                    motion.ValueRW.Flags &= ~PureDOTS.Runtime.Physics.BallisticMotionFlags.Active;
                    chunkState.ValueRW.Flags &= ~ResourceChunkFlags.Thrown;
                    ecb.RemoveComponent<PureDOTS.Runtime.Physics.BallisticMotion>(entity);
                    ecb.RemoveComponent<PureDOTS.Runtime.Physics.GroundCollisionCheck>(entity);
                }

                transform.ValueRW.Position = position;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Centralized scene spawn processor. Instantiates entities based on requests baked from SceneSpawnProfileAsset.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SceneSpawnSystem : ISystem
    {
        private EntityQuery _pendingControllers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _pendingControllers = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SceneSpawnController, LocalTransform>()
                .WithAll<SceneSpawnRequest>()
                .WithNone<SceneSpawnProcessedTag>());

            state.RequireForUpdate(_pendingControllers);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_pendingControllers.IsEmpty)
            {
                return;
            }

            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool hasPresentationQueue = SystemAPI.TryGetSingletonEntity<PresentationCommandQueue>(out var presentationQueueEntity);

            foreach (var (controller, rootTransform, entity) in
                     SystemAPI.Query<RefRO<SceneSpawnController>, RefRO<LocalTransform>>()
                         .WithEntityAccess()
                         .WithNone<SceneSpawnProcessedTag>())
            {
                if (!entityManager.HasBuffer<SceneSpawnRequest>(entity))
                {
                    ecb.AddComponent<SceneSpawnProcessedTag>(entity);
                    continue;
                }

                var requests = entityManager.GetBuffer<SceneSpawnRequest>(entity);
                var pointsBuffer = entityManager.HasBuffer<SceneSpawnPoint>(entity)
                    ? entityManager.GetBuffer<SceneSpawnPoint>(entity)
                    : default;
                var points = pointsBuffer.IsCreated ? pointsBuffer.AsNativeArray() : default;

                uint baseSeed = controller.ValueRO.Seed != 0 ? controller.ValueRO.Seed : (uint)(entity.Index + 1);
                var transform = rootTransform.ValueRO;

                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    if (request.Prefab == Entity.Null || request.Count <= 0)
                    {
                        continue;
                    }

                    uint requestSeed = baseSeed ^ request.SeedOffset ^ (uint)(i + 1);
                    requestSeed = requestSeed == 0 ? (uint)(i + 0x2C9277B5u) : requestSeed;
                    var random = Unity.Mathematics.Random.CreateFromIndex(requestSeed);

                    float prefabScale = 1f;
                    quaternion prefabRotation = quaternion.identity;
                    if (entityManager.HasComponent<LocalTransform>(request.Prefab))
                    {
                        var prefabTransform = entityManager.GetComponentData<LocalTransform>(request.Prefab);
                        prefabScale = prefabTransform.Scale;
                        prefabRotation = prefabTransform.Rotation;
                    }

                    if (request.Placement == SpawnPlacementMode.CustomPoints &&
                        request.CustomPointCount > 0 &&
                        points.IsCreated &&
                        request.CustomPointStart >= 0 &&
                        request.CustomPointStart + request.CustomPointCount <= points.Length)
                    {
                        SpawnFromCustomPoints(ref ecb, in request, ref random, transform, prefabRotation, prefabScale,
                            points, request.CustomPointStart, request.CustomPointCount, presentationQueueEntity, hasPresentationQueue, requestSeed);
                    }
                    else
                    {
                        SpawnByPattern(ref ecb, in request, ref random, transform, prefabRotation, prefabScale, requestSeed, presentationQueueEntity, hasPresentationQueue);
                    }
                }

                ecb.AddComponent<SceneSpawnProcessedTag>(entity);
            }

            ecb.Playback(entityManager);
        }

        private static void SpawnByPattern(ref EntityCommandBuffer ecb,
            in SceneSpawnRequest request,
            ref Unity.Mathematics.Random random,
            in LocalTransform rootTransform,
            quaternion prefabRotation,
            float prefabScale,
            uint requestSeed,
            Entity presentationQueue,
            bool hasPresentationQueue)
        {
            var baseOffset = request.Offset;
            switch (request.Placement)
            {
                case SpawnPlacementMode.Point:
                {
                    for (int index = 0; index < request.Count; index++)
                    {
                        float3 relative = float3.zero;
                        if (request.Count > 1 && request.Radius > 0f)
                        {
                            relative = SampleDisc(ref random, request.Radius);
                        }

                        SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                            baseOffset + relative, relative, index, requestSeed, presentationQueue, hasPresentationQueue);
                    }
                    break;
                }

                case SpawnPlacementMode.RandomCircle:
                {
                    float radius = math.max(request.Radius, 0f);
                    for (int index = 0; index < request.Count; index++)
                    {
                        var relative = SampleDisc(ref random, radius);
                        SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                            baseOffset + relative, relative, index, requestSeed, presentationQueue, hasPresentationQueue);
                    }
                    break;
                }

                case SpawnPlacementMode.Ring:
                {
                    float maxRadius = math.max(request.Radius, 0f);
                    float minRadius = math.clamp(request.InnerRadius, 0f, maxRadius);
                    for (int index = 0; index < request.Count; index++)
                    {
                        float radius = maxRadius > minRadius
                            ? random.NextFloat(minRadius, maxRadius)
                            : maxRadius;
                        var direction = random.NextFloat2Direction();
                        var relative = new float3(direction.x * radius, 0f, direction.y * radius);
                        SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                            baseOffset + relative, relative, index, requestSeed, presentationQueue, hasPresentationQueue);
                    }
                    break;
                }

                case SpawnPlacementMode.Grid:
                {
                    int columns = math.max(1, request.GridDimensions.x);
                    int rows = math.max(1, request.GridDimensions.y);
                    int requiredRows = (request.Count + columns - 1) / columns;
                    rows = math.max(rows, requiredRows);

                    float spacingX = math.max(0.01f, request.GridSpacing.x);
                    float spacingZ = math.max(0.01f, request.GridSpacing.y);

                    float totalWidth = (columns - 1) * spacingX;
                    float totalDepth = (rows - 1) * spacingZ;
                    var origin = baseOffset + new float3(-totalWidth * 0.5f, 0f, -totalDepth * 0.5f);

                    for (int index = 0; index < request.Count; index++)
                    {
                        int col = index % columns;
                        int row = index / columns;
                        var relative = new float3(col * spacingX, 0f, row * spacingZ);
                        SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                            origin + relative, relative + (origin - baseOffset), index, requestSeed, presentationQueue, hasPresentationQueue);
                    }
                    break;
                }

                default:
                {
                    // Fallback to point placement.
                    for (int index = 0; index < request.Count; index++)
                    {
                        SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                            baseOffset, float3.zero, index, requestSeed, presentationQueue, hasPresentationQueue);
                    }
                    break;
                }
            }
        }

        private static void SpawnFromCustomPoints(ref EntityCommandBuffer ecb,
            in SceneSpawnRequest request,
            ref Unity.Mathematics.Random random,
            in LocalTransform rootTransform,
            quaternion prefabRotation,
            float prefabScale,
            NativeArray<SceneSpawnPoint> points,
            int startIndex,
            int count,
            Entity presentationQueue,
            bool hasPresentationQueue,
            uint requestSeed)
        {
            int spawnTotal = math.min(request.Count, count);
            for (int i = 0; i < spawnTotal; i++)
            {
                var point = points[startIndex + i].LocalPoint;
                SpawnInstance(ref ecb, in request, ref random, rootTransform, prefabRotation, prefabScale,
                    request.Offset + point, point, i, requestSeed, presentationQueue, hasPresentationQueue);
            }
        }

        private static void SpawnInstance(ref EntityCommandBuffer ecb,
            in SceneSpawnRequest request,
            ref Unity.Mathematics.Random random,
            in LocalTransform rootTransform,
            quaternion prefabRotation,
            float prefabScale,
            float3 localPosition,
            float3 relativePosition,
            int spawnOrdinal,
            uint requestSeed,
            Entity presentationQueue,
            bool hasPresentationQueue)
        {
            float heightOffset = 0f;
            if (math.abs(request.HeightRange.y - request.HeightRange.x) > 1e-4f)
            {
                float minHeight = math.min(request.HeightRange.x, request.HeightRange.y);
                float maxHeight = math.max(request.HeightRange.x, request.HeightRange.y);
                heightOffset = random.NextFloat(minHeight, maxHeight);
            }
            else
            {
                heightOffset = request.HeightRange.x;
            }

            localPosition.y += heightOffset;

            float3 worldPosition = TransformLocalToWorld(rootTransform, localPosition);
            quaternion worldRotation = ResolveRotation(in request, ref random, prefabRotation, rootTransform.Rotation, relativePosition);

            var instance = ecb.Instantiate(request.Prefab);
            ecb.SetComponent(instance, LocalTransform.FromPositionRotationScale(worldPosition, worldRotation, prefabScale));
            ApplySpawnPayload(ref ecb, request, instance, worldPosition, worldRotation, spawnOrdinal, requestSeed, hasPresentationQueue, presentationQueue);
        }

        private static float3 TransformLocalToWorld(in LocalTransform root, float3 local)
        {
            return root.Position + math.mul(root.Rotation, local * root.Scale);
        }

        private static float3 SampleDisc(ref Unity.Mathematics.Random random, float radius)
        {
            var direction = random.NextFloat2Direction();
            float distance = radius > 0f ? random.NextFloat(0f, radius) : 0f;
            return new float3(direction.x * distance, 0f, direction.y * distance);
        }

        private static quaternion ResolveRotation(in SceneSpawnRequest request,
            ref Unity.Mathematics.Random random,
            quaternion prefabRotation,
            quaternion rootRotation,
            float3 relativePosition)
        {
            quaternion localRotation = prefabRotation;
            switch (request.Rotation)
            {
                case SpawnRotationMode.RandomYaw:
                {
                    float yaw = random.NextFloat(-math.PI, math.PI);
                    localRotation = math.mul(quaternion.RotateY(yaw), localRotation);
                    break;
                }

                case SpawnRotationMode.FixedYaw:
                {
                    localRotation = math.mul(quaternion.RotateY(math.radians(request.FixedYawDegrees)), localRotation);
                    break;
                }

                case SpawnRotationMode.AlignOutward:
                {
                    float3 planar = new float3(relativePosition.x, 0f, relativePosition.z);
                    float3 forward = math.normalizesafe(planar, new float3(0f, 0f, 1f));
                    var outward = quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f));
                    localRotation = math.mul(outward, localRotation);
                    break;
                }
            }

            return math.mul(rootRotation, localRotation);
        }

        private static void ApplySpawnPayload(ref EntityCommandBuffer ecb,
            in SceneSpawnRequest request,
            Entity instance,
            float3 worldPosition,
            quaternion worldRotation,
            int spawnOrdinal,
            uint requestSeed,
            bool hasPresentationQueue,
            Entity presentationQueue)
        {
            if (hasPresentationQueue && request.PresentationDescriptor.IsValid)
            {
                var flags = request.PresentationFlags;
                float3 position = worldPosition;
                quaternion rotation = worldRotation;

                if ((flags & PresentationSpawnFlags.OverrideTransform) != 0)
                {
                    if (math.any(request.PresentationOffset != float3.zero))
                    {
                        position += math.mul(worldRotation, request.PresentationOffset);
                    }

                    rotation = math.mul(worldRotation, request.PresentationRotationOffset);
                }

                float scaleMultiplier = request.PresentationScaleMultiplier > 0f
                    ? request.PresentationScaleMultiplier
                    : 1f;

                uint variantSeed = request.PresentationVariantSeed != 0
                    ? request.PresentationVariantSeed
                    : math.hash(new uint4(requestSeed, (uint)(spawnOrdinal + 1), (uint)request.Count, 0xB5297A4Du));

                ecb.AppendToBuffer(presentationQueue, new PresentationSpawnRequest
                {
                    Target = instance,
                    DescriptorHash = request.PresentationDescriptor,
                    Position = position,
                    Rotation = rotation,
                    ScaleMultiplier = scaleMultiplier,
                    Tint = request.PresentationTint,
                    VariantSeed = variantSeed,
                    Flags = flags
                });
            }

            // Placeholder for future domain-specific initialization (jobs, species, teams, etc.).
            // Keep method in place so future extensions centralize payload handling.
        }
    }
}

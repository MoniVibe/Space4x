using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XEffectRequestConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEffectRequestStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XEffectRequestStream>(out var streamEntity))
            {
                return;
            }

            if (!SystemAPI.HasBuffer<PlayEffectRequest>(streamEntity))
            {
                return;
            }

            var requests = SystemAPI.GetBuffer<PlayEffectRequest>(streamEntity);
            if (requests.Length == 0)
            {
                return;
            }

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            using var existingMap = BuildExistingMap(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                var position = ResolveEffectPosition(request, transformLookup);
                var direction = ResolveEffectDirection(request);
                var key = new EffectKey { EffectId = request.EffectId, Source = request.AttachTo };

                if (existingMap.IsCreated && existingMap.TryGetValue(key, out var existingEntity))
                {
                    if (state.EntityManager.HasComponent<Space4XEffectInstance>(existingEntity))
                    {
                        var instance = state.EntityManager.GetComponentData<Space4XEffectInstance>(existingEntity);
                        instance.Position = position;
                        instance.Direction = direction;
                        instance.Intensity = request.Intensity;
                        instance.Lifetime = math.max(instance.Lifetime, request.Lifetime);
                        instance.Source = request.AttachTo;
                        state.EntityManager.SetComponentData(existingEntity, instance);
                    }

                    if (state.EntityManager.HasComponent<LocalTransform>(existingEntity))
                    {
                        var local = state.EntityManager.GetComponentData<LocalTransform>(existingEntity);
                        local.Position = position;
                        state.EntityManager.SetComponentData(existingEntity, local);
                    }

                    continue;
                }

                var effectEntity = ecb.CreateEntity();
                ecb.AddComponent(effectEntity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                ecb.AddComponent(effectEntity, new EffectId { Id = request.EffectId });
                ecb.AddComponent(effectEntity, new Space4XEffectInstance
                {
                    EffectId = request.EffectId,
                    Source = request.AttachTo,
                    Position = position,
                    Direction = direction,
                    Intensity = request.Intensity,
                    Lifetime = math.max(0.01f, request.Lifetime)
                });
            }

            requests.Clear();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static float3 ResolveEffectPosition(in PlayEffectRequest request, ComponentLookup<LocalTransform> transformLookup)
        {
            if (math.lengthsq(request.Position) > 1e-6f)
            {
                return request.Position;
            }

            if (request.AttachTo != Entity.Null && transformLookup.HasComponent(request.AttachTo))
            {
                return transformLookup[request.AttachTo].Position;
            }

            return float3.zero;
        }

        private static float3 ResolveEffectDirection(in PlayEffectRequest request)
        {
            if (math.lengthsq(request.Direction) > 1e-6f)
            {
                return math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));
            }

            return new float3(0f, 0f, 1f);
        }

        private static NativeParallelHashMap<EffectKey, Entity> BuildExistingMap(ref SystemState state)
        {
            var count = SystemAPI.QueryBuilder().WithAll<Space4XEffectInstance>().Build().CalculateEntityCount();
            if (count <= 0)
            {
                return default;
            }

            var map = new NativeParallelHashMap<EffectKey, Entity>(count, Allocator.Temp);
            foreach (var (instance, entity) in SystemAPI.Query<RefRO<Space4XEffectInstance>>().WithEntityAccess())
            {
                map.TryAdd(new EffectKey { EffectId = instance.ValueRO.EffectId, Source = instance.ValueRO.Source }, entity);
            }

            return map;
        }

        private struct EffectKey : System.IEquatable<EffectKey>
        {
            public FixedString64Bytes EffectId;
            public Entity Source;

            public bool Equals(EffectKey other)
            {
                return EffectId.Equals(other.EffectId) && Source.Equals(other.Source);
            }

            public override int GetHashCode()
            {
                return EffectId.GetHashCode() ^ Source.GetHashCode();
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XEffectRequestConsumeSystem))]
    public partial struct Space4XEffectLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (instance, entity) in SystemAPI.Query<RefRW<Space4XEffectInstance>>().WithEntityAccess())
            {
                var data = instance.ValueRO;
                data.Lifetime -= deltaTime;
                if (data.Lifetime <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                instance.ValueRW = data;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

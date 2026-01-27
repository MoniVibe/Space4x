using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup), OrderFirst = true)]
    public partial class BeginPresentationECBSystem : EntityCommandBufferSystem
    {
    }

    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PresentationCleanupSystem))]
    public partial class EndPresentationECBSystem : EntityCommandBufferSystem
    {
    }

    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(BeginPresentationECBSystem))]
    public partial struct PresentationBridgePlaybackSystem : ISystem
    {
        private ComponentLookup<CompanionPresentation> _companionLookup;
        private ComponentLookup<Presentable> _presentableLookup;
        private EntityQuery _rewindQuery;
        private EntityQuery _bindingQuery;
        private EntityQuery _requestHubQuery;

        public void OnCreate(ref SystemState state)
        {
            _companionLookup = state.GetComponentLookup<CompanionPresentation>();
            _presentableLookup = state.GetComponentLookup<Presentable>();
            state.RequireForUpdate<PresentationRequestHub>();
            state.RequireForUpdate<PresentationRequestFailures>();
            _rewindQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>());
            _bindingQuery = state.GetEntityQuery(ComponentType.ReadOnly<PresentationBindingReference>());
            _requestHubQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PresentationRequestHub>(),
                    ComponentType.ReadWrite<PresentationRequestFailures>(),
                    ComponentType.ReadWrite<PlayEffectRequest>(),
                    ComponentType.ReadWrite<SpawnCompanionRequest>(),
                    ComponentType.ReadWrite<DespawnCompanionRequest>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewind = _rewindQuery.GetSingleton<RewindState>();
                if (rewind.Mode != RewindMode.Record)
                {
                    return;
                }
            }

            var requestEntity = _requestHubQuery.GetSingletonEntity();
            var effectRequests = state.EntityManager.GetBuffer<PlayEffectRequest>(requestEntity);
            var spawnRequests = state.EntityManager.GetBuffer<SpawnCompanionRequest>(requestEntity);
            var despawnRequests = state.EntityManager.GetBuffer<DespawnCompanionRequest>(requestEntity);

            if (effectRequests.Length == 0 && spawnRequests.Length == 0 && despawnRequests.Length == 0)
            {
                return;
            }

            var failureCounts = state.EntityManager.GetComponentData<PresentationRequestFailures>(requestEntity);

            if (_bindingQuery.IsEmptyIgnoreFilter)
            {
                failureCounts.MissingBindings += spawnRequests.Length + effectRequests.Length;
                state.EntityManager.SetComponentData(requestEntity, failureCounts);
                ClearBuffers(effectRequests, spawnRequests, despawnRequests);
                return;
            }

            var bindingRef = _bindingQuery.GetSingleton<PresentationBindingReference>();
            var bridge = PresentationBridgeLocator.TryResolve();
            if (bridge == null)
            {
                failureCounts.MissingBridge += spawnRequests.Length + effectRequests.Length + despawnRequests.Length;
                state.EntityManager.SetComponentData(requestEntity, failureCounts);
                ClearBuffers(effectRequests, spawnRequests, despawnRequests);
                return;
            }

            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var ecb = endEcb.CreateCommandBuffer();
            _companionLookup.Update(ref state);
            _presentableLookup.Update(ref state);

        for (int i = 0; i < spawnRequests.Length; i++)
        {
                var request = spawnRequests[i];
                if (!PresentationBindingUtility.TryGetCompanionBinding(ref bindingRef, request.CompanionId, out var binding))
                {
                    failureCounts.MissingBindings++;
                    continue;
                }

                var style = PresentationBindingUtility.ResolveStyle(binding.Style, request.StyleOverride);
                var attachRule = request.AttachRule != PresentationAttachRule.World
                    ? request.AttachRule
                    : binding.AttachRule;

                if (_companionLookup.HasComponent(request.Target))
                {
                    var existing = _companionLookup[request.Target];
                    bridge.ReleaseHandle(existing.Handle);
                }

            var handle = bridge.SpawnCompanion(binding.Kind, style, request.Position, request.Rotation);
            if (!handle.IsValid)
            {
                failureCounts.FailedPlayback++;
                continue;
            }
            failureCounts.SuccessfulSpawns++;

            var companionComponent = new CompanionPresentation
            {
                CompanionId = request.CompanionId,
                Handle = handle.HandleId,
                    Kind = binding.Kind,
                    Style = style,
                    AttachRule = attachRule,
                    Offset = request.Offset,
                    FollowLerp = math.saturate(request.FollowLerp)
                };

                if (_companionLookup.HasComponent(request.Target))
                {
                    ecb.SetComponent(request.Target, companionComponent);
                }
                else
                {
                    ecb.AddComponent(request.Target, companionComponent);
                }

                if (!_presentableLookup.HasComponent(request.Target))
                {
                    ecb.AddComponent<Presentable>(request.Target);
                }
            }

            for (int i = 0; i < effectRequests.Length; i++)
            {
                var request = effectRequests[i];
                if (!PresentationBindingUtility.TryGetEffectBinding(ref bindingRef, request.EffectId, out var binding))
                {
                    failureCounts.MissingBindings++;
                    continue;
                }

                var style = PresentationBindingUtility.ResolveStyle(binding.Style, request.StyleOverride);
                var lifetime = request.LifetimePolicy != PresentationLifetimePolicy.Timed
                    ? request.LifetimePolicy
                    : binding.Lifetime;
                var attachRule = request.AttachRule != PresentationAttachRule.World
                    ? request.AttachRule
                    : binding.AttachRule;
                float duration = request.DurationSeconds > 0f ? request.DurationSeconds : math.max(0f, binding.DurationSeconds);
            var handle = bridge.PlayEffect(binding.Kind, style, request.Position, request.Rotation);
            if (!handle.IsValid)
            {
                failureCounts.FailedPlayback++;
                continue;
            }
            failureCounts.SuccessfulEffects++;

            var effectEntity = ecb.CreateEntity();
            ecb.AddComponent(effectEntity, new PresentationEffect
            {
                    EffectId = request.EffectId,
                    Handle = handle.HandleId,
                    Kind = binding.Kind,
                    Style = style,
                    Lifetime = lifetime,
                    AttachRule = attachRule,
                    Target = request.Target
                });
                ecb.AddComponent(effectEntity, new PresentationCleanupTag
                {
                    Handle = handle.HandleId,
                    Kind = binding.Kind,
                    SecondsRemaining = duration,
                    Lifetime = lifetime,
                    AttachRule = attachRule,
                    Target = request.Target
                });
            }

            for (int i = 0; i < despawnRequests.Length; i++)
            {
                var request = despawnRequests[i];
                if (!_companionLookup.HasComponent(request.Target))
                {
                    continue;
                }

                var handle = _companionLookup[request.Target];
                bridge.ReleaseHandle(handle.Handle);
                ecb.RemoveComponent<CompanionPresentation>(request.Target);

                if (_presentableLookup.HasComponent(request.Target))
                {
                    ecb.RemoveComponent<Presentable>(request.Target);
                }
            }

            state.EntityManager.SetComponentData(requestEntity, failureCounts);
            ClearBuffers(effectRequests, spawnRequests, despawnRequests);
        }

        private static void ClearBuffers(DynamicBuffer<PlayEffectRequest> effectRequests, DynamicBuffer<SpawnCompanionRequest> spawnRequests, DynamicBuffer<DespawnCompanionRequest> despawnRequests)
        {
            effectRequests.Clear();
            spawnRequests.Clear();
            despawnRequests.Clear();
        }
    }

    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(PresentationBridgePlaybackSystem))]
    [UpdateBefore(typeof(EndPresentationECBSystem))]
    public partial struct PresentationCleanupSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private EntityQuery _rewindQuery;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _rewindQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewind = _rewindQuery.GetSingleton<RewindState>();
                if (rewind.Mode != RewindMode.Record)
                {
                    return;
                }
            }

            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var bridge = PresentationBridgeLocator.TryResolve();
            var ecb = endEcb.CreateCommandBuffer();
            float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            _transformLookup.Update(ref state);

            foreach (var (cleanup, effect, entity) in SystemAPI.Query<RefRW<PresentationCleanupTag>, RefRO<PresentationEffect>>()
                         .WithEntityAccess())
            {
                bool targetMissing = cleanup.ValueRO.Target != Entity.Null && !state.EntityManager.Exists(cleanup.ValueRO.Target);
                if (targetMissing)
                {
                    if (bridge != null)
                    {
                        bridge.ReleaseHandle(effect.ValueRO.Handle);
                    }

                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (bridge != null
                    && cleanup.ValueRO.AttachRule != PresentationAttachRule.World
                    && cleanup.ValueRO.Target != Entity.Null
                    && _transformLookup.HasComponent(cleanup.ValueRO.Target)
                    && bridge.TryGetInstance(effect.ValueRO.Handle, out var instance))
                {
                    var targetTransform = _transformLookup[cleanup.ValueRO.Target];
                    var pos = targetTransform.Position;
                    var rot = targetTransform.Rotation;
                    instance.transform.position = new Vector3(pos.x, pos.y, pos.z);
                    instance.transform.rotation = new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w);
                }

                switch (cleanup.ValueRO.Lifetime)
                {
                    case PresentationLifetimePolicy.Timed:
                    {
                        float remaining = cleanup.ValueRO.SecondsRemaining - deltaTime;
                        if (remaining <= 0f)
                        {
                            if (bridge != null)
                            {
                                bridge.ReleaseHandle(effect.ValueRO.Handle);
                            }

                            ecb.DestroyEntity(entity);
                        }
                        else
                        {
                            cleanup.ValueRW.SecondsRemaining = remaining;
                        }
                        break;
                    }
                    case PresentationLifetimePolicy.UntilRecycle:
                        // Keep alive until explicitly recycled or target disappears.
                        break;
                    case PresentationLifetimePolicy.Manual:
                        // Manual lifetimes are controlled externally; keep the tag for observability.
                        break;
                }
            }
        }
    }
}

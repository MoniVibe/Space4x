using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using PDStructuralChangePresentationSystemGroup = PureDOTS.Systems.StructuralChangePresentationSystemGroup;
using PDUpdatePresentationSystemGroup = PureDOTS.Systems.UpdatePresentationSystemGroup;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PDStructuralChangePresentationSystemGroup))]
    public partial struct Space4XSpectatorRenderProxyEnsureSystem : ISystem
    {
        private static readonly FixedString64Bytes Capital20v20ScenarioId = new FixedString64Bytes("space4x_capital_20_vs_20_supergreen");
        private static readonly FixedString64Bytes Capital100v100ScenarioId = new FixedString64Bytes("space4x_capital_100_vs_100_proper");

        public void OnCreate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSpectatorRenderBridgeConfig>();
            state.RequireForUpdate<RenderPresentationCatalog>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<Space4XSpectatorRenderBridgeConfig>();
            var scenarioSupported = IsScenarioSupported(ref state, config);
            if (config.Enabled == 0 || !scenarioSupported)
            {
                CleanupAllProxies(ref state);
                return;
            }

            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var ecb = endEcb.CreateCommandBuffer();

            CleanupDanglingLinks(ref state, ref ecb);
            CleanupOrphanedProxies(ref state, ref ecb);
            SpawnMissingCarrierProxies(ref state, config, ref ecb);
            SpawnMissingStrikeCraftProxies(ref state, config, ref ecb);
        }

        private static bool IsScenarioSupported(ref SystemState state, in Space4XSpectatorRenderBridgeConfig config)
        {
            if (config.OnlyCapitalBattleScenarios == 0)
            {
                return true;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return false;
            }

            var scenarioId = scenarioInfo.ScenarioId;
            return scenarioId.Equals(Capital20v20ScenarioId) || scenarioId.Equals(Capital100v100ScenarioId);
        }

        private static void CleanupAllProxies(ref SystemState state)
        {
            var em = state.EntityManager;
            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var ecb = endEcb.CreateCommandBuffer();

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RenderProxyLink>>().WithAll<Carrier>().WithEntityAccess())
            {
                ecb.RemoveComponent<RenderProxyLink>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RenderProxyLink>>().WithAll<StrikeCraftProfile>().WithEntityAccess())
            {
                ecb.RemoveComponent<RenderProxyLink>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RenderProxyTag>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            if (em.WorldUnmanaged.IsCreated)
            {
                // no-op branch keeps static analysis happy when no entities exist in world
            }
        }

        private static void CleanupDanglingLinks(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            foreach (var (link, entity) in SystemAPI.Query<RefRO<RenderProxyLink>>().WithAll<Carrier>().WithEntityAccess())
            {
                var proxy = link.ValueRO.ProxyEntity;
                if (proxy == Entity.Null || !em.Exists(proxy) || !em.HasComponent<RenderProxyTag>(proxy))
                {
                    ecb.RemoveComponent<RenderProxyLink>(entity);
                }
            }

            foreach (var (link, entity) in SystemAPI.Query<RefRO<RenderProxyLink>>().WithAll<StrikeCraftProfile>().WithEntityAccess())
            {
                var proxy = link.ValueRO.ProxyEntity;
                if (proxy == Entity.Null || !em.Exists(proxy) || !em.HasComponent<RenderProxyTag>(proxy))
                {
                    ecb.RemoveComponent<RenderProxyLink>(entity);
                }
            }
        }

        private static void CleanupOrphanedProxies(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            foreach (var (source, entity) in SystemAPI.Query<RefRO<RenderProxySource>>().WithAll<RenderProxyTag>().WithEntityAccess())
            {
                var sourceEntity = source.ValueRO.Value;
                var sourceExists = sourceEntity != Entity.Null && em.Exists(sourceEntity);
                var sourceHasTransform = sourceExists && em.HasComponent<LocalTransform>(sourceEntity);
                var sourceIsSupportedType = sourceExists && (em.HasComponent<Carrier>(sourceEntity) || em.HasComponent<StrikeCraftProfile>(sourceEntity));

                if (sourceExists && em.HasComponent<RenderProxyLink>(sourceEntity))
                {
                    var link = em.GetComponentData<RenderProxyLink>(sourceEntity);
                    if (link.ProxyEntity != entity)
                    {
                        ecb.SetComponent(sourceEntity, new RenderProxyLink { ProxyEntity = entity });
                    }
                }

                if (sourceHasTransform && sourceIsSupportedType)
                {
                    continue;
                }

                if (sourceExists && em.HasComponent<RenderProxyLink>(sourceEntity))
                {
                    ecb.RemoveComponent<RenderProxyLink>(sourceEntity);
                }

                ecb.DestroyEntity(entity);
            }
        }

        private static void SpawnMissingCarrierProxies(
            ref SystemState state,
            in Space4XSpectatorRenderBridgeConfig config,
            ref EntityCommandBuffer ecb)
        {
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Carrier>().WithNone<Prefab, RenderProxyLink>().WithEntityAccess())
            {
                SpawnProxyForSource(
                    ref state,
                    entity,
                    transform.ValueRO,
                    SpectatorRenderProxyKind.Capital,
                    config,
                    ref ecb);
            }
        }

        private static void SpawnMissingStrikeCraftProxies(
            ref SystemState state,
            in Space4XSpectatorRenderBridgeConfig config,
            ref EntityCommandBuffer ecb)
        {
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<StrikeCraftProfile>().WithNone<Prefab, RenderProxyLink>().WithEntityAccess())
            {
                SpawnProxyForSource(
                    ref state,
                    entity,
                    transform.ValueRO,
                    SpectatorRenderProxyKind.Fighter,
                    config,
                    ref ecb);
            }
        }

        private static void SpawnProxyForSource(
            ref SystemState state,
            Entity source,
            in LocalTransform sourceTransform,
            SpectatorRenderProxyKind kind,
            in Space4XSpectatorRenderBridgeConfig config,
            ref EntityCommandBuffer ecb)
        {
            var side = ResolveProxySide(source, state.EntityManager);
            var color = side == 0 ? config.Side0Color : config.Side1Color;
            var scale = kind == SpectatorRenderProxyKind.Capital ? config.CapitalScale : config.FighterScale;

            var proxy = ecb.CreateEntity();
            ecb.AddComponent(proxy, new LocalTransform
            {
                Position = sourceTransform.Position,
                Rotation = sourceTransform.Rotation,
                Scale = scale
            });
            ecb.AddComponent(proxy, new LocalToWorld { Value = float4x4.identity });
            ecb.AddComponent(proxy, new RenderProxyTag());
            ecb.AddComponent(proxy, new RenderProxySource { Value = source });
            ecb.AddComponent(proxy, new RenderProxyKind { Value = kind });
            ecb.AddComponent(proxy, new RenderProxySide { Value = side });
            ecb.AddComponent(proxy, new PresentationLayer { Value = PresentationLayerId.Orbital });

            AddRenderComponents(ref ecb, proxy, kind, color, source.Index);
            ecb.AddComponent(source, new RenderProxyLink { ProxyEntity = proxy });
        }

        private static byte ResolveProxySide(Entity source, EntityManager em)
        {
            if (em.HasComponent<ScenarioSide>(source))
            {
                return (byte)(em.GetComponentData<ScenarioSide>(source).Side & 1);
            }

            if (em.HasComponent<StrikeCraftProfile>(source))
            {
                var profile = em.GetComponentData<StrikeCraftProfile>(source);
                if (profile.Carrier != Entity.Null && em.Exists(profile.Carrier) && em.HasComponent<ScenarioSide>(profile.Carrier))
                {
                    return (byte)(em.GetComponentData<ScenarioSide>(profile.Carrier).Side & 1);
                }
            }

            return 0;
        }

        private static void AddRenderComponents(
            ref EntityCommandBuffer ecb,
            Entity entity,
            SpectatorRenderProxyKind kind,
            float4 color,
            int sourceEntityIndex)
        {
            var renderKey = kind == SpectatorRenderProxyKind.Capital
                ? Space4XRenderKeys.Carrier
                : Space4XRenderKeys.StrikeCraft;
            var boundsExtents = kind == SpectatorRenderProxyKind.Capital
                ? new float3(8f, 8f, 8f)
                : new float3(2f, 2f, 2f);

            ecb.AddComponent(entity, new RenderKey
            {
                ArchetypeId = renderKey,
                LOD = 0
            });
            ecb.AddComponent(entity, new RenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
            });
            ecb.AddComponent(entity, new RenderSemanticKey { Value = renderKey });
            ecb.AddComponent(entity, new RenderVariantKey { Value = 0 });

            ecb.AddComponent<RenderThemeOverride>(entity);
            ecb.SetComponentEnabled<RenderThemeOverride>(entity, false);

            ecb.AddComponent<MeshPresenter>(entity);
            ecb.SetComponentEnabled<MeshPresenter>(entity, true);

            ecb.AddComponent<SpritePresenter>(entity);
            ecb.SetComponentEnabled<SpritePresenter>(entity, false);

            ecb.AddComponent<DebugPresenter>(entity);
            ecb.SetComponentEnabled<DebugPresenter>(entity, false);

            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = kind == SpectatorRenderProxyKind.Capital ? 1f : 0.55f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });
            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = 40000f,
                Priority = kind == SpectatorRenderProxyKind.Capital ? (byte)180 : (byte)150
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(sourceEntityIndex, 1024);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 1024,
                ShouldRender = 1
            });

            ecb.AddComponent(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = float3.zero,
                    Extents = boundsExtents
                }
            });
            ecb.AddComponent(entity, new RenderTint { Value = color });
            ecb.AddComponent(entity, new RenderTexSlice { Value = 0 });
            ecb.AddComponent(entity, new RenderUvTransform { Value = new float4(1f, 1f, 0f, 0f) });
        }
    }

    [UpdateInGroup(typeof(PDUpdatePresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationDepthSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct Space4XSpectatorRenderProxySyncSystem : ISystem
    {
        private static readonly FixedString64Bytes Capital20v20ScenarioId = new FixedString64Bytes("space4x_capital_20_vs_20_supergreen");
        private static readonly FixedString64Bytes Capital100v100ScenarioId = new FixedString64Bytes("space4x_capital_100_vs_100_proper");

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ScenarioSide> _scenarioSideLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeCraftLookup;

        public void OnCreate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSpectatorRenderBridgeConfig>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _scenarioSideLookup = state.GetComponentLookup<ScenarioSide>(true);
            _strikeCraftLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<Space4XSpectatorRenderBridgeConfig>();
            if (config.Enabled == 0 || !IsScenarioSupported(config))
            {
                return;
            }

            _transformLookup.Update(ref state);
            _scenarioSideLookup.Update(ref state);
            _strikeCraftLookup.Update(ref state);

            foreach (var (source, kind, proxySide, proxyTransform, tint) in SystemAPI
                         .Query<RefRO<RenderProxySource>, RefRO<RenderProxyKind>, RefRW<RenderProxySide>, RefRW<LocalTransform>, RefRW<RenderTint>>()
                         .WithAll<RenderProxyTag>())
            {
                var sourceEntity = source.ValueRO.Value;
                if (!_transformLookup.HasComponent(sourceEntity))
                {
                    continue;
                }

                var sourceTransform = _transformLookup[sourceEntity];
                var side = ResolveProxySide(sourceEntity);
                proxySide.ValueRW.Value = side;
                tint.ValueRW.Value = side == 0 ? config.Side0Color : config.Side1Color;

                var scale = kind.ValueRO.Value == SpectatorRenderProxyKind.Capital
                    ? config.CapitalScale
                    : config.FighterScale;

                proxyTransform.ValueRW = new LocalTransform
                {
                    Position = sourceTransform.Position,
                    Rotation = sourceTransform.Rotation,
                    Scale = scale
                };
            }
        }

        private bool IsScenarioSupported(in Space4XSpectatorRenderBridgeConfig config)
        {
            if (config.OnlyCapitalBattleScenarios == 0)
            {
                return true;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return false;
            }

            var scenarioId = scenarioInfo.ScenarioId;
            return scenarioId.Equals(Capital20v20ScenarioId) || scenarioId.Equals(Capital100v100ScenarioId);
        }

        private byte ResolveProxySide(Entity source)
        {
            if (_scenarioSideLookup.HasComponent(source))
            {
                return (byte)(_scenarioSideLookup[source].Side & 1);
            }

            if (_strikeCraftLookup.HasComponent(source))
            {
                var carrier = _strikeCraftLookup[source].Carrier;
                if (carrier != Entity.Null && _scenarioSideLookup.HasComponent(carrier))
                {
                    return (byte)(_scenarioSideLookup[carrier].Side & 1);
                }
            }

            return 0;
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Rendering;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Spawns and updates ghost entities during rewind preview phases.
    /// Ghosts show historical positions/states while the real world stays frozen.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(RewindControlSystem))]
    public partial struct GhostSpawnSystem : ISystem
    {
        private EntityQuery _ghostQuery;
        private EntityQuery _tetherQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private const float TetherThickness = 0.04f;
        private const float TetherMinLength = 0.2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindControlState>();
            _ghostQuery = state.GetEntityQuery(ComponentType.ReadOnly<GhostTag>());
            _tetherQuery = state.GetEntityQuery(ComponentType.ReadOnly<GhostTetherTag>());
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                CleanupGhosts(ref state);
                return;
            }

            var controlState = SystemAPI.GetSingleton<RewindControlState>();

            // Only spawn/update ghosts during preview phases
            if (controlState.Phase != RewindPhase.ScrubbingPreview &&
                controlState.Phase != RewindPhase.FrozenPreview)
            {
                // Clean up any existing ghosts when not in preview
                CleanupGhosts(ref state);
                return;
            }

            int previewTick = math.max(0, controlState.PreviewTick);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int ghostCount = _ghostQuery.CalculateEntityCount();
            int tetherCount = _tetherQuery.CalculateEntityCount();
            var ghostSources = new NativeParallelHashSet<Entity>(math.max(ghostCount, 1), Allocator.Temp);
            var ghostLinks = new NativeList<GhostLink>(math.max(ghostCount, 1), Allocator.Temp);
            var tetherGhosts = new NativeParallelHashSet<Entity>(math.max(tetherCount, 1), Allocator.Temp);

            _localTransformLookup.Update(ref state);

            // Validate existing ghosts, update preview position, and track active sources.
            foreach (var (ghostSource, ghostPreviewTick, ghostTransform, ghostEntity) in SystemAPI
                         .Query<RefRO<GhostSourceEntity>, RefRW<GhostPreviewTick>, RefRW<LocalTransform>>()
                         .WithAll<GhostTag>()
                         .WithEntityAccess())
            {
                var sourceEntity = ghostSource.ValueRO.SourceEntity;
                if (sourceEntity == Entity.Null ||
                    !state.EntityManager.Exists(sourceEntity) ||
                    !state.EntityManager.HasComponent<RenderSemanticKey>(sourceEntity) ||
                    !state.EntityManager.HasBuffer<ComponentHistory<LocalTransform>>(sourceEntity))
                {
                    ecb.DestroyEntity(ghostEntity);
                    continue;
                }

                ghostSources.Add(sourceEntity);
                ghostLinks.Add(new GhostLink { GhostEntity = ghostEntity, SourceEntity = sourceEntity });

                if (ghostPreviewTick.ValueRO.Tick != previewTick)
                {
                    UpdateGhostPosition(ref state, sourceEntity, previewTick, ref ghostTransform.ValueRW);
                    ghostPreviewTick.ValueRW.Tick = previewTick;
                }
            }

            // Update existing tethers and track which ghosts are already linked.
            foreach (var (link, tetherTransform, tetherEntity) in SystemAPI
                         .Query<RefRO<GhostTetherLink>, RefRW<LocalTransform>>()
                         .WithAll<GhostTetherTag>()
                         .WithEntityAccess())
            {
                var ghostEntity = link.ValueRO.GhostEntity;
                var sourceEntity = link.ValueRO.SourceEntity;
                if (ghostEntity == Entity.Null || sourceEntity == Entity.Null ||
                    !state.EntityManager.Exists(ghostEntity) || !state.EntityManager.Exists(sourceEntity) ||
                    !_localTransformLookup.HasComponent(ghostEntity) || !_localTransformLookup.HasComponent(sourceEntity))
                {
                    ecb.DestroyEntity(tetherEntity);
                    continue;
                }

                tetherGhosts.Add(ghostEntity);
                var ghostTransform = _localTransformLookup[ghostEntity];
                var sourceTransform = _localTransformLookup[sourceEntity];
                UpdateTetherTransform(in sourceTransform, in ghostTransform, ref tetherTransform.ValueRW, ref state, tetherEntity, ecb);
            }

            // Ensure every existing ghost has a tether.
            for (int i = 0; i < ghostLinks.Length; i++)
            {
                var link = ghostLinks[i];
                if (tetherGhosts.Contains(link.GhostEntity))
                {
                    continue;
                }

                if (!_localTransformLookup.HasComponent(link.GhostEntity) ||
                    !_localTransformLookup.HasComponent(link.SourceEntity))
                {
                    continue;
                }

                var ghostTransform = _localTransformLookup[link.GhostEntity];
                var sourceTransform = _localTransformLookup[link.SourceEntity];
                SpawnTether(ref state, link.GhostEntity, link.SourceEntity, in sourceTransform, in ghostTransform, ecb);
            }

            // Spawn ghosts for entities that are rewindable, have history, and no ghost yet.
            foreach (var (transform, historyBuffer, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, DynamicBuffer<ComponentHistory<LocalTransform>>>()
                         .WithAll<RewindableTag>()
                         .WithNone<GhostTag>()
                         .WithEntityAccess())
            {
                if (ghostSources.Contains(entity))
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<RenderSemanticKey>(entity))
                {
                    continue;
                }

                var historyBufferLocal = historyBuffer;
                SpawnGhost(ref state, entity, previewTick, ref historyBufferLocal, transform.ValueRO, ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ghostSources.Dispose();
            ghostLinks.Dispose();
            tetherGhosts.Dispose();
        }

        private void SpawnGhost(ref SystemState state, Entity sourceEntity, int previewTick, ref DynamicBuffer<ComponentHistory<LocalTransform>> historyBuffer, in LocalTransform fallbackTransform, EntityCommandBuffer ecb)
        {
            var ghostEntity = ecb.CreateEntity();
            var ghostStyle = DefaultGhostStyle();
            var ghostTransform = ResolveGhostTransform(ref historyBuffer, previewTick, fallbackTransform);

            ecb.AddComponent(ghostEntity, ghostTransform);
            ecb.AddComponent(ghostEntity, new LocalToWorld { Value = float4x4.identity });
            ecb.AddComponent(ghostEntity, new GhostTag());
            ecb.AddComponent(ghostEntity, new GhostSourceEntity { SourceEntity = sourceEntity });
            ecb.AddComponent(ghostEntity, new GhostPreviewTick { Tick = previewTick });
            ecb.AddComponent(ghostEntity, ghostStyle);
            ecb.AddComponent(ghostEntity, new RenderOwner { Owner = sourceEntity });

            CopyPresentationComponents(ref state, sourceEntity, ghostEntity, ghostStyle, ecb);
            SpawnTether(ref state, ghostEntity, sourceEntity, in fallbackTransform, in ghostTransform, ecb);
        }

        private void UpdateGhostPosition(ref SystemState state, Entity sourceEntity, int previewTick, ref LocalTransform ghostTransform)
        {
            if (!state.EntityManager.HasBuffer<ComponentHistory<LocalTransform>>(sourceEntity))
            {
                return;
            }

            var historyBuffer = state.EntityManager.GetBuffer<ComponentHistory<LocalTransform>>(sourceEntity);
            var updatedTransform = ghostTransform;
            if (TimeHistoryPlaybackSystem.TryGetInterpolatedSample(ref historyBuffer, (uint)previewTick, ref updatedTransform))
            {
                ghostTransform = updatedTransform;
            }
            else if (state.EntityManager.HasComponent<LocalTransform>(sourceEntity))
            {
                ghostTransform = state.EntityManager.GetComponentData<LocalTransform>(sourceEntity);
            }
        }

        private void SpawnTether(ref SystemState state, Entity ghostEntity, Entity sourceEntity, in LocalTransform sourceTransform, in LocalTransform ghostTransform, EntityCommandBuffer ecb)
        {
            var tetherEntity = ecb.CreateEntity();
            var tetherStyle = DefaultTetherStyle();
            ResolveTetherTransform(in sourceTransform, in ghostTransform, out var tetherTransform, out var tetherPostTransform);

            ecb.AddComponent(tetherEntity, tetherTransform);
            ecb.AddComponent(tetherEntity, new LocalToWorld { Value = float4x4.identity });
            ecb.AddComponent(tetherEntity, tetherPostTransform);
            ecb.AddComponent(tetherEntity, new GhostTetherTag());
            ecb.AddComponent(tetherEntity, new GhostTetherLink
            {
                GhostEntity = ghostEntity,
                SourceEntity = sourceEntity
            });
            ecb.AddComponent(tetherEntity, new RenderOwner { Owner = sourceEntity });

            CopyPresentationComponents(ref state, sourceEntity, tetherEntity, tetherStyle, ecb);
            ApplyTetherPresentationOverrides(ref state, sourceEntity, tetherEntity, ecb);
        }

        private void UpdateTetherTransform(in LocalTransform sourceTransform, in LocalTransform ghostTransform, ref LocalTransform tetherTransform, ref SystemState state, Entity tetherEntity, EntityCommandBuffer ecb)
        {
            ResolveTetherTransform(in sourceTransform, in ghostTransform, out tetherTransform, out var tetherPostTransform);

            if (state.EntityManager.HasComponent<PostTransformMatrix>(tetherEntity))
            {
                ecb.SetComponent(tetherEntity, tetherPostTransform);
            }
            else
            {
                ecb.AddComponent(tetherEntity, tetherPostTransform);
            }
        }

        private void CleanupGhosts(ref SystemState state)
        {
            // Remove all ghost entities when not in preview phase
            if (_ghostQuery.IsEmptyIgnoreFilter && _tetherQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (!_ghostQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _ghostQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    ecb.DestroyEntity(entities[i]);
                }
            }

            if (!_tetherQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _tetherQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    ecb.DestroyEntity(entities[i]);
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static GhostVisualStyle DefaultGhostStyle()
        {
            return new GhostVisualStyle
            {
                Opacity = 0.35f,
                ColorTint = new float4(0.55f, 0.8f, 1f, 1f)
            };
        }

        private static GhostVisualStyle DefaultTetherStyle()
        {
            return new GhostVisualStyle
            {
                Opacity = 0.2f,
                ColorTint = new float4(0.35f, 0.6f, 0.85f, 1f)
            };
        }

        private static void ResolveTetherTransform(in LocalTransform sourceTransform, in LocalTransform ghostTransform, out LocalTransform tetherTransform, out PostTransformMatrix tetherPostTransform)
        {
            var delta = ghostTransform.Position - sourceTransform.Position;
            var length = math.max(math.length(delta), TetherMinLength);
            var direction = math.normalizesafe(delta, new float3(0f, 0f, 1f));
            var rotation = quaternion.LookRotationSafe(direction, math.up());
            var midpoint = sourceTransform.Position + delta * 0.5f;

            tetherTransform = new LocalTransform
            {
                Position = midpoint,
                Rotation = rotation,
                Scale = 1f
            };

            tetherPostTransform = new PostTransformMatrix
            {
                Value = float4x4.Scale(new float3(TetherThickness, TetherThickness, length))
            };
        }

        private static LocalTransform ResolveGhostTransform(ref DynamicBuffer<ComponentHistory<LocalTransform>> historyBuffer, int previewTick, in LocalTransform fallbackTransform)
        {
            var resolved = fallbackTransform;
            if (historyBuffer.Length == 0)
            {
                return resolved;
            }

            if (TimeHistoryPlaybackSystem.TryGetInterpolatedSample(ref historyBuffer, (uint)previewTick, ref resolved))
            {
                return resolved;
            }

            return resolved;
        }

        private static float4 ResolveGhostTint(in float4 baseTint, in GhostVisualStyle style)
        {
            var blend = math.saturate(style.Opacity);
            var tinted = math.lerp(baseTint, style.ColorTint, 0.55f);
            tinted.w = math.min(baseTint.w, blend);
            return tinted;
        }

        private void CopyPresentationComponents(ref SystemState state, Entity sourceEntity, Entity ghostEntity, in GhostVisualStyle style, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            if (em.HasComponent<RenderSemanticKey>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderSemanticKey>(sourceEntity));
            }

            var variantKey = em.HasComponent<RenderVariantKey>(sourceEntity)
                ? em.GetComponentData<RenderVariantKey>(sourceEntity)
                : new RenderVariantKey { Value = 0 };
            ecb.AddComponent(ghostEntity, variantKey);

            if (em.HasComponent<RenderKey>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderKey>(sourceEntity));
            }

            var flags = em.HasComponent<RenderFlags>(sourceEntity)
                ? em.GetComponentData<RenderFlags>(sourceEntity)
                : new RenderFlags { Visible = 1, ShadowCaster = 0, HighlightMask = 0 };
            flags.Visible = 1;
            flags.ShadowCaster = 0;
            ecb.AddComponent(ghostEntity, flags);

            var baseTint = em.HasComponent<RenderTint>(sourceEntity)
                ? em.GetComponentData<RenderTint>(sourceEntity).Value
                : new float4(1f, 1f, 1f, 1f);
            ecb.AddComponent(ghostEntity, new RenderTint { Value = ResolveGhostTint(baseTint, style) });

            if (em.HasComponent<RenderTexSlice>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderTexSlice>(sourceEntity));
            }

            if (em.HasComponent<RenderUvTransform>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderUvTransform>(sourceEntity));
            }

            if (em.HasComponent<RenderThemeOverride>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderThemeOverride>(sourceEntity));
                ecb.SetComponentEnabled<RenderThemeOverride>(ghostEntity, em.IsComponentEnabled<RenderThemeOverride>(sourceEntity));
            }

            if (em.HasComponent<RenderVariantOverride>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderVariantOverride>(sourceEntity));
                ecb.SetComponentEnabled<RenderVariantOverride>(ghostEntity, em.IsComponentEnabled<RenderVariantOverride>(sourceEntity));
            }

            CopyEnableablePresenter<MeshPresenter>(ref state, sourceEntity, ghostEntity, ecb);
            CopyEnableablePresenter<SpritePresenter>(ref state, sourceEntity, ghostEntity, ecb);
            CopyEnableablePresenter<DebugPresenter>(ref state, sourceEntity, ghostEntity, ecb);
            CopyEnableablePresenter<TracerPresenter>(ref state, sourceEntity, ghostEntity, ecb);

            if (em.HasComponent<ProjectileVisual>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<ProjectileVisual>(sourceEntity));
            }

            if (em.HasComponent<TracerShapeProperty>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<TracerShapeProperty>(sourceEntity));
            }

            if (em.HasComponent<TracerColorProperty>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<TracerColorProperty>(sourceEntity));
            }

            if (em.HasComponent<RenderLODData>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderLODData>(sourceEntity));
            }

            if (em.HasComponent<RenderCullable>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderCullable>(sourceEntity));
            }

            if (em.HasComponent<RenderSampleIndex>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<RenderSampleIndex>(sourceEntity));
            }

            if (em.HasComponent<PresentationScaleMultiplier>(sourceEntity))
            {
                ecb.AddComponent(ghostEntity, em.GetComponentData<PresentationScaleMultiplier>(sourceEntity));
            }
        }

        private void ApplyTetherPresentationOverrides(ref SystemState state, Entity sourceEntity, Entity tetherEntity, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            var tetherSemantic = new RenderSemanticKey { Value = RenderPresentationConstants.GhostTetherSemanticKey };
            if (em.HasComponent<RenderSemanticKey>(sourceEntity))
            {
                ecb.SetComponent(tetherEntity, tetherSemantic);
            }
            else
            {
                ecb.AddComponent(tetherEntity, tetherSemantic);
            }

            var tetherRenderKey = new RenderKey
            {
                ArchetypeId = RenderPresentationConstants.GhostTetherSemanticKey,
                LOD = 0
            };
            if (em.HasComponent<RenderKey>(sourceEntity))
            {
                ecb.SetComponent(tetherEntity, tetherRenderKey);
            }
            else
            {
                ecb.AddComponent(tetherEntity, tetherRenderKey);
            }

            if (em.HasComponent<MeshPresenter>(sourceEntity))
            {
                ecb.SetComponentEnabled<MeshPresenter>(tetherEntity, true);
            }
            else
            {
                ecb.AddComponent(tetherEntity, new MeshPresenter { DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex });
                ecb.SetComponentEnabled<MeshPresenter>(tetherEntity, true);
            }

            SetPresenterEnabledIfSourceHas<SpritePresenter>(sourceEntity, tetherEntity, false, ref state, ecb);
            SetPresenterEnabledIfSourceHas<DebugPresenter>(sourceEntity, tetherEntity, false, ref state, ecb);
            SetPresenterEnabledIfSourceHas<TracerPresenter>(sourceEntity, tetherEntity, false, ref state, ecb);

            if (em.HasComponent<RenderThemeOverride>(sourceEntity))
            {
                ecb.SetComponentEnabled<RenderThemeOverride>(tetherEntity, false);
            }

            if (em.HasComponent<RenderVariantOverride>(sourceEntity))
            {
                ecb.SetComponentEnabled<RenderVariantOverride>(tetherEntity, false);
            }

            if (em.HasComponent<PresentationScaleMultiplier>(sourceEntity))
            {
                ecb.RemoveComponent<PresentationScaleMultiplier>(tetherEntity);
            }
        }

        private static void SetPresenterEnabledIfSourceHas<T>(Entity sourceEntity, Entity tetherEntity, bool enabled, ref SystemState state, EntityCommandBuffer ecb)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var em = state.EntityManager;
            if (!em.HasComponent<T>(sourceEntity))
            {
                return;
            }

            ecb.SetComponentEnabled<T>(tetherEntity, enabled);
        }

        private static void CopyEnableablePresenter<T>(ref SystemState state, Entity sourceEntity, Entity ghostEntity, EntityCommandBuffer ecb)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var em = state.EntityManager;
            if (!em.HasComponent<T>(sourceEntity))
            {
                return;
            }

            ecb.AddComponent(ghostEntity, em.GetComponentData<T>(sourceEntity));
            ecb.SetComponentEnabled<T>(ghostEntity, em.IsComponentEnabled<T>(sourceEntity));
        }

        private struct GhostLink
        {
            public Entity GhostEntity;
            public Entity SourceEntity;
        }
    }
}

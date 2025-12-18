using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures Space4X gameplay entities receive presentation components compatible with the RenderKey/catalog pipeline.
    /// Assigns RenderKey/RenderFlags, colors, and visual-state tags per entity type.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XPresentationLifecycleSystem : ISystem
    {
        private EntityQuery _carrierInitQuery;
        private EntityQuery _vesselInitQuery;
        private EntityQuery _asteroidInitQuery;

        private const ushort RenderSampleModulus = 1024;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();

            _carrierInitQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, LocalTransform>()
                .WithNone<CarrierPresentationTag>()
                .Build();

            _vesselInitQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, LocalTransform>()
                .WithNone<CraftPresentationTag>()
                .Build();

            _asteroidInitQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform>()
                .WithNone<AsteroidPresentationTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (_carrierInitQuery.IsEmptyIgnoreFilter &&
                _vesselInitQuery.IsEmptyIgnoreFilter &&
                _asteroidInitQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            AddCarrierPresentation(ref state, ref ecb);
            AddVesselPresentation(ref state, ref ecb);
            AddAsteroidPresentation(ref state, ref ecb);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private void AddCarrierPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                         .WithNone<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                var factionColor = Space4XFactionColors.Blue;

                ecb.AddComponent(entity, new CarrierPresentationTag());
                ecb.AddComponent(entity, new CarrierVisualState
                {
                    State = CarrierVisualStateType.Idle,
                    StateTimer = 0f
                });
                AddMaterialColor(ref ecb, entity, factionColor);

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Carrier,
                    cullDistance: 2500f,
                    cullPriority: 200,
                    importance: 0.95f);
            }
        }

        private void AddVesselPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (vessel, _, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithNone<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                var parentCarrier = vessel.ValueRO.CarrierEntity;
                float4 vesselColor = Space4XFactionColors.Blue;

                if (parentCarrier != Entity.Null && SystemAPI.HasComponent<RenderTint>(parentCarrier))
                {
                    vesselColor = SystemAPI.GetComponentRO<RenderTint>(parentCarrier).ValueRO.Value;
                }

                ecb.AddComponent(entity, new CraftPresentationTag());
                ecb.AddComponent(entity, new CraftVisualState
                {
                    State = CraftVisualStateType.Idle,
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new ParentCarrier { Value = parentCarrier });
                AddMaterialColor(ref ecb, entity, vesselColor);

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Miner,
                    cullDistance: 1800f,
                    cullPriority: 150,
                    importance: 0.75f);
            }
        }

        private void AddAsteroidPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (asteroid, _, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithNone<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                var color = GetResourceColor(asteroid.ValueRO.ResourceType);
                var ratio = asteroid.ValueRO.MaxResourceAmount > 0f
                    ? asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount)
                    : 1f;

                ecb.AddComponent(entity, new AsteroidPresentationTag());
                ecb.AddComponent(entity, new ResourceTypeColor { Value = color });
                ecb.AddComponent(entity, new AsteroidVisualState
                {
                    State = ratio > 0.1f ? AsteroidVisualStateType.Full : AsteroidVisualStateType.Depleted,
                    DepletionRatio = 1f - math.saturate(ratio),
                    StateTimer = 0f
                });

                AddMaterialColor(ref ecb, entity, color);

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Asteroid,
                    cullDistance: 3000f,
                    cullPriority: 100,
                    importance: 0.6f);
            }
        }

        private static void AddCommonRenderComponents(
            ref EntityCommandBuffer ecb,
            Entity entity,
            int semanticKey,
            float cullDistance,
            byte cullPriority,
            float importance)
        {
            ecb.AddComponent(entity, new RenderKey
            {
                ArchetypeId = (ushort)semanticKey,
                LOD = 0
            });

            ecb.AddComponent(entity, new RenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
            });

            ecb.AddComponent(entity, new RenderSemanticKey
            {
                Value = (ushort)semanticKey
            });

            ecb.AddComponent(entity, new RenderVariantKey
            {
                Value = 0
            });

            ecb.AddComponent<RenderThemeOverride>(entity);
            ecb.SetComponentEnabled<RenderThemeOverride>(entity, false);

            ecb.AddComponent<MeshPresenter>(entity);
            ecb.AddComponent<SpritePresenter>(entity);
            ecb.SetComponentEnabled<SpritePresenter>(entity, false);
            ecb.AddComponent<DebugPresenter>(entity);
            ecb.SetComponentEnabled<DebugPresenter>(entity, false);

            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = importance,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = cullPriority
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, RenderSampleModulus);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = RenderSampleModulus,
                ShouldRender = 1
            });

        }

        private static void AddMaterialColor(ref EntityCommandBuffer ecb, Entity entity, float4 baseColor)
        {
            ecb.AddComponent(entity, new MaterialPropertyOverride
            {
                BaseColor = baseColor,
                EmissiveColor = float4.zero,
                Alpha = 1f,
                PulsePhase = 0f
            });

            ecb.AddComponent(entity, new URPMaterialPropertyBaseColor
            {
                Value = baseColor
            });

            ecb.AddComponent(entity, new RenderTint
            {
                Value = baseColor
            });
            ecb.AddComponent(entity, new RenderTexSlice
            {
                Value = 0
            });
            ecb.AddComponent(entity, new RenderUvTransform
            {
                Value = new float4(1f, 1f, 0f, 0f)
            });
        }

        private static float4 GetResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Minerals => new float4(0.6f, 0.6f, 0.6f, 1f),
                ResourceType.RareMetals => new float4(0.8f, 0.7f, 0.2f, 1f),
                ResourceType.EnergyCrystals => new float4(0.2f, 0.8f, 1f, 1f),
                ResourceType.OrganicMatter => new float4(0.2f, 0.8f, 0.3f, 1f),
                ResourceType.Ore => new float4(0.5f, 0.3f, 0.2f, 1f),
                _ => new float4(1f, 1f, 1f, 1f)
            };
        }
    }
}

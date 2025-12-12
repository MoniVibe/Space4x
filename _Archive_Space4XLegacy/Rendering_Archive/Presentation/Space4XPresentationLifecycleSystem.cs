using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Shared.Demo;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that manages presentation component lifecycle for entities.
    /// Adds presentation components to new entities and handles destruction/cleanup.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateBefore(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XPresentationLifecycleSystem : ISystem
    {
        private ComponentLookup<RenderLODData> _lodLookup;
        private ComponentLookup<FactionColor> _factionColorLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _lodLookup = state.GetComponentLookup<RenderLODData>(false);
            _factionColorLookup = state.GetComponentLookup<FactionColor>(false);
        }

        // NOTE: Not Burst compiled because we call ECB.AddSharedComponentManaged(RenderMeshArray) which boxes the struct
        // Burst doesn't support boxing, so this must run in managed code
        public void OnUpdate(ref SystemState state)
        {
            _lodLookup.Update(ref state);
            _factionColorLookup.Update(ref state);

            // Check if DemoRenderReady exists (for render components)
            if (!SystemAPI.HasSingleton<DemoRenderReady>())
            {
                return; // Can't add render components without RenderMeshArray
            }

            var renderReadyEntity = SystemAPI.GetSingletonEntity<DemoRenderReady>();
            var renderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(renderReadyEntity);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add presentation components to carriers that don't have them
            foreach (var (carrier, transform, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                         .WithNone<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new CarrierPresentationTag());
                
                // Add PureDOTS-compatible LOD components
                ecb.AddComponent(entity, new RenderLODData
                {
                    RecommendedLOD = 0, // Full detail
                    DistanceToCamera = 0f,
                    Importance = 0.8f
                });
                ecb.AddComponent(entity, new RenderCullable
                {
                    CullDistance = 2000f,
                    Priority = 100
                });
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index,
                    ShouldRender = 1 // Render by default
                });
                
                ecb.AddComponent(entity, new CarrierVisualState
                {
                    State = CarrierVisualStateType.Idle,
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new MaterialPropertyOverride
                {
                    BaseColor = new float4(0.5f, 0.5f, 1f, 1f), // Default blue
                    EmissiveColor = float4.zero,
                    Alpha = 1f,
                    PulsePhase = 0f
                });
                ecb.AddComponent(entity, new ShouldRenderTag());

                // Add faction color - will be set by presentation system based on AffiliationTag buffer
                // Default to blue for now
                ecb.AddComponent(entity, FactionColor.Blue);

                // Add render components (MaterialMeshInfo and RenderMeshArray) - CRITICAL for visibility
                ecb.AddSharedComponentManaged(entity, renderMeshArray);
                ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)); // Use mesh 0, material 0 from RenderMeshArray
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(0.5f, 0.5f, 1f, 1f) }); // Blue
            }

            // Add presentation components to crafts that don't have them
            foreach (var (vessel, transform, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithNone<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new CraftPresentationTag());
                
                // Add PureDOTS-compatible LOD components
                ecb.AddComponent(entity, new RenderLODData
                {
                    RecommendedLOD = 0, // Full detail
                    DistanceToCamera = 0f,
                    Importance = 0.6f
                });
                ecb.AddComponent(entity, new RenderCullable
                {
                    CullDistance = 1500f,
                    Priority = 80
                });
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index,
                    ShouldRender = 1 // Render by default
                });
                
                ecb.AddComponent(entity, new CraftVisualState
                {
                    State = CraftVisualStateType.Idle,
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new MaterialPropertyOverride
                {
                    BaseColor = new float4(0.5f, 0.5f, 0.5f, 1f), // Default gray
                    EmissiveColor = float4.zero,
                    Alpha = 1f,
                    PulsePhase = 0f
                });
                ecb.AddComponent(entity, new ShouldRenderTag());

                // Inherit faction color from parent carrier if available
                float4 craftColor = new float4(0.5f, 0.5f, 1f, 1f); // Default blue
                if (vessel.ValueRO.CarrierEntity != Entity.Null && _factionColorLookup.HasComponent(vessel.ValueRO.CarrierEntity))
                {
                    var carrierColor = _factionColorLookup[vessel.ValueRO.CarrierEntity];
                    ecb.AddComponent(entity, carrierColor);
                    craftColor = carrierColor.Value; // Fixed: FactionColor is a struct, access .Value directly
                }
                else
                {
                    ecb.AddComponent(entity, FactionColor.Blue);
                }

                // Add render components (MaterialMeshInfo and RenderMeshArray) - CRITICAL for visibility
                ecb.AddSharedComponentManaged(entity, renderMeshArray);
                ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)); // Use mesh 0, material 0 from RenderMeshArray
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = craftColor });
            }

            // Add presentation components to asteroids that don't have them
            foreach (var (asteroid, transform, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithNone<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new AsteroidPresentationTag());
                
                // Add PureDOTS-compatible LOD components
                ecb.AddComponent(entity, new RenderLODData
                {
                    RecommendedLOD = 0, // Full detail
                    DistanceToCamera = 0f,
                    Importance = 0.7f
                });
                ecb.AddComponent(entity, new RenderCullable
                {
                    CullDistance = 1800f,
                    Priority = 90
                });
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index,
                    ShouldRender = 1 // Render by default
                });

                // Determine resource color based on ResourceType
                var resourceColor = GetResourceColor(asteroid.ValueRO.ResourceType);
                ecb.AddComponent(entity, new ResourceTypeColor { Value = resourceColor });

                ecb.AddComponent(entity, new AsteroidVisualState
                {
                    State = asteroid.ValueRO.ResourceAmount > 0.05f * asteroid.ValueRO.MaxResourceAmount
                        ? AsteroidVisualStateType.Full
                        : AsteroidVisualStateType.Depleted,
                    DepletionRatio = 1f - (asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount)),
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new MaterialPropertyOverride
                {
                    BaseColor = resourceColor,
                    EmissiveColor = float4.zero,
                    Alpha = 1f,
                    PulsePhase = 0f
                });
                ecb.AddComponent(entity, new ShouldRenderTag());

                // Add render components (MaterialMeshInfo and RenderMeshArray) - CRITICAL for visibility
                ecb.AddSharedComponentManaged(entity, renderMeshArray);
                ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)); // Use mesh 0, material 0 from RenderMeshArray
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = resourceColor });
            }

            // Handle entity destruction - fade out visuals
            // Note: Actual entity destruction is handled by sim systems
            // This system just ensures presentation components are cleaned up gracefully

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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

    /// <summary>
    /// System that handles fleet merge/split events and updates fleet impostors.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial struct Space4XFleetAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleet>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Track fleet changes by comparing current fleet state to previous frame
            // For now, this is a placeholder - full implementation would track fleet membership changes
            // and create/update/destroy fleet impostor entities accordingly

            // Update fleet aggregate data for existing fleet impostors
            foreach (var (fleet, aggregateData, transform) in SystemAPI
                         .Query<RefRO<Space4XFleet>, RefRW<FleetAggregateData>, RefRO<LocalTransform>>()
                         .WithAll<FleetImpostorTag>())
            {
                aggregateData.ValueRW.Centroid = transform.ValueRO.Position;
                aggregateData.ValueRW.ShipCount = fleet.ValueRO.ShipCount;
                aggregateData.ValueRW.Strength = math.saturate(fleet.ValueRO.ShipCount / 100f);
            }

            // Create fleet impostors for fleets that don't have them (when at Impostor LOD)
            // This is handled by the fleet impostor system based on LOD level
        }
    }
}


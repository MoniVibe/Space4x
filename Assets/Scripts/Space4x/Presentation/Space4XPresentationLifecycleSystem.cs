using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that manages presentation component lifecycle for entities.
    /// Adds presentation components to new entities and handles destruction/cleanup.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XPresentationLifecycleSystem : ISystem
    {
        private ComponentLookup<PresentationLOD> _lodLookup;
        private ComponentLookup<FactionColor> _factionColorLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _lodLookup = state.GetComponentLookup<PresentationLOD>(false);
            _factionColorLookup = state.GetComponentLookup<FactionColor>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _lodLookup.Update(ref state);
            _factionColorLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add presentation components to carriers that don't have them
            foreach (var (carrier, transform, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                         .WithNone<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new CarrierPresentationTag());
                ecb.AddComponent(entity, new PresentationLOD
                {
                    Level = PresentationLODLevel.FullDetail,
                    DistanceToCamera = 0f
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
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index
                });
                ecb.AddComponent(entity, new ShouldRenderTag());

                // Add faction color - will be set by presentation system based on AffiliationTag buffer
                // Default to blue for now
                ecb.AddComponent(entity, FactionColor.Blue);
            }

            // Add presentation components to crafts that don't have them
            foreach (var (vessel, transform, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithNone<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new CraftPresentationTag());
                ecb.AddComponent(entity, new PresentationLOD
                {
                    Level = PresentationLODLevel.FullDetail,
                    DistanceToCamera = 0f
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
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index
                });
                ecb.AddComponent(entity, new ShouldRenderTag());

                // Inherit faction color from parent carrier if available
                if (vessel.ValueRO.CarrierEntity != Entity.Null && _factionColorLookup.HasComponent(vessel.ValueRO.CarrierEntity))
                {
                    var carrierColor = _factionColorLookup[vessel.ValueRO.CarrierEntity];
                    ecb.AddComponent(entity, carrierColor);
                }
                else
                {
                    ecb.AddComponent(entity, FactionColor.Blue);
                }
            }

            // Add presentation components to asteroids that don't have them
            foreach (var (asteroid, transform, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithNone<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new AsteroidPresentationTag());
                ecb.AddComponent(entity, new PresentationLOD
                {
                    Level = PresentationLODLevel.FullDetail,
                    DistanceToCamera = 0f
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
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    Index = (uint)entity.Index
                });
                ecb.AddComponent(entity, new ShouldRenderTag());
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
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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


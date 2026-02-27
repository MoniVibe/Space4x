using PureDOTS.Rendering;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Physics;
using PureDOTS.Systems.Physics;
using Space4X.Physics;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Physics
{
    /// <summary>
    /// Idempotently wraps gameplay entities with Space4X/PureDOTS proxy collider components.
    /// Safe to run every frame in Initialization; only missing pieces are added.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XColliderProfileSyncSystem))]
    [UpdateBefore(typeof(PhysicsBodyBootstrapSystem))]
    public partial struct Space4XProxyColliderBootstrapSystem : ISystem
    {
        private const float MinRadius = 0.1f;
        private const float DefaultShipRadius = 0.6f;
        private const float DefaultMinerRadius = 0.6f;
        private const float DefaultAsteroidRadius = 1.2f;
        private const float DefaultStationRadius = 4f;
        private const float MinMass = 0.05f;

        private EntityQuery _candidateQuery;

        public void OnCreate(ref SystemState state)
        {
            _candidateQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<LocalTransform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<Asteroid>(),
                    ComponentType.ReadOnly<StrikeCraftProfile>(),
                    ComponentType.ReadOnly<VesselPhysicalProperties>(),
                    ComponentType.ReadOnly<RockTag>(),
                    ComponentType.ReadOnly<SpacePhysicsBody>(),
                    ComponentType.ReadOnly<NeedsPhysicsSetup>()
                },
                None = new[] { ComponentType.ReadOnly<Prefab>() }
            });

            state.RequireForUpdate(_candidateQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var profileReady = false;
            BlobAssetReference<PhysicsColliderProfileBlob> profileBlob = default;
            if (SystemAPI.TryGetSingleton<PhysicsColliderProfileComponent>(out var profileComponent) &&
                profileComponent.Profile.IsCreated)
            {
                profileReady = true;
                profileBlob = profileComponent.Profile;
            }

            using var entities = _candidateQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!TryResolveLayerAndPriority(em, entity, out var layer, out var priority))
                {
                    continue;
                }

                var transform = em.GetComponentData<LocalTransform>(entity);
                var hasProfileSpec = TryResolveProfileSpec(em, entity, profileReady, profileBlob, out var profileSpec);
                var radius = ResolveRadius(em, entity, transform.Scale, hasProfileSpec ? profileSpec : default, hasProfileSpec, layer);
                var isTrigger = layer == Space4XPhysicsLayer.SensorOnly || layer == Space4XPhysicsLayer.DockingZone;
                var continuous = false;
                var raisesEvents =
                    em.HasBuffer<SpaceCollisionEvent>(entity) ||
                    em.HasBuffer<PhysicsCollisionEventElement>(entity);

                if (em.HasComponent<SpacePhysicsBody>(entity))
                {
                    var existing = em.GetComponentData<SpacePhysicsBody>(entity);
                    isTrigger |= (existing.Flags & SpacePhysicsFlags.IsTrigger) != 0;
                    continuous = (existing.Flags & SpacePhysicsFlags.ContinuousCollision) != 0;
                    raisesEvents = (existing.Flags & SpacePhysicsFlags.RaisesCollisionEvents) != 0;
                }

                if (!em.HasComponent<SpacePhysicsBody>(entity))
                {
                    var flags = SpacePhysicsFlags.IsActive;
                    if (raisesEvents)
                    {
                        flags |= SpacePhysicsFlags.RaisesCollisionEvents;
                    }

                    if (isTrigger)
                    {
                        flags |= SpacePhysicsFlags.IsTrigger;
                    }

                    if (continuous)
                    {
                        flags |= SpacePhysicsFlags.ContinuousCollision;
                    }

                    ecb.AddComponent(entity, new SpacePhysicsBody
                    {
                        Layer = layer,
                        Priority = priority,
                        Flags = flags
                    });
                }

                if (!em.HasComponent<SpaceColliderData>(entity))
                {
                    var colliderData = CreateColliderData(radius, hasProfileSpec ? profileSpec : default, hasProfileSpec);
                    ecb.AddComponent(entity, colliderData);
                }

                var interactionFlags = ResolveInteractionFlags(em, entity, isTrigger, continuous);

                if (!em.HasComponent<RequiresPhysics>(entity))
                {
                    ecb.AddComponent(entity, new RequiresPhysics
                    {
                        Priority = priority,
                        Flags = interactionFlags
                    });
                }

                if (!em.HasComponent<PhysicsInteractionConfig>(entity))
                {
                    ecb.AddComponent(entity, new PhysicsInteractionConfig
                    {
                        Mass = ResolveMass(em, entity, radius),
                        CollisionRadius = radius,
                        Restitution = 0f,
                        Friction = 0f,
                        LinearDamping = 0f,
                        AngularDamping = 0f
                    });
                }

                if (!em.HasComponent<PhysicsColliderSpec>(entity))
                {
                    var spec = CreateColliderSpec(
                        hasProfileSpec ? profileSpec : default,
                        hasProfileSpec,
                        radius,
                        interactionFlags,
                        layer,
                        isTrigger);
                    ecb.AddComponent(entity, spec);
                }

                if (raisesEvents)
                {
                    if (!em.HasBuffer<SpaceCollisionEvent>(entity))
                    {
                        ecb.AddBuffer<SpaceCollisionEvent>(entity);
                    }

                    if (!em.HasBuffer<PhysicsCollisionEventElement>(entity))
                    {
                        ecb.AddBuffer<PhysicsCollisionEventElement>(entity);
                    }
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static bool TryResolveLayerAndPriority(
            EntityManager em,
            Entity entity,
            out Space4XPhysicsLayer layer,
            out byte priority)
        {
            if (em.HasComponent<SpacePhysicsBody>(entity))
            {
                var body = em.GetComponentData<SpacePhysicsBody>(entity);
                layer = body.Layer;
                priority = body.Priority != 0 ? body.Priority : Space4XPhysicsLayers.GetDefaultPriority(layer);
                return true;
            }

            if (em.HasComponent<Asteroid>(entity) || em.HasComponent<RockTag>(entity))
            {
                layer = Space4XPhysicsLayer.Asteroid;
                priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                return true;
            }

            if (em.HasComponent<MiningVessel>(entity))
            {
                layer = Space4XPhysicsLayer.Miner;
                priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                return true;
            }

            if (em.HasComponent<StationId>(entity) || em.HasComponent<Space4XColony>(entity))
            {
                layer = Space4XPhysicsLayer.Station;
                priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                return true;
            }

            if (em.HasComponent<Carrier>(entity) ||
                em.HasComponent<StrikeCraftProfile>(entity) ||
                em.HasComponent<VesselPhysicalProperties>(entity))
            {
                layer = Space4XPhysicsLayer.Ship;
                priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                return true;
            }

            if (em.HasComponent<RenderSemanticKey>(entity))
            {
                var semantic = em.GetComponentData<RenderSemanticKey>(entity).Value;
                if (semantic == Space4XRenderKeys.Asteroid)
                {
                    layer = Space4XPhysicsLayer.Asteroid;
                    priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                    return true;
                }

                if (semantic == Space4XRenderKeys.Miner)
                {
                    layer = Space4XPhysicsLayer.Miner;
                    priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                    return true;
                }

                if (semantic == Space4XRenderKeys.Carrier || semantic == Space4XRenderKeys.StrikeCraft)
                {
                    layer = Space4XPhysicsLayer.Ship;
                    priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
                    return true;
                }
            }

            layer = default;
            priority = 0;
            return false;
        }

        private static bool TryResolveProfileSpec(
            EntityManager em,
            Entity entity,
            bool profileReady,
            BlobAssetReference<PhysicsColliderProfileBlob> profileBlob,
            out PhysicsColliderSpec spec)
        {
            if (!profileReady || !profileBlob.IsCreated || !em.HasComponent<RenderSemanticKey>(entity))
            {
                spec = default;
                return false;
            }

            var semantic = em.GetComponentData<RenderSemanticKey>(entity).Value;
            ref var profileEntries = ref profileBlob.Value.Entries;
            return PhysicsColliderProfileHelpers.TryGetSpec(ref profileEntries, semantic, out spec);
        }

        private static float ResolveRadius(
            EntityManager em,
            Entity entity,
            float transformScale,
            in PhysicsColliderSpec profileSpec,
            bool hasProfileSpec,
            Space4XPhysicsLayer layer)
        {
            if (em.HasComponent<SpaceColliderData>(entity))
            {
                var collider = em.GetComponentData<SpaceColliderData>(entity);
                var radius = ResolveRadius(collider);
                if (radius > 0f)
                {
                    return radius;
                }
            }

            if (em.HasComponent<VesselPhysicalProperties>(entity))
            {
                var vessel = em.GetComponentData<VesselPhysicalProperties>(entity);
                if (vessel.Radius > 0f)
                {
                    return vessel.Radius;
                }
            }

            if (hasProfileSpec)
            {
                var profileRadius = ResolveRadius(profileSpec);
                if (profileRadius > 0f)
                {
                    return profileRadius;
                }
            }

            if (em.HasComponent<PhysicsInteractionConfig>(entity))
            {
                var interaction = em.GetComponentData<PhysicsInteractionConfig>(entity);
                if (interaction.CollisionRadius > 0f)
                {
                    return interaction.CollisionRadius;
                }
            }

            var scaledFallback = math.max(MinRadius, transformScale * 0.5f);
            return layer switch
            {
                Space4XPhysicsLayer.Asteroid => math.max(DefaultAsteroidRadius, scaledFallback),
                Space4XPhysicsLayer.Miner => math.max(DefaultMinerRadius, scaledFallback),
                Space4XPhysicsLayer.Station => math.max(DefaultStationRadius, scaledFallback),
                _ => math.max(DefaultShipRadius, scaledFallback)
            };
        }

        private static float ResolveRadius(in SpaceColliderData data)
        {
            return data.Type switch
            {
                ColliderType.Box => math.cmax(data.Size) * 0.5f,
                ColliderType.Capsule => data.Radius,
                _ => data.Radius
            };
        }

        private static float ResolveRadius(in PhysicsColliderSpec spec)
        {
            return spec.Shape switch
            {
                PhysicsColliderShape.Box => math.cmax(spec.Dimensions) * 0.5f,
                PhysicsColliderShape.Capsule => spec.Dimensions.x,
                _ => spec.Dimensions.x
            };
        }

        private static float ResolveMass(EntityManager em, Entity entity, float radius)
        {
            if (em.HasComponent<PhysicsInteractionConfig>(entity))
            {
                var interaction = em.GetComponentData<PhysicsInteractionConfig>(entity);
                if (interaction.Mass > 0f)
                {
                    return math.max(MinMass, interaction.Mass);
                }
            }

            if (em.HasComponent<VesselPhysicalProperties>(entity))
            {
                var vessel = em.GetComponentData<VesselPhysicalProperties>(entity);
                if (vessel.BaseMass > 0f)
                {
                    return math.max(MinMass, vessel.BaseMass);
                }
            }

            var estimated = radius * radius * radius;
            return math.max(MinMass, estimated);
        }

        private static SpaceColliderData CreateColliderData(
            float radius,
            in PhysicsColliderSpec profileSpec,
            bool hasProfileSpec)
        {
            if (!hasProfileSpec)
            {
                return SpaceColliderData.CreateSphere(radius);
            }

            return profileSpec.Shape switch
            {
                PhysicsColliderShape.Box => SpaceColliderData.CreateBox(
                    math.max(profileSpec.Dimensions, new float3(MinRadius))),
                PhysicsColliderShape.Capsule => SpaceColliderData.CreateCapsule(
                    math.max(profileSpec.Dimensions.x, MinRadius),
                    math.max(profileSpec.Dimensions.y, MinRadius * 2f)),
                _ => SpaceColliderData.CreateSphere(math.max(profileSpec.Dimensions.x, MinRadius))
            };
        }

        private static PhysicsInteractionFlags ResolveInteractionFlags(
            EntityManager em,
            Entity entity,
            bool isTrigger,
            bool continuous)
        {
            var flags = PhysicsInteractionFlags.Collidable;
            if (em.HasComponent<RequiresPhysics>(entity))
            {
                flags = em.GetComponentData<RequiresPhysics>(entity).Flags;
            }

            if (isTrigger)
            {
                flags |= PhysicsInteractionFlags.Trigger;
            }

            if (continuous)
            {
                flags |= PhysicsInteractionFlags.ContinuousCollision;
            }

            return flags;
        }

        private static PhysicsColliderSpec CreateColliderSpec(
            in PhysicsColliderSpec profileSpec,
            bool hasProfileSpec,
            float radius,
            PhysicsInteractionFlags interactionFlags,
            Space4XPhysicsLayer layer,
            bool isTrigger)
        {
            var spec = hasProfileSpec
                ? profileSpec
                : PhysicsColliderSpec.CreateSphere(math.max(radius, MinRadius), interactionFlags);

            if (spec.Flags == PhysicsInteractionFlags.None)
            {
                spec.Flags = interactionFlags;
            }

            if (isTrigger)
            {
                spec.IsTrigger = 1;
                spec.Flags |= PhysicsInteractionFlags.Trigger;
            }

            if (!HasValidDimensions(spec))
            {
                spec.Shape = PhysicsColliderShape.Sphere;
                spec.Dimensions = new float3(math.max(radius, MinRadius), 0f, 0f);
            }

            if (spec.UseCustomFilter == 0)
            {
                spec.UseCustomFilter = 1;
                spec.CustomFilter = Space4XPhysicsLayers.CreateFilter(layer);
            }

            return spec;
        }

        private static bool HasValidDimensions(in PhysicsColliderSpec spec)
        {
            return spec.Shape switch
            {
                PhysicsColliderShape.Box => spec.Dimensions.x > 0f &&
                                            spec.Dimensions.y > 0f &&
                                            spec.Dimensions.z > 0f,
                PhysicsColliderShape.Capsule => spec.Dimensions.x > 0f &&
                                                spec.Dimensions.y > 0f,
                _ => spec.Dimensions.x > 0f
            };
        }
    }
}

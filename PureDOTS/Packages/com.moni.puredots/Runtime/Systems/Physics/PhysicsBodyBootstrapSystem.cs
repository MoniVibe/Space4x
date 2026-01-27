using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Bootstraps physics bodies for entities marked with physics participation components.
    /// Runs in InitializationSystemGroup to safely add physics components via ECB.
    /// </summary>
    /// <remarks>
    /// This system:
    /// - Detects entities with RequiresPhysics or PhysicsInteractionConfig that lack physics colliders
    /// - Creates appropriate Unity Physics components (PhysicsCollider, PhysicsVelocity, PhysicsMass)
    /// - Sets up kinematic bodies for ECS-driven movement
    /// - Respects PhysicsConfig singleton for enable/disable toggles
    /// 
    /// Philosophy:
    /// - ECS is authoritative; physics bodies are kinematic (driven by ECS transforms)
    /// - Structural changes are safe here (InitializationSystemGroup)
    /// </remarks>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PhysicsBodyBootstrapSystem : ISystem
    {
        private EntityQuery _needsSetupQuery;
        private NativeParallelHashMap<int, BlobAssetReference<Unity.Physics.Collider>> _colliderCache;

        public void OnCreate(ref SystemState state)
        {
            // Query for entities that need physics setup
            _needsSetupQuery = SystemAPI.QueryBuilder()
                .WithAll<RequiresPhysics, LocalTransform>()
                .WithNone<PhysicsCollider>()
                .Build();

            state.RequireForUpdate(_needsSetupQuery);
            _colliderCache = new NativeParallelHashMap<int, BlobAssetReference<Unity.Physics.Collider>>(8, Allocator.Persistent);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if physics is globally enabled
            if (!SystemAPI.HasSingleton<PhysicsConfig>())
            {
                return;
            }

            var config = SystemAPI.GetSingleton<PhysicsConfig>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev-only assert: Unity Physics gravity must be zero to prevent double-gravity
            // ThrownObjectGravitySystem is the single authority for thrown object gravity
            if (SystemAPI.TryGetSingleton<PhysicsStep>(out var physicsStep))
            {
                if (math.lengthsq(physicsStep.Gravity) > 0.0001f)
                {
                    UnityEngine.Debug.LogWarning(
                        "[PhysicsBootstrap] Unity Physics gravity is non-zero! " +
                        $"Gravity = {physicsStep.Gravity}. " +
                        "This will cause double-gravity for thrown objects. " +
                        "Set PhysicsStep.Gravity = float3.zero to use ECS-authoritative gravity.");
                }
            }
#endif

            // Early out if both game modes have physics disabled
            if (!config.IsSpace4XPhysicsEnabled && !config.IsGodgamePhysicsEnabled)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (requiresPhysics, transform, entity) in 
                SystemAPI.Query<RefRO<RequiresPhysics>, RefRO<LocalTransform>>()
                    .WithNone<PhysicsCollider>()
                    .WithEntityAccess())
            {
                var flags = requiresPhysics.ValueRO.Flags;
                var hasSpec = SystemAPI.HasComponent<PhysicsColliderSpec>(entity);
                var spec = hasSpec
                    ? SystemAPI.GetComponent<PhysicsColliderSpec>(entity)
                    : PhysicsColliderSpec.CreateSphere(ResolveCollisionRadius(state.EntityManager, entity), flags);

                if (spec.Flags != 0)
                {
                    flags = spec.Flags;
                }

                var collider = GetOrCreateCollider(spec, flags);

                // Add PhysicsCollider
                ecb.AddComponent(entity, new PhysicsCollider { Value = collider });

                // Add PhysicsVelocity for kinematic bodies
                ecb.AddComponent(entity, new PhysicsVelocity
                {
                    Linear = float3.zero,
                    Angular = float3.zero
                });

                // Add PhysicsMass for kinematic body (infinite mass = kinematic)
                var mass = PhysicsMass.CreateKinematic(MassProperties.UnitSphere);
                ecb.AddComponent(entity, mass);

                // Add PhysicsGravityFactor (0 for kinematic)
                ecb.AddComponent(entity, new PhysicsGravityFactor { Value = 0f });

                // Add PhysicsDamping
                ecb.AddComponent(entity, new PhysicsDamping
                {
                    Linear = 0f,
                    Angular = 0f
                });

                // Mark as having physics collider (for game-specific systems to detect)
                // Note: Game-specific tags like HasPhysicsCollider are added by game systems

                if (config.IsLoggingEnabled)
                {
                    UnityEngine.Debug.Log($"[PhysicsBootstrap] Added physics components to entity {entity.Index}:{entity.Version}");
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_colliderCache.IsCreated)
            {
                var values = _colliderCache.GetValueArray(Allocator.Temp);
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i].IsCreated)
                    {
                        values[i].Dispose();
                    }
                }
                values.Dispose();
                _colliderCache.Dispose();
            }
        }

        private BlobAssetReference<Unity.Physics.Collider> GetOrCreateCollider(in PhysicsColliderSpec spec, PhysicsInteractionFlags flags)
        {
            var filter = spec.UseCustomFilter != 0 ? spec.CustomFilter : BuildCollisionFilter(flags);
            var isTrigger = spec.IsTrigger != 0 || (flags & PhysicsInteractionFlags.Trigger) != 0;
            var key = CombineKey(spec, flags, filter, isTrigger);
            if (_colliderCache.TryGetValue(key, out var cached) && cached.IsCreated)
            {
                return cached;
            }

            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = isTrigger
                ? CollisionResponsePolicy.RaiseTriggerEvents
                : CollisionResponsePolicy.Collide;

            BlobAssetReference<Unity.Physics.Collider> collider;
            switch (spec.Shape)
            {
                case PhysicsColliderShape.Capsule:
                {
                    var radius = math.max(spec.Dimensions.x, 0.01f);
                    var height = math.max(spec.Dimensions.y, radius * 2f);
                    var halfHeight = math.max(0f, (height * 0.5f) - radius);
                    var capsuleGeometry = new CapsuleGeometry
                    {
                        Vertex0 = new float3(0f, -halfHeight, 0f),
                        Vertex1 = new float3(0f, halfHeight, 0f),
                        Radius = radius
                    };
                    collider = Unity.Physics.CapsuleCollider.Create(capsuleGeometry, filter, material);
                    break;
                }
                case PhysicsColliderShape.Box:
                {
                    var halfExtents = math.max(spec.Dimensions * 0.5f, new float3(0.01f));
                    var boxGeometry = new BoxGeometry
                    {
                        Center = float3.zero,
                        Size = halfExtents * 2f,
                        Orientation = quaternion.identity,
                        BevelRadius = 0f
                    };
                    collider = Unity.Physics.BoxCollider.Create(boxGeometry, filter, material);
                    break;
                }
                default:
                {
                    var sphereGeometry = new SphereGeometry
                    {
                        Center = float3.zero,
                        Radius = math.max(spec.Dimensions.x, 0.01f)
                    };
                    collider = Unity.Physics.SphereCollider.Create(sphereGeometry, filter, material);
                    break;
                }
            }

            _colliderCache.TryAdd(key, collider);
            return collider;
        }

        private static CollisionFilter BuildCollisionFilter(PhysicsInteractionFlags flags)
        {
            var collidable = (flags & PhysicsInteractionFlags.Collidable) != 0;
            return new CollisionFilter
            {
                BelongsTo = collidable ? 1u : 0u,
                CollidesWith = collidable ? ~0u : 0u,
                GroupIndex = 0
            };
        }

        private static int CombineKey(in PhysicsColliderSpec spec, PhysicsInteractionFlags flags, CollisionFilter filter, bool isTrigger)
        {
            unchecked
            {
                var hash = (int)spec.Shape;
                hash = (hash * 397) ^ unchecked((int)math.hash(spec.Dimensions));
                hash = (hash * 397) ^ (int)flags;
                hash = (hash * 397) ^ (isTrigger ? 1 : 0);
                hash = (hash * 397) ^ (spec.UseCustomFilter != 0 ? 1 : 0);
                hash = (hash * 397) ^ unchecked((int)filter.BelongsTo);
                hash = (hash * 397) ^ unchecked((int)filter.CollidesWith);
                hash = (hash * 397) ^ filter.GroupIndex;
                return hash;
            }
        }

        private float ResolveCollisionRadius(EntityManager entityManager, Entity entity)
        {
            var collisionRadius = 1f;
            if (entityManager.HasComponent<PhysicsInteractionConfig>(entity))
            {
                var interactionConfig = entityManager.GetComponentData<PhysicsInteractionConfig>(entity);
                collisionRadius = interactionConfig.CollisionRadius;
            }

            return collisionRadius;
        }
    }

    /// <summary>
    /// System group for physics-related systems.
    /// Runs at the start of the main physics simulation group.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup), OrderFirst = true)]
    public partial class PhysicsPreSyncSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for post-physics event processing.
    /// Runs at the end of the main physics simulation group.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup), OrderLast = true)]
    public partial class PhysicsPostEventSystemGroup : ComponentSystemGroup { }
}

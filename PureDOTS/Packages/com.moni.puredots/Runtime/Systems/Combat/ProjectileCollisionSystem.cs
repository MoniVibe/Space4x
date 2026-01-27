using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using PureDOTS.Runtime.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Performs continuous collision detection for projectiles using raycast/spherecast from PrevPos→Pos.
    /// Runs in CombatSystemGroup after projectile advance systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct ProjectileCollisionSystem : ISystem
    {
        private BufferLookup<PlayEffectRequest> _impactFxBufferLookup;
        private NativeParallelHashMap<ulong, BlobAssetReference<Unity.Physics.Collider>> _aoeColliderCache;
        private BlobAssetReference<ProjectileCatalogBlob> _cachedCatalog;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileEntity>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProjectileActive>();
            _impactFxBufferLookup = state.GetBufferLookup<PlayEffectRequest>();
            _aoeColliderCache = new NativeParallelHashMap<ulong, BlobAssetReference<Unity.Physics.Collider>>(8, Allocator.Persistent);
            _cachedCatalog = default;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }
            if (!projectileCatalog.Catalog.IsCreated)
            {
                return;
            }

            EnsureAoEColliders(projectileCatalog.Catalog);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Get presentation request hub for impact FX
            var hasPresentationHub = SystemAPI.TryGetSingletonEntity<PresentationRequestHub>(out var hubEntity);
            _impactFxBufferLookup.Update(ref state);

            var job = new ProjectileCollisionJob
            {
                PhysicsWorld = physicsWorld,
                ProjectileCatalog = projectileCatalog.Catalog,
                Ecb = ecb,
                HasPresentationHub = hasPresentationHub,
                HubEntity = hasPresentationHub ? hubEntity : Entity.Null,
                ImpactFxBuffers = _impactFxBufferLookup,
                ColliderCache = _aoeColliderCache
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
            DisposeAoEColliders();
            if (_aoeColliderCache.IsCreated)
            {
                _aoeColliderCache.Dispose();
            }
            _cachedCatalog = default;
        }

        private void EnsureAoEColliders(in BlobAssetReference<ProjectileCatalogBlob> catalog)
        {
            if (_cachedCatalog.IsCreated && _cachedCatalog.Equals(catalog))
            {
                return;
            }

            DisposeAoEColliders();
            _cachedCatalog = catalog;

            ref var projectiles = ref catalog.Value.Projectiles;
            for (int i = 0; i < projectiles.Length; i++)
            {
                ref var spec = ref projectiles[i];
                if (spec.AoERadius <= 0f)
                {
                    continue;
                }

                var filter = new CollisionFilter
                {
                    BelongsTo = 0xFFFFFFFF,
                    CollidesWith = spec.HitFilter,
                    GroupIndex = 0
                };

                var sphereGeometry = new SphereGeometry
                {
                    Center = float3.zero,
                    Radius = math.max(spec.AoERadius, 0.001f)
                };

                var collider = Unity.Physics.SphereCollider.Create(sphereGeometry, filter);
                var key = BuildColliderKey(spec.AoERadius, spec.HitFilter);
                _aoeColliderCache.TryAdd(key, collider);
            }
        }

        private void DisposeAoEColliders()
        {
            if (!_aoeColliderCache.IsCreated)
            {
                return;
            }

            var values = _aoeColliderCache.GetValueArray(Allocator.Temp);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].IsCreated)
                {
                    values[i].Dispose();
                }
            }

            values.Dispose();
            _aoeColliderCache.Clear();
        }

        private static ulong BuildColliderKey(float radius, uint hitFilter)
        {
            unchecked
            {
                return ((ulong)math.asuint(radius) << 32) | hitFilter;
            }
        }

        [BurstCompile]
        public partial struct ProjectileCollisionJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;
            public EntityCommandBuffer.ParallelWriter Ecb;
            public bool HasPresentationHub;
            public Entity HubEntity;
            [NativeDisableParallelForRestriction] public BufferLookup<PlayEffectRequest> ImpactFxBuffers;
            [ReadOnly] public NativeParallelHashMap<ulong, BlobAssetReference<Unity.Physics.Collider>> ColliderCache;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                ref ProjectileEntity projectile,
                ref LocalTransform transform,
                DynamicBuffer<ProjectileHitResult> hitResults,
                EnabledRefRO<ProjectileActive> active)
            {
                if (!active.ValueRO)
                {
                    return;
                }

                hitResults.Clear();

                // Find projectile spec
                ref var spec = ref FindProjectileSpec(ProjectileCatalog, projectile.ProjectileId);
                if (UnsafeRef.IsNull(ref spec))
                {
                    return;
                }

                // Skip if projectile hasn't moved
                float3 currentPos = transform.Position;
                float3 prevPos = projectile.PrevPos;
                float3 delta = currentPos - prevPos;
                float deltaLength = math.length(delta);
                if (deltaLength < 1e-6f)
                {
                    return;
                }

                // Build collision filter from spec
                var filter = new CollisionFilter
                {
                    BelongsTo = 0xFFFFFFFF, // Projectiles belong to all layers
                    CollidesWith = spec.HitFilter,
                    GroupIndex = 0
                };

                bool hit = false;
                float3 hitPos = currentPos;
                float3 hitNormal = -math.normalize(delta);
                Entity hitEntity = Entity.Null;
                float timeOfImpact = 1f;

                // Choose collision method based on projectile radius
                if (spec.AoERadius > 0f)
                {
                    var key = BuildColliderKey(spec.AoERadius, spec.HitFilter);
                    if (!ColliderCache.TryGetValue(key, out var collider) || !collider.IsCreated)
                    {
                        return;
                    }

                    var colliderCastInput = new ColliderCastInput(
                        collider,
                        prevPos,
                        currentPos,
                        quaternion.identity);

                    if (PhysicsWorld.CastCollider(colliderCastInput, out var castHit))
                    {
                        hit = true;
                        hitPos = castHit.Position;
                        hitNormal = castHit.SurfaceNormal;
                        hitEntity = castHit.Entity;
                        timeOfImpact = castHit.Fraction;
                    }
                }
                else
                {
                    // Raycast for small/fast projectiles
                    var raycastInput = new RaycastInput
                    {
                        Start = prevPos,
                        End = currentPos,
                        Filter = filter
                    };

                    if (PhysicsWorld.CastRay(raycastInput, out var raycastHit))
                    {
                        hit = true;
                        hitPos = raycastHit.Position;
                        hitNormal = raycastHit.SurfaceNormal;
                        hitEntity = raycastHit.Entity;
                        timeOfImpact = raycastHit.Fraction;
                    }
                }

                if (hit)
                {
                    // Add hit result to buffer
                    hitResults.Add(new ProjectileHitResult
                    {
                        HitPosition = hitPos,
                        HitNormal = hitNormal,
                        HitEntity = hitEntity,
                        TimeOfImpact = timeOfImpact
                    });

                    // Update projectile position to hit point
                    projectile.PrevPos = transform.Position;
                    transform.Position = hitPos;

                    // Emit impact FX request
                    if (HasPresentationHub && ImpactFxBuffers.HasBuffer(HubEntity))
                    {
                        var fxRequests = ImpactFxBuffers[HubEntity];
                        // Use projectile ID hash as effect ID (would be mapped to actual FX in presentation bindings)
                        int effectId = (int)(projectile.ProjectileId.GetHashCode() & 0x7FFFFFFF);
                        // Use 3D-aware look rotation for impact FX orientation
                        OrientationHelpers.LookRotationSafe3D(hitNormal, OrientationHelpers.WorldUp, out var impactRotation);
                        fxRequests.Add(new PlayEffectRequest
                        {
                            EffectId = effectId,
                            Target = Entity.Null,
                            Position = hitPos,
                            Rotation = impactRotation,
                            DurationSeconds = 1f,
                            LifetimePolicy = PresentationLifetimePolicy.Timed,
                            AttachRule = PresentationAttachRule.World
                        });
                    }
                }
            }

            private static ref ProjectileSpec FindProjectileSpec(
                BlobAssetReference<ProjectileCatalogBlob> catalog,
                FixedString64Bytes projectileId)
            {
                if (!catalog.IsCreated)
                {
                    return ref UnsafeRef.Null<ProjectileSpec>();
                }

                ref var projectiles = ref catalog.Value.Projectiles;
                for (int i = 0; i < projectiles.Length; i++)
                {
                    ref var projectileSpec = ref projectiles[i];
                    if (projectileSpec.Id.Equals(projectileId))
                    {
                        return ref projectileSpec;
                    }
                }

                return ref UnsafeRef.Null<ProjectileSpec>();
            }

            private static ulong BuildColliderKey(float radius, uint hitFilter)
            {
                return ProjectileCollisionSystem.BuildColliderKey(radius, hitFilter);
            }
        }
    }
}

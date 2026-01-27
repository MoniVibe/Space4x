using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using PureDOTS.Runtime.Movement;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(ProjectileCollisionSystem))]
    public partial struct ProjectilePoolSpawnSystem : ISystem
    {
        private EntityQuery _spawnQuery;

        public void OnCreate(ref SystemState state)
        {
            _spawnQuery = SystemAPI.QueryBuilder()
                .WithAll<ProjectileSpawnRequest>()
                .Build();

            state.RequireForUpdate(_spawnQuery);
            state.RequireForUpdate<ProjectilePoolConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog) ||
                !projectileCatalog.Catalog.IsCreated)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<ProjectilePoolConfig>(out var poolEntity))
            {
                return;
            }

            var entityManager = state.EntityManager;
            if (!entityManager.HasComponent<ProjectilePoolState>(poolEntity) ||
                !entityManager.HasBuffer<ProjectilePoolEntry>(poolEntity))
            {
                return;
            }

            var poolState = SystemAPI.GetComponentRW<ProjectilePoolState>(poolEntity);
            var poolBuffer = SystemAPI.GetBuffer<ProjectilePoolEntry>(poolEntity);
            var currentTime = timeState.ElapsedTime;

            foreach (var requests in SystemAPI.Query<DynamicBuffer<ProjectileSpawnRequest>>())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < requests.Length; i++)
                {
                    if (poolBuffer.Length == 0)
                    {
                        poolState.ValueRW.Dropped++;
                        break;
                    }

                    var request = requests[i];
                    ref var spec = ref FindProjectileSpec(projectileCatalog.Catalog, request.ProjectileId);
                    if (UnsafeRef.IsNull(ref spec))
                    {
                        continue;
                    }

                    var pooled = poolBuffer[poolBuffer.Length - 1].Projectile;
                    poolBuffer.RemoveAtSwapBack(poolBuffer.Length - 1);

                    if (!entityManager.Exists(pooled))
                    {
                        poolState.ValueRW.Dropped++;
                        continue;
                    }

                    ActivateProjectile(entityManager, pooled, request, ref spec, currentTime);
                }

                requests.Clear();
            }

            poolState.ValueRW.Available = poolBuffer.Length;
            poolState.ValueRW.Active = math.max(0, poolState.ValueRO.Capacity - poolState.ValueRW.Available);
        }

        private static void ActivateProjectile(
            EntityManager entityManager,
            Entity projectile,
            in ProjectileSpawnRequest request,
            ref ProjectileSpec spec,
            float currentTime)
        {
            OrientationHelpers.LookRotationSafe3D(request.SpawnDirection, OrientationHelpers.WorldUp, out var rotation);
            var transform = LocalTransform.FromPositionRotation(request.SpawnPosition, rotation);

            if (entityManager.HasComponent<LocalTransform>(projectile))
            {
                entityManager.SetComponentData(projectile, transform);
            }
            else
            {
                entityManager.AddComponentData(projectile, transform);
            }

            var projectileData = new ProjectileEntity
            {
                ProjectileId = request.ProjectileId,
                SourceEntity = request.SourceEntity,
                TargetEntity = request.TargetEntity,
                Velocity = request.SpawnDirection * spec.Speed,
                PrevPos = request.SpawnPosition,
                SpawnTime = currentTime,
                DistanceTraveled = 0f,
                HitsLeft = math.max(0f, spec.Pierce),
                Age = 0f,
                Seed = request.ShotSeed,
                ShotSequence = request.ShotSequence,
                PelletIndex = request.PelletIndex
            };

            if (entityManager.HasComponent<ProjectileEntity>(projectile))
            {
                entityManager.SetComponentData(projectile, projectileData);
            }
            else
            {
                entityManager.AddComponentData(projectile, projectileData);
            }

            if (entityManager.HasBuffer<ProjectileHitResult>(projectile))
            {
                var hitBuffer = entityManager.GetBuffer<ProjectileHitResult>(projectile);
                hitBuffer.Clear();
            }
            else
            {
                entityManager.AddBuffer<ProjectileHitResult>(projectile);
            }

            if (!entityManager.HasComponent<ProjectileActive>(projectile))
            {
                entityManager.AddComponent<ProjectileActive>(projectile);
            }
            entityManager.SetComponentEnabled<ProjectileActive>(projectile, true);

            if (entityManager.HasComponent<ProjectileRecycleTag>(projectile))
            {
                entityManager.SetComponentEnabled<ProjectileRecycleTag>(projectile, false);
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
                ref var projectile = ref projectiles[i];
                if (projectile.Id.Equals(projectileId))
                {
                    return ref projectile;
                }
            }

            return ref UnsafeRef.Null<ProjectileSpec>();
        }
    }
}

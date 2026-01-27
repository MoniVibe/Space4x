using PureDOTS.Rendering;
using PureDOTS.Runtime.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ProjectilePoolBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectilePoolConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ProjectilePoolConfig>(out var poolEntity))
            {
                return;
            }

            var entityManager = state.EntityManager;
            var config = entityManager.GetComponentData<ProjectilePoolConfig>(poolEntity);
            if (config.Capacity <= 0 || config.Prefab == Entity.Null)
            {
                return;
            }

            if (!entityManager.HasComponent<ProjectilePoolState>(poolEntity))
            {
                entityManager.AddComponentData(poolEntity, new ProjectilePoolState());
            }

            if (!entityManager.HasBuffer<ProjectilePoolEntry>(poolEntity))
            {
                entityManager.AddBuffer<ProjectilePoolEntry>(poolEntity);
            }

            var poolState = entityManager.GetComponentData<ProjectilePoolState>(poolEntity);
            var poolBuffer = entityManager.GetBuffer<ProjectilePoolEntry>(poolEntity);
            var desiredCapacity = math.max(0, config.Capacity);
            var currentCapacity = math.max(poolState.Capacity, poolBuffer.Length);

            if (currentCapacity < desiredCapacity)
            {
                var toCreate = desiredCapacity - currentCapacity;
                for (int i = 0; i < toCreate; i++)
                {
                    var instance = entityManager.Instantiate(config.Prefab);
                    PreparePooledProjectile(entityManager, instance);
                    poolBuffer.Add(new ProjectilePoolEntry { Projectile = instance });
                }

                poolState.Capacity = desiredCapacity;
            }
            else
            {
                poolState.Capacity = currentCapacity;
            }

            poolState.Available = poolBuffer.Length;
            poolState.Active = math.max(0, poolState.Capacity - poolState.Available);
            poolState.Initialized = 1;
            entityManager.SetComponentData(poolEntity, poolState);
        }

        private static void PreparePooledProjectile(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<ProjectileEntity>(entity))
            {
                entityManager.AddComponentData(entity, default(ProjectileEntity));
            }

            if (!entityManager.HasComponent<LocalTransform>(entity))
            {
                entityManager.AddComponentData(entity, LocalTransform.FromPosition(float3.zero));
            }

            if (!entityManager.HasBuffer<ProjectileHitResult>(entity))
            {
                entityManager.AddBuffer<ProjectileHitResult>(entity);
            }

            if (!entityManager.HasComponent<ProjectileActive>(entity))
            {
                entityManager.AddComponent<ProjectileActive>(entity);
            }
            entityManager.SetComponentEnabled<ProjectileActive>(entity, false);

            if (!entityManager.HasComponent<ProjectileRecycleTag>(entity))
            {
                entityManager.AddComponent<ProjectileRecycleTag>(entity);
            }
            entityManager.SetComponentEnabled<ProjectileRecycleTag>(entity, false);

            DisablePresenter<MeshPresenter>(entityManager, entity);
            DisablePresenter<SpritePresenter>(entityManager, entity);
            DisablePresenter<DebugPresenter>(entityManager, entity);
            DisablePresenter<TracerPresenter>(entityManager, entity);
        }

        private static void DisablePresenter<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (entityManager.HasComponent<T>(entity))
            {
                entityManager.SetComponentEnabled<T>(entity, false);
            }
        }
    }
}

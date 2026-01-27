using PureDOTS.Rendering;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ProjectileEffectExecutionSystem))]
    [UpdateAfter(typeof(ProjectileDamageSystem))]
    public partial struct ProjectilePoolRecycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectilePoolConfig>();
            state.RequireForUpdate<ProjectileRecycleTag>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
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

            foreach (var (recycleTag, entity) in SystemAPI.Query<EnabledRefRW<ProjectileRecycleTag>>().WithEntityAccess())
            {
                if (!recycleTag.ValueRO)
                {
                    continue;
                }

                recycleTag.ValueRW = false;

                if (entityManager.HasComponent<ProjectileActive>(entity))
                {
                    entityManager.SetComponentEnabled<ProjectileActive>(entity, false);
                }

                if (entityManager.HasComponent<ProjectileEntity>(entity))
                {
                    entityManager.SetComponentData(entity, default(ProjectileEntity));
                }

                if (entityManager.HasBuffer<ProjectileHitResult>(entity))
                {
                    var hits = entityManager.GetBuffer<ProjectileHitResult>(entity);
                    hits.Clear();
                }

                DisablePresenter<MeshPresenter>(entityManager, entity);
                DisablePresenter<SpritePresenter>(entityManager, entity);
                DisablePresenter<DebugPresenter>(entityManager, entity);
                DisablePresenter<TracerPresenter>(entityManager, entity);

                poolBuffer.Add(new ProjectilePoolEntry { Projectile = entity });
            }

            poolState.ValueRW.Available = poolBuffer.Length;
            poolState.ValueRW.Active = math.max(0, poolState.ValueRO.Capacity - poolState.ValueRW.Available);
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

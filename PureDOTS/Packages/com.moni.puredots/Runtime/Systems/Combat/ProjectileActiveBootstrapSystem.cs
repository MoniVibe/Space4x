using PureDOTS.Runtime.Combat;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    public partial struct ProjectileActiveBootstrapSystem : ISystem
    {
        private EntityQuery _missingActiveQuery;

        public void OnCreate(ref SystemState state)
        {
            _missingActiveQuery = SystemAPI.QueryBuilder()
                .WithAll<ProjectileEntity>()
                .WithNone<ProjectileActive>()
                .Build();

            state.RequireForUpdate(_missingActiveQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_missingActiveQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = state.EntityManager;
            using var entities = _missingActiveQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                entityManager.AddComponent<ProjectileActive>(entity);
                entityManager.SetComponentEnabled<ProjectileActive>(entity, true);

                if (!entityManager.HasComponent<ProjectileRecycleTag>(entity))
                {
                    entityManager.AddComponent<ProjectileRecycleTag>(entity);
                }
                entityManager.SetComponentEnabled<ProjectileRecycleTag>(entity, false);
            }
        }
    }
}

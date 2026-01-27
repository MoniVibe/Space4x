using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="AgencyModuleTag"/> have the required agency/control components/buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct AgencyModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AgencyModuleTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AgencyModuleTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<AgencySelf>(entity))
                {
                    if (em.HasComponent<AgencySelfPreset>(entity))
                    {
                        var preset = em.GetComponentData<AgencySelfPreset>(entity);
                        ecb.AddComponent(entity, AgencySelfPresetUtility.Resolve(preset));
                    }
                    else
                    {
                        ecb.AddComponent(entity, AgencyDefaults.DefaultSelf());
                    }
                }

                if (!em.HasBuffer<ControlLink>(entity))
                {
                    ecb.AddBuffer<ControlLink>(entity);
                }

                if (!em.HasBuffer<ResolvedControl>(entity))
                {
                    ecb.AddBuffer<ResolvedControl>(entity);
                }

                if (!em.HasComponent<RewindableTag>(entity))
                {
                    ecb.AddComponent<RewindableTag>(entity);
                }
            }

            ecb.Playback(em);
        }
    }
}

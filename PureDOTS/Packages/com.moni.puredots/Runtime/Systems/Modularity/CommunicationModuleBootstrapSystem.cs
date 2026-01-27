using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Modularity;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="CommunicationModuleTag"/> have comm endpoint state and buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct CommunicationModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CommunicationModuleTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CommunicationModuleTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<CommEndpoint>(entity))
                {
                    ecb.AddComponent(entity, CommEndpoint.Default);
                }

                if (!em.HasBuffer<CommAttempt>(entity))
                {
                    ecb.AddBuffer<CommAttempt>(entity);
                }

                if (!em.HasBuffer<CommSendRequest>(entity))
                {
                    ecb.AddBuffer<CommSendRequest>(entity);
                }

                if (!em.HasBuffer<CommReceipt>(entity))
                {
                    ecb.AddBuffer<CommReceipt>(entity);
                }

                if (!em.HasBuffer<CommOutboundEntry>(entity))
                {
                    ecb.AddBuffer<CommOutboundEntry>(entity);
                }

                if (!em.HasBuffer<CommRecentMessage>(entity))
                {
                    ecb.AddBuffer<CommRecentMessage>(entity);
                }

                if (!em.HasBuffer<CommPendingClarify>(entity))
                {
                    ecb.AddBuffer<CommPendingClarify>(entity);
                }

                if (!em.HasBuffer<CommDecision>(entity))
                {
                    ecb.AddBuffer<CommDecision>(entity);
                }

                if (!em.HasComponent<CommBudgetState>(entity))
                {
                    ecb.AddComponent(entity, new CommBudgetState());
                }
            }

            ecb.Playback(em);
        }
    }
}

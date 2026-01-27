using PureDOTS.Runtime.Communication;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Communication
{
    /// <summary>
    /// Ensures comm endpoints have the required buffers and state for communications.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup), OrderFirst = true)]
    public partial struct CommunicationEndpointBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommEndpoint>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (endpoint, entity) in SystemAPI.Query<RefRO<CommEndpoint>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<CommAttempt>(entity))
                {
                    ecb.AddBuffer<CommAttempt>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommSendRequest>(entity))
                {
                    ecb.AddBuffer<CommSendRequest>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommReceipt>(entity))
                {
                    ecb.AddBuffer<CommReceipt>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommOutboundEntry>(entity))
                {
                    ecb.AddBuffer<CommOutboundEntry>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommRecentMessage>(entity))
                {
                    ecb.AddBuffer<CommRecentMessage>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommPendingClarify>(entity))
                {
                    ecb.AddBuffer<CommPendingClarify>(entity);
                }

                if (!state.EntityManager.HasBuffer<CommDecision>(entity))
                {
                    ecb.AddBuffer<CommDecision>(entity);
                }

                if (!state.EntityManager.HasComponent<CommBudgetState>(entity))
                {
                    ecb.AddComponent(entity, new CommBudgetState());
                }

                if (!state.EntityManager.HasComponent<CommDecisionConfig>(entity))
                {
                    ecb.AddComponent(entity, CommDecisionConfig.Default);
                }

                if (!state.EntityManager.HasComponent<CommDecodeFactors>(entity))
                {
                    ecb.AddComponent(entity, CommDecodeFactors.Default);
                }

                if (!state.EntityManager.HasComponent<CommLinkQuality>(entity))
                {
                    ecb.AddComponent(entity, CommLinkQuality.Default);
                }
            }
        }
    }
}

using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Ensures logistics boards carry the contract components and buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(LogisticsBoardClaimSystem))]
    public partial struct LogisticsBoardBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LogisticsBoard>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (board, entity) in SystemAPI.Query<RefRO<LogisticsBoard>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<LogisticsBoardConfig>(entity))
                {
                    ecb.AddComponent(entity, LogisticsBoardConfig.Default);
                }

                if (!state.EntityManager.HasComponent<DemandLedger>(entity))
                {
                    ecb.AddComponent(entity, new DemandLedger
                    {
                        ScopeEntity = board.ValueRO.DomainEntity
                    });
                }

                if (!state.EntityManager.HasComponent<TaskDispatcher>(entity))
                {
                    ecb.AddComponent(entity, new TaskDispatcher
                    {
                        ScopeEntity = board.ValueRO.DomainEntity,
                        LastDispatchTick = board.ValueRO.LastUpdateTick
                    });
                }

                if (!state.EntityManager.HasBuffer<LogisticsDemandEntry>(entity))
                {
                    ecb.AddBuffer<LogisticsDemandEntry>(entity);
                }

                if (!state.EntityManager.HasBuffer<LogisticsReservationEntry>(entity))
                {
                    ecb.AddBuffer<LogisticsReservationEntry>(entity);
                }

                if (!state.EntityManager.HasBuffer<LogisticsClaimRequest>(entity))
                {
                    ecb.AddBuffer<LogisticsClaimRequest>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Storehouse
{
    /// <summary>
    /// Ensures storehouses have ledger settings/buffers and comm outboxes for notifications.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup), OrderFirst = true)]
    public partial struct StorehouseLedgerBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StorehouseConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (config, entity) in SystemAPI.Query<RefRO<StorehouseConfig>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<StorehouseLedgerSettings>(entity))
                {
                    ecb.AddComponent(entity, StorehouseLedgerSettings.Default);
                }

                if (!state.EntityManager.HasBuffer<StorehouseLedgerEvent>(entity))
                {
                    ecb.AddBuffer<StorehouseLedgerEvent>(entity);
                }

                if (!state.EntityManager.HasBuffer<PureDOTS.Runtime.Comms.CommsOutboxEntry>(entity))
                {
                    ecb.AddBuffer<PureDOTS.Runtime.Comms.CommsOutboxEntry>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}






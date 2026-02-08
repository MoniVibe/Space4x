using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Perception
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4x.Scenario.Space4XMiningScenarioSystem))]
    [BurstCompile]
    public partial struct Space4XCommsBeatBootstrapSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XCommsBeatConfig>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XCommsBeatConfig>();
            if (config.CommsEnsured != 0)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            var sender = ResolveCarrier(config.SenderCarrierId, ref state);
            var receiver = ResolveCarrier(config.ReceiverCarrierId, ref state);
            if (sender == Entity.Null || receiver == Entity.Null)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            EnsureCommEndpoint(ref ecb, em, sender);
            EnsureCommEndpoint(ref ecb, em, receiver);
            EnsureReceiver(ref ecb, em, receiver, config.TransportMask);

            EnsureMediumContext(ref ecb, em, sender);
            EnsureMediumContext(ref ecb, em, receiver);

            ecb.Playback(em);
            ecb.Dispose();

            config.CommsEnsured = 1;
            SystemAPI.SetSingleton(config);
        }

        private Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            if (carrierId.IsEmpty)
            {
                return Entity.Null;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private static void EnsureCommEndpoint(ref EntityCommandBuffer ecb, EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<CommEndpoint>(entity))
            {
                ecb.AddComponent(entity, CommEndpoint.Default);
            }
        }

        private static void EnsureReceiver(ref EntityCommandBuffer ecb, EntityManager entityManager, Entity entity, PerceptionChannel transportMask)
        {
            var receiverConfig = entityManager.HasComponent<CommsReceiverConfig>(entity)
                ? entityManager.GetComponentData<CommsReceiverConfig>(entity)
                : CommsReceiverConfig.Default;

            if (transportMask != PerceptionChannel.None)
            {
                receiverConfig.TransportMask = transportMask;
            }

            if (receiverConfig.MaxInbox < 32)
            {
                receiverConfig.MaxInbox = 32;
            }

            if (entityManager.HasComponent<CommsReceiverConfig>(entity))
            {
                ecb.SetComponent(entity, receiverConfig);
            }
            else
            {
                ecb.AddComponent(entity, receiverConfig);
            }

            if (!entityManager.HasBuffer<CommsInboxEntry>(entity))
            {
                ecb.AddBuffer<CommsInboxEntry>(entity);
            }

            if (!entityManager.HasBuffer<Interrupt>(entity))
            {
                ecb.AddBuffer<Interrupt>(entity);
            }

            var signal = entityManager.HasComponent<SignalPerceptionState>(entity)
                ? entityManager.GetComponentData<SignalPerceptionState>(entity)
                : default;

            signal.EMLevel = 1f;
            signal.EMConfidence = 1f;
            signal.SoundLevel = 1f;
            signal.SoundConfidence = 1f;

            if (entityManager.HasComponent<SignalPerceptionState>(entity))
            {
                ecb.SetComponent(entity, signal);
            }
            else
            {
                ecb.AddComponent(entity, signal);
            }
        }

        private static void EnsureMediumContext(ref EntityCommandBuffer ecb, EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<MediumContext>(entity))
            {
                ecb.AddComponent(entity, MediumContext.Vacuum);
            }
        }
    }
}

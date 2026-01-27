using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(AgencyControlResolutionSystem))]
    public partial struct ControlHeartbeatSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (order, link) in SystemAPI.Query<RefRO<ControlOrderState>, RefRW<ControlLinkState>>())
            {
                if (order.ValueRO.LastUpdatedTick != 0u && order.ValueRO.LastUpdatedTick > link.ValueRO.LastHeartbeatTick)
                {
                    var updated = link.ValueRO;
                    updated.LastHeartbeatTick = order.ValueRO.LastUpdatedTick;
                    link.ValueRW = updated;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ControlHeartbeatSystem))]
    public partial struct ControlLinkHealthSystem : ISystem
    {
        private ComponentLookup<ControllerIntegrityState> _integrityLookup;
        private ComponentLookup<CompromiseState> _compromiseLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _integrityLookup = state.GetComponentLookup<ControllerIntegrityState>(true);
            _compromiseLookup = state.GetComponentLookup<CompromiseState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _integrityLookup.Update(ref state);
            _compromiseLookup.Update(ref state);

            var tick = timeState.Tick;
            ControlLinkHealthConfig config = new ControlLinkHealthConfig
            {
                HeartbeatTimeoutTicks = 120u,
                MinCommsQuality = 0.2f
            };

            if (SystemAPI.TryGetSingleton<ControlLinkHealthConfig>(out var configOverride))
            {
                config = configOverride;
            }

            foreach (var link in SystemAPI.Query<RefRW<ControlLinkState>>())
            {
                var stateLink = link.ValueRO;
                bool lost = false;

                if (stateLink.ControllerEntity == Entity.Null || !SystemAPI.Exists(stateLink.ControllerEntity))
                {
                    lost = true;
                }

                if (stateLink.CommsQuality01 < config.MinCommsQuality)
                {
                    lost = true;
                }

                if (stateLink.LastHeartbeatTick != 0u && tick > stateLink.LastHeartbeatTick + config.HeartbeatTimeoutTicks)
                {
                    lost = true;
                }

                byte compromised = 0;
                Entity compromiseSource = Entity.Null;
                if (stateLink.ControllerEntity != Entity.Null)
                {
                    if (_compromiseLookup.HasComponent(stateLink.ControllerEntity))
                    {
                        var compromise = _compromiseLookup[stateLink.ControllerEntity];
                        if (compromise.IsCompromised != 0)
                        {
                            compromised = 1;
                            compromiseSource = compromise.Source;
                        }
                    }
                    else if (_integrityLookup.HasComponent(stateLink.ControllerEntity))
                    {
                        var integrity = _integrityLookup[stateLink.ControllerEntity];
                        if (integrity.IsCompromised != 0)
                        {
                            compromised = 1;
                            compromiseSource = integrity.CompromisedBy;
                        }
                    }
                }

                if (stateLink.IsLost != (byte)(lost ? 1 : 0) ||
                    stateLink.IsCompromised != compromised ||
                    stateLink.CompromiseSource != compromiseSource)
                {
                    stateLink.IsLost = (byte)(lost ? 1 : 0);
                    stateLink.IsCompromised = compromised;
                    stateLink.CompromiseSource = compromiseSource;
                    link.ValueRW = stateLink;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ControlLinkHealthSystem))]
    public partial struct ControlOrderRetentionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = timeState.Tick;
            uint heartbeatTimeout = 120u;

            if (SystemAPI.TryGetSingleton<ControlLinkHealthConfig>(out var configOverride))
            {
                heartbeatTimeout = math.max(1u, configOverride.HeartbeatTimeoutTicks);
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (order, link, entity) in SystemAPI.Query<RefRW<ControlOrderState>, RefRO<ControlLinkState>>()
                         .WithEntityAccess())
            {
                bool heartbeatLost = order.ValueRO.RequiresHeartbeat != 0 &&
                    (link.ValueRO.LastHeartbeatTick == 0u || tick > link.ValueRO.LastHeartbeatTick + heartbeatTimeout);

                bool expired = order.ValueRO.ExpiryTick != 0u && tick >= order.ValueRO.ExpiryTick;

                if ((link.ValueRO.IsLost != 0 || heartbeatLost) && expired && order.ValueRO.Kind != order.ValueRO.FallbackKind)
                {
                    var updated = order.ValueRO;
                    updated.Kind = updated.FallbackKind;
                    updated.IssuedTick = tick;
                    updated.LastUpdatedTick = tick;
                    updated.Sequence += 1u;
                    updated.ExpiryTick = 0u;
                    order.ValueRW = updated;
                }

                if ((link.ValueRO.IsLost != 0 && expired) || link.ValueRO.IsCompromised != 0)
                {
                    if (!state.EntityManager.HasComponent<RogueToolState>(entity))
                    {
                        ecb.AddComponent(entity, new RogueToolState
                        {
                            Reason = link.ValueRO.IsCompromised != 0 ? RogueToolReason.HostileOverride : RogueToolReason.LostControl,
                            SinceTick = tick,
                            AllowFriendlyDestructionNoPenalty = 1,
                            Hackable = 1,
                            Reserved0 = 0
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

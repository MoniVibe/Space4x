using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Translates executing captain orders into headless directives (mining orders, haul route activation).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XCaptainDirectiveSystem : ISystem
    {
        private ComponentLookup<CaptainOrder> _captainOrderLookup;
        private ComponentLookup<ResourceTypeId> _resourceTypeLookup;
        private ComponentLookup<MiningOrder> _miningOrderLookup;
        private ComponentLookup<MiningYield> _yieldLookup;
        private ComponentLookup<Space4XTradeRoute> _tradeRouteLookup;
        private ComponentLookup<TradeRouteStatus> _tradeStatusLookup;
        private ComponentLookup<Space4XLogisticsRoute> _logisticsRouteLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CaptainOrder>();

            _captainOrderLookup = state.GetComponentLookup<CaptainOrder>(false);
            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);
            _miningOrderLookup = state.GetComponentLookup<MiningOrder>(false);
            _yieldLookup = state.GetComponentLookup<MiningYield>(false);
            _tradeRouteLookup = state.GetComponentLookup<Space4XTradeRoute>(false);
            _tradeStatusLookup = state.GetComponentLookup<TradeRouteStatus>(false);
            _logisticsRouteLookup = state.GetComponentLookup<Space4XLogisticsRoute>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceTypeLookup.Update(ref state);
            _miningOrderLookup.Update(ref state);
            _yieldLookup.Update(ref state);
            _tradeRouteLookup.Update(ref state);
            _tradeStatusLookup.Update(ref state);
            _logisticsRouteLookup.Update(ref state);
            _captainOrderLookup.Update(ref state);

            var currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var mineOrders = new NativeHashMap<Entity, MiningDirective>(16, Allocator.Temp);
            var hasMineOrders = false;
            var carriersApplied = new NativeList<Entity>(Allocator.Temp);

            foreach (var (order, entity) in SystemAPI.Query<RefRW<CaptainOrder>>().WithEntityAccess())
            {
                if (order.ValueRO.Status != CaptainOrderStatus.Executing)
                {
                    continue;
                }

                var handled = false;
                switch (order.ValueRO.Type)
                {
                    case CaptainOrderType.Mine:
                        if (SystemAPI.HasComponent<Carrier>(entity))
                        {
                            if (!mineOrders.ContainsKey(entity))
                            {
                                mineOrders.TryAdd(entity, ResolveMiningDirective(order.ValueRO, ref _resourceTypeLookup));
                                hasMineOrders = true;
                            }
                        }
                        else if (SystemAPI.HasComponent<MiningVessel>(entity))
                        {
                            var directive = ResolveMiningDirective(order.ValueRO, ref _resourceTypeLookup);
                            ApplyMiningDirective(entity, directive, currentTick, ref _miningOrderLookup, ref _yieldLookup, ref ecb);
                            handled = true;
                        }
                        break;

                    case CaptainOrderType.Haul:
                        handled |= TryActivateTradeRoute(order.ValueRO.TargetEntity, currentTick, ref _tradeRouteLookup, ref _tradeStatusLookup);
                        handled |= TryActivateTradeRoute(entity, currentTick, ref _tradeRouteLookup, ref _tradeStatusLookup);
                        handled |= TryActivateLogisticsRoute(order.ValueRO.TargetEntity, ref _logisticsRouteLookup);
                        handled |= TryActivateLogisticsRoute(entity, ref _logisticsRouteLookup);
                        break;
                }

                if (handled)
                {
                    order.ValueRW.Status = CaptainOrderStatus.Completed;
                }
            }

            if (hasMineOrders)
            {
                foreach (var (vessel, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithEntityAccess())
                {
                    var carrierEntity = vessel.ValueRO.CarrierEntity;
                    if (carrierEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (!mineOrders.TryGetValue(carrierEntity, out var directive))
                    {
                        continue;
                    }

                    ApplyMiningDirective(entity, directive, currentTick, ref _miningOrderLookup, ref _yieldLookup, ref ecb);
                    carriersApplied.Add(carrierEntity);
                }
            }

            for (int i = 0; i < carriersApplied.Length; i++)
            {
                var carrierEntity = carriersApplied[i];
                if (!_captainOrderLookup.HasComponent(carrierEntity))
                {
                    continue;
                }

                var order = _captainOrderLookup[carrierEntity];
                if (order.Status == CaptainOrderStatus.Executing && order.Type == CaptainOrderType.Mine)
                {
                    order.Status = CaptainOrderStatus.Completed;
                    _captainOrderLookup[carrierEntity] = order;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            mineOrders.Dispose();
            carriersApplied.Dispose();
        }

        private static MiningDirective ResolveMiningDirective(in CaptainOrder order, ref ComponentLookup<ResourceTypeId> resourceTypeLookup)
        {
            var directive = new MiningDirective
            {
                PreferredTarget = Entity.Null,
                ResourceId = default
            };

            if (order.TargetEntity != Entity.Null && resourceTypeLookup.HasComponent(order.TargetEntity))
            {
                directive.ResourceId = resourceTypeLookup[order.TargetEntity].Value;
                directive.PreferredTarget = order.TargetEntity;
            }

            return directive;
        }

        private static void ApplyMiningDirective(
            Entity vesselEntity,
            in MiningDirective directive,
            uint currentTick,
            ref ComponentLookup<MiningOrder> orderLookup,
            ref ComponentLookup<MiningYield> yieldLookup,
            ref EntityCommandBuffer ecb)
        {
            if (orderLookup.HasComponent(vesselEntity))
            {
                var miningOrder = orderLookup[vesselEntity];
                if (!directive.ResourceId.IsEmpty)
                {
                    miningOrder.ResourceId = directive.ResourceId;
                }

                miningOrder.Source = MiningOrderSource.Input;
                miningOrder.Status = MiningOrderStatus.Pending;
                miningOrder.PreferredTarget = directive.PreferredTarget;
                miningOrder.TargetEntity = Entity.Null;
                miningOrder.IssuedTick = currentTick;
                orderLookup[vesselEntity] = miningOrder;
            }
            else
            {
                var miningOrder = new MiningOrder
                {
                    ResourceId = directive.ResourceId,
                    Source = MiningOrderSource.Input,
                    Status = MiningOrderStatus.Pending,
                    PreferredTarget = directive.PreferredTarget,
                    TargetEntity = Entity.Null,
                    IssuedTick = currentTick
                };

                ecb.AddComponent(vesselEntity, miningOrder);
            }

            if (!directive.ResourceId.IsEmpty && yieldLookup.HasComponent(vesselEntity))
            {
                var yield = yieldLookup[vesselEntity];
                if (yield.ResourceId.IsEmpty)
                {
                    yield.ResourceId = directive.ResourceId;
                    yieldLookup[vesselEntity] = yield;
                }
            }
        }

        private static bool TryActivateTradeRoute(
            Entity routeEntity,
            uint currentTick,
            ref ComponentLookup<Space4XTradeRoute> tradeRouteLookup,
            ref ComponentLookup<TradeRouteStatus> tradeStatusLookup)
        {
            if (routeEntity == Entity.Null || !tradeRouteLookup.HasComponent(routeEntity))
            {
                return false;
            }

            var route = tradeRouteLookup[routeEntity];
            route.IsActive = 1;
            route.LastTripTick = route.TripFrequency > 0 ? currentTick - route.TripFrequency : currentTick;
            tradeRouteLookup[routeEntity] = route;

            if (tradeStatusLookup.HasComponent(routeEntity))
            {
                var status = tradeStatusLookup[routeEntity];
                if (status.Phase == TradeRoutePhase.Idle)
                {
                    status.Progress = (half)0f;
                    tradeStatusLookup[routeEntity] = status;
                }
            }

            return true;
        }

        private static bool TryActivateLogisticsRoute(
            Entity routeEntity,
            ref ComponentLookup<Space4XLogisticsRoute> logisticsRouteLookup)
        {
            if (routeEntity == Entity.Null || !logisticsRouteLookup.HasComponent(routeEntity))
            {
                return false;
            }

            var route = logisticsRouteLookup[routeEntity];
            route.Status = Space4XLogisticsRouteStatus.Operational;
            logisticsRouteLookup[routeEntity] = route;
            return true;
        }

        private struct MiningDirective
        {
            public FixedString64Bytes ResourceId;
            public Entity PreferredTarget;
        }
    }
}

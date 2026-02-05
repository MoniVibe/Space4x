using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XSeatConsoleInterfaceSystem))]
    public partial struct Space4XSeatRecommendationSystem : ISystem
    {
        private ComponentLookup<SeatInstrumentFeed> _feedLookup;
        private ComponentLookup<SeatConsoleState> _consoleLookup;
        private ComponentLookup<TargetPriority> _targetPriorityLookup;
        private BufferLookup<SeatRecommendation> _recommendationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<AuthoritySeat>();

            _feedLookup = state.GetComponentLookup<SeatInstrumentFeed>(true);
            _consoleLookup = state.GetComponentLookup<SeatConsoleState>(true);
            _targetPriorityLookup = state.GetComponentLookup<TargetPriority>(true);
            _recommendationLookup = state.GetBufferLookup<SeatRecommendation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _feedLookup.Update(ref state);
            _consoleLookup.Update(ref state);
            _targetPriorityLookup.Update(ref state);
            _recommendationLookup.Update(ref state);

            var tuning = SeatRecommendationTuning.Default;
            if (SystemAPI.TryGetSingleton<SeatRecommendationTuning>(out var tuningSingleton))
            {
                tuning = tuningSingleton;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (seat, occupant, seatEntity) in SystemAPI.Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>().WithEntityAccess())
            {
                if (!_feedLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var rights = seat.ValueRO.Rights;
                if ((rights & AuthoritySeatRights.Recommend) == 0 && (rights & AuthoritySeatRights.Issue) == 0)
                {
                    continue;
                }

                var controller = occupant.ValueRO.OccupantEntity;
                if (controller == Entity.Null)
                {
                    continue;
                }

                var buffer = _recommendationLookup.HasBuffer(seatEntity)
                    ? _recommendationLookup[seatEntity]
                    : ecb.AddBuffer<SeatRecommendation>(seatEntity);
                buffer.Clear();

                var feed = _feedLookup[seatEntity];
                var consoleQuality = _consoleLookup.HasComponent(seatEntity)
                    ? math.saturate(_consoleLookup[seatEntity].ConsoleQuality)
                    : 0.5f;
                var confidence = (half)math.saturate(0.45f + consoleQuality * 0.55f);

                var domains = seat.ValueRO.Domains;
                if ((domains & AgencyDomain.Logistics) != 0)
                {
                    TryAddLogisticsRecommendation(feed, tuning.Logistics, confidence, timeState.Tick, ref buffer);
                }

                if ((domains & AgencyDomain.Combat) != 0)
                {
                    TryAddCombatRecommendation(seat.ValueRO.BodyEntity, feed, tuning.Combat, confidence, timeState.Tick, ref buffer, _targetPriorityLookup);
                }

                if ((domains & AgencyDomain.Sensors) != 0)
                {
                    TryAddSensorsRecommendation(seat.ValueRO.BodyEntity, feed, tuning.Sensors, confidence, timeState.Tick, ref buffer, _targetPriorityLookup);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void TryAddLogisticsRecommendation(
            in SeatInstrumentFeed feed,
            in LogisticsRecommendationTuning tuning,
            half confidence,
            uint tick,
            ref DynamicBuffer<SeatRecommendation> buffer)
        {
            if (feed.HullRatio <= tuning.RetreatHullRatio)
            {
                buffer.Add(new SeatRecommendation
                {
                    Source = SeatRecommendationSource.Logistics,
                    Domain = AgencyDomain.Logistics,
                    OrderType = CaptainOrderType.Retreat,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    Priority = tuning.RetreatPriority,
                    Confidence = confidence,
                    RecommendedTick = tick
                });
                return;
            }

            if (feed.FuelRatio <= tuning.ResupplyFuelRatio || feed.AmmoRatio <= tuning.ResupplyAmmoRatio)
            {
                buffer.Add(new SeatRecommendation
                {
                    Source = SeatRecommendationSource.Logistics,
                    Domain = AgencyDomain.Logistics,
                    OrderType = CaptainOrderType.Resupply,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    Priority = tuning.ResupplyPriority,
                    Confidence = confidence,
                    RecommendedTick = tick
                });
            }
        }

        private static void TryAddCombatRecommendation(
            Entity shipEntity,
            in SeatInstrumentFeed feed,
            in CombatRecommendationTuning tuning,
            half confidence,
            uint tick,
            ref DynamicBuffer<SeatRecommendation> buffer,
            ComponentLookup<TargetPriority> targetPriorityLookup)
        {
            if (feed.WeaponMounts == 0 || feed.ContactsTracked < tuning.MinContacts)
            {
                return;
            }

            var onlineRatio = feed.WeaponMounts > 0
                ? math.saturate(feed.WeaponsOnline / (float)feed.WeaponMounts)
                : 0f;
            if (onlineRatio < tuning.MinWeaponsOnlineRatio)
            {
                return;
            }

            if (shipEntity == Entity.Null || !targetPriorityLookup.HasComponent(shipEntity))
            {
                return;
            }

            var priority = targetPriorityLookup[shipEntity];
            if (priority.CurrentTarget == Entity.Null)
            {
                return;
            }

            var contactWeight = math.clamp(
                tuning.AttackPriorityBase - feed.ContactsTracked * tuning.AttackPriorityPerContact,
                tuning.AttackPriorityMin,
                tuning.AttackPriorityBase);
            buffer.Add(new SeatRecommendation
            {
                Source = SeatRecommendationSource.Weapons,
                Domain = AgencyDomain.Combat,
                OrderType = CaptainOrderType.Attack,
                TargetEntity = priority.CurrentTarget,
                TargetPosition = float3.zero,
                Priority = (byte)contactWeight,
                Confidence = confidence,
                RecommendedTick = tick
            });
        }

        private static void TryAddSensorsRecommendation(
            Entity shipEntity,
            in SeatInstrumentFeed feed,
            in SensorsRecommendationTuning tuning,
            half confidence,
            uint tick,
            ref DynamicBuffer<SeatRecommendation> buffer,
            ComponentLookup<TargetPriority> targetPriorityLookup)
        {
            if (feed.SensorRange < tuning.MinSensorRange)
            {
                return;
            }

            if (feed.ContactsTracked == 0)
            {
                buffer.Add(new SeatRecommendation
                {
                    Source = SeatRecommendationSource.Sensors,
                    Domain = AgencyDomain.Sensors,
                    OrderType = CaptainOrderType.Patrol,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    Priority = tuning.PatrolPriority,
                    Confidence = confidence,
                    RecommendedTick = tick
                });
                return;
            }

            if (shipEntity == Entity.Null || !targetPriorityLookup.HasComponent(shipEntity))
            {
                return;
            }

            var priority = targetPriorityLookup[shipEntity];
            if (priority.CurrentTarget == Entity.Null)
            {
                return;
            }

            buffer.Add(new SeatRecommendation
            {
                Source = SeatRecommendationSource.Sensors,
                Domain = AgencyDomain.Sensors,
                OrderType = CaptainOrderType.Intercept,
                TargetEntity = priority.CurrentTarget,
                TargetPosition = float3.zero,
                Priority = tuning.InterceptPriority,
                Confidence = confidence,
                RecommendedTick = tick
            });
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XCaptainRecommendationBridgeSystem : ISystem
    {
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private BufferLookup<SeatRecommendation> _recommendationLookup;
        private BufferLookup<CaptainOrderQueue> _queueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<CaptainOrder>();

            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _recommendationLookup = state.GetBufferLookup<SeatRecommendation>(true);
            _queueLookup = state.GetBufferLookup<CaptainOrderQueue>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _seatRefLookup.Update(ref state);
            _recommendationLookup.Update(ref state);
            _queueLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (order, shipEntity) in SystemAPI.Query<RefRW<CaptainOrder>>().WithEntityAccess())
            {
                if (!_seatRefLookup.HasBuffer(shipEntity))
                {
                    continue;
                }

                var queue = _queueLookup.HasBuffer(shipEntity)
                    ? _queueLookup[shipEntity]
                    : ecb.AddBuffer<CaptainOrderQueue>(shipEntity);
                queue.Clear();

                var seats = _seatRefLookup[shipEntity];
                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null || !_recommendationLookup.HasBuffer(seatEntity))
                    {
                        continue;
                    }

                    var recommendations = _recommendationLookup[seatEntity];
                    for (int r = 0; r < recommendations.Length; r++)
                    {
                        var rec = recommendations[r];
                        var candidate = CreateOrder(rec, seatEntity, timeState.Tick);
                        if (candidate.Type == CaptainOrderType.None)
                        {
                            continue;
                        }

                        if (IsDuplicate(queue, candidate))
                        {
                            continue;
                        }

                        queue.Add(new CaptainOrderQueue { Order = candidate });
                    }
                }

                if (order.ValueRO.Type != CaptainOrderType.None || queue.Length == 0)
                {
                    continue;
                }

                var bestIndex = 0;
                var bestPriority = queue[0].Order.Priority;
                for (int i = 1; i < queue.Length; i++)
                {
                    var priority = queue[i].Order.Priority;
                    if (priority < bestPriority)
                    {
                        bestPriority = priority;
                        bestIndex = i;
                    }
                }

                order.ValueRW = queue[bestIndex].Order;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static CaptainOrder CreateOrder(in SeatRecommendation rec, Entity seatEntity, uint tick)
        {
            if (rec.OrderType == CaptainOrderType.None)
            {
                return new CaptainOrder();
            }

            if (rec.TargetEntity != Entity.Null)
            {
                return CaptainOrder.Create(rec.OrderType, rec.TargetEntity, rec.Priority, tick, seatEntity);
            }

            return CaptainOrder.CreatePositional(rec.OrderType, rec.TargetPosition, rec.Priority, tick, seatEntity);
        }

        private static bool IsDuplicate(DynamicBuffer<CaptainOrderQueue> queue, in CaptainOrder candidate)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                var existing = queue[i].Order;
                if (existing.Type != candidate.Type)
                {
                    continue;
                }

                if (existing.TargetEntity != Entity.Null || candidate.TargetEntity != Entity.Null)
                {
                    if (existing.TargetEntity == candidate.TargetEntity)
                    {
                        return true;
                    }

                    continue;
                }

                if (math.distancesq(existing.TargetPosition, candidate.TargetPosition) <= 0.01f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

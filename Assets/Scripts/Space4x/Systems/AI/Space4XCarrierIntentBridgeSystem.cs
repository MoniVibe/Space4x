using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using FixedString64Bytes = Unity.Collections.FixedString64Bytes;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Bridges carrier mining targets into EntityIntent so intent-driven movement can take over.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierMiningScanSystem))]
    public partial struct Space4XCarrierIntentBridgeSystem : ISystem
    {
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<Space4XFleet> _fleetLookup;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleCaptain;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CarrierMiningTarget>();

            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _fleetLookup = state.GetComponentLookup<Space4XFleet>(true);
            _roleNavigationOfficer = default;
            _roleNavigationOfficer.Append('s');
            _roleNavigationOfficer.Append('h');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('p');
            _roleNavigationOfficer.Append('.');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('v');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('g');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('t');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('_');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('c');
            _roleNavigationOfficer.Append('e');
            _roleNavigationOfficer.Append('r');

            _roleShipmaster = default;
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('h');
            _roleShipmaster.Append('i');
            _roleShipmaster.Append('p');
            _roleShipmaster.Append('.');
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('h');
            _roleShipmaster.Append('i');
            _roleShipmaster.Append('p');
            _roleShipmaster.Append('m');
            _roleShipmaster.Append('a');
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('t');
            _roleShipmaster.Append('e');
            _roleShipmaster.Append('r');

            _roleCaptain = default;
            _roleCaptain.Append('s');
            _roleCaptain.Append('h');
            _roleCaptain.Append('i');
            _roleCaptain.Append('p');
            _roleCaptain.Append('.');
            _roleCaptain.Append('c');
            _roleCaptain.Append('a');
            _roleCaptain.Append('p');
            _roleCaptain.Append('t');
            _roleCaptain.Append('a');
            _roleCaptain.Append('i');
            _roleCaptain.Append('n');
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

            if (SystemAPI.HasSingleton<Space4XCollisionScenarioTag>())
            {
                return;
            }

            _behaviorDispositionLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _fleetLookup.Update(ref state);

            var beatConfig = default(Space4XSteeringStabilityBeatConfig);
            var applyBeatHold = false;
            if (SystemAPI.TryGetSingleton<Space4XSteeringStabilityBeatConfig>(out beatConfig) &&
                beatConfig.FleetId.Length > 0 &&
                beatConfig.HoldCarrierMiningIntent != 0)
            {
                var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
                var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);
                var startTick = runtime.StartTick + SecondsToTicks(beatConfig.StartSeconds, fixedDt);
                var settleTicks = SecondsToTicks(beatConfig.SettleSeconds, fixedDt);
                var measureTicks = math.max(1u, SecondsToTicks(beatConfig.MeasureSeconds, fixedDt));
                var endTick = startTick + settleTicks + measureTicks;
                applyBeatHold = timeState.Tick >= startTick && timeState.Tick <= endTick;
            }

            foreach (var (target, intent, transform, entity) in SystemAPI
                         .Query<RefRO<CarrierMiningTarget>, RefRW<EntityIntent>, RefRO<LocalTransform>>()
                         .WithAll<Carrier>()
                         .WithEntityAccess())
            {
                if (applyBeatHold && _fleetLookup.HasComponent(entity) && _fleetLookup[entity].FleetId.Equals(beatConfig.FleetId))
                {
                    continue;
                }

                if (target.ValueRO.TargetEntity == Entity.Null)
                {
                    if (intent.ValueRO.IsValid != 0 && intent.ValueRO.Mode != IntentMode.Idle)
                    {
                        intent.ValueRW.Mode = IntentMode.Idle;
                        intent.ValueRW.IsValid = 0;
                        intent.ValueRW.IntentSetTick = timeState.Tick;
                    }
                    continue;
                }

                var targetPosition = target.ValueRO.TargetPosition;
                if (math.lengthsq(targetPosition) < 0.01f)
                {
                    targetPosition = transform.ValueRO.Position;
                }

                var profileEntity = ResolveProfileEntity(entity);
                var disposition = ResolveDisposition(profileEntity, entity);
                var priority = ResolvePriority(disposition);

                if (intent.ValueRO.IsValid != 0 &&
                    intent.ValueRO.Mode != IntentMode.Idle &&
                    intent.ValueRO.Priority > priority &&
                    intent.ValueRO.IntentSetTick >= target.ValueRO.AssignedTick)
                {
                    continue;
                }

                var shouldUpdate = intent.ValueRO.IsValid == 0
                                   || intent.ValueRO.Mode != IntentMode.MoveTo
                                   || intent.ValueRO.TargetEntity != target.ValueRO.TargetEntity
                                   || math.distance(intent.ValueRO.TargetPosition, targetPosition) > 0.1f
                                   || intent.ValueRO.Priority != priority;

                if (!shouldUpdate)
                {
                    continue;
                }

                intent.ValueRW.Mode = IntentMode.MoveTo;
                intent.ValueRW.TargetEntity = target.ValueRO.TargetEntity;
                intent.ValueRW.TargetPosition = targetPosition;
                intent.ValueRW.TriggeringInterrupt = InterruptType.ObjectiveChanged;
                intent.ValueRW.Priority = priority;
                intent.ValueRW.IntentSetTick = timeState.Tick;
                intent.ValueRW.IsValid = 1;
            }
        }

        private InterruptPriority ResolvePriority(in BehaviorDisposition disposition)
        {
            var score = disposition.Compliance * 0.7f + (1f - disposition.Caution) * 0.3f;
            if (score >= 0.7f)
            {
                return InterruptPriority.High;
            }

            if (score <= 0.4f)
            {
                return InterruptPriority.Low;
            }

            return InterruptPriority.Normal;
        }

        private BehaviorDisposition ResolveDisposition(Entity profileEntity, Entity carrierEntity)
        {
            if (_behaviorDispositionLookup.HasComponent(profileEntity))
            {
                return _behaviorDispositionLookup[profileEntity];
            }

            if (_behaviorDispositionLookup.HasComponent(carrierEntity))
            {
                return _behaviorDispositionLookup[carrierEntity];
            }

            return BehaviorDisposition.Default;
        }

        private Entity ResolveProfileEntity(Entity carrierEntity)
        {
            if (TryResolveController(carrierEntity, AgencyDomain.Movement, out var controller))
            {
                return controller != Entity.Null ? controller : carrierEntity;
            }

            if (_pilotLookup.HasComponent(carrierEntity))
            {
                var pilot = _pilotLookup[carrierEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            var navigationOfficer = ResolveSeatOccupant(carrierEntity, _roleNavigationOfficer);
            if (navigationOfficer != Entity.Null)
            {
                return navigationOfficer;
            }

            var shipmaster = ResolveSeatOccupant(carrierEntity, _roleShipmaster);
            if (shipmaster != Entity.Null)
            {
                return shipmaster;
            }

            var captain = ResolveSeatOccupant(carrierEntity, _roleCaptain);
            if (captain != Entity.Null)
            {
                return captain;
            }

            return carrierEntity;
        }

        private bool TryResolveController(Entity carrierEntity, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(carrierEntity))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[carrierEntity];
            for (int i = 0; i < resolved.Length; i++)
            {
                if (resolved[i].Domain == domain)
                {
                    controller = resolved[i].Controller;
                    return true;
                }
            }

            return false;
        }

        private Entity ResolveSeatOccupant(Entity carrierEntity, FixedString64Bytes roleId)
        {
            if (!_seatRefLookup.HasBuffer(carrierEntity))
            {
                return Entity.Null;
            }

            var seats = _seatRefLookup[carrierEntity];
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!seat.RoleId.Equals(roleId))
                {
                    continue;
                }

                if (_seatOccupantLookup.HasComponent(seatEntity))
                {
                    return _seatOccupantLookup[seatEntity].OccupantEntity;
                }

                return Entity.Null;
            }

            return Entity.Null;
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            var safeDt = math.max(1e-6f, fixedDt);
            return (uint)math.ceil(math.max(0f, seconds) / safeDt);
        }
    }
}

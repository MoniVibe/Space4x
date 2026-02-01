using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Profile;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Drives seat-specific focus abilities and issues focus requests for seat occupants.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFocusAbilitySystem))]
    public partial struct Space4XSeatFocusDecisionSystem : ISystem
    {
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<ShipSystemsSnapshot> _snapshotLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorLookup;
        private ComponentLookup<Space4XEntityFocus> _focusLookup;
        private ComponentLookup<OfficerFocusProfile> _profileLookup;
        private BufferLookup<Space4XActiveFocusAbility> _abilityLookup;
        private ComponentLookup<FocusAbilityRequest> _requestLookup;
        private ComponentLookup<FocusAbilityDeactivateRequest> _deactivateLookup;
        private ComponentLookup<Carrier> _carrierLookup;

        private FixedString64Bytes _roleCaptain;
        private FixedString64Bytes _roleXO;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleFleetAdmiral;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleWeaponsOfficer;
        private FixedString64Bytes _roleSensorsOfficer;
        private FixedString64Bytes _roleCommunicationsOfficer;
        private FixedString64Bytes _roleLogisticsOfficer;
        private FixedString64Bytes _roleChiefEngineer;
        private FixedString64Bytes _roleSecurityOfficer;
        private FixedString64Bytes _roleMarineCommander;
        private FixedString64Bytes _roleMarineSergeant;
        private FixedString64Bytes _roleFlightCommander;
        private FixedString64Bytes _roleFlightDirector;
        private FixedString64Bytes _roleHangarDeckOfficer;

        private enum SeatFocusRole : byte
        {
            None = 0,
            Command = 1,
            Weapons = 2,
            Sensors = 3,
            Engineering = 4,
            Tactical = 5,
            Operations = 6
        }

        private struct ShipFocusStatus
        {
            public float HullRatio;
            public float ShieldRatio;
            public float FuelRatio;
            public float AmmoRatio;
            public float SensorRange;
            public byte ContactsTracked;
            public byte WeaponMounts;
            public byte WeaponsOnline;
        }

        public void OnCreate(ref SystemState state)
        {
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _snapshotLookup = state.GetComponentLookup<ShipSystemsSnapshot>(true);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(true);
            _behaviorLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _focusLookup = state.GetComponentLookup<Space4XEntityFocus>(true);
            _profileLookup = state.GetComponentLookup<OfficerFocusProfile>(true);
            _abilityLookup = state.GetBufferLookup<Space4XActiveFocusAbility>(true);
            _requestLookup = state.GetComponentLookup<FocusAbilityRequest>(true);
            _deactivateLookup = state.GetComponentLookup<FocusAbilityDeactivateRequest>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);

            _roleCaptain = new FixedString64Bytes("ship.captain");
            _roleXO = new FixedString64Bytes("ship.xo");
            _roleShipmaster = new FixedString64Bytes("ship.shipmaster");
            _roleFleetAdmiral = new FixedString64Bytes("ship.fleet_admiral");
            _roleNavigationOfficer = new FixedString64Bytes("ship.navigation_officer");
            _roleWeaponsOfficer = new FixedString64Bytes("ship.weapons_officer");
            _roleSensorsOfficer = new FixedString64Bytes("ship.sensors_officer");
            _roleCommunicationsOfficer = new FixedString64Bytes("ship.communications_officer");
            _roleLogisticsOfficer = new FixedString64Bytes("ship.logistics_officer");
            _roleChiefEngineer = new FixedString64Bytes("ship.chief_engineer");
            _roleSecurityOfficer = new FixedString64Bytes("ship.security_officer");
            _roleMarineCommander = new FixedString64Bytes("ship.marine_commander");
            _roleMarineSergeant = new FixedString64Bytes("ship.marine_sergeant");
            _roleFlightCommander = new FixedString64Bytes("ship.flight_commander");
            _roleFlightDirector = new FixedString64Bytes("ship.flight_director");
            _roleHangarDeckOfficer = new FixedString64Bytes("ship.hangar_deck_officer");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _seatLookup.Update(ref state);
            _occupantLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _snapshotLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _behaviorLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _abilityLookup.Update(ref state);
            _requestLookup.Update(ref state);
            _deactivateLookup.Update(ref state);
            _carrierLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (seat, occupant) in SystemAPI.Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>())
            {
                var seatRole = ResolveRole(seat.ValueRO.RoleId);
                if (seatRole == SeatFocusRole.None)
                {
                    continue;
                }

                var occupantEntity = occupant.ValueRO.OccupantEntity;
                if (occupantEntity == Entity.Null || !_focusLookup.HasComponent(occupantEntity) || !_profileLookup.HasComponent(occupantEntity))
                {
                    continue;
                }

                if (!_abilityLookup.HasBuffer(occupantEntity))
                {
                    continue;
                }

                var shipEntity = seat.ValueRO.BodyEntity;
                if (shipEntity == Entity.Null)
                {
                    continue;
                }

                var focus = _focusLookup[occupantEntity];
                if (focus.IsInComa != 0)
                {
                    continue;
                }

                var profile = _profileLookup[occupantEntity];
                var behavior = _behaviorLookup.HasComponent(occupantEntity)
                    ? _behaviorLookup[occupantEntity]
                    : BehaviorDisposition.Default;

                var inCombat = false;
                if (_engagementLookup.HasComponent(shipEntity))
                {
                    var engagement = _engagementLookup[shipEntity];
                    inCombat = engagement.Phase == EngagementPhase.Approaching || engagement.Phase == EngagementPhase.Engaged;
                }

                var status = ResolveShipStatus(shipEntity);
                var focusRatio = focus.MaxFocus > 0f ? focus.CurrentFocus / focus.MaxFocus : 0f;

                var desiredAbility = ResolveDesiredAbility(seatRole, status, behavior, inCombat, focusRatio, shipEntity);
                var abilities = _abilityLookup[occupantEntity];

                var queuedActivation = false;
                var queuedDeactivation = false;

                if (desiredAbility != Space4XFocusAbilityType.None)
                {
                    if (!HasAbility(abilities, desiredAbility) &&
                        Space4XFocusAbilityDefinitions.CanActivate(desiredAbility, focus, profile))
                    {
                        QueueActivate(ref ecb, occupantEntity, desiredAbility);
                        queuedActivation = true;
                    }

                    if (!queuedDeactivation)
                    {
                        queuedDeactivation = QueueDeactivateConflicts(seatRole, desiredAbility, abilities, occupantEntity, ref ecb);
                    }
                }
                else
                {
                    QueueDeactivateAny(seatRole, abilities, occupantEntity, ref ecb);
                }

                if (!queuedActivation && !queuedDeactivation && desiredAbility == Space4XFocusAbilityType.None)
                {
                    // Nothing to do
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private SeatFocusRole ResolveRole(in FixedString64Bytes roleId)
        {
            if (roleId.Equals(_roleWeaponsOfficer))
                return SeatFocusRole.Weapons;
            if (roleId.Equals(_roleSensorsOfficer) || roleId.Equals(_roleCommunicationsOfficer))
                return SeatFocusRole.Sensors;
            if (roleId.Equals(_roleChiefEngineer))
                return SeatFocusRole.Engineering;
            if (roleId.Equals(_roleNavigationOfficer) || roleId.Equals(_roleFlightCommander) ||
                roleId.Equals(_roleFlightDirector) || roleId.Equals(_roleHangarDeckOfficer))
                return SeatFocusRole.Tactical;
            if (roleId.Equals(_roleLogisticsOfficer))
                return SeatFocusRole.Operations;
            if (roleId.Equals(_roleCaptain) || roleId.Equals(_roleXO) || roleId.Equals(_roleShipmaster) || roleId.Equals(_roleFleetAdmiral) ||
                roleId.Equals(_roleSecurityOfficer) || roleId.Equals(_roleMarineCommander) || roleId.Equals(_roleMarineSergeant))
                return SeatFocusRole.Command;

            return SeatFocusRole.None;
        }

        private ShipFocusStatus ResolveShipStatus(Entity shipEntity)
        {
            if (_snapshotLookup.HasComponent(shipEntity))
            {
                var snapshot = _snapshotLookup[shipEntity];
                return new ShipFocusStatus
                {
                    HullRatio = snapshot.HullRatio,
                    ShieldRatio = snapshot.ShieldRatio,
                    FuelRatio = snapshot.FuelRatio,
                    AmmoRatio = snapshot.AmmoRatio,
                    SensorRange = snapshot.SensorRange,
                    ContactsTracked = snapshot.ContactsTracked,
                    WeaponMounts = snapshot.WeaponMounts,
                    WeaponsOnline = snapshot.WeaponsOnline
                };
            }

            var hullRatio = 1f;
            if (_hullLookup.HasComponent(shipEntity))
            {
                var hull = _hullLookup[shipEntity];
                hullRatio = hull.Max > 0f ? hull.Current / hull.Max : 1f;
            }

            var shieldRatio = 0f;
            if (_shieldLookup.HasComponent(shipEntity))
            {
                var shield = _shieldLookup[shipEntity];
                shieldRatio = shield.Maximum > 0f ? shield.Current / shield.Maximum : 0f;
            }

            var fuelRatio = 1f;
            var ammoRatio = 1f;
            if (_supplyLookup.HasComponent(shipEntity))
            {
                var supply = _supplyLookup[shipEntity];
                fuelRatio = supply.FuelRatio;
                ammoRatio = supply.AmmoRatio;
            }

            return new ShipFocusStatus
            {
                HullRatio = math.saturate(hullRatio),
                ShieldRatio = math.saturate(shieldRatio),
                FuelRatio = math.saturate(fuelRatio),
                AmmoRatio = math.saturate(ammoRatio),
                SensorRange = 0f,
                ContactsTracked = 0,
                WeaponMounts = 0,
                WeaponsOnline = 0
            };
        }

        private Space4XFocusAbilityType ResolveDesiredAbility(
            SeatFocusRole role,
            in ShipFocusStatus status,
            in BehaviorDisposition behavior,
            bool inCombat,
            float focusRatio,
            Entity shipEntity)
        {
            switch (role)
            {
                case SeatFocusRole.Weapons:
                    return ResolveWeaponsAbility(status, behavior, inCombat, focusRatio);
                case SeatFocusRole.Sensors:
                    return ResolveSensorsAbility(status, inCombat, focusRatio);
                case SeatFocusRole.Engineering:
                    return ResolveEngineeringAbility(status, inCombat, focusRatio);
                case SeatFocusRole.Tactical:
                    return ResolveTacticalAbility(status, inCombat, focusRatio, shipEntity);
                case SeatFocusRole.Command:
                    return ResolveCommandAbility(status, inCombat, focusRatio);
                case SeatFocusRole.Operations:
                    return ResolveOperationsAbility(status, inCombat, focusRatio);
            }

            return Space4XFocusAbilityType.None;
        }

        private Space4XFocusAbilityType ResolveWeaponsAbility(in ShipFocusStatus status, in BehaviorDisposition behavior, bool inCombat, float focusRatio)
        {
            if (!inCombat || status.WeaponMounts == 0 || focusRatio < 0.2f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (status.AmmoRatio < 0.2f)
            {
                return Space4XFocusAbilityType.PrecisionFire;
            }

            var aggression = math.saturate(behavior.Aggression);
            var caution = math.saturate(behavior.Caution);
            if (aggression > caution + 0.1f)
            {
                return Space4XFocusAbilityType.RapidFire;
            }

            return Space4XFocusAbilityType.PrecisionFire;
        }

        private Space4XFocusAbilityType ResolveSensorsAbility(in ShipFocusStatus status, bool inCombat, float focusRatio)
        {
            if (focusRatio < 0.15f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (inCombat)
            {
                if (status.ContactsTracked >= 3)
                {
                    return Space4XFocusAbilityType.FleetTracking;
                }

                return Space4XFocusAbilityType.ECMCountermeasures;
            }

            if (status.SensorRange > 0f)
            {
                return Space4XFocusAbilityType.LongRangeScan;
            }

            return Space4XFocusAbilityType.None;
        }

        private Space4XFocusAbilityType ResolveEngineeringAbility(in ShipFocusStatus status, bool inCombat, float focusRatio)
        {
            if (focusRatio < 0.2f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (status.HullRatio < 0.5f || status.ShieldRatio < 0.3f)
            {
                return Space4XFocusAbilityType.EmergencyRepairs;
            }

            if (status.HullRatio < 0.75f || status.ShieldRatio < 0.5f)
            {
                return Space4XFocusAbilityType.DamageControl;
            }

            if (inCombat)
            {
                return Space4XFocusAbilityType.SystemOptimization;
            }

            return Space4XFocusAbilityType.None;
        }

        private Space4XFocusAbilityType ResolveTacticalAbility(in ShipFocusStatus status, bool inCombat, float focusRatio, Entity shipEntity)
        {
            if (focusRatio < 0.15f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (inCombat)
            {
                if (_carrierLookup.HasComponent(shipEntity))
                {
                    return Space4XFocusAbilityType.AttackRunCoordination;
                }

                return Space4XFocusAbilityType.EvasiveManeuvers;
            }

            if (status.ContactsTracked > 0)
            {
                return Space4XFocusAbilityType.FormationHold;
            }

            return Space4XFocusAbilityType.None;
        }

        private Space4XFocusAbilityType ResolveCommandAbility(in ShipFocusStatus status, bool inCombat, float focusRatio)
        {
            if (focusRatio < 0.2f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (inCombat && status.HullRatio < 0.6f)
            {
                return Space4XFocusAbilityType.CrisisManagement;
            }

            if (inCombat)
            {
                return Space4XFocusAbilityType.InspiringPresence;
            }

            return Space4XFocusAbilityType.None;
        }

        private Space4XFocusAbilityType ResolveOperationsAbility(in ShipFocusStatus status, bool inCombat, float focusRatio)
        {
            if (inCombat || focusRatio < 0.15f)
            {
                return Space4XFocusAbilityType.None;
            }

            if (status.FuelRatio < 0.35f || status.AmmoRatio < 0.35f)
            {
                return Space4XFocusAbilityType.ResourceEfficiency;
            }

            return Space4XFocusAbilityType.ProductionSurge;
        }

        private bool HasAbility(DynamicBuffer<Space4XActiveFocusAbility> abilities, Space4XFocusAbilityType ability)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].AbilityType == (ushort)ability)
                {
                    return true;
                }
            }

            return false;
        }

        private bool QueueDeactivateConflicts(
            SeatFocusRole role,
            Space4XFocusAbilityType desired,
            DynamicBuffer<Space4XActiveFocusAbility> abilities,
            Entity occupant,
            ref EntityCommandBuffer ecb)
        {
            switch (role)
            {
                case SeatFocusRole.Weapons:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.PrecisionFire,
                        Space4XFocusAbilityType.RapidFire,
                        Space4XFocusAbilityType.CoolingOverdrive,
                        ref ecb);
                case SeatFocusRole.Sensors:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.FleetTracking,
                        Space4XFocusAbilityType.LongRangeScan,
                        Space4XFocusAbilityType.ECMCountermeasures,
                        ref ecb);
                case SeatFocusRole.Engineering:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.EmergencyRepairs,
                        Space4XFocusAbilityType.DamageControl,
                        Space4XFocusAbilityType.SystemOptimization,
                        ref ecb);
                case SeatFocusRole.Tactical:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.EvasiveManeuvers,
                        Space4XFocusAbilityType.AttackRunCoordination,
                        Space4XFocusAbilityType.FormationHold,
                        ref ecb);
                case SeatFocusRole.Command:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.CrisisManagement,
                        Space4XFocusAbilityType.InspiringPresence,
                        Space4XFocusAbilityType.OfficerSupport,
                        ref ecb);
                case SeatFocusRole.Operations:
                    return QueueDeactivateOther(abilities, occupant, desired,
                        Space4XFocusAbilityType.ProductionSurge,
                        Space4XFocusAbilityType.QualityFocus,
                        Space4XFocusAbilityType.ResourceEfficiency,
                        ref ecb);
            }

            return false;
        }

        private void QueueDeactivateAny(SeatFocusRole role, DynamicBuffer<Space4XActiveFocusAbility> abilities, Entity occupant, ref EntityCommandBuffer ecb)
        {
            switch (role)
            {
                case SeatFocusRole.Weapons:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.PrecisionFire,
                        Space4XFocusAbilityType.RapidFire,
                        Space4XFocusAbilityType.CoolingOverdrive,
                        ref ecb);
                    break;
                case SeatFocusRole.Sensors:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.FleetTracking,
                        Space4XFocusAbilityType.LongRangeScan,
                        Space4XFocusAbilityType.ECMCountermeasures,
                        ref ecb);
                    break;
                case SeatFocusRole.Engineering:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.EmergencyRepairs,
                        Space4XFocusAbilityType.DamageControl,
                        Space4XFocusAbilityType.SystemOptimization,
                        ref ecb);
                    break;
                case SeatFocusRole.Tactical:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.EvasiveManeuvers,
                        Space4XFocusAbilityType.AttackRunCoordination,
                        Space4XFocusAbilityType.FormationHold,
                        ref ecb);
                    break;
                case SeatFocusRole.Command:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.CrisisManagement,
                        Space4XFocusAbilityType.InspiringPresence,
                        Space4XFocusAbilityType.OfficerSupport,
                        ref ecb);
                    break;
                case SeatFocusRole.Operations:
                    QueueDeactivateOther(abilities, occupant, Space4XFocusAbilityType.None,
                        Space4XFocusAbilityType.ProductionSurge,
                        Space4XFocusAbilityType.QualityFocus,
                        Space4XFocusAbilityType.ResourceEfficiency,
                        ref ecb);
                    break;
            }
        }

        private bool QueueDeactivateOther(
            DynamicBuffer<Space4XActiveFocusAbility> abilities,
            Entity occupant,
            Space4XFocusAbilityType desired,
            Space4XFocusAbilityType a,
            Space4XFocusAbilityType b,
            Space4XFocusAbilityType c,
            ref EntityCommandBuffer ecb)
        {
            if (a != desired && HasAbility(abilities, a))
            {
                QueueDeactivate(ref ecb, occupant, a);
                return true;
            }

            if (b != desired && HasAbility(abilities, b))
            {
                QueueDeactivate(ref ecb, occupant, b);
                return true;
            }

            if (c != desired && HasAbility(abilities, c))
            {
                QueueDeactivate(ref ecb, occupant, c);
                return true;
            }

            return false;
        }

        private void QueueActivate(ref EntityCommandBuffer ecb, Entity occupant, Space4XFocusAbilityType ability)
        {
            var request = new FocusAbilityRequest
            {
                RequestedAbility = (ushort)ability,
                TargetEntity = Entity.Null,
                RequestedDuration = 0
            };

            if (_requestLookup.HasComponent(occupant))
            {
                ecb.SetComponent(occupant, request);
            }
            else
            {
                ecb.AddComponent(occupant, request);
            }
        }

        private void QueueDeactivate(ref EntityCommandBuffer ecb, Entity occupant, Space4XFocusAbilityType ability)
        {
            var request = new FocusAbilityDeactivateRequest
            {
                AbilityToDeactivate = (ushort)ability
            };

            if (_deactivateLookup.HasComponent(occupant))
            {
                ecb.SetComponent(occupant, request);
            }
            else
            {
                ecb.AddComponent(occupant, request);
            }
        }
    }
}

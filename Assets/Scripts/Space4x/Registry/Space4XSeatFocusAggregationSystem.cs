using PureDOTS.Runtime.Authority;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates seat occupant focus modifiers onto the ship entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainSupportSystem))]
    [UpdateAfter(typeof(Space4XFocusGrowthBonusSystem))]
    [UpdateBefore(typeof(Space4XFocusCombatIntegrationSystem))]
    public partial struct Space4XSeatFocusAggregationSystem : ISystem
    {
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;

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

        private enum SeatFocusDomain : byte
        {
            None = 0,
            Command = 1,
            Weapons = 2,
            Sensors = 3,
            Engineering = 4,
            Tactical = 5,
            Operations = 6
        }

        private struct DomainSelection
        {
            public byte Priority;
            public Space4XFocusModifiers Modifiers;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AuthoritySeatRef>();

            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);

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
            _focusLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entityManager = state.EntityManager;

            foreach (var (seats, shipEntity) in SystemAPI.Query<DynamicBuffer<AuthoritySeatRef>>()
                         .WithNone<Prefab>()
                         .WithEntityAccess())
            {
                if (seats.Length == 0)
                {
                    continue;
                }

                var command = new DomainSelection();
                var weapons = new DomainSelection();
                var sensors = new DomainSelection();
                var engineering = new DomainSelection();
                var tactical = new DomainSelection();
                var operations = new DomainSelection();

                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null ||
                        !_seatLookup.HasComponent(seatEntity) ||
                        !_occupantLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var occupant = _occupantLookup[seatEntity].OccupantEntity;
                    if (occupant == Entity.Null || !_focusLookup.HasComponent(occupant))
                    {
                        continue;
                    }

                    if (!TryResolveDomain(_seatLookup[seatEntity].RoleId, out var domain, out var priority))
                    {
                        continue;
                    }

                    var modifiers = _focusLookup[occupant];
                    switch (domain)
                    {
                        case SeatFocusDomain.Command:
                            TrySelect(ref command, priority, modifiers);
                            break;
                        case SeatFocusDomain.Weapons:
                            TrySelect(ref weapons, priority, modifiers);
                            break;
                        case SeatFocusDomain.Sensors:
                            TrySelect(ref sensors, priority, modifiers);
                            break;
                        case SeatFocusDomain.Engineering:
                            TrySelect(ref engineering, priority, modifiers);
                            break;
                        case SeatFocusDomain.Tactical:
                            TrySelect(ref tactical, priority, modifiers);
                            break;
                        case SeatFocusDomain.Operations:
                            TrySelect(ref operations, priority, modifiers);
                            break;
                    }
                }

                var aggregated = Space4XFocusModifiers.Default();

                if (command.Priority > 0)
                    ApplyCommand(command.Modifiers, ref aggregated);
                if (weapons.Priority > 0)
                    ApplyWeapons(weapons.Modifiers, ref aggregated);
                if (sensors.Priority > 0)
                    ApplySensors(sensors.Modifiers, ref aggregated);
                if (engineering.Priority > 0)
                    ApplyEngineering(engineering.Modifiers, ref aggregated);
                if (tactical.Priority > 0)
                    ApplyTactical(tactical.Modifiers, ref aggregated);
                if (operations.Priority > 0)
                    ApplyOperations(operations.Modifiers, ref aggregated);

                if (entityManager.HasComponent<Space4XFocusModifiers>(shipEntity))
                {
                    ecb.SetComponent(shipEntity, aggregated);
                }
                else
                {
                    ecb.AddComponent(shipEntity, aggregated);
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private bool TryResolveDomain(in FixedString64Bytes roleId, out SeatFocusDomain domain, out byte priority)
        {
            if (roleId.Equals(_roleWeaponsOfficer))
            {
                domain = SeatFocusDomain.Weapons;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleSensorsOfficer))
            {
                domain = SeatFocusDomain.Sensors;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleCommunicationsOfficer))
            {
                domain = SeatFocusDomain.Sensors;
                priority = 1;
                return true;
            }

            if (roleId.Equals(_roleChiefEngineer))
            {
                domain = SeatFocusDomain.Engineering;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleLogisticsOfficer))
            {
                domain = SeatFocusDomain.Operations;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleNavigationOfficer))
            {
                domain = SeatFocusDomain.Tactical;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleFlightCommander) || roleId.Equals(_roleFlightDirector) || roleId.Equals(_roleHangarDeckOfficer))
            {
                domain = SeatFocusDomain.Tactical;
                priority = 1;
                return true;
            }

            if (roleId.Equals(_roleCaptain))
            {
                domain = SeatFocusDomain.Command;
                priority = 2;
                return true;
            }

            if (roleId.Equals(_roleXO) || roleId.Equals(_roleShipmaster) || roleId.Equals(_roleFleetAdmiral) ||
                roleId.Equals(_roleSecurityOfficer) || roleId.Equals(_roleMarineCommander) || roleId.Equals(_roleMarineSergeant))
            {
                domain = SeatFocusDomain.Command;
                priority = 1;
                return true;
            }

            domain = SeatFocusDomain.None;
            priority = 0;
            return false;
        }

        private static void TrySelect(ref DomainSelection selection, byte priority, in Space4XFocusModifiers modifiers)
        {
            if (priority > selection.Priority)
            {
                selection.Priority = priority;
                selection.Modifiers = modifiers;
            }
        }

        private static void ApplyCommand(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.OfficerSupportBonus = source.OfficerSupportBonus;
            target.BoardingEffectivenessBonus = source.BoardingEffectivenessBonus;
            target.CrewStressReduction = source.CrewStressReduction;
            target.MoraleBonus = source.MoraleBonus;
        }

        private static void ApplyWeapons(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.CoolingEfficiency = source.CoolingEfficiency;
            target.AccuracyBonus = source.AccuracyBonus;
            target.RateOfFireMultiplier = source.RateOfFireMultiplier;
            target.MultiTargetCount = source.MultiTargetCount;
            target.SubsystemTargetingBonus = source.SubsystemTargetingBonus;
        }

        private static void ApplySensors(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.DetectionBonus = source.DetectionBonus;
            target.TrackingCapacityBonus = source.TrackingCapacityBonus;
            target.ScanRangeMultiplier = source.ScanRangeMultiplier;
            target.ECMResistance = source.ECMResistance;
        }

        private static void ApplyEngineering(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.RepairSpeedMultiplier = source.RepairSpeedMultiplier;
            target.SystemEfficiencyBonus = source.SystemEfficiencyBonus;
            target.DamageControlBonus = source.DamageControlBonus;
            target.ShieldAdaptationRate = source.ShieldAdaptationRate;
        }

        private static void ApplyTactical(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.EvasionBonus = source.EvasionBonus;
            target.FormationCohesionBonus = source.FormationCohesionBonus;
            target.StrikeCraftCoordinationBonus = source.StrikeCraftCoordinationBonus;
        }

        private static void ApplyOperations(in Space4XFocusModifiers source, ref Space4XFocusModifiers target)
        {
            target.ProductionSpeedMultiplier = source.ProductionSpeedMultiplier;
            target.ProductionQualityMultiplier = source.ProductionQualityMultiplier;
            target.ResourceEfficiencyBonus = source.ResourceEfficiencyBonus;
            target.BatchCapacityBonus = source.BatchCapacityBonus;
        }
    }
}

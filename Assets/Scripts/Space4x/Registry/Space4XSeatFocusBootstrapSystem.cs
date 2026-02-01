using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures authority seat occupants have focus components and role-appropriate focus profiles.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XAuthoritySeatBootstrapSystem))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XSeatFocusBootstrapSystem : ISystem
    {
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

        public void OnCreate(ref SystemState state)
        {
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

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (seat, occupant) in SystemAPI.Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>())
            {
                var occupantEntity = occupant.ValueRO.OccupantEntity;
                if (occupantEntity == Entity.Null || !em.Exists(occupantEntity) || em.HasComponent<Prefab>(occupantEntity))
                {
                    continue;
                }

                var profile = ResolveProfile(seat.ValueRO.RoleId);
                EnsureFocusComponents(em, occupantEntity, profile, ref ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void EnsureFocusComponents(EntityManager em, Entity occupant, in OfficerFocusProfile desiredProfile, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<Space4XEntityFocus>(occupant))
            {
                ecb.AddComponent(occupant, Space4XEntityFocus.Default());
            }

            if (!em.HasComponent<OfficerFocusProfile>(occupant))
            {
                ecb.AddComponent(occupant, desiredProfile);
            }
            else
            {
                var profile = em.GetComponentData<OfficerFocusProfile>(occupant);
                if (profile.PrimaryArchetype != desiredProfile.PrimaryArchetype ||
                    profile.SecondaryArchetype != desiredProfile.SecondaryArchetype)
                {
                    ecb.SetComponent(occupant, desiredProfile);
                }
                else if (profile.IsOnDuty == 0)
                {
                    profile.IsOnDuty = 1;
                    ecb.SetComponent(occupant, profile);
                }
            }

            if (!em.HasComponent<Space4XFocusModifiers>(occupant))
            {
                ecb.AddComponent(occupant, Space4XFocusModifiers.Default());
            }

            if (!em.HasComponent<FocusGrowth>(occupant))
            {
                ecb.AddComponent(occupant, new FocusGrowth());
            }

            if (!em.HasComponent<FocusUsageTracking>(occupant))
            {
                ecb.AddComponent(occupant, new FocusUsageTracking());
            }

            if (!em.HasComponent<FocusPersonality>(occupant))
            {
                ecb.AddComponent(occupant, FocusPersonality.Disciplined());
            }

            if (!em.HasComponent<FocusBehaviorContext>(occupant))
            {
                ecb.AddComponent(occupant, new FocusBehaviorContext());
            }

            if (!em.HasBuffer<Space4XActiveFocusAbility>(occupant))
            {
                ecb.AddBuffer<Space4XActiveFocusAbility>(occupant);
            }

            if (!em.HasBuffer<FocusAchievement>(occupant))
            {
                ecb.AddBuffer<FocusAchievement>(occupant);
            }

            if (!em.HasBuffer<FocusExhaustionEvent>(occupant))
            {
                ecb.AddBuffer<FocusExhaustionEvent>(occupant);
            }
        }

        private OfficerFocusProfile ResolveProfile(in FixedString64Bytes roleId)
        {
            if (roleId.Equals(_roleWeaponsOfficer))
            {
                return OfficerFocusProfile.WeaponsOfficer();
            }

            if (roleId.Equals(_roleSensorsOfficer) || roleId.Equals(_roleCommunicationsOfficer))
            {
                return OfficerFocusProfile.SensorsOfficer();
            }

            if (roleId.Equals(_roleChiefEngineer))
            {
                return OfficerFocusProfile.ChiefEngineer();
            }

            if (roleId.Equals(_roleNavigationOfficer) || roleId.Equals(_roleFlightCommander) ||
                roleId.Equals(_roleFlightDirector) || roleId.Equals(_roleHangarDeckOfficer))
            {
                return OfficerFocusProfile.HelmOfficer();
            }

            if (roleId.Equals(_roleLogisticsOfficer))
            {
                return OfficerFocusProfile.FacilityWorker();
            }

            if (roleId.Equals(_roleSecurityOfficer) || roleId.Equals(_roleMarineCommander) || roleId.Equals(_roleMarineSergeant))
            {
                return OfficerFocusProfile.Captain();
            }

            if (roleId.Equals(_roleCaptain) || roleId.Equals(_roleXO) || roleId.Equals(_roleShipmaster) || roleId.Equals(_roleFleetAdmiral))
            {
                return OfficerFocusProfile.Captain();
            }

            return OfficerFocusProfile.FacilityWorker();
        }
    }
}

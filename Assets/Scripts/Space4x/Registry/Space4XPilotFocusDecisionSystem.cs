using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Profile;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Space4X.Runtime;

namespace Space4X.Registry
{
    /// <summary>
    /// Issues focus ability requests for pilots based on combat context.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFocusAbilitySystem))]
    public partial struct Space4XPilotFocusDecisionSystem : ISystem
    {
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<VesselAIState> _aiStateLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorLookup;
        private ComponentLookup<Space4XEntityFocus> _focusLookup;
        private ComponentLookup<OfficerFocusProfile> _profileLookup;
        private BufferLookup<Space4XActiveFocusAbility> _abilityLookup;
        private ComponentLookup<FocusAbilityRequest> _requestLookup;
        private ComponentLookup<FocusAbilityDeactivateRequest> _deactivateLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _aiStateLookup = state.GetComponentLookup<VesselAIState>(true);
            _behaviorLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _focusLookup = state.GetComponentLookup<Space4XEntityFocus>(true);
            _profileLookup = state.GetComponentLookup<OfficerFocusProfile>(true);
            _abilityLookup = state.GetBufferLookup<Space4XActiveFocusAbility>(true);
            _requestLookup = state.GetComponentLookup<FocusAbilityRequest>(true);
            _deactivateLookup = state.GetComponentLookup<FocusAbilityDeactivateRequest>(true);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _pilotLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _behaviorLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _abilityLookup.Update(ref state);
            _requestLookup.Update(ref state);
            _deactivateLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var seatOccupants = new NativeParallelHashSet<Entity>(128, Allocator.Temp);
            foreach (var occupant in SystemAPI.Query<RefRO<AuthoritySeatOccupant>>())
            {
                var occupantEntity = occupant.ValueRO.OccupantEntity;
                if (occupantEntity != Entity.Null && _entityInfoLookup.Exists(occupantEntity))
                {
                    seatOccupants.Add(occupantEntity);
                }
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, shipEntity) in SystemAPI.Query<RefRO<VesselPilotLink>>().WithNone<Prefab>().WithEntityAccess())
            {
                ProcessShip(shipEntity, seatOccupants, ref ecb);
            }

            foreach (var (_, shipEntity) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithNone<Prefab>().WithEntityAccess())
            {
                ProcessShip(shipEntity, seatOccupants, ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            seatOccupants.Dispose();
        }

        private void ProcessShip(Entity shipEntity, in NativeParallelHashSet<Entity> seatOccupants, ref EntityCommandBuffer ecb)
        {
            var pilot = ResolvePilot(shipEntity);
            if (pilot == Entity.Null || !_focusLookup.HasComponent(pilot) || !_profileLookup.HasComponent(pilot))
            {
                return;
            }
            if (seatOccupants.Contains(pilot))
            {
                return;
            }

            var focus = _focusLookup[pilot];
            var profile = _profileLookup[pilot];
            if (focus.IsInComa != 0)
            {
                return;
            }

            var inCombat = false;
            if (_engagementLookup.HasComponent(shipEntity))
            {
                var engagement = _engagementLookup[shipEntity];
                inCombat = engagement.Phase == EngagementPhase.Approaching || engagement.Phase == EngagementPhase.Engaged;
            }
            else if (_aiStateLookup.HasComponent(shipEntity))
            {
                var aiState = _aiStateLookup[shipEntity];
                inCombat = aiState.TargetEntity != Entity.Null;
            }

            var focusRatio = focus.MaxFocus > 0f ? focus.CurrentFocus / focus.MaxFocus : 0f;
            var hasAbilities = _abilityLookup.HasBuffer(pilot);
            var abilities = hasAbilities ? _abilityLookup[pilot] : default;

            var wantEvasion = inCombat && focusRatio > 0.15f &&
                              Space4XFocusAbilityDefinitions.CanActivate(Space4XFocusAbilityType.EvasiveManeuvers, focus, profile);
            var desiredWeaponAbility = ResolveWeaponAbility(pilot);
            var wantWeaponAbility = inCombat && focusRatio > 0.2f &&
                                    Space4XFocusAbilityDefinitions.CanActivate(desiredWeaponAbility, focus, profile);

            var evasiveActive = hasAbilities && HasAbility(abilities, Space4XFocusAbilityType.EvasiveManeuvers);
            var weaponActive = hasAbilities && HasAbility(abilities, desiredWeaponAbility);
            var otherWeapon = desiredWeaponAbility == Space4XFocusAbilityType.PrecisionFire
                ? Space4XFocusAbilityType.RapidFire
                : Space4XFocusAbilityType.PrecisionFire;
            var otherWeaponActive = hasAbilities && HasAbility(abilities, otherWeapon);

            var queuedActivation = false;
            var queuedDeactivation = false;

            if (wantEvasion && !evasiveActive)
            {
                QueueActivate(ref ecb, pilot, Space4XFocusAbilityType.EvasiveManeuvers);
                queuedActivation = true;
            }
            else if (!wantEvasion && evasiveActive)
            {
                QueueDeactivate(ref ecb, pilot, Space4XFocusAbilityType.EvasiveManeuvers);
                queuedDeactivation = true;
            }

            if (!queuedActivation && wantWeaponAbility && !weaponActive)
            {
                QueueActivate(ref ecb, pilot, desiredWeaponAbility);
                queuedActivation = true;
            }
            else if (!queuedDeactivation && !wantWeaponAbility && weaponActive)
            {
                QueueDeactivate(ref ecb, pilot, desiredWeaponAbility);
                queuedDeactivation = true;
            }

            if (!queuedDeactivation && otherWeaponActive)
            {
                QueueDeactivate(ref ecb, pilot, otherWeapon);
            }
        }

        private Entity ResolvePilot(Entity shipEntity)
        {
            if (_pilotLookup.HasComponent(shipEntity))
            {
                var pilot = _pilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null && _entityInfoLookup.Exists(pilot))
                {
                    return pilot;
                }
            }

            if (_strikePilotLookup.HasComponent(shipEntity))
            {
                var pilot = _strikePilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null && _entityInfoLookup.Exists(pilot))
                {
                    return pilot;
                }
            }

            return Entity.Null;
        }

        private Space4XFocusAbilityType ResolveWeaponAbility(Entity pilot)
        {
            var aggression = 0.5f;
            var caution = 0.5f;
            if (_behaviorLookup.HasComponent(pilot))
            {
                var behavior = _behaviorLookup[pilot];
                aggression = math.saturate(behavior.Aggression);
                caution = math.saturate(behavior.Caution);
            }

            if (aggression > caution + 0.1f)
            {
                return Space4XFocusAbilityType.RapidFire;
            }

            return Space4XFocusAbilityType.PrecisionFire;
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

        private void QueueActivate(ref EntityCommandBuffer ecb, Entity pilot, Space4XFocusAbilityType ability)
        {
            var request = new FocusAbilityRequest
            {
                RequestedAbility = (ushort)ability,
                TargetEntity = Entity.Null,
                RequestedDuration = 0
            };

            if (_requestLookup.HasComponent(pilot))
            {
                ecb.SetComponent(pilot, request);
            }
            else
            {
                ecb.AddComponent(pilot, request);
            }
        }

        private void QueueDeactivate(ref EntityCommandBuffer ecb, Entity pilot, Space4XFocusAbilityType ability)
        {
            var request = new FocusAbilityDeactivateRequest
            {
                AbilityToDeactivate = (ushort)ability
            };

            if (_deactivateLookup.HasComponent(pilot))
            {
                ecb.SetComponent(pilot, request);
            }
            else
            {
                ecb.AddComponent(pilot, request);
            }
        }
    }
}

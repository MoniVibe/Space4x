using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Profile;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Emits profile action events for combat damage based on target classification.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDamageResolutionSystem))]
    public partial struct Space4XCombatProfileActionSystem : ISystem
    {
        private ComponentLookup<EntityDisposition> _dispositionLookup;
        private ComponentLookup<ScenarioSide> _sideLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeProfileLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<IssuedByAuthority> _issuedByLookup;
        private ComponentLookup<TraderProfile> _traderLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<EscortAssignment> _escortLookup;
        private BufferLookup<WeaponMount> _weaponLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamageEvent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _dispositionLookup = state.GetComponentLookup<EntityDisposition>(true);
            _sideLookup = state.GetComponentLookup<ScenarioSide>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _strikeProfileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _issuedByLookup = state.GetComponentLookup<IssuedByAuthority>(true);
            _traderLookup = state.GetComponentLookup<TraderProfile>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _escortLookup = state.GetComponentLookup<EscortAssignment>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
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

            if (!SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var streamEntity) ||
                !SystemAPI.TryGetSingleton<ProfileActionEventStreamConfig>(out var streamConfig))
            {
                return;
            }

            _dispositionLookup.Update(ref state);
            _sideLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _strikeProfileLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _issuedByLookup.Update(ref state);
            _traderLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _escortLookup.Update(ref state);
            _weaponLookup.Update(ref state);

            var actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);
            var actionStream = SystemAPI.GetComponentRW<ProfileActionEventStream>(streamEntity);

            foreach (var (damageEvents, targetEntity) in SystemAPI.Query<DynamicBuffer<DamageEvent>>().WithEntityAccess())
            {
                if (damageEvents.Length == 0)
                {
                    continue;
                }

                var targetDisposition = ResolveDisposition(targetEntity);
                var targetIsCivilian = EntityDispositionUtility.IsCivilian(targetDisposition);
                var targetIsCombatant = EntityDispositionUtility.IsCombatant(targetDisposition);

                for (int i = 0; i < damageEvents.Length; i++)
                {
                    var damageEvent = damageEvents[i];
                    if (damageEvent.Source == Entity.Null)
                    {
                        continue;
                    }

                    var isHostile = IsHostilePair(damageEvent.Source, targetEntity, targetDisposition);
                    var token = ResolveToken(targetIsCivilian, targetIsCombatant, isHostile);
                    if (token == ProfileActionToken.None)
                    {
                        continue;
                    }

                    var actor = ResolveProfileEntity(damageEvent.Source);
                    var issuedBy = ResolveIssuedBy(damageEvent.Source);
                    var actionEvent = new ProfileActionEvent
                    {
                        Token = token,
                        IntentFlags = ProfileActionIntentFlags.None,
                        JustificationFlags = isHostile ? ProfileActionJustificationFlags.SelfDefense : ProfileActionJustificationFlags.None,
                        OutcomeFlags = ProfileActionOutcomeFlags.Harm,
                        Magnitude = ComputeMagnitude(damageEvent.RawDamage),
                        Actor = actor,
                        Target = targetEntity,
                        IssuingSeat = issuedBy.IssuingSeat,
                        IssuingOccupant = issuedBy.IssuingOccupant,
                        ActingSeat = issuedBy.ActingSeat,
                        ActingOccupant = issuedBy.ActingOccupant,
                        Tick = damageEvent.Tick
                    };

                    ProfileActionEventUtility.TryAppend(ref actionStream.ValueRW, actionBuffer, actionEvent, streamConfig.MaxEvents);
                }
            }
        }

        private EntityDispositionFlags ResolveDisposition(Entity entity)
        {
            if (_dispositionLookup.HasComponent(entity))
            {
                return _dispositionLookup[entity].Flags;
            }

            var flags = EntityDispositionFlags.None;

            if (_traderLookup.HasComponent(entity))
            {
                flags |= EntityDispositionFlags.Trader | EntityDispositionFlags.Civilian;
            }

            if (_miningVesselLookup.HasComponent(entity))
            {
                flags |= EntityDispositionFlags.Mining | EntityDispositionFlags.Civilian;
            }

            if (_strikeProfileLookup.HasComponent(entity))
            {
                flags |= EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            }

            if (_escortLookup.HasComponent(entity))
            {
                flags |= EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            }

            if (_carrierLookup.HasComponent(entity) && flags == EntityDispositionFlags.None)
            {
                flags |= EntityDispositionFlags.Civilian | EntityDispositionFlags.Support;
            }

            if (_weaponLookup.HasBuffer(entity) || _engagementLookup.HasComponent(entity))
            {
                flags |= EntityDispositionFlags.Combatant;
            }

            return flags;
        }

        private bool IsHostilePair(Entity source, Entity target, EntityDispositionFlags targetDisposition)
        {
            if (EntityDispositionUtility.IsHostile(targetDisposition))
            {
                return true;
            }

            if (_sideLookup.HasComponent(source) && _sideLookup.HasComponent(target))
            {
                return _sideLookup[source].Side != _sideLookup[target].Side;
            }

            return false;
        }

        private static ProfileActionToken ResolveToken(bool targetIsCivilian, bool targetIsCombatant, bool isHostile)
        {
            if (targetIsCivilian)
            {
                return ProfileActionToken.AttackCivilian;
            }

            if (isHostile || targetIsCombatant)
            {
                return ProfileActionToken.AttackHostile;
            }

            return ProfileActionToken.None;
        }

        private Entity ResolveProfileEntity(Entity source)
        {
            if (_strikePilotLookup.HasComponent(source))
            {
                var pilot = _strikePilotLookup[source].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(source))
            {
                var pilot = _vesselPilotLookup[source].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            return source;
        }

        private IssuedByAuthority ResolveIssuedBy(Entity source)
        {
            if (_issuedByLookup.HasComponent(source))
            {
                return _issuedByLookup[source];
            }

            if (_strikeProfileLookup.HasComponent(source))
            {
                var carrier = _strikeProfileLookup[source].Carrier;
                if (carrier != Entity.Null && _issuedByLookup.HasComponent(carrier))
                {
                    return _issuedByLookup[carrier];
                }
            }

            if (_miningVesselLookup.HasComponent(source))
            {
                var carrier = _miningVesselLookup[source].CarrierEntity;
                if (carrier != Entity.Null && _issuedByLookup.HasComponent(carrier))
                {
                    return _issuedByLookup[carrier];
                }
            }

            return default;
        }

        private static byte ComputeMagnitude(float rawDamage)
        {
            var scaled = math.saturate(rawDamage / 50f);
            var magnitude = math.clamp(scaled * 100f, 1f, 100f);
            return (byte)math.round(magnitude);
        }
    }
}

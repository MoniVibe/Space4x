using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Issues wing regroup/break directives based on leader alignment and stance.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftLaunchSystem))]
    [UpdateBefore(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XStrikeCraftWingDecisionSystem : ISystem
    {
        private const byte WingModeFormUp = 0;
        private const byte WingModeBreak = 1;

        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<StrikeCraftPilotLink> _pilotLinkLookup;
        private ComponentLookup<IssuedByAuthority> _issuedByLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(false);
            _pilotLinkLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _issuedByLookup = state.GetComponentLookup<IssuedByAuthority>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            _issuedByLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var config = StrikeCraftWingDecisionConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftWingDecisionConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var actionStreamConfig = default(ProfileActionEventStreamConfig);
            var canEmitActions = SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var actionStreamEntity) &&
                                 SystemAPI.TryGetSingleton(out actionStreamConfig);
            DynamicBuffer<ProfileActionEvent> actionBuffer = default;
            RefRW<ProfileActionEventStream> actionStream = default;
            if (canEmitActions)
            {
                actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(actionStreamEntity);
                actionStream = SystemAPI.GetComponentRW<ProfileActionEventStream>(actionStreamEntity);
            }

            var craftCount = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build().CalculateEntityCount();
            var leaders = new NativeList<Entity>(math.max(1, craftCount), Allocator.Temp);
            var wingMembers = new NativeParallelMultiHashMap<Entity, Entity>(math.max(1, craftCount), Allocator.Temp);
            var unassignedByCarrier = new NativeParallelMultiHashMap<Entity, Entity>(math.max(1, craftCount), Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRW<StrikeCraftProfile>>().WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<StrikeCraftWingDirective>(entity))
                {
                    ecb.AddComponent(entity, new StrikeCraftWingDirective
                    {
                        Mode = WingModeFormUp,
                        NextDecisionTick = time.Tick + config.DecisionCooldownTicks,
                        LastDecisionTick = 0
                    });
                }

                if (!SystemAPI.HasComponent<StrikeCraftOrderDecision>(entity))
                {
                    ecb.AddComponent(entity, new StrikeCraftOrderDecision
                    {
                        LastDirectiveTick = 0,
                        LastDirectiveMode = WingModeFormUp,
                        LastDecision = 0,
                        LastEmittedTick = 0
                    });
                }

                if (profile.ValueRO.Carrier == Entity.Null)
                {
                    continue;
                }

                if (profile.ValueRO.WingLeader != Entity.Null)
                {
                    wingMembers.Add(profile.ValueRO.WingLeader, entity);
                    continue;
                }

                if (profile.ValueRO.WingPosition != 0)
                {
                    profile.ValueRW.WingPosition = 0;
                }

                leaders.Add(entity);
                unassignedByCarrier.Add(profile.ValueRO.Carrier, entity);
            }

            for (var i = 0; i < leaders.Length; i++)
            {
                var leader = leaders[i];
                if (!_profileLookup.HasComponent(leader) || !SystemAPI.HasComponent<StrikeCraftWingDirective>(leader))
                {
                    continue;
                }

                var leaderProfile = _profileLookup[leader];
                if (leaderProfile.Carrier == Entity.Null)
                {
                    continue;
                }

                var directive = SystemAPI.GetComponentRW<StrikeCraftWingDirective>(leader);
                if (time.Tick < directive.ValueRO.NextDecisionTick)
                {
                    continue;
                }

                var profileEntity = ResolveProfileEntity(leader);
                var lawfulness = 0.5f;
                var chaos = 0.5f;
                if (_alignmentLookup.HasComponent(profileEntity))
                {
                    var alignment = _alignmentLookup[profileEntity];
                    lawfulness = AlignmentMath.Lawfulness(alignment);
                    chaos = AlignmentMath.Chaos(alignment);
                }

                var discipline = 0.5f;
                if (_outlookLookup.HasBuffer(profileEntity))
                {
                    discipline = ComputeDiscipline(_outlookLookup[profileEntity]);
                }

                var disposition = ResolveBehaviorDisposition(profileEntity, leader);
                var compliance = disposition.Compliance;
                var formationAdherence = disposition.FormationAdherence;
                var patience = disposition.Patience;
                var aggression = disposition.Aggression;
                var riskTolerance = disposition.RiskTolerance;
                var caution = disposition.Caution;

                discipline = math.saturate(math.lerp(discipline, (compliance + formationAdherence) * 0.5f, 0.55f));

                var stance = VesselStanceMode.Balanced;
                if (_patrolStanceLookup.HasComponent(leaderProfile.Carrier))
                {
                    stance = _patrolStanceLookup[leaderProfile.Carrier].Stance;
                }
                else if (_patrolStanceLookup.HasComponent(leader))
                {
                    stance = _patrolStanceLookup[leader].Stance;
                }

                var breakThreshold = math.saturate(config.ChaosBreakThreshold +
                                                   (discipline - 0.5f) * 0.2f -
                                                   (aggression - 0.5f) * 0.15f -
                                                   (riskTolerance - 0.5f) * 0.1f +
                                                   (caution - 0.5f) * 0.1f);
                var aggressiveBreakThreshold = math.saturate(config.ChaosBreakAggressiveThreshold +
                                                             (discipline - 0.5f) * 0.2f -
                                                             (aggression - 0.5f) * 0.2f -
                                                             (riskTolerance - 0.5f) * 0.15f +
                                                             (caution - 0.5f) * 0.1f);
                var formThreshold = math.saturate(config.LawfulnessFormThreshold -
                                                  (discipline - 0.5f) * 0.2f -
                                                  (compliance - 0.5f) * 0.15f -
                                                  (formationAdherence - 0.5f) * 0.15f +
                                                  (aggression - 0.5f) * 0.05f);

                var wantsBreak = chaos >= breakThreshold &&
                                 (leaderProfile.Phase == AttackRunPhase.Approach || leaderProfile.Phase == AttackRunPhase.Execute);
                if (stance == VesselStanceMode.Aggressive && chaos >= aggressiveBreakThreshold)
                {
                    wantsBreak = true;
                }

                var wantsForm = lawfulness >= formThreshold || stance == VesselStanceMode.Defensive;
                if (leaderProfile.Phase == AttackRunPhase.FormUp || leaderProfile.Phase == AttackRunPhase.CombatAirPatrol)
                {
                    wantsForm = true;
                }

                var desiredMode = wantsBreak ? WingModeBreak : WingModeFormUp;
                if (wantsForm && !wantsBreak)
                {
                    desiredMode = WingModeFormUp;
                }

                if (directive.ValueRO.Mode != desiredMode)
                {
                    directive.ValueRW.Mode = desiredMode;
                    directive.ValueRW.LastDecisionTick = time.Tick;

                    if (canEmitActions)
                    {
                        var issuedBy = ResolveIssuedByAuthority(leaderProfile.Carrier);
                        var actionEvent = new ProfileActionEvent
                        {
                            Token = ProfileActionToken.OrderIssued,
                            IntentFlags = ProfileActionIntentFlags.None,
                            JustificationFlags = ProfileActionJustificationFlags.None,
                            OutcomeFlags = ProfileActionOutcomeFlags.None,
                            Magnitude = 100,
                            Actor = profileEntity,
                            Target = leader,
                            IssuingSeat = issuedBy.IssuingSeat,
                            IssuingOccupant = issuedBy.IssuingOccupant,
                            ActingSeat = issuedBy.ActingSeat,
                            ActingOccupant = issuedBy.ActingOccupant,
                            Tick = time.Tick
                        };
                        ProfileActionEventUtility.TryAppend(ref actionStream.ValueRW, actionBuffer, actionEvent, actionStreamConfig.MaxEvents);
                    }
                }

                directive.ValueRW.NextDecisionTick = time.Tick + ResolveDecisionCooldown(config.DecisionCooldownTicks, patience);

                if (desiredMode == WingModeBreak)
                {
                    if (wingMembers.TryGetFirstValue(leader, out var member, out var iterator))
                    {
                        do
                        {
                            if (_profileLookup.HasComponent(member))
                            {
                                var memberProfile = _profileLookup[member];
                                memberProfile.WingLeader = Entity.Null;
                                memberProfile.WingPosition = 0;
                                _profileLookup[member] = memberProfile;
                            }
                        } while (wingMembers.TryGetNextValue(out member, ref iterator));
                    }

                    continue;
                }

                var assigned = (byte)0;
                if (wingMembers.TryGetFirstValue(leader, out var existing, out var existingIterator))
                {
                    do
                    {
                        assigned++;
                    } while (wingMembers.TryGetNextValue(out existing, ref existingIterator));
                }

                if (assigned >= config.MaxWingSize)
                {
                    continue;
                }

                if (unassignedByCarrier.TryGetFirstValue(leaderProfile.Carrier, out var candidate, out var candidateIterator))
                {
                    do
                    {
                        if (candidate == leader)
                        {
                            continue;
                        }

                        if (!_profileLookup.HasComponent(candidate))
                        {
                            continue;
                        }

                        var candidateProfile = _profileLookup[candidate];
                        if (candidateProfile.WingLeader != Entity.Null || candidateProfile.WingPosition != 0)
                        {
                            continue;
                        }

                        if (candidateProfile.Role != leaderProfile.Role)
                        {
                            continue;
                        }

                        assigned++;
                        candidateProfile.WingLeader = leader;
                        candidateProfile.WingPosition = assigned;
                        _profileLookup[candidate] = candidateProfile;

                        if (assigned >= config.MaxWingSize)
                        {
                            break;
                        }
                    } while (unassignedByCarrier.TryGetNextValue(out candidate, ref candidateIterator));
                }
            }

            leaders.Dispose();
            wingMembers.Dispose();
            unassignedByCarrier.Dispose();
        }

        private Entity ResolveProfileEntity(Entity craftEntity)
        {
            if (TryResolveController(craftEntity, AgencyDomain.FlightOps, out var controller))
            {
                return controller != Entity.Null ? controller : craftEntity;
            }

            if (_pilotLinkLookup.HasComponent(craftEntity))
            {
                var link = _pilotLinkLookup[craftEntity];
                if (link.Pilot != Entity.Null)
                {
                    return link.Pilot;
                }
            }

            return craftEntity;
        }

        private bool TryResolveController(Entity craftEntity, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(craftEntity))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[craftEntity];
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

        private IssuedByAuthority ResolveIssuedByAuthority(Entity carrier)
        {
            if (carrier != Entity.Null && _issuedByLookup.HasComponent(carrier))
            {
                return _issuedByLookup[carrier];
            }

            return default;
        }

        private BehaviorDisposition ResolveBehaviorDisposition(Entity profileEntity, Entity craftEntity)
        {
            if (_behaviorDispositionLookup.HasComponent(profileEntity))
            {
                return _behaviorDispositionLookup[profileEntity];
            }

            if (_behaviorDispositionLookup.HasComponent(craftEntity))
            {
                return _behaviorDispositionLookup[craftEntity];
            }

            return BehaviorDisposition.Default;
        }

        private static uint ResolveDecisionCooldown(uint baseCooldown, float patience)
        {
            var multiplier = math.lerp(0.85f, 1.25f, patience);
            return (uint)math.max(1f, math.round(baseCooldown * multiplier));
        }

        private static float ComputeDiscipline(DynamicBuffer<TopOutlook> outlooks)
        {
            var discipline = 0.5f;
            for (var i = 0; i < outlooks.Length; i++)
            {
                var entry = outlooks[i];
                var weight = math.clamp((float)entry.Weight, 0f, 1f);
                switch (entry.OutlookId)
                {
                    case OutlookId.Loyalist:
                        discipline += 0.2f * weight;
                        break;
                    case OutlookId.Fanatic:
                        discipline += 0.25f * weight;
                        break;
                    case OutlookId.Opportunist:
                        discipline -= 0.15f * weight;
                        break;
                    case OutlookId.Mutinous:
                        discipline -= 0.3f * weight;
                        break;
                }
            }

            return math.saturate(discipline);
        }
    }
}

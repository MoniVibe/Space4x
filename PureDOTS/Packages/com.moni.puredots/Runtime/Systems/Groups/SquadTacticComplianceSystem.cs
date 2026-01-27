using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Deterministic compliance evaluation for squad tactic orders with one-step escalation and safe downgrade.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(GroupTaskAllocatorSystem))]
    [UpdateBefore(typeof(SquadCohesionSystem))]
    public partial struct SquadTacticComplianceSystem : ISystem
    {
        private BufferLookup<ControlClaim> _claimLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<BehaviorDisposition> _dispositionLookup;
        private ComponentLookup<AuthorityBody> _authorityBodyLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SquadTacticOrder>();

            _claimLookup = state.GetBufferLookup<ControlClaim>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _dispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _authorityBodyLookup = state.GetComponentLookup<AuthorityBody>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var config = OrderComplianceConfig.Default;
            if (SystemAPI.TryGetSingleton<OrderComplianceConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            _claimLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _dispositionLookup.Update(ref state);
            _authorityBodyLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            ProfileActionEventStreamConfig streamConfig = default;
            var canEmitActions = SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var streamEntity) &&
                                 SystemAPI.TryGetSingleton(out streamConfig);
            DynamicBuffer<ProfileActionEvent> actionBuffer = default;
            RefRW<ProfileActionEventStream> actionStream = default;
            if (canEmitActions)
            {
                actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);
                actionStream = SystemAPI.GetComponentRW<ProfileActionEventStream>(streamEntity);
            }

            foreach (var (tactic, entity) in SystemAPI.Query<RefRW<SquadTacticOrder>>().WithAll<GroupTag>().WithEntityAccess())
            {
                var orderSnapshot = tactic.ValueRO;
                if (orderSnapshot.IssueTick == 0 || orderSnapshot.Kind == SquadTacticKind.None)
                {
                    continue;
                }

                var hasIssued = em.HasComponent<OrderIssued>(entity);
                var issued = hasIssued ? em.GetComponentData<OrderIssued>(entity) : default;
                var isNewOrder = !hasIssued
                                 || issued.IssueTick != orderSnapshot.IssueTick
                                 || issued.SquadTactic != orderSnapshot.Kind
                                 || issued.Issuer != orderSnapshot.Issuer;

                var hasEscalation = em.HasComponent<OrderEscalationState>(entity);
                var escalation = hasEscalation
                    ? em.GetComponentData<OrderEscalationState>(entity)
                    : new OrderEscalationState { CooldownTicks = config.EscalationCooldownTicks };

                if (isNewOrder)
                {
                    escalation.AttemptCount = 0;
                    escalation.LastAttemptTick = 0;
                }

                var hasOutcome = em.HasComponent<OrderOutcome>(entity);
                var outcome = hasOutcome ? em.GetComponentData<OrderOutcome>(entity) : default;

                if (!isNewOrder && outcome.DecidedTick == orderSnapshot.IssueTick)
                {
                    continue;
                }

                var orderDomain = ResolveOrderDomain(orderSnapshot.Kind);
                var issuerInfo = ResolveIssuerInfo(entity, orderSnapshot.Issuer, orderDomain);
                var subjectInfo = ResolveSubjectSeat(entity, orderDomain);

                if (isNewOrder)
                {
                    var orderIssued = BuildOrderIssued(orderSnapshot, entity, issuerInfo, subjectInfo);
                    if (hasIssued)
                    {
                        ecb.SetComponent(entity, orderIssued);
                    }
                    else
                    {
                        ecb.AddComponent(entity, orderIssued);
                    }

                    if (canEmitActions)
                    {
                        EmitProfileEvent(
                            ref actionStream.ValueRW,
                            actionBuffer,
                            streamConfig.MaxEvents,
                            ProfileActionToken.OrderIssued,
                            issuerInfo.Actor,
                            entity,
                            orderIssued,
                            orderSnapshot.IssueTick);
                    }
                }

                var disposition = ResolveDisposition(subjectInfo.Actor, entity);
                var threshold = ResolveThreshold(config, orderSnapshot.Kind);
                var complianceScore = ComputeComplianceScore(
                    config,
                    issuerInfo.Claim,
                    disposition,
                    orderSnapshot.Kind,
                    entity,
                    orderSnapshot.Issuer,
                    orderSnapshot.IssueTick);

                var accepted = complianceScore >= threshold;
                var refusalReason = accepted ? OrderRefusalReason.None : ResolveRefusalReason(issuerInfo.Claim, disposition, orderSnapshot.Kind);

                var finalOutcome = accepted ? OrderOutcomeKind.Obey : OrderOutcomeKind.Refuse;
                var finalIssuerInfo = issuerInfo;

                if (!accepted && ShouldAttemptEscalation(escalation, config, timeState.Tick))
                {
                    if (TryResolveEscalationClaim(entity, orderSnapshot.Issuer, orderDomain, issuerInfo.Claim, out var escalated))
                    {
                        escalation.AttemptCount++;
                        escalation.LastAttemptTick = timeState.Tick;
                        finalIssuerInfo = escalated;

                        var escalatedScore = ComputeComplianceScore(
                            config,
                            escalated.Claim,
                            disposition,
                            orderSnapshot.Kind,
                            entity,
                            escalated.Actor,
                            orderSnapshot.IssueTick,
                            applyEscalationBonus: true);

                        if (escalatedScore >= threshold)
                        {
                            accepted = true;
                            finalOutcome = OrderOutcomeKind.EscalatedObey;
                            finalIssuerInfo = escalated;
                            refusalReason = OrderRefusalReason.None;
                        }
                        else
                        {
                            finalOutcome = OrderOutcomeKind.EscalatedRefuse;
                            refusalReason = ResolveRefusalReason(escalated.Claim, disposition, orderSnapshot.Kind);
                        }
                    }
                }

                if (!accepted)
                {
                    var fallback = ResolveFallback(orderSnapshot.Kind);
                    if (fallback != orderSnapshot.Kind)
                    {
                        tactic.ValueRW.Kind = fallback;
                        tactic.ValueRW.AckMode = ComputeAckMode(fallback);
                    }
                }
                else if (finalOutcome == OrderOutcomeKind.EscalatedObey && finalIssuerInfo.Actor != Entity.Null)
                {
                    tactic.ValueRW.Issuer = finalIssuerInfo.Actor;
                }

                var orderOutcome = new OrderOutcome
                {
                    Kind = OrderKind.SquadTactic,
                    SquadTactic = orderSnapshot.Kind,
                    Outcome = finalOutcome,
                    Reason = refusalReason,
                    Issuer = finalIssuerInfo.Actor != Entity.Null ? finalIssuerInfo.Actor : issuerInfo.Actor,
                    Subject = entity,
                    ActingSeat = subjectInfo.Seat,
                    ActingOccupant = subjectInfo.Actor,
                    IssuedTick = orderSnapshot.IssueTick,
                    DecidedTick = timeState.Tick
                };

                if (hasOutcome)
                {
                    ecb.SetComponent(entity, orderOutcome);
                }
                else
                {
                    ecb.AddComponent(entity, orderOutcome);
                }

                if (hasEscalation)
                {
                    ecb.SetComponent(entity, escalation);
                }
                else
                {
                    ecb.AddComponent(entity, escalation);
                }

                if (canEmitActions)
                {
                    var token = accepted ? ProfileActionToken.ObeyOrder : ProfileActionToken.DisobeyOrder;
                    EmitProfileEvent(
                        ref actionStream.ValueRW,
                        actionBuffer,
                        streamConfig.MaxEvents,
                        token,
                        subjectInfo.Actor,
                        entity,
                        BuildOrderIssued(orderSnapshot, entity, finalIssuerInfo, subjectInfo),
                        timeState.Tick);
                }
            }

            ecb.Playback(em);
        }

        private static OrderIssued BuildOrderIssued(
            in SquadTacticOrder tactic,
            Entity subject,
            in IssuerInfo issuerInfo,
            in SubjectInfo subjectInfo)
        {
            return new OrderIssued
            {
                Kind = OrderKind.SquadTactic,
                SquadTactic = tactic.Kind,
                Issuer = issuerInfo.Actor,
                Subject = subject,
                Target = tactic.Target,
                FocusBudgetCost = tactic.FocusBudgetCost,
                DisciplineRequired = tactic.DisciplineRequired,
                AckMode = tactic.AckMode,
                IssueTick = tactic.IssueTick,
                IssuingSeat = issuerInfo.Seat,
                IssuingOccupant = issuerInfo.Actor,
                ActingSeat = subjectInfo.Seat,
                ActingOccupant = subjectInfo.Actor
            };
        }

        private void EmitProfileEvent(
            ref ProfileActionEventStream stream,
            DynamicBuffer<ProfileActionEvent> buffer,
            int maxEvents,
            ProfileActionToken token,
            Entity actor,
            Entity target,
            in OrderIssued issued,
            uint tick)
        {
            var actionEvent = new ProfileActionEvent
            {
                Token = token,
                IntentFlags = ProfileActionIntentFlags.None,
                JustificationFlags = ProfileActionJustificationFlags.None,
                OutcomeFlags = ProfileActionOutcomeFlags.None,
                Magnitude = 100,
                Actor = actor != Entity.Null ? actor : target,
                Target = target,
                IssuingSeat = issued.IssuingSeat,
                IssuingOccupant = issued.IssuingOccupant,
                ActingSeat = issued.ActingSeat,
                ActingOccupant = issued.ActingOccupant,
                Tick = tick
            };

            ProfileActionEventUtility.TryAppend(ref stream, buffer, actionEvent, maxEvents);
        }

        private struct IssuerInfo
        {
            public Entity Actor;
            public Entity Seat;
            public ControlClaim Claim;
        }

        private struct SubjectInfo
        {
            public Entity Actor;
            public Entity Seat;
        }

        private IssuerInfo ResolveIssuerInfo(Entity subject, Entity issuer, AgencyDomain domain)
        {
            var info = new IssuerInfo
            {
                Actor = issuer,
                Seat = Entity.Null,
                Claim = default
            };

            if (issuer == Entity.Null || !_claimLookup.HasBuffer(subject))
            {
                return info;
            }

            var claims = _claimLookup[subject];
            var domainMask = (uint)domain;
            float bestPressure = float.NegativeInfinity;

            for (int i = 0; i < claims.Length; i++)
            {
                var claim = claims[i];
                if ((((uint)claim.Domains) & domainMask) == 0u)
                {
                    continue;
                }

                if (claim.Controller != issuer && claim.SourceSeat != issuer)
                {
                    continue;
                }

                var pressure = math.max(0f, claim.Pressure);
                if (pressure <= bestPressure)
                {
                    continue;
                }

                bestPressure = pressure;
                info.Claim = claim;
                info.Actor = claim.Controller != Entity.Null ? claim.Controller : issuer;
                info.Seat = claim.SourceSeat;
            }

            return info;
        }

        private bool TryResolveEscalationClaim(
            Entity subject,
            Entity issuer,
            AgencyDomain domain,
            in ControlClaim baseline,
            out IssuerInfo escalated)
        {
            escalated = default;
            if (!_claimLookup.HasBuffer(subject))
            {
                return false;
            }

            var claims = _claimLookup[subject];
            var domainMask = (uint)domain;
            float baselineScore = baseline.Pressure * (1f + baseline.Legitimacy);
            float bestScore = baselineScore;

            for (int i = 0; i < claims.Length; i++)
            {
                var claim = claims[i];
                if ((((uint)claim.Domains) & domainMask) == 0u)
                {
                    continue;
                }

                if (claim.Controller == issuer)
                {
                    continue;
                }

                var score = math.max(0f, claim.Pressure) * (1f + math.saturate(claim.Legitimacy));
                if (claim.SourceKind == ControlClaimSourceKind.Authority)
                {
                    score += 0.05f;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                escalated = new IssuerInfo
                {
                    Actor = claim.Controller,
                    Seat = claim.SourceSeat,
                    Claim = claim
                };
            }

            return escalated.Actor != Entity.Null;
        }

        private SubjectInfo ResolveSubjectSeat(Entity subject, AgencyDomain domain)
        {
            var info = new SubjectInfo
            {
                Actor = subject,
                Seat = Entity.Null
            };

            if (_authorityBodyLookup.HasComponent(subject))
            {
                var body = _authorityBodyLookup[subject];
                var seatEntity = body.ExecutiveSeat;
                if (seatEntity != Entity.Null && _seatOccupantLookup.HasComponent(seatEntity))
                {
                    var occupant = _seatOccupantLookup[seatEntity];
                    if (occupant.OccupantEntity != Entity.Null)
                    {
                        info.Seat = seatEntity;
                        info.Actor = occupant.OccupantEntity;
                        return info;
                    }
                }
            }

            if (_seatRefLookup.HasBuffer(subject))
            {
                var seats = _seatRefLookup[subject];
                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity) || !_seatOccupantLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var seat = _seatLookup[seatEntity];
                    if ((((uint)seat.Domains) & (uint)domain) == 0u)
                    {
                        continue;
                    }

                    var occupant = _seatOccupantLookup[seatEntity];
                    if (occupant.OccupantEntity == Entity.Null)
                    {
                        continue;
                    }

                    info.Seat = seatEntity;
                    info.Actor = occupant.OccupantEntity;
                    return info;
                }
            }

            if (_resolvedControlLookup.HasBuffer(subject))
            {
                var resolved = _resolvedControlLookup[subject];
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (resolved[i].Domain == domain && resolved[i].Controller != Entity.Null)
                    {
                        info.Actor = resolved[i].Controller;
                        return info;
                    }
                }
            }

            return info;
        }

        private BehaviorDisposition ResolveDisposition(Entity actor, Entity fallback)
        {
            if (actor != Entity.Null && _dispositionLookup.HasComponent(actor))
            {
                return _dispositionLookup[actor];
            }

            if (_dispositionLookup.HasComponent(fallback))
            {
                return _dispositionLookup[fallback];
            }

            return BehaviorDisposition.Default;
        }

        private static float ResolveThreshold(in OrderComplianceConfig config, SquadTacticKind kind)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => config.TightenThreshold,
                SquadTacticKind.Loosen => config.LoosenThreshold,
                SquadTacticKind.FlankLeft => config.FlankThreshold,
                SquadTacticKind.FlankRight => config.FlankThreshold,
                SquadTacticKind.Collapse => config.CollapseThreshold,
                SquadTacticKind.Retreat => config.RetreatThreshold,
                _ => 0.5f
            };
        }

        private static float ComputeComplianceScore(
            in OrderComplianceConfig config,
            in ControlClaim claim,
            in BehaviorDisposition disposition,
            SquadTacticKind kind,
            Entity subject,
            Entity issuer,
            uint issueTick,
            bool applyEscalationBonus = false)
        {
            float pressure = math.max(0f, claim.Pressure);
            float legitimacy = math.saturate(claim.Legitimacy);
            float consent = math.saturate(claim.Consent);
            float hostility = math.saturate(claim.Hostility);

            if (applyEscalationBonus)
            {
                pressure = math.min(1f, pressure + config.EscalationPressureBonus);
                legitimacy = math.min(1f, legitimacy + config.EscalationLegitimacyBonus);
            }

            float claimScore = (pressure * config.PressureWeight)
                               + (legitimacy * config.LegitimacyWeight)
                               + (consent * config.ConsentWeight)
                               - (hostility * config.HostilityWeight);
            claimScore = math.saturate(claimScore);

            float dispositionScore = ResolveDispositionScore(disposition, kind);
            float mixedScore = math.lerp(claimScore, dispositionScore, math.saturate(config.DispositionWeight));

            float bias = 0f;
            if (config.DeterministicBias > 0f)
            {
                var hash = math.hash(new int3(subject.Index ^ issuer.Index, (int)issueTick, (int)kind));
                bias = ((hash & 1023) / 1023f - 0.5f) * config.DeterministicBias;
            }

            return math.saturate(mixedScore + bias);
        }

        private static float ResolveDispositionScore(in BehaviorDisposition disposition, SquadTacticKind kind)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => math.saturate(
                    disposition.Compliance * 0.35f +
                    disposition.FormationAdherence * 0.45f +
                    disposition.Patience * 0.2f),
                SquadTacticKind.Collapse => math.saturate(
                    disposition.Compliance * 0.4f +
                    disposition.FormationAdherence * 0.4f +
                    disposition.Aggression * 0.2f),
                SquadTacticKind.FlankLeft => math.saturate(
                    disposition.Aggression * 0.4f +
                    disposition.RiskTolerance * 0.35f +
                    disposition.Compliance * 0.25f),
                SquadTacticKind.FlankRight => math.saturate(
                    disposition.Aggression * 0.4f +
                    disposition.RiskTolerance * 0.35f +
                    disposition.Compliance * 0.25f),
                SquadTacticKind.Retreat => math.saturate(
                    disposition.Caution * 0.45f +
                    disposition.Patience * 0.25f +
                    disposition.Compliance * 0.3f),
                SquadTacticKind.Loosen => math.saturate(
                    disposition.Compliance * 0.35f +
                    (1f - disposition.Caution) * 0.2f +
                    disposition.FormationAdherence * 0.25f +
                    disposition.Patience * 0.2f),
                _ => 0.5f
            };
        }

        private static OrderRefusalReason ResolveRefusalReason(in ControlClaim claim, in BehaviorDisposition disposition, SquadTacticKind kind)
        {
            if (claim.Pressure <= 0.05f)
            {
                return OrderRefusalReason.WeakClaim;
            }

            if (claim.Legitimacy <= 0.2f)
            {
                return OrderRefusalReason.LowLegitimacy;
            }

            if (claim.Consent <= 0.2f)
            {
                return OrderRefusalReason.LowConsent;
            }

            if (claim.Hostility >= 0.7f)
            {
                return OrderRefusalReason.HostileIssuer;
            }

            return OrderRefusalReason.DispositionMismatch;
        }

        private static bool ShouldAttemptEscalation(in OrderEscalationState escalation, in OrderComplianceConfig config, uint tick)
        {
            if (escalation.AttemptCount >= config.MaxEscalationAttempts)
            {
                return false;
            }

            if (config.EscalationCooldownTicks == 0)
            {
                return true;
            }

            if (escalation.LastAttemptTick == 0)
            {
                return true;
            }

            return tick - escalation.LastAttemptTick >= config.EscalationCooldownTicks;
        }

        private static SquadTacticKind ResolveFallback(SquadTacticKind kind)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => SquadTacticKind.Loosen,
                SquadTacticKind.FlankLeft => SquadTacticKind.Tighten,
                SquadTacticKind.FlankRight => SquadTacticKind.Tighten,
                SquadTacticKind.Collapse => SquadTacticKind.Tighten,
                _ => kind
            };
        }

        private static byte ComputeAckMode(SquadTacticKind kind)
        {
            return kind == SquadTacticKind.Tighten
                   || kind == SquadTacticKind.FlankLeft
                   || kind == SquadTacticKind.FlankRight
                   || kind == SquadTacticKind.Collapse
                ? (byte)1
                : (byte)0;
        }

        private static AgencyDomain ResolveOrderDomain(SquadTacticKind kind)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => AgencyDomain.FlightOps | AgencyDomain.Movement | AgencyDomain.Combat,
                SquadTacticKind.Loosen => AgencyDomain.FlightOps | AgencyDomain.Movement,
                SquadTacticKind.FlankLeft => AgencyDomain.FlightOps | AgencyDomain.Movement | AgencyDomain.Combat,
                SquadTacticKind.FlankRight => AgencyDomain.FlightOps | AgencyDomain.Movement | AgencyDomain.Combat,
                SquadTacticKind.Collapse => AgencyDomain.FlightOps | AgencyDomain.Movement | AgencyDomain.Combat,
                SquadTacticKind.Retreat => AgencyDomain.FlightOps | AgencyDomain.Movement,
                _ => AgencyDomain.Movement
            };
        }
    }
}

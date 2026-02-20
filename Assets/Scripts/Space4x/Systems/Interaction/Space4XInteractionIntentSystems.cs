using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XInteractionIntentBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XInteractionIntentStream>(out _))
            {
                return;
            }

            var streamEntity = state.EntityManager.CreateEntity(typeof(Space4XInteractionIntentStream));
            state.EntityManager.AddBuffer<Space4XInteractionIntent>(streamEntity);

            if (!SystemAPI.TryGetSingletonEntity<Space4XInteractionIntentPolicyConfig>(out _))
            {
                var configEntity = state.EntityManager.CreateEntity(typeof(Space4XInteractionIntentPolicyConfig));
                state.EntityManager.SetComponentData(configEntity, Space4XInteractionIntentPolicyConfig.Default);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromCommOrderSystem))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromDockingRequestSystem))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromGateEventSystem))]
    public partial struct Space4XInteractionIntentResetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var stream = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);
            stream.Clear();
        }
    }

    /// <summary>
    /// Converts comm order intents into shared interaction intents for AI/player.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XCommDecisionBridgeSystem))]
    public partial struct Space4XInteractionIntentFromCommOrderSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
            state.RequireForUpdate<Space4XCommOrderIntent>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var stream = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);

            foreach (var (commIntent, actor) in SystemAPI.Query<RefRO<Space4XCommOrderIntent>>().WithEntityAccess())
            {
                var messageTick = commIntent.ValueRO.ReceivedTick;
                if (messageTick == 0)
                {
                    continue;
                }

                var cursor = ReadCursor(state.EntityManager, actor);
                if (messageTick <= cursor.LastCommTick)
                {
                    continue;
                }

                var target = commIntent.ValueRO.Target != Entity.Null ? commIntent.ValueRO.Target : commIntent.ValueRO.Sender;
                var action = ResolveCommAction(commIntent.ValueRO.Verb);
                if (action == Space4XInteractionIntentAction.None)
                {
                    continue;
                }

                var actorFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var targetFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(target, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var source = Space4XInteractionIntentSourceUtility.ResolveSource(actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);

                var topicId = new FixedString64Bytes("space4x.intent.comm.");
                topicId.Append((int)commIntent.ValueRO.Verb);

                stream.Add(new Space4XInteractionIntent
                {
                    Action = action,
                    Source = source,
                    Actor = actor,
                    Target = target,
                    ContextEntity = actor,
                    ActorFactionId = actorFactionId,
                    TargetFactionId = targetFactionId,
                    Tick = messageTick,
                    CorrelationId = commIntent.ValueRO.SourceMessageId != 0 ? commIntent.ValueRO.SourceMessageId : commIntent.ValueRO.ContextHash,
                    Priority = (byte)commIntent.ValueRO.Priority,
                    Confidence = (half)math.saturate(commIntent.ValueRO.Confidence),
                    TopicId = topicId
                });

                cursor.LastCommTick = messageTick;
                WriteCursor(state.EntityManager, actor, in cursor);
            }
        }

        private static Space4XInteractionIntentAction ResolveCommAction(CommOrderVerb verb)
        {
            return verb switch
            {
                CommOrderVerb.Attack => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.FocusFire => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.Suppress => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.DrawFire => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.Spearhead => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.Flank => Space4XInteractionIntentAction.Attack,
                CommOrderVerb.None => Space4XInteractionIntentAction.Hail,
                _ => Space4XInteractionIntentAction.Hail
            };
        }

        private static Space4XInteractionIntentCursor ReadCursor(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                return default;
            }

            return entityManager.GetComponentData<Space4XInteractionIntentCursor>(entity);
        }

        private static void WriteCursor(EntityManager entityManager, Entity entity, in Space4XInteractionIntentCursor cursor)
        {
            if (entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                entityManager.SetComponentData(entity, cursor);
            }
            else
            {
                entityManager.AddComponentData(entity, cursor);
            }
        }
    }

    /// <summary>
    /// Converts docking requests into shared interaction intents for AI/player.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XVesselDockingRequestSystem))]
    public partial struct Space4XInteractionIntentFromDockingRequestSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
            state.RequireForUpdate<DockingRequest>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var stream = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);
            var currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            foreach (var (dockingRequest, actor) in SystemAPI.Query<RefRO<DockingRequest>>().WithEntityAccess())
            {
                var emissionTick = dockingRequest.ValueRO.RequestTick != 0 ? dockingRequest.ValueRO.RequestTick : currentTick;
                if (emissionTick == 0)
                {
                    continue;
                }

                var cursor = ReadCursor(state.EntityManager, actor);
                if (emissionTick <= cursor.LastDockingTick)
                {
                    continue;
                }

                var target = dockingRequest.ValueRO.TargetCarrier;
                var actorFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var targetFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(target, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var source = Space4XInteractionIntentSourceUtility.ResolveSource(actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);

                stream.Add(new Space4XInteractionIntent
                {
                    Action = Space4XInteractionIntentAction.Dock,
                    Source = source,
                    Actor = actor,
                    Target = target,
                    ContextEntity = actor,
                    ActorFactionId = actorFactionId,
                    TargetFactionId = targetFactionId,
                    Tick = emissionTick,
                    CorrelationId = (uint)math.max(target.Index, 0),
                    Priority = dockingRequest.ValueRO.Priority,
                    Confidence = (half)1f,
                    TopicId = new FixedString64Bytes("space4x.intent.dock")
                });

                cursor.LastDockingTick = emissionTick;
                WriteCursor(state.EntityManager, actor, in cursor);
            }
        }

        private static Space4XInteractionIntentCursor ReadCursor(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                return default;
            }

            return entityManager.GetComponentData<Space4XInteractionIntentCursor>(entity);
        }

        private static void WriteCursor(EntityManager entityManager, Entity entity, in Space4XInteractionIntentCursor cursor)
        {
            if (entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                entityManager.SetComponentData(entity, cursor);
            }
            else
            {
                entityManager.AddComponentData(entity, cursor);
            }
        }
    }

    /// <summary>
    /// Converts accepted player/agent gate selections into shared interaction intents.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XInteractionIntentFromGateEventSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
            state.RequireForUpdate<Space4XInteractionGateState>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var stream = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateEvents = state.EntityManager.GetBuffer<Space4XYarnGateEvent>(gateEntity);
            if (gateEvents.Length == 0)
            {
                return;
            }

            for (int i = gateEvents.Length - 1; i >= 0; i--)
            {
                var gateEvent = gateEvents[i];
                if (gateEvent.Accepted == 0 || gateEvent.Actor == Entity.Null)
                {
                    continue;
                }

                if (!TryMapGateEventToAction(in gateEvent, out var action))
                {
                    continue;
                }

                var cursor = ReadCursor(state.EntityManager, gateEvent.Actor);
                if (gateEvent.Tick != 0 && gateEvent.Tick <= cursor.LastGateTick)
                {
                    continue;
                }

                var trackedTick = gateEvent.Tick != 0 ? gateEvent.Tick : cursor.LastGateTick + 1;
                var actorFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(gateEvent.Actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var targetFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(gateEvent.Target, in _carrierLookup, in _affiliationLookup, in _factionLookup);
                var source = Space4XInteractionIntentSourceUtility.ResolveSource(gateEvent.Actor, in _carrierLookup, in _affiliationLookup, in _factionLookup);

                stream.Add(new Space4XInteractionIntent
                {
                    Action = action,
                    Source = source,
                    Actor = gateEvent.Actor,
                    Target = gateEvent.Target,
                    ContextEntity = gateEvent.ContextEntity,
                    ActorFactionId = actorFactionId,
                    TargetFactionId = targetFactionId,
                    Tick = trackedTick,
                    CorrelationId = (uint)gateEvent.Slot,
                    Priority = 1,
                    Confidence = (half)1f,
                    TopicId = gateEvent.YarnNodeId
                });

                cursor.LastGateTick = trackedTick;
                WriteCursor(state.EntityManager, gateEvent.Actor, in cursor);
            }
        }

        private static bool TryMapGateEventToAction(in Space4XYarnGateEvent gateEvent, out Space4XInteractionIntentAction action)
        {
            switch (gateEvent.Kind)
            {
                case Space4XInteractionGateKind.Hail:
                    switch (gateEvent.Slot)
                    {
                        case 1:
                            action = Space4XInteractionIntentAction.Hail;
                            return true;
                        case 2:
                            action = Space4XInteractionIntentAction.Trade;
                            return true;
                        case 3:
                            action = Space4XInteractionIntentAction.Socialize;
                            return true;
                        case 4:
                            action = Space4XInteractionIntentAction.Attack;
                            return true;
                        default:
                            action = default;
                            return false;
                    }

                case Space4XInteractionGateKind.Docking:
                    if (gateEvent.Slot <= 4)
                    {
                        action = Space4XInteractionIntentAction.Dock;
                        return true;
                    }
                    action = default;
                    return false;

                case Space4XInteractionGateKind.Trade:
                    if (gateEvent.Slot <= 4)
                    {
                        action = Space4XInteractionIntentAction.Trade;
                        return true;
                    }
                    action = default;
                    return false;

                case Space4XInteractionGateKind.Equipping:
                    if (gateEvent.Slot <= 4)
                    {
                        action = Space4XInteractionIntentAction.Equip;
                        return true;
                    }
                    action = default;
                    return false;

                case Space4XInteractionGateKind.Production:
                    if (gateEvent.Slot <= 4)
                    {
                        action = Space4XInteractionIntentAction.Produce;
                        return true;
                    }
                    action = default;
                    return false;

                default:
                    action = default;
                    return false;
            }
        }

        private static Space4XInteractionIntentCursor ReadCursor(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                return default;
            }

            return entityManager.GetComponentData<Space4XInteractionIntentCursor>(entity);
        }

        private static void WriteCursor(EntityManager entityManager, Entity entity, in Space4XInteractionIntentCursor cursor)
        {
            if (entityManager.HasComponent<Space4XInteractionIntentCursor>(entity))
            {
                entityManager.SetComponentData(entity, cursor);
            }
            else
            {
                entityManager.AddComponentData(entity, cursor);
            }
        }
    }

    /// <summary>
    /// Ensures entities participating in interaction intent generation have a behavior profile.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromCommOrderSystem))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromDockingRequestSystem))]
    [UpdateBefore(typeof(Space4XInteractionIntentFromGateEventSystem))]
    public partial struct Space4XInteractionBehaviorProfileBootstrapSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;

        public void OnCreate(ref SystemState state)
        {
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _alignmentLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>()
                         .WithNone<Space4XInteractionBehaviorProfile>()
                         .WithEntityAccess())
            {
                var profile = BuildProfile(faction.ValueRO, ResolveLawAxis(entity));
                ecb.AddComponent(entity, profile);
            }

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>>()
                         .WithNone<Space4XFaction, Space4XInteractionBehaviorProfile>()
                         .WithEntityAccess())
            {
                var lawAxis = (float)alignment.ValueRO.Law;
                var profile = Space4XInteractionBehaviorProfile.Default;
                profile.ChaoticBias = (half)math.saturate(-lawAxis);
                profile.LawfulBias = (half)math.saturate(lawAxis);
                ecb.AddComponent(entity, profile);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float ResolveLawAxis(Entity entity)
        {
            if (!_alignmentLookup.HasComponent(entity))
            {
                return 0f;
            }

            return math.clamp((float)_alignmentLookup[entity].Law, -1f, 1f);
        }

        private static Space4XInteractionBehaviorProfile BuildProfile(in Space4XFaction faction, float lawAxis)
        {
            var profile = Space4XInteractionBehaviorProfile.Default;
            float tradeLeniency = 0f;
            float aggressionBias = math.clamp((float)faction.Aggression * 2f - 1f, -1f, 1f) * 0.4f;
            float chaoticBias = 0f;
            float lawfulBias = 0f;

            if ((faction.Outlook & FactionOutlook.Materialist) != 0)
            {
                tradeLeniency += 0.4f;
            }
            if ((faction.Outlook & FactionOutlook.Spiritualist) != 0)
            {
                tradeLeniency -= 0.4f;
            }
            if ((faction.Outlook & FactionOutlook.Militarist) != 0)
            {
                aggressionBias += 0.25f;
            }
            if ((faction.Outlook & FactionOutlook.Pacifist) != 0)
            {
                aggressionBias -= 0.25f;
            }
            if ((faction.Outlook & FactionOutlook.Corrupt) != 0)
            {
                chaoticBias += 0.35f;
            }
            if ((faction.Outlook & FactionOutlook.Honorable) != 0 ||
                (faction.Outlook & FactionOutlook.Authoritarian) != 0)
            {
                lawfulBias += 0.35f;
            }

            if (lawAxis > 0f)
            {
                lawfulBias += lawAxis * 0.65f;
            }
            else if (lawAxis < 0f)
            {
                chaoticBias += -lawAxis * 0.65f;
            }

            profile.TradeLeniency = (half)math.clamp(tradeLeniency, -1f, 1f);
            profile.AggressionBias = (half)math.clamp(aggressionBias, -1f, 1f);
            profile.ChaoticBias = (half)math.saturate(chaoticBias);
            profile.LawfulBias = (half)math.saturate(lawfulBias);
            return profile;
        }
    }

    /// <summary>
    /// Applies relation thresholds and profile-aware behavior gates to shared intents.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionIntentFromCommOrderSystem))]
    [UpdateAfter(typeof(Space4XInteractionIntentFromDockingRequestSystem))]
    [UpdateAfter(typeof(Space4XInteractionIntentFromGateEventSystem))]
    public partial struct Space4XInteractionIntentPolicySystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<Space4XInteractionBehaviorProfile> _profileLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;
        private BufferLookup<TradeOffer> _offerLookup;
        private BufferLookup<MarketPriceEntry> _priceLookup;
        private BufferLookup<ResourceStorage> _storageLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
            state.RequireForUpdate<Space4XInteractionIntentPolicyConfig>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _profileLookup = state.GetComponentLookup<Space4XInteractionBehaviorProfile>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
            _offerLookup = state.GetBufferLookup<TradeOffer>(true);
            _priceLookup = state.GetBufferLookup<MarketPriceEntry>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(true);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);
            _offerLookup.Update(ref state);
            _priceLookup.Update(ref state);
            _storageLookup.Update(ref state);
            _supplyLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XInteractionIntentPolicyConfig>();
            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var stream = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);
            if (stream.Length == 0)
            {
                return;
            }

            for (int i = stream.Length - 1; i >= 0; i--)
            {
                var intent = stream[i];
                if (intent.Actor == Entity.Null)
                {
                    continue;
                }

                var targetFactionId = intent.TargetFactionId;
                if (targetFactionId == 0 && intent.Target != Entity.Null)
                {
                    targetFactionId = Space4XInteractionIntentSourceUtility.ResolveFactionId(
                        intent.Target,
                        in _carrierLookup,
                        in _affiliationLookup,
                        in _factionLookup);
                }

                var relationScore = ResolveRelationScore(intent.Actor, targetFactionId);
                bool remove = false;
                switch (intent.Action)
                {
                    case Space4XInteractionIntentAction.Trade:
                        if (relationScore < config.MinTradeRelationScore)
                        {
                            remove = true;
                            break;
                        }

                        if (intent.Source == Space4XInteractionIntentSource.AgentAI)
                        {
                            if (!TryEvaluateTradeAcceptability(in intent, relationScore, in config, out var desirability))
                            {
                                remove = true;
                                break;
                            }

                            // Bias AI toward higher-relation and better-margin trade partners.
                            var relationBonus = math.clamp((relationScore - config.MinTradeRelationScore) / 15, 0, 4);
                            var desirabilityBonus = math.clamp((int)math.floor(desirability * 6f), 0, 6);
                            intent.Priority = (byte)math.min(255, intent.Priority + relationBonus + desirabilityBonus);
                            stream[i] = intent;
                        }
                        break;

                    case Space4XInteractionIntentAction.Hail:
                        if (relationScore < config.MinHailRelationScore)
                        {
                            remove = true;
                        }
                        break;
                }

                if (remove)
                {
                    stream.RemoveAt(i);
                    continue;
                }

                if (intent.Source != Space4XInteractionIntentSource.AgentAI ||
                    intent.Target == Entity.Null ||
                    (intent.Action != Space4XInteractionIntentAction.Hail &&
                     intent.Action != Space4XInteractionIntentAction.Trade &&
                     intent.Action != Space4XInteractionIntentAction.Socialize))
                {
                    continue;
                }

                if (!ShouldEscalateToAttack(in intent, relationScore, in config))
                {
                    continue;
                }

                intent.Action = Space4XInteractionIntentAction.Attack;
                intent.Priority = (byte)math.max(intent.Priority, (byte)CommOrderPriority.High);
                if (intent.TopicId.IsEmpty)
                {
                    intent.TopicId = new FixedString64Bytes("space4x.intent.attack.escalated");
                }
                stream[i] = intent;
            }
        }

        private int ResolveRelationScore(Entity actor, ushort targetFactionId)
        {
            if (targetFactionId == 0)
            {
                return 0;
            }

            if (!Space4XInteractionIntentSourceUtility.TryResolveFaction(
                    actor,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var actorFaction,
                    out var actorFactionId))
            {
                return 0;
            }

            if (actorFactionId == targetFactionId)
            {
                return 100;
            }

            if (_relationLookup.HasBuffer(actorFaction))
            {
                var relations = _relationLookup[actorFaction];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == targetFactionId)
                    {
                        return math.clamp((int)relation.Score, -100, 100);
                    }
                }
            }

            if (_contactLookup.HasBuffer(actorFaction))
            {
                var contacts = _contactLookup[actorFaction];
                for (int i = 0; i < contacts.Length; i++)
                {
                    var contact = contacts[i];
                    if (contact.ContactFactionId == targetFactionId)
                    {
                        var fromStanding = (int)math.round((float)contact.Standing * 200f - 100f);
                        return math.clamp(fromStanding, -100, 100);
                    }
                }
            }

            return 0;
        }

        private bool TryEvaluateTradeAcceptability(
            in Space4XInteractionIntent intent,
            int relationScore,
            in Space4XInteractionIntentPolicyConfig config,
            out float desirability)
        {
            desirability = 0f;
            float bestOffset = ResolveBestTradeOffset(intent.Target, out var hasOffers);
            if (!hasOffers)
            {
                return false;
            }

            ResolveProfileContext(
                intent.Actor,
                out var factionOutlook,
                out var lawAxis,
                out var profile);

            float requiredOffset = math.max(0f, (float)config.BaseTradePriceOffset);
            requiredOffset += ComputeTradeFeeOffset(relationScore, in config, in profile, factionOutlook);

            // High relation relaxes pricing requirements slightly.
            float relationAboveTradeGate = math.max(0f, relationScore - config.MinTradeRelationScore);
            requiredOffset -= math.saturate(relationAboveTradeGate / 80f) * 0.025f;

            // Entity profile and faction outlook influence market strictness.
            requiredOffset -= (float)profile.TradeLeniency * 0.02f;
            if ((factionOutlook & FactionOutlook.Materialist) != 0)
            {
                requiredOffset -= (float)config.MaterialistLeniency;
            }
            if ((factionOutlook & FactionOutlook.Spiritualist) != 0)
            {
                requiredOffset += (float)config.SpiritualStrictness;
            }

            // Lawful actors adhere harder to declared outlook; chaotic actors vary per tick.
            float lawfulAdherence = math.max((float)profile.LawfulBias, math.saturate(lawAxis));
            if (lawfulAdherence > 0f)
            {
                requiredOffset += lawfulAdherence * (float)config.LawfulOutlookWeight * 0.02f;
            }

            float chaos = math.max((float)profile.ChaoticBias, math.saturate(-lawAxis));
            if (chaos > 0f)
            {
                float variance = (float)config.ChaoticPriceVariance * chaos;
                requiredOffset += SampleSigned(intent.Actor, intent.Target, intent.Tick, 811u) * variance;
            }

            ResolveNeedPressure(intent.Actor, intent.Target, out var desperation, out var scarcity);
            requiredOffset -= desperation * (float)config.TradeDesperationOffsetScale;
            requiredOffset -= scarcity * (float)config.TradeScarcityOffsetScale;
            if (desperation >= 0.85f && scarcity >= 0.5f)
            {
                requiredOffset -= (float)config.TradeExtremeNeedOffsetBonus;
            }

            requiredOffset = math.clamp(requiredOffset, -1f, 0.5f);
            float margin = bestOffset - requiredOffset;
            desirability = math.saturate((margin + 0.4f) / 0.9f);
            return margin + 0.0001f >= 0f;
        }

        private static float ComputeTradeFeeOffset(
            int relationScore,
            in Space4XInteractionIntentPolicyConfig config,
            in Space4XInteractionBehaviorProfile profile,
            FactionOutlook factionOutlook)
        {
            float feeBps = config.TradeBrokerFeeBps + config.TradeSalesTaxBps;
            if (relationScore < 0)
            {
                feeBps += math.saturate(-relationScore / 100f) * config.TradeHostileFeeBps;
            }
            else if (relationScore > 0)
            {
                var relationDiscount = math.saturate(relationScore / 100f) * config.TradeRelationDiscountBpsAt100;
                feeBps = math.max(0f, feeBps - relationDiscount);
            }

            // Profiles/outlooks shift how much expected fee drag affects acceptability.
            feeBps -= math.max(0f, (float)profile.TradeLeniency) * 25f;
            if ((factionOutlook & FactionOutlook.Materialist) != 0)
            {
                feeBps -= 15f;
            }
            if ((factionOutlook & FactionOutlook.Spiritualist) != 0)
            {
                feeBps += 20f;
            }

            feeBps = math.max(0f, feeBps);
            return feeBps / 10000f;
        }

        private bool ShouldEscalateToAttack(
            in Space4XInteractionIntent intent,
            int relationScore,
            in Space4XInteractionIntentPolicyConfig config)
        {
            if (relationScore > config.AttackTriggerRelationScore)
            {
                return false;
            }

            ResolveProfileContext(
                intent.Actor,
                out var factionOutlook,
                out var lawAxis,
                out var profile);

            float trigger = config.AttackTriggerRelationScore;
            float severe = config.AttackSevereRelationScore;
            float t = trigger <= severe ? 1f : math.saturate((trigger - relationScore) / math.max(1f, trigger - severe));
            float chance = math.lerp((float)config.AttackChanceAtTrigger, (float)config.AttackChanceAtSevere, t);
            chance += (float)profile.AggressionBias * 0.12f;

            if ((factionOutlook & FactionOutlook.Militarist) != 0)
            {
                chance += 0.08f;
            }
            if ((factionOutlook & FactionOutlook.Pacifist) != 0)
            {
                chance -= 0.12f;
            }

            float chaos = math.max((float)profile.ChaoticBias, math.saturate(-lawAxis));
            float lawful = math.max((float)profile.LawfulBias, math.saturate(lawAxis));
            if (chaos > 0f)
            {
                chance += SampleSigned(intent.Actor, intent.Target, intent.Tick, 997u) * 0.2f * chaos;
            }

            if (lawful > 0f)
            {
                // Lawful actors follow their outlook with less volatility.
                chance = math.lerp(chance, math.saturate(chance), 0.35f * lawful);
            }

            chance = math.saturate(chance);
            return Sample01(intent.Actor, intent.Target, intent.Tick, 4099u) < chance;
        }

        private void ResolveProfileContext(
            Entity actor,
            out FactionOutlook factionOutlook,
            out float lawAxis,
            out Space4XInteractionBehaviorProfile profile)
        {
            factionOutlook = FactionOutlook.None;
            lawAxis = 0f;
            profile = Space4XInteractionBehaviorProfile.Default;
            bool hasActorProfile = false;

            if (_profileLookup.HasComponent(actor))
            {
                profile = _profileLookup[actor];
                hasActorProfile = true;
            }

            if (_alignmentLookup.HasComponent(actor))
            {
                lawAxis = math.clamp((float)_alignmentLookup[actor].Law, -1f, 1f);
            }

            if (!Space4XInteractionIntentSourceUtility.TryResolveFaction(
                    actor,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return;
            }

            if (factionEntity != Entity.Null && _factionLookup.HasComponent(factionEntity))
            {
                factionOutlook = _factionLookup[factionEntity].Outlook;

                if (!hasActorProfile && _profileLookup.HasComponent(factionEntity))
                {
                    profile = _profileLookup[factionEntity];
                }

                if (lawAxis == 0f && _alignmentLookup.HasComponent(factionEntity))
                {
                    lawAxis = math.clamp((float)_alignmentLookup[factionEntity].Law, -1f, 1f);
                }
            }
        }

        private float ResolveBestTradeOffset(Entity market, out bool hasOffers)
        {
            if (market == Entity.Null || !_offerLookup.HasBuffer(market))
            {
                hasOffers = false;
                return 0f;
            }

            var offers = _offerLookup[market];
            DynamicBuffer<MarketPriceEntry> prices = default;
            bool hasPrices = _priceLookup.HasBuffer(market);
            if (hasPrices)
            {
                prices = _priceLookup[market];
            }

            bool foundAny = false;
            float bestOffset = float.NegativeInfinity;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.IsFulfilled != 0 || offer.Quantity <= 0f)
                {
                    continue;
                }

                var referencePrice = math.max(0.01f, offer.PricePerUnit);
                if (hasPrices && TryGetMarketReferencePrice(prices, offer.ResourceType, out var marketPrice))
                {
                    referencePrice = marketPrice;
                }

                float offset = offer.Type switch
                {
                    TradeOfferType.Sell => (referencePrice - offer.PricePerUnit) / referencePrice,
                    TradeOfferType.Buy => (offer.PricePerUnit - referencePrice) / referencePrice,
                    TradeOfferType.Contract => 0f,
                    _ => 0f
                };

                foundAny = true;
                bestOffset = math.max(bestOffset, offset);
            }

            hasOffers = foundAny;
            return foundAny ? bestOffset : 0f;
        }

        private static bool TryGetMarketReferencePrice(
            DynamicBuffer<MarketPriceEntry> prices,
            MarketResourceType resource,
            out float price)
        {
            for (int i = 0; i < prices.Length; i++)
            {
                if (prices[i].ResourceType != resource)
                {
                    continue;
                }

                var buy = math.max(0f, prices[i].BuyPrice);
                var sell = math.max(0f, prices[i].SellPrice);
                var mid = (buy + sell) * 0.5f;
                price = math.max(0.01f, mid > 0f ? mid : buy);
                return true;
            }

            price = 0f;
            return false;
        }

        private void ResolveNeedPressure(
            Entity actor,
            Entity market,
            out float desperation,
            out float scarcity)
        {
            desperation = 0f;
            scarcity = 0f;
            if (actor == Entity.Null || market == Entity.Null || !_offerLookup.HasBuffer(market))
            {
                return;
            }

            DynamicBuffer<MarketPriceEntry> prices = default;
            bool hasPrices = _priceLookup.HasBuffer(market);
            if (hasPrices)
            {
                prices = _priceLookup[market];
            }

            var offers = _offerLookup[market];
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.IsFulfilled != 0 || offer.Type != TradeOfferType.Sell || offer.Quantity <= 0f)
                {
                    continue;
                }

                var need = ResolveResourceNeed(actor, offer.ResourceType);
                var marketScarcity = ResolveMarketScarcity(offer.ResourceType, offer.Quantity, hasPrices ? prices : default);
                desperation = math.max(desperation, need);
                scarcity = math.max(scarcity, marketScarcity);
            }
        }

        private float ResolveResourceNeed(Entity actor, MarketResourceType marketResource)
        {
            float needBySupply = 0f;
            if (_supplyLookup.HasComponent(actor))
            {
                var supply = _supplyLookup[actor];
                switch (marketResource)
                {
                    case MarketResourceType.Food:
                    case MarketResourceType.Water:
                        needBySupply = 1f - math.saturate(supply.ProvisionsRatio);
                        break;
                    case MarketResourceType.Energy:
                        needBySupply = 1f - math.saturate(supply.FuelRatio);
                        break;
                }
            }

            if (!TryMapMarketToResource(marketResource, out var resourceType) || !_storageLookup.HasBuffer(actor))
            {
                return math.saturate(needBySupply);
            }

            var storage = _storageLookup[actor];
            float storedRatio = ResolveStorageRatio(storage, resourceType);
            float baselineNeed = marketResource == MarketResourceType.Food ||
                                 marketResource == MarketResourceType.Water ||
                                 marketResource == MarketResourceType.Energy
                ? 0.35f
                : 0.05f;
            float needByStorage = storedRatio >= 0f ? 1f - storedRatio : baselineNeed;
            return math.saturate(math.max(needBySupply, needByStorage));
        }

        private static float ResolveStorageRatio(DynamicBuffer<ResourceStorage> storage, ResourceType type)
        {
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type != type)
                {
                    continue;
                }

                if (storage[i].Capacity <= 0f)
                {
                    return storage[i].Amount > 0f ? 1f : 0f;
                }

                return math.saturate(storage[i].Amount / storage[i].Capacity);
            }

            return -1f;
        }

        private static float ResolveMarketScarcity(
            MarketResourceType resource,
            float offeredQuantity,
            DynamicBuffer<MarketPriceEntry> prices)
        {
            float scarcity = 0f;
            if (prices.IsCreated)
            {
                for (int i = 0; i < prices.Length; i++)
                {
                    if (prices[i].ResourceType != resource)
                    {
                        continue;
                    }

                    var supply = math.max(0f, prices[i].Supply);
                    var demand = math.max(0f, prices[i].Demand);
                    if (supply <= 0.001f)
                    {
                        scarcity = 1f;
                    }
                    else
                    {
                        scarcity = math.saturate((demand + 1f) / (supply + 1f) - 1f);
                    }
                    break;
                }
            }

            if (offeredQuantity <= 2f)
            {
                scarcity = math.max(scarcity, 0.9f);
            }
            else if (offeredQuantity <= 5f)
            {
                scarcity = math.max(scarcity, 0.6f);
            }

            return scarcity;
        }

        private static bool TryMapMarketToResource(MarketResourceType market, out ResourceType resource)
        {
            switch (market)
            {
                case MarketResourceType.Ore:
                    resource = ResourceType.Ore;
                    return true;
                case MarketResourceType.RefinedMetal:
                    resource = ResourceType.Minerals;
                    return true;
                case MarketResourceType.RareEarth:
                    resource = ResourceType.RareMetals;
                    return true;
                case MarketResourceType.Energy:
                    resource = ResourceType.Fuel;
                    return true;
                case MarketResourceType.Food:
                    resource = ResourceType.Food;
                    return true;
                case MarketResourceType.Water:
                    resource = ResourceType.Water;
                    return true;
                case MarketResourceType.Industrial:
                    resource = ResourceType.Supplies;
                    return true;
                case MarketResourceType.Tech:
                    resource = ResourceType.RelicData;
                    return true;
                case MarketResourceType.Luxury:
                    resource = ResourceType.BoosterGas;
                    return true;
                case MarketResourceType.Military:
                    resource = ResourceType.StrontiumClathrates;
                    return true;
                case MarketResourceType.Consumer:
                    resource = ResourceType.OrganicMatter;
                    return true;
                default:
                    resource = default;
                    return false;
            }
        }

        private static float Sample01(Entity actor, Entity target, uint tick, uint salt)
        {
            uint a = (uint)math.max(actor.Index, 0);
            uint b = (uint)math.max(target.Index, 0);
            uint hash = math.hash(new uint4(a + salt, b + 1u, tick + 17u, (uint)actor.Version + 3u));
            return (hash & 0x00FFFFFFu) / 16777216f;
        }

        private static float SampleSigned(Entity actor, Entity target, uint tick, uint salt)
        {
            return Sample01(actor, target, tick, salt) * 2f - 1f;
        }
    }

    internal static class Space4XInteractionIntentSourceUtility
    {
        public static Space4XInteractionIntentSource ResolveSource(
            Entity actor,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    actor,
                    in carrierLookup,
                    in affiliationLookup,
                    in factionLookup,
                    out var factionEntity,
                    out _))
            {
                return Space4XInteractionIntentSource.System;
            }

            if (factionEntity == Entity.Null || !factionLookup.HasComponent(factionEntity))
            {
                return Space4XInteractionIntentSource.System;
            }

            return factionLookup[factionEntity].Type == FactionType.Player
                ? Space4XInteractionIntentSource.Player
                : Space4XInteractionIntentSource.AgentAI;
        }

        public static ushort ResolveFactionId(
            Entity entity,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in carrierLookup,
                    in affiliationLookup,
                    in factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }

        public static bool TryResolveFaction(
            Entity entity,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup,
            out Entity factionEntity,
            out ushort factionId)
        {
            return Space4XStandingUtility.TryResolveFaction(
                entity,
                in carrierLookup,
                in affiliationLookup,
                in factionLookup,
                out factionEntity,
                out factionId);
        }
    }
}

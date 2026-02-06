using System;
using System.Globalization;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Authority;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Space4X.Registry;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSimServerHttpSystem : ISystem
    {
        private bool _started;
        private uint _lastStatusTick;
        private uint _lastMissionTick;

        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
            state.RequireForUpdate<Space4XFaction>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_started)
            {
                Space4XSimHttpServer.Stop();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_started)
            {
                var config = SystemAPI.GetSingleton<Space4XSimServerConfig>();
                var host = Space4XSimServerSettings.ResolveHost();
                Space4XSimHttpServer.Start(host, config.HttpPort);
                _started = true;
                UnityEngine.Debug.Log($"[Space4XSimServer] HTTP listening on http://{host}:{config.HttpPort}/");
            }

            ApplyDirectives(ref state);
            ApplyMissionDecisions(ref state);
            UpdateStatusIfNeeded(ref state);
            UpdateMissionCacheIfNeeded(ref state);
        }

        private void ApplyDirectives(ref SystemState state)
        {
            var tick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var ticksPerSecond = ResolveTicksPerSecond(timeState);
            var applied = 0;

            while (applied < 64 && Space4XSimHttpServer.TryDequeueDirective(out var json))
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                DirectiveRequest request;
                try
                {
                    request = JsonUtility.FromJson<DirectiveRequest>(json);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XSimServer] Failed to parse directive JSON: {ex.Message}");
                    continue;
                }

                if (request == null)
                {
                    continue;
                }

                ApplyDirectiveToFactions(ref state, request, tick, ticksPerSecond);
                applied++;
            }
        }

        private void ApplyDirectiveToFactions(ref SystemState state, DirectiveRequest request, uint tick, float ticksPerSecond)
        {
            var factionId = request.ResolveFactionId();
            var factionName = request.ResolveFactionName();
            var applyAll = factionId == 0 && string.IsNullOrWhiteSpace(factionName);
            var directiveId = request.ResolveDirectiveId();
            var orderId = request.ResolveOrderId(directiveId);
            var priority = request.ResolvePriority();
            var expiresAtTick = ResolveExpiryTick(request, tick, ticksPerSecond);
            var mode = request.ResolveMode();
            var source = request.ResolveSource();
            var replaceOrders = request.ResolveReplaceOrders();

            var resolvedWeights = ResolveWeights(request, orderId);
            var entityManager = state.EntityManager;
            var factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            var relationLookup = state.GetComponentLookup<AffiliationRelation>(true);

            foreach (var (faction, entity) in SystemAPI.Query<RefRW<Space4XFaction>>().WithEntityAccess())
            {
                if (!applyAll && !MatchesFaction(factionLookup, relationLookup, entity, factionId, factionName))
                {
                    continue;
                }

                ref var factionRw = ref faction.ValueRW;

                var security = ResolveWeight(resolvedWeights.Security, (float)factionRw.MilitaryFocus);
                var economy = ResolveWeight(resolvedWeights.Economy, (float)factionRw.TradeFocus);
                var research = ResolveWeight(resolvedWeights.Research, (float)factionRw.ResearchFocus);
                var expansion = ResolveWeight(resolvedWeights.Expansion, (float)factionRw.ExpansionDrive);
                var diplomacy = ResolveWeight(resolvedWeights.Diplomacy, 0.5f);
                var production = ResolveWeight(resolvedWeights.Production, economy);
                var food = ResolveWeight(resolvedWeights.Food, 0.5f);

                var aggression = ResolveWeight(resolvedWeights.Aggression,
                    math.saturate(security * 0.7f + (1f - diplomacy) * 0.3f));
                var risk = ResolveWeight(resolvedWeights.RiskTolerance,
                    math.saturate(expansion * 0.5f + economy * 0.3f + security * 0.2f));

                EnsureOrderBuffer(entityManager, entity, replaceOrders);
                UpsertOrder(entityManager, entity, new Space4XFactionOrder
                {
                    OrderId = new FixedString64Bytes(orderId ?? string.Empty),
                    Source = source,
                    Mode = mode,
                    Priority = priority,
                    IssuedTick = tick,
                    ExpiresAtTick = expiresAtTick,
                    Security = security,
                    Economy = economy,
                    Research = research,
                    Expansion = expansion,
                    Diplomacy = diplomacy,
                    Production = production,
                    Food = food,
                    Aggression = aggression,
                    RiskTolerance = risk
                });
            }
        }

        private static void EnsureOrderBuffer(EntityManager entityManager, Entity entity, bool replaceOrders)
        {
            DynamicBuffer<Space4XFactionOrder> buffer;
            if (entityManager.HasBuffer<Space4XFactionOrder>(entity))
            {
                buffer = entityManager.GetBuffer<Space4XFactionOrder>(entity);
                if (replaceOrders)
                {
                    buffer.Clear();
                }
                return;
            }

            buffer = entityManager.AddBuffer<Space4XFactionOrder>(entity);
            if (replaceOrders)
            {
                buffer.Clear();
            }
        }

        private static void UpsertOrder(EntityManager entityManager, Entity entity, in Space4XFactionOrder order)
        {
            if (!entityManager.HasBuffer<Space4XFactionOrder>(entity))
            {
                entityManager.AddBuffer<Space4XFactionOrder>(entity);
            }

            var buffer = entityManager.GetBuffer<Space4XFactionOrder>(entity);
            var orderId = order.OrderId;

            if (!orderId.IsEmpty)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].OrderId.Equals(orderId))
                    {
                        buffer[i] = order;
                        return;
                    }
                }
            }

            if (buffer.Length < buffer.Capacity)
            {
                buffer.Add(order);
                return;
            }

            var lowestIndex = -1;
            var lowestPriority = float.MaxValue;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Priority < lowestPriority)
                {
                    lowestPriority = buffer[i].Priority;
                    lowestIndex = i;
                }
            }

            if (lowestIndex >= 0 && order.Priority >= lowestPriority)
            {
                buffer[lowestIndex] = order;
            }
        }

        private static bool MatchesFaction(ComponentLookup<Space4XFaction> factionLookup, ComponentLookup<AffiliationRelation> relationLookup, Entity entity, ushort id, string name)
        {
            if (id == 0 && string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            if (id != 0 && factionLookup.HasComponent(entity))
            {
                var faction = factionLookup[entity];
                if (faction.FactionId == id)
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(name) && relationLookup.HasComponent(entity))
            {
                var relation = relationLookup[entity];
                return string.Equals(relation.AffiliationName.ToString(), name, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static float ResolveWeight(float candidate, float fallback)
        {
            return candidate >= 0f ? math.saturate(candidate) : math.saturate(fallback);
        }

        private float ResolveTicksPerSecond(in TimeState timeState)
        {
            if (SystemAPI.TryGetSingleton(out Space4XSimServerConfig config) && config.TargetTicksPerSecond > 0f)
            {
                return config.TargetTicksPerSecond;
            }

            if (timeState.FixedDeltaTime > math.FLT_MIN_NORMAL)
            {
                return 1f / timeState.FixedDeltaTime;
            }

            return 2f;
        }

        private void UpdateStatusIfNeeded(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XSimServerConfig>(out var config))
            {
                return;
            }

            var timeState = SystemAPI.TryGetSingleton<TimeState>(out var time) ? time : default;
            var stride = (uint)math.max(1f, config.TargetTicksPerSecond);
            if (timeState.Tick < _lastStatusTick + stride)
            {
                return;
            }

            _lastStatusTick = timeState.Tick;
            BuildStatusJson(ref state, timeState);
        }

        private void UpdateMissionCacheIfNeeded(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XSimServerConfig>(out var config))
            {
                return;
            }

            var timeState = SystemAPI.TryGetSingleton<TimeState>(out var time) ? time : default;
            var stride = (uint)math.max(1f, config.TargetTicksPerSecond);
            if (timeState.Tick < _lastMissionTick + stride)
            {
                return;
            }

            _lastMissionTick = timeState.Tick;
            BuildOffersJson(ref state, timeState);
            BuildAssignmentsJson(ref state, timeState);
        }

        private void BuildStatusJson(ref SystemState state, TimeState timeState)
        {
            var builder = new StringBuilder(4096);
            var inv = CultureInfo.InvariantCulture;

            builder.Append("{\"tick\":").Append(timeState.Tick);
            builder.Append(",\"worldSeconds\":").Append(timeState.WorldSeconds.ToString("0.##", inv));
            builder.Append(",\"factions\":[");

            var first = true;
            foreach (var (faction, resources, territory, entity) in SystemAPI.Query<RefRO<Space4XFaction>, RefRO<FactionResources>, RefRO<Space4XTerritoryControl>>().WithEntityAccess())
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;

                var name = state.EntityManager.HasComponent<AffiliationRelation>(entity)
                    ? state.EntityManager.GetComponentData<AffiliationRelation>(entity).AffiliationName.ToString()
                    : string.Empty;

                builder.Append("{\"id\":").Append(faction.ValueRO.FactionId);
                builder.Append(",\"name\":\"").Append(EscapeJson(name)).Append("\"");
                builder.Append(",\"type\":\"").Append(faction.ValueRO.Type.ToString()).Append("\"");
                builder.Append(",\"aggression\":").Append(((float)faction.ValueRO.Aggression).ToString("0.###", inv));
                builder.Append(",\"risk\":").Append(((float)faction.ValueRO.RiskTolerance).ToString("0.###", inv));
                builder.Append(",\"expansion\":").Append(((float)faction.ValueRO.ExpansionDrive).ToString("0.###", inv));
                builder.Append(",\"trade\":").Append(((float)faction.ValueRO.TradeFocus).ToString("0.###", inv));
                builder.Append(",\"research\":").Append(((float)faction.ValueRO.ResearchFocus).ToString("0.###", inv));
                builder.Append(",\"military\":").Append(((float)faction.ValueRO.MilitaryFocus).ToString("0.###", inv));
                builder.Append(",\"credits\":").Append(resources.ValueRO.Credits.ToString("0.##", inv));
                builder.Append(",\"materials\":").Append(resources.ValueRO.Materials.ToString("0.##", inv));
                builder.Append(",\"colonies\":").Append(territory.ValueRO.ColonyCount);

                if (state.EntityManager.HasComponent<Space4XFactionDirective>(entity))
                {
                    var directive = state.EntityManager.GetComponentData<Space4XFactionDirective>(entity);
                    builder.Append(",\"directive\":{");
                    builder.Append("\"id\":\"").Append(EscapeJson(directive.DirectiveId.ToString())).Append("\"");
                    builder.Append(",\"priority\":").Append(directive.Priority.ToString("0.###", inv));
                    builder.Append(",\"expiresAt\":").Append(directive.ExpiresAtTick);
                    builder.Append(",\"lastUpdated\":").Append(directive.LastUpdatedTick);
                    builder.Append("}");
                }
                else
                {
                    builder.Append(",\"directive\":null");
                }

                var orderCount = state.EntityManager.HasBuffer<Space4XFactionOrder>(entity)
                    ? state.EntityManager.GetBuffer<Space4XFactionOrder>(entity).Length
                    : 0;
                builder.Append(",\"orders\":").Append(orderCount);

                builder.Append("}");
            }

            builder.Append("]}");
            var payload = builder.ToString();
            Space4XSimHttpServer.UpdateStatus(payload);
            Space4XSimServerPaths.WriteStatus(payload);
        }

        private void BuildOffersJson(ref SystemState state, TimeState timeState)
        {
            var builder = new StringBuilder(4096);
            var inv = CultureInfo.InvariantCulture;

            builder.Append("{\"tick\":").Append(timeState.Tick).Append(",\"offers\":[");
            var first = true;
            foreach (var (offer, entity) in SystemAPI.Query<RefRO<Space4XMissionOffer>>().WithEntityAccess())
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;

                builder.Append("{\"id\":").Append(offer.ValueRO.OfferId);
                builder.Append(",\"status\":\"").Append(offer.ValueRO.Status.ToString()).Append('"');
                builder.Append(",\"type\":\"").Append(offer.ValueRO.Type.ToString()).Append('"');
                builder.Append(",\"issuerFactionId\":").Append(offer.ValueRO.IssuerFactionId);
                builder.Append(",\"risk\":").Append(offer.ValueRO.Risk.ToString("0.###", inv));
                builder.Append(",\"rewardCredits\":").Append(offer.ValueRO.RewardCredits.ToString("0.##", inv));
                builder.Append(",\"rewardStanding\":").Append(offer.ValueRO.RewardStanding.ToString("0.###", inv));
                builder.Append(",\"rewardLp\":").Append(offer.ValueRO.RewardLp.ToString("0.###", inv));
                builder.Append(",\"units\":").Append(offer.ValueRO.Units.ToString("0.##", inv));
                builder.Append(",\"resourceType\":").Append(offer.ValueRO.ResourceTypeIndex);
                builder.Append(",\"priority\":").Append(offer.ValueRO.Priority);
                builder.Append(",\"createdTick\":").Append(offer.ValueRO.CreatedTick);
                builder.Append(",\"expiryTick\":").Append(offer.ValueRO.ExpiryTick);
                builder.Append(",\"assignedTick\":").Append(offer.ValueRO.AssignedTick);
                builder.Append(",\"completedTick\":").Append(offer.ValueRO.CompletedTick);
                builder.Append(",\"entityIndex\":").Append(entity.Index);
                builder.Append(",\"entityVersion\":").Append(entity.Version);
                builder.Append(",\"assignedEntityIndex\":").Append(offer.ValueRO.AssignedEntity.Index);
                builder.Append(",\"assignedEntityVersion\":").Append(offer.ValueRO.AssignedEntity.Version);
                builder.Append(",\"targetPos\":");
                AppendVector(builder, offer.ValueRO.TargetPosition, inv);
                builder.Append("}");
            }
            builder.Append("]}");

            Space4XSimHttpServer.UpdateOffers(builder.ToString());
        }

        private void BuildAssignmentsJson(ref SystemState state, TimeState timeState)
        {
            var builder = new StringBuilder(4096);
            var inv = CultureInfo.InvariantCulture;

            builder.Append("{\"tick\":").Append(timeState.Tick).Append(",\"assignments\":[");
            var first = true;
            foreach (var (assignment, entity) in SystemAPI.Query<RefRO<Space4XMissionAssignment>>().WithEntityAccess())
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;

                builder.Append("{\"offerId\":").Append(assignment.ValueRO.OfferId);
                builder.Append(",\"status\":\"").Append(assignment.ValueRO.Status.ToString()).Append('"');
                builder.Append(",\"type\":\"").Append(assignment.ValueRO.Type.ToString()).Append('"');
                builder.Append(",\"phase\":\"").Append(assignment.ValueRO.Phase.ToString()).Append('"');
                builder.Append(",\"cargoState\":\"").Append(assignment.ValueRO.CargoState.ToString()).Append('"');
                builder.Append(",\"issuerFactionId\":").Append(assignment.ValueRO.IssuerFactionId);
                builder.Append(",\"dueTick\":").Append(assignment.ValueRO.DueTick);
                builder.Append(",\"startedTick\":").Append(assignment.ValueRO.StartedTick);
                builder.Append(",\"completedTick\":").Append(assignment.ValueRO.CompletedTick);
                builder.Append(",\"agentIndex\":").Append(entity.Index);
                builder.Append(",\"agentVersion\":").Append(entity.Version);
                builder.Append(",\"targetPos\":");
                AppendVector(builder, assignment.ValueRO.TargetPosition, inv);
                builder.Append(",\"sourcePos\":");
                AppendVector(builder, assignment.ValueRO.SourcePosition, inv);
                builder.Append(",\"destinationPos\":");
                AppendVector(builder, assignment.ValueRO.DestinationPosition, inv);
                builder.Append("}");
            }
            builder.Append("]}");

            Space4XSimHttpServer.UpdateAssignments(builder.ToString());
        }

        private static void AppendVector(StringBuilder builder, float3 value, CultureInfo inv)
        {
            builder.Append('[')
                .Append(value.x.ToString("0.###", inv)).Append(',')
                .Append(value.y.ToString("0.###", inv)).Append(',')
                .Append(value.z.ToString("0.###", inv)).Append(']');
        }

        private void ApplyMissionDecisions(ref SystemState state)
        {
            var tick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var processed = 0;

            while (processed < 64 && Space4XSimHttpServer.TryDequeueMissionAccept(out var json))
            {
                if (TryParseMissionDecision(json, out var request))
                {
                    TryAcceptMission(ref state, request, tick);
                }
                processed++;
            }

            processed = 0;
            while (processed < 64 && Space4XSimHttpServer.TryDequeueMissionDecline(out var json))
            {
                if (TryParseMissionDecision(json, out var request))
                {
                    TryDeclineMission(ref state, request, tick);
                }
                processed++;
            }
        }

        private bool TryAcceptMission(ref SystemState state, MissionDecisionRequest request, uint tick)
        {
            if (!TryResolveOffer(ref state, request, out var offerEntity, out var offer))
            {
                return false;
            }

            if (offer.Status != Space4XMissionStatus.Open)
            {
                return false;
            }

            if (!TryResolveAssignee(ref state, request, offer, out var agent))
            {
                return false;
            }

            if (state.EntityManager.HasComponent<Space4XMissionAssignment>(agent))
            {
                return false;
            }

            if (!state.EntityManager.HasComponent<CaptainOrder>(agent) || !state.EntityManager.HasComponent<LocalTransform>(agent))
            {
                return false;
            }

            var isHaul = offer.Type == Space4XMissionType.HaulDelivery || offer.Type == Space4XMissionType.HaulProcure;
            var sourceEntity = offer.TargetEntity;
            var sourcePos = offer.TargetPosition;
            var destPos = offer.TargetPosition;

            if (isHaul)
            {
                ResolveHaulEndpoints(ref state, offer, out sourceEntity, out sourcePos, out destPos);
            }

            var config = SystemAPI.TryGetSingleton<Space4XMissionBoardConfig>(out var boardConfig)
                ? boardConfig
                : Space4XMissionBoardConfig.Default;
            var duration = ResolveDuration(config, offer.Type, offer.Risk);
            var dueTick = tick + duration;

            var order = state.EntityManager.GetComponentData<CaptainOrder>(agent);
            order.Type = MapMissionToOrder(offer.Type);
            order.Status = CaptainOrderStatus.Received;
            order.TargetEntity = isHaul ? sourceEntity : offer.TargetEntity;
            order.TargetPosition = isHaul ? sourcePos : offer.TargetPosition;
            order.Priority = offer.Priority;
            order.IssuedTick = tick;
            order.TimeoutTick = dueTick;
            state.EntityManager.SetComponentData(agent, order);

            state.EntityManager.AddComponentData(agent, new Space4XMissionAssignment
            {
                OfferEntity = offerEntity,
                OfferId = offer.OfferId,
                Type = offer.Type,
                Status = Space4XMissionStatus.Assigned,
                TargetEntity = offer.TargetEntity,
                TargetPosition = isHaul ? sourcePos : offer.TargetPosition,
                SourceEntity = sourceEntity,
                SourcePosition = sourcePos,
                DestinationPosition = destPos,
                Phase = isHaul ? Space4XMissionPhase.ToSource : Space4XMissionPhase.None,
                CargoState = Space4XMissionCargoState.None,
                ResourceTypeIndex = offer.ResourceTypeIndex,
                Units = offer.Units,
                CargoUnits = 0f,
                RewardCredits = offer.RewardCredits,
                RewardStanding = offer.RewardStanding,
                RewardLp = offer.RewardLp,
                IssuerFactionId = offer.IssuerFactionId,
                StartedTick = tick,
                DueTick = dueTick,
                CompletedTick = 0,
                AutoComplete = (byte)(isHaul ? 0 : 1)
            });

            offer.Status = Space4XMissionStatus.Assigned;
            offer.AssignedEntity = agent;
            offer.AssignedTick = tick;
            state.EntityManager.SetComponentData(offerEntity, offer);
            return true;
        }

        private bool TryDeclineMission(ref SystemState state, MissionDecisionRequest request, uint tick)
        {
            if (!TryResolveOffer(ref state, request, out var offerEntity, out var offer))
            {
                return false;
            }

            if (offer.Status != Space4XMissionStatus.Open)
            {
                return false;
            }

            offer.Status = Space4XMissionStatus.Expired;
            offer.CompletedTick = tick;
            offer.ExpiryTick = tick;
            state.EntityManager.SetComponentData(offerEntity, offer);
            return true;
        }

        private bool TryResolveOffer(ref SystemState state, MissionDecisionRequest request, out Entity offerEntity, out Space4XMissionOffer offer)
        {
            offerEntity = Entity.Null;
            offer = default;

            var resolved = request.ResolveOfferEntity();
            if (resolved.Index >= 0)
            {
                if (state.EntityManager.Exists(resolved) && state.EntityManager.HasComponent<Space4XMissionOffer>(resolved))
                {
                    offerEntity = resolved;
                    offer = state.EntityManager.GetComponentData<Space4XMissionOffer>(resolved);
                    return true;
                }
            }

            var targetId = request.ResolveOfferId();
            if (targetId == 0)
            {
                return false;
            }

            foreach (var (offerRef, entity) in SystemAPI.Query<RefRO<Space4XMissionOffer>>().WithEntityAccess())
            {
                if (offerRef.ValueRO.OfferId != targetId)
                {
                    continue;
                }

                offerEntity = entity;
                offer = offerRef.ValueRO;
                return true;
            }

            return false;
        }

        private bool TryResolveAssignee(ref SystemState state, MissionDecisionRequest request, in Space4XMissionOffer offer, out Entity agent)
        {
            agent = Entity.Null;

            var resolvedAgent = request.ResolveAssigneeEntity();
            if (resolvedAgent.Index >= 0 && state.EntityManager.Exists(resolvedAgent))
            {
                agent = resolvedAgent;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(request.assigneeCarrierId))
            {
                foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
                {
                    if (carrier.ValueRO.CarrierId.Equals(request.assigneeCarrierId))
                    {
                        agent = entity;
                        return true;
                    }
                }
            }

            var factionId = request.ResolveAssigneeFactionId();
            foreach (var (order, transform, entity) in SystemAPI.Query<RefRO<CaptainOrder>, RefRO<LocalTransform>>().WithNone<Space4XMissionAssignment>().WithEntityAccess())
            {
                if (!IsAgentEligible(ref state, entity, offer.Type))
                {
                    continue;
                }

                if (factionId != 0 && !MatchesFactionId(ref state, entity, factionId))
                {
                    continue;
                }

                agent = entity;
                return true;
            }

            return false;
        }

        private bool MatchesFactionId(ref SystemState state, Entity entity, ushort factionId)
        {
            if (!state.EntityManager.HasBuffer<AffiliationTag>(entity))
            {
                return false;
            }

            var buffer = state.EntityManager.GetBuffer<AffiliationTag>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                var tag = buffer[i];
                if (tag.Type != AffiliationType.Faction || tag.Target == Entity.Null)
                {
                    continue;
                }

                if (state.EntityManager.HasComponent<Space4XFaction>(tag.Target) &&
                    state.EntityManager.GetComponentData<Space4XFaction>(tag.Target).FactionId == factionId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAgentEligible(ref SystemState state, Entity entity, Space4XMissionType type)
        {
            var dispositionLookup = state.GetComponentLookup<EntityDisposition>(true);
            var miningLookup = state.GetComponentLookup<MiningVessel>(true);

            if (type == Space4XMissionType.Mine)
            {
                return miningLookup.HasComponent(entity)
                       || (dispositionLookup.HasComponent(entity) && (dispositionLookup[entity].Flags & EntityDispositionFlags.Mining) != 0);
            }

            if (type == Space4XMissionType.HaulDelivery || type == Space4XMissionType.HaulProcure)
            {
                return !dispositionLookup.HasComponent(entity) || (dispositionLookup[entity].Flags & EntityDispositionFlags.Hauler) != 0;
            }

            if (type == Space4XMissionType.Patrol || type == Space4XMissionType.Intercept)
            {
                return !dispositionLookup.HasComponent(entity) || EntityDispositionUtility.IsCombatant(dispositionLookup[entity].Flags);
            }

            return true;
        }

        private static CaptainOrderType MapMissionToOrder(Space4XMissionType type)
        {
            return type switch
            {
                Space4XMissionType.Scout => CaptainOrderType.Survey,
                Space4XMissionType.Mine => CaptainOrderType.Mine,
                Space4XMissionType.HaulDelivery => CaptainOrderType.Haul,
                Space4XMissionType.HaulProcure => CaptainOrderType.Haul,
                Space4XMissionType.Patrol => CaptainOrderType.Patrol,
                Space4XMissionType.Intercept => CaptainOrderType.Intercept,
                Space4XMissionType.BuildStation => CaptainOrderType.BuildStation,
                _ => CaptainOrderType.None
            };
        }

        private static uint ResolveDuration(in Space4XMissionBoardConfig config, Space4XMissionType type, float risk)
        {
            var baseTicks = config.AssignmentBaseTicks;
            var variance = config.AssignmentVarianceTicks;
            var typeScale = type switch
            {
                Space4XMissionType.Mine => 1.4f,
                Space4XMissionType.HaulDelivery => 1.2f,
                Space4XMissionType.HaulProcure => 1.1f,
                Space4XMissionType.Patrol => 1.5f,
                Space4XMissionType.Intercept => 1.0f,
                Space4XMissionType.BuildStation => 2.0f,
                _ => 1.0f
            };

            var riskScale = math.saturate(0.8f + risk);
            return (uint)math.max(60f, baseTicks * typeScale * riskScale + variance * 0.5f);
        }

        private static void ResolveHaulEndpoints(ref SystemState state, in Space4XMissionOffer offer, out Entity sourceEntity, out float3 sourcePosition, out float3 destinationPosition)
        {
            sourceEntity = offer.TargetEntity;
            sourcePosition = offer.TargetPosition;
            destinationPosition = offer.TargetPosition;

            var colonyQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XColony>(),
                ComponentType.ReadOnly<LocalTransform>());
            var colonies = colonyQuery.ToEntityArray(Allocator.Temp);
            var colonyTransforms = colonyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var colonyData = colonyQuery.ToComponentDataArray<Space4XColony>(Allocator.Temp);

            if (colonies.Length == 0)
            {
                colonies.Dispose();
                colonyTransforms.Dispose();
                colonyData.Dispose();
                return;
            }

            var colonyFactionMap = new NativeHashMap<Entity, ushort>(math.max(8, colonies.Length), Allocator.Temp);
            for (int i = 0; i < colonies.Length; i++)
            {
                var colonyEntity = colonies[i];
                if (!state.EntityManager.HasBuffer<AffiliationTag>(colonyEntity))
                {
                    continue;
                }

                var affiliations = state.EntityManager.GetBuffer<AffiliationTag>(colonyEntity);
                for (int a = 0; a < affiliations.Length; a++)
                {
                    var tag = affiliations[a];
                    if (tag.Type == AffiliationType.Faction && tag.Target != Entity.Null &&
                        state.EntityManager.HasComponent<Space4XFaction>(tag.Target))
                    {
                        colonyFactionMap[colonyEntity] = state.EntityManager.GetComponentData<Space4XFaction>(tag.Target).FactionId;
                        break;
                    }
                }
            }

            var preferIssuer = offer.Type == Space4XMissionType.HaulDelivery;
            var bestIndex = -1;
            var bestScore = -1f;

            if (preferIssuer && offer.IssuerFactionId != 0)
            {
                for (int i = 0; i < colonies.Length; i++)
                {
                    if (colonies[i] == offer.TargetEntity)
                    {
                        continue;
                    }

                    if (colonyFactionMap.TryGetValue(colonies[i], out var factionId) && factionId != offer.IssuerFactionId)
                    {
                        continue;
                    }

                    var score = colonyData[i].StoredResources;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex < 0)
            {
                for (int i = 0; i < colonies.Length; i++)
                {
                    if (colonies[i] == offer.TargetEntity)
                    {
                        continue;
                    }

                    var score = colonyData[i].StoredResources;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex >= 0)
            {
                sourceEntity = colonies[bestIndex];
                sourcePosition = colonyTransforms[bestIndex].Position;
            }

            colonies.Dispose();
            colonyTransforms.Dispose();
            colonyData.Dispose();
            colonyFactionMap.Dispose();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static DirectiveWeights ResolveWeights(DirectiveRequest request, string directiveId)
        {
            var weights = new DirectiveWeights
            {
                Security = request.ResolveWeight(request.security, request.weights?.security),
                Economy = request.ResolveWeight(request.economy, request.weights?.economy),
                Research = request.ResolveWeight(request.research, request.weights?.research),
                Expansion = request.ResolveWeight(request.expansion, request.weights?.expansion),
                Diplomacy = request.ResolveWeight(request.diplomacy, request.weights?.diplomacy),
                Production = request.ResolveWeight(request.production, request.weights?.production),
                Food = request.ResolveWeight(request.food, request.weights?.food),
                Aggression = request.ResolveWeight(request.aggression, request.weights?.aggression),
                RiskTolerance = request.ResolveWeight(request.riskTolerance, request.weights?.riskTolerance)
            };

            if (weights.HasAny())
            {
                return weights;
            }

            if (string.IsNullOrWhiteSpace(directiveId))
            {
                return weights;
            }

            var id = directiveId.Trim().ToLowerInvariant();
            switch (id)
            {
                case "secure_resources":
                    weights.Security = 0.6f;
                    weights.Economy = 0.8f;
                    weights.Expansion = 0.4f;
                    weights.Research = 0.3f;
                    weights.Diplomacy = 0.4f;
                    break;
                case "invest_research":
                    weights.Research = 0.9f;
                    weights.Economy = 0.4f;
                    weights.Expansion = 0.2f;
                    weights.Security = 0.3f;
                    weights.Diplomacy = 0.6f;
                    break;
                case "fortify":
                    weights.Security = 0.85f;
                    weights.Economy = 0.4f;
                    weights.Expansion = 0.1f;
                    weights.Diplomacy = 0.3f;
                    break;
                case "expand":
                    weights.Expansion = 0.9f;
                    weights.Economy = 0.5f;
                    weights.Security = 0.4f;
                    weights.Diplomacy = 0.3f;
                    break;
            }

            return weights;
        }

        [Serializable]
        private sealed class MissionDecisionRequest
        {
            public uint offerId;
            public uint offer_id;
            public int offerEntityIndex = -1;
            public int offer_entity_index = -1;
            public int offerEntityVersion = -1;
            public int offer_entity_version = -1;
            public int assigneeEntityIndex = -1;
            public int assignee_entity_index = -1;
            public int assigneeEntityVersion = -1;
            public int assignee_entity_version = -1;
            public string assigneeCarrierId;
            public string assignee_carrier_id;
            public ushort assigneeFactionId;
            public ushort assignee_faction_id;

            public uint ResolveOfferId()
            {
                return offerId != 0 ? offerId : offer_id;
            }

            public Entity ResolveOfferEntity()
            {
                var index = offerEntityIndex >= 0 ? offerEntityIndex : offer_entity_index;
                var version = offerEntityVersion >= 0 ? offerEntityVersion : offer_entity_version;
                return index >= 0 && version >= 0 ? new Entity { Index = index, Version = version } : new Entity { Index = -1, Version = 0 };
            }

            public Entity ResolveAssigneeEntity()
            {
                var index = assigneeEntityIndex >= 0 ? assigneeEntityIndex : assignee_entity_index;
                var version = assigneeEntityVersion >= 0 ? assigneeEntityVersion : assignee_entity_version;
                return index >= 0 && version >= 0 ? new Entity { Index = index, Version = version } : new Entity { Index = -1, Version = 0 };
            }

            public ushort ResolveAssigneeFactionId()
            {
                return assigneeFactionId != 0 ? assigneeFactionId : assignee_faction_id;
            }
        }

        private static bool TryParseMissionDecision(string json, out MissionDecisionRequest request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                request = JsonUtility.FromJson<MissionDecisionRequest>(json);
                return request != null;
            }
            catch
            {
                return false;
            }
        }

        private static uint ResolveExpiryTick(DirectiveRequest request, uint currentTick, float ticksPerSecond)
        {
            var explicitTick = request.ResolveExpiresAtTick();
            if (explicitTick > 0)
            {
                return explicitTick;
            }

            var durationTicks = request.ResolveDurationTicks();
            if (durationTicks > 0)
            {
                return currentTick + durationTicks;
            }

            var durationSeconds = request.ResolveDurationSeconds();
            if (durationSeconds > 0f)
            {
                var delta = (uint)math.max(1f, math.round(durationSeconds * math.max(1f, ticksPerSecond)));
                return currentTick + delta;
            }

            return 0;
        }

        [Serializable]
        private sealed class DirectiveRequest
        {
            public ushort factionId;
            public ushort faction_id;
            public string factionName;
            public string faction_name;
            public string directiveId;
            public string directive_id;
            public string orderId;
            public string order_id;
            public string mode;
            public string source;
            public bool replaceOrders;
            public bool replace_orders;
            public float priority = -1f;
            public float priority_weight = -1f;
            public uint expiresAtTick;
            public uint expires_at_tick;
            public int durationTicks;
            public int duration_ticks;
            public float durationSeconds;
            public float duration_seconds;
            public float security = -1f;
            public float economy = -1f;
            public float research = -1f;
            public float expansion = -1f;
            public float diplomacy = -1f;
            public float production = -1f;
            public float food = -1f;
            public float aggression = -1f;
            public float riskTolerance = -1f;
            public WeightBlock weights;

            public ushort ResolveFactionId()
            {
                return factionId != 0 ? factionId : faction_id;
            }

            public string ResolveFactionName()
            {
                return !string.IsNullOrWhiteSpace(factionName) ? factionName : faction_name;
            }

            public string ResolveDirectiveId()
            {
                return !string.IsNullOrWhiteSpace(directiveId) ? directiveId : directive_id;
            }

            public string ResolveOrderId(string fallback)
            {
                var resolved = !string.IsNullOrWhiteSpace(orderId) ? orderId : order_id;
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback;
                }

                return "manual";
            }

            public float ResolvePriority()
            {
                var value = priority >= 0f ? priority : priority_weight;
                if (value >= 0f)
                {
                    return math.saturate(value);
                }
                return 0.5f;
            }

            public uint ResolveExpiresAtTick()
            {
                return expiresAtTick != 0 ? expiresAtTick : expires_at_tick;
            }

            public uint ResolveDurationTicks()
            {
                var value = durationTicks != 0 ? durationTicks : duration_ticks;
                return value > 0 ? (uint)value : 0u;
            }

            public float ResolveDurationSeconds()
            {
                return durationSeconds > 0f ? durationSeconds : duration_seconds;
            }

            public Space4XDirectiveMode ResolveMode()
            {
                if (string.IsNullOrWhiteSpace(mode))
                {
                    return Space4XDirectiveMode.Blend;
                }

                var normalized = mode.Trim().ToLowerInvariant();
                return normalized switch
                {
                    "override" => Space4XDirectiveMode.Override,
                    "blend" => Space4XDirectiveMode.Blend,
                    _ => Space4XDirectiveMode.Blend
                };
            }

            public Space4XDirectiveSource ResolveSource()
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    return Space4XDirectiveSource.Player;
                }

                var normalized = source.Trim().ToLowerInvariant();
                return normalized switch
                {
                    "ai" => Space4XDirectiveSource.AI,
                    "scripted" => Space4XDirectiveSource.Scripted,
                    "player" => Space4XDirectiveSource.Player,
                    _ => Space4XDirectiveSource.Player
                };
            }

            public bool ResolveReplaceOrders()
            {
                return replaceOrders || replace_orders;
            }

            public float ResolveWeight(float direct, float? nested)
            {
                if (direct >= 0f)
                {
                    return direct;
                }

                if (nested.HasValue && nested.Value >= 0f)
                {
                    return nested.Value;
                }

                return -1f;
            }
        }

        [Serializable]
        private sealed class WeightBlock
        {
            public float security = -1f;
            public float economy = -1f;
            public float research = -1f;
            public float expansion = -1f;
            public float diplomacy = -1f;
            public float production = -1f;
            public float food = -1f;
            public float aggression = -1f;
            public float riskTolerance = -1f;
        }

        private struct DirectiveWeights
        {
            public float Security;
            public float Economy;
            public float Research;
            public float Expansion;
            public float Diplomacy;
            public float Production;
            public float Food;
            public float Aggression;
            public float RiskTolerance;

            public bool HasAny()
            {
                return Security >= 0f || Economy >= 0f || Research >= 0f || Expansion >= 0f ||
                       Diplomacy >= 0f || Production >= 0f || Food >= 0f || Aggression >= 0f || RiskTolerance >= 0f;
            }
        }
    }
}

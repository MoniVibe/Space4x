using System;
using System.Globalization;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Authority;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Space4X.Registry;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSimServerHttpSystem : ISystem
    {
        private bool _started;
        private uint _lastStatusTick;
        private StringBuilder _builder;

        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
            state.RequireForUpdate<Space4XFaction>();
            _builder = new StringBuilder(4096);
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
                Debug.Log($"[Space4XSimServer] HTTP listening on http://{host}:{config.HttpPort}/");
            }

            ApplyDirectives(ref state);
            UpdateStatusIfNeeded(ref state);
        }

        private void ApplyDirectives(ref SystemState state)
        {
            var tick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var ticksPerSecond = ResolveTicksPerSecond(ref state, timeState);
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
                    Debug.LogWarning($"[Space4XSimServer] Failed to parse directive JSON: {ex.Message}");
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
            var priority = request.ResolvePriority();
            var expiresAtTick = ResolveExpiryTick(request, tick, ticksPerSecond);

            var resolvedWeights = ResolveWeights(request, directiveId);
            var entityManager = state.EntityManager;
            var factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            var relationLookup = state.GetComponentLookup<AffiliationRelation>(true);

            foreach (var (faction, entity) in SystemAPI.Query<RefRW<Space4XFaction>>().WithEntityAccess())
            {
                if (!applyAll && !MatchesFaction(factionLookup, relationLookup, entity, factionId, factionName))
                {
                    continue;
                }

                if (entityManager.HasComponent<Space4XFactionDirective>(entity))
                {
                    var existing = entityManager.GetComponentData<Space4XFactionDirective>(entity);
                    var expired = existing.ExpiresAtTick != 0 && tick >= existing.ExpiresAtTick;
                    if (!expired && existing.Priority > priority)
                    {
                        continue;
                    }
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

                factionRw.MilitaryFocus = (half)security;
                factionRw.TradeFocus = (half)economy;
                factionRw.ResearchFocus = (half)research;
                factionRw.ExpansionDrive = (half)expansion;
                factionRw.Aggression = (half)aggression;
                factionRw.RiskTolerance = (half)risk;

                if (!entityManager.HasComponent<Space4XFactionDirective>(entity))
                {
                    entityManager.AddComponent<Space4XFactionDirective>(entity);
                }

                entityManager.SetComponentData(entity, new Space4XFactionDirective
                {
                    Security = security,
                    Economy = economy,
                    Research = research,
                    Expansion = expansion,
                    Diplomacy = diplomacy,
                    Production = production,
                    Food = food,
                    Priority = priority,
                    LastUpdatedTick = tick,
                    ExpiresAtTick = expiresAtTick,
                    DirectiveId = new FixedString64Bytes(directiveId ?? string.Empty)
                });

                var leader = ResolveLeader(entityManager, entity);
                ApplyDirectiveProfile(entityManager, leader != Entity.Null ? leader : entity,
                    security, economy, research, expansion, diplomacy, aggression, risk, food);
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

        private static float ResolveTicksPerSecond(ref SystemState state, in TimeState timeState)
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

        private static void ApplyDirectiveProfile(
            EntityManager entityManager,
            Entity entity,
            float security,
            float economy,
            float research,
            float expansion,
            float diplomacy,
            float aggression,
            float risk,
            float food)
        {
            var target = Space4XSimServerProfileUtility.BuildLeaderDisposition(
                security,
                economy,
                research,
                expansion,
                diplomacy,
                aggression,
                risk,
                food);

            if (entityManager.HasComponent<BehaviorDisposition>(entity))
            {
                var current = entityManager.GetComponentData<BehaviorDisposition>(entity);
                var blended = Space4XSimServerProfileUtility.LerpDisposition(current, target, 0.35f);
                entityManager.SetComponentData(entity, blended);
                return;
            }

            entityManager.AddComponentData(entity, target);
        }

        private static Entity ResolveLeader(EntityManager entityManager, Entity factionEntity)
        {
            if (!entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return Entity.Null;
            }

            var body = entityManager.GetComponentData<AuthorityBody>(factionEntity);
            if (body.ExecutiveSeat == Entity.Null || !entityManager.HasComponent<AuthoritySeatOccupant>(body.ExecutiveSeat))
            {
                return Entity.Null;
            }

            var occupant = entityManager.GetComponentData<AuthoritySeatOccupant>(body.ExecutiveSeat).OccupantEntity;
            return entityManager.Exists(occupant) ? occupant : Entity.Null;
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

        private void BuildStatusJson(ref SystemState state, TimeState timeState)
        {
            _builder.Clear();
            var inv = CultureInfo.InvariantCulture;

            _builder.Append("{\"tick\":").Append(timeState.Tick);
            _builder.Append(",\"worldSeconds\":").Append(timeState.WorldSeconds.ToString("0.##", inv));
            _builder.Append(",\"factions\":[");

            var first = true;
            foreach (var (faction, resources, territory, entity) in SystemAPI.Query<RefRO<Space4XFaction>, RefRO<FactionResources>, RefRO<Space4XTerritoryControl>>().WithEntityAccess())
            {
                if (!first)
                {
                    _builder.Append(',');
                }
                first = false;

                var name = state.EntityManager.HasComponent<AffiliationRelation>(entity)
                    ? state.EntityManager.GetComponentData<AffiliationRelation>(entity).AffiliationName.ToString()
                    : string.Empty;

                _builder.Append("{\"id\":").Append(faction.ValueRO.FactionId);
                _builder.Append(",\"name\":\"").Append(EscapeJson(name)).Append("\"");
                _builder.Append(",\"type\":\"").Append(faction.ValueRO.Type.ToString()).Append("\"");
                _builder.Append(",\"aggression\":").Append(((float)faction.ValueRO.Aggression).ToString("0.###", inv));
                _builder.Append(",\"risk\":").Append(((float)faction.ValueRO.RiskTolerance).ToString("0.###", inv));
                _builder.Append(",\"expansion\":").Append(((float)faction.ValueRO.ExpansionDrive).ToString("0.###", inv));
                _builder.Append(",\"trade\":").Append(((float)faction.ValueRO.TradeFocus).ToString("0.###", inv));
                _builder.Append(",\"research\":").Append(((float)faction.ValueRO.ResearchFocus).ToString("0.###", inv));
                _builder.Append(",\"military\":").Append(((float)faction.ValueRO.MilitaryFocus).ToString("0.###", inv));
                _builder.Append(",\"credits\":").Append(resources.ValueRO.Credits.ToString("0.##", inv));
                _builder.Append(",\"materials\":").Append(resources.ValueRO.Materials.ToString("0.##", inv));
                _builder.Append(",\"colonies\":").Append(territory.ValueRO.ColonyCount);

                if (state.EntityManager.HasComponent<Space4XFactionDirective>(entity))
                {
                    var directive = state.EntityManager.GetComponentData<Space4XFactionDirective>(entity);
                    _builder.Append(",\"directive\":{");
                    _builder.Append("\"id\":\"").Append(EscapeJson(directive.DirectiveId.ToString())).Append("\"");
                    _builder.Append(",\"priority\":").Append(directive.Priority.ToString("0.###", inv));
                    _builder.Append(",\"expiresAt\":").Append(directive.ExpiresAtTick);
                    _builder.Append(",\"lastUpdated\":").Append(directive.LastUpdatedTick);
                    _builder.Append("}");
                }
                else
                {
                    _builder.Append(",\"directive\":null");
                }

                _builder.Append("}");
            }

            _builder.Append("]}");
            var payload = _builder.ToString();
            Space4XSimHttpServer.UpdateStatus(payload);
            Space4XSimServerPaths.WriteStatus(payload);
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

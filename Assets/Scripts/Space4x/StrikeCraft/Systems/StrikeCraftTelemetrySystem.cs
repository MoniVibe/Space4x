using System;
using SystemEnv = global::System.Environment;
using System.Text;
using Space4X.Registry;
using Space4X.Telemetry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Emits per-strike craft telemetry for role/profile assignments and attack run phase transitions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct StrikeCraftTelemetrySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<StrikeCraftWingDirective> _wingDirectiveLookup;
        private ComponentLookup<StrikeCraftPilotLink> _pilotLinkLookup;
        private ComponentLookup<StrikeCraftMaintenanceQuality> _maintenanceQualityLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private EntityQuery _missingTelemetryQuery;

        private static readonly FixedString64Bytes SourceId = new FixedString64Bytes("Space4X.StrikeCraft");
        private static readonly FixedString64Bytes EventProfileAssigned = new FixedString64Bytes("BehaviorProfileAssigned");
        private static readonly FixedString64Bytes EventPhaseChanged = new FixedString64Bytes("RoleStateChanged");
        private static readonly FixedString64Bytes EventAttackRunStart = new FixedString64Bytes("AttackRunStart");
        private static readonly FixedString64Bytes EventAttackRunEnd = new FixedString64Bytes("AttackRunEnd");
        private static readonly FixedString64Bytes EventPatrolStart = new FixedString64Bytes("CombatAirPatrolStart");
        private static readonly FixedString64Bytes EventPatrolEnd = new FixedString64Bytes("CombatAirPatrolEnd");
        private static readonly FixedString64Bytes EventWingDirective = new FixedString64Bytes("WingDirectiveChanged");

        private static bool TelemetryEnabled()
        {
            if (Application.isBatchMode)
                return true;

            var v = SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_ENABLE");
            if (string.IsNullOrWhiteSpace(v))
                return false;

            v = v.Trim();
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                           || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
                           || v.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        public void OnCreate(ref SystemState state)
        {
            if (!TelemetryEnabled())
            {
                state.Enabled = false;
                return;
            }
	            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryStreamSingleton>();
            state.RequireForUpdate<ScenarioTick>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _wingDirectiveLookup = state.GetComponentLookup<StrikeCraftWingDirective>(true);
            _pilotLinkLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _maintenanceQualityLookup = state.GetComponentLookup<StrikeCraftMaintenanceQuality>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _missingTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftProfile>()
                .WithNone<StrikeCraftTelemetryState>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_missingTelemetryQuery.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                state.EntityManager.AddComponent<StrikeCraftTelemetryState>(_missingTelemetryQuery);
            }

            if (!TryGetTelemetryEventBuffer(ref state, out var eventBuffer))
            {
                return;
            }

            _transformLookup.Update(ref state);
            _wingDirectiveLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            _maintenanceQualityLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            var tick = SystemAPI.GetSingleton<ScenarioTick>().Value;
            var metricsReady = TryGetTelemetryMetricBuffer(ref state, out var metricBuffer);
            var capActive = 0;
            var capNonCombat = 0;
            var totalCraft = 0;
            var attackRunActive = 0;
            var wingMembers = 0;
            var wingDistanceSum = 0f;
            var wingCohesionSum = 0f;
            var wingLeaders = 0;
            var wingBreak = 0;
            var maintenanceQualitySum = 0f;

            foreach (var (profile, config, telemetry, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<StrikeCraftTelemetryState>>()
                         .WithEntityAccess())
            {
                totalCraft++;
                if (profile.ValueRO.Phase == AttackRunPhase.CombatAirPatrol)
                {
                    capActive++;
                    if (profile.ValueRO.Role == StrikeCraftRole.Recon || profile.ValueRO.Role == StrikeCraftRole.EWar)
                    {
                        capNonCombat++;
                    }
                }
                else if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                {
                    attackRunActive++;
                }

                if (profile.ValueRO.WingLeader != Entity.Null &&
                    _transformLookup.HasComponent(profile.ValueRO.WingLeader) &&
                    _transformLookup.HasComponent(entity))
                {
                    var leaderPos = _transformLookup[profile.ValueRO.WingLeader].Position;
                    var craftPos = _transformLookup[entity].Position;
                    var distance = math.distance(leaderPos, craftPos);
                    var spacing = math.max(1f, config.ValueRO.FormationSpacing);
                    var cohesion = math.saturate(1f - (distance / (spacing * 2f)));
                    wingMembers++;
                    wingDistanceSum += distance;
                    wingCohesionSum += cohesion;
                }

                if (_maintenanceQualityLookup.HasComponent(entity))
                {
                    maintenanceQualitySum += math.saturate(_maintenanceQualityLookup[entity].Value);
                }

                var isLeader = profile.ValueRO.WingLeader == Entity.Null && profile.ValueRO.WingPosition == 0;
                if (isLeader && _wingDirectiveLookup.HasComponent(entity))
                {
                    wingLeaders++;
                    var directive = _wingDirectiveLookup[entity];
                    if (directive.Mode == 1)
                    {
                        wingBreak++;
                    }

                    if (telemetry.ValueRO.LastWingDirectiveMode != directive.Mode ||
                        telemetry.ValueRO.LastWingDirectiveTick != directive.LastDecisionTick)
                    {
                    var profileEntity = ResolveProfileEntity(entity);
                    var discipline = ComputeDiscipline(profileEntity);
                    var payload = BuildWingDirectivePayload(entity, profile.ValueRO, directive, discipline, profileEntity);
                    eventBuffer.AddEvent(EventWingDirective, tick, SourceId, payload);
                    telemetry.ValueRW.LastWingDirectiveMode = directive.Mode;
                    telemetry.ValueRW.LastWingDirectiveTick = directive.LastDecisionTick;
                }
            }

                var profileHash = ComputeProfileHash(profile.ValueRO, config.ValueRO);
                if (telemetry.ValueRO.BehaviorLogged == 0 ||
                    telemetry.ValueRO.ProfileHash != profileHash ||
                    telemetry.ValueRO.LastDeliveryType != config.ValueRO.DeliveryType)
                {
                    var payload = BuildBehaviorProfilePayload(entity, profile.ValueRO, config.ValueRO, profileHash);
                    eventBuffer.AddEvent(EventProfileAssigned, tick, SourceId, payload);

                    telemetry.ValueRW.ProfileHash = profileHash;
                    telemetry.ValueRW.LastDeliveryType = config.ValueRO.DeliveryType;
                    telemetry.ValueRW.BehaviorLogged = 1;
                }

                if (profile.ValueRO.Phase != telemetry.ValueRO.LastPhase)
                {
                    var phaseDuration = telemetry.ValueRO.LastPhaseTick == 0
                        ? 0u
                        : tick - telemetry.ValueRO.LastPhaseTick;
                    var payload = BuildPhaseChangePayload(entity, profile.ValueRO, config.ValueRO,
                        telemetry.ValueRO.LastPhase, phaseDuration);
                    eventBuffer.AddEvent(EventPhaseChanged, tick, SourceId, payload);

                    if (profile.ValueRO.Phase == AttackRunPhase.CombatAirPatrol)
                    {
                        var patrolPayload = BuildPatrolPayload(entity, profile.ValueRO, "Start",
                            ResolvePatrolReason(telemetry.ValueRO.LastPhase, profile.ValueRO.Phase, profile.ValueRO.Target));
                        eventBuffer.AddEvent(EventPatrolStart, tick, SourceId, patrolPayload);
                    }
                    else if (telemetry.ValueRO.LastPhase == AttackRunPhase.CombatAirPatrol)
                    {
                        var patrolPayload = BuildPatrolPayload(entity, profile.ValueRO, "End",
                            ResolvePatrolReason(telemetry.ValueRO.LastPhase, profile.ValueRO.Phase, profile.ValueRO.Target));
                        eventBuffer.AddEvent(EventPatrolEnd, tick, SourceId, patrolPayload);
                    }

                    if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                    {
                        var startPayload = BuildAttackRunPayload(entity, profile.ValueRO, config.ValueRO, "Start", string.Empty);
                        eventBuffer.AddEvent(EventAttackRunStart, tick, SourceId, startPayload);
                        telemetry.ValueRW.AttackRunActive = 1;
                    }
                    else if (telemetry.ValueRO.LastPhase == AttackRunPhase.Execute && telemetry.ValueRO.AttackRunActive == 1)
                    {
                        var outcome = ResolveAttackRunOutcome(profile.ValueRO.Phase);
                        var endPayload = BuildAttackRunPayload(entity, profile.ValueRO, config.ValueRO, "End", outcome);
                        eventBuffer.AddEvent(EventAttackRunEnd, tick, SourceId, endPayload);
                        telemetry.ValueRW.AttackRunActive = 0;
                    }

                    telemetry.ValueRW.LastPhase = profile.ValueRO.Phase;
                    telemetry.ValueRW.LastPhaseTick = tick;
                }
            }

            if (metricsReady)
            {
                var total = math.max(1, totalCraft);
                var attackRatio = attackRunActive / (float)total;
                var capRatio = capActive / (float)total;
                var wingAvgDistance = wingMembers > 0 ? wingDistanceSum / wingMembers : 0f;
                var wingCohesion = wingMembers > 0 ? wingCohesionSum / wingMembers : 0f;
                var avgMaintenanceQuality = totalCraft > 0 ? maintenanceQualitySum / totalCraft : 0f;
                metricBuffer.AddMetric("space4x.strikecraft.total", totalCraft, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.cap.active", capActive, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.cap.noncombat", capNonCombat, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.attack.active", attackRunActive, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.attack.ratio", attackRatio, TelemetryMetricUnit.Ratio);
                metricBuffer.AddMetric("space4x.strikecraft.cap.ratio", capRatio, TelemetryMetricUnit.Ratio);
                metricBuffer.AddMetric("space4x.strikecraft.wing.members", wingMembers, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.wing.avgDistance", wingAvgDistance, TelemetryMetricUnit.Custom);
                metricBuffer.AddMetric("space4x.strikecraft.wing.cohesion", wingCohesion, TelemetryMetricUnit.Ratio);
                metricBuffer.AddMetric("space4x.strikecraft.wing.leaders", wingLeaders, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.wing.break", wingBreak, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.wing.form", math.max(0, wingLeaders - wingBreak), TelemetryMetricUnit.Count);
                metricBuffer.AddMetric("space4x.strikecraft.maintenance.quality", avgMaintenanceQuality, TelemetryMetricUnit.Ratio);
            }
        }

        private bool TryGetTelemetryEventBuffer(ref SystemState state, out DynamicBuffer<TelemetryEvent> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryEvent>(telemetryRef.Stream);
            return true;
        }

        private bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryRef.Stream);
            return true;
        }

        private static uint ComputeProfileHash(in StrikeCraftProfile profile, in AttackRunConfig config)
        {
            uint hash = 2166136261u;
            hash = HashStep(hash, (uint)profile.Role);
            hash = HashStep(hash, (uint)config.DeliveryType);
            hash = HashStep(hash, (uint)config.ApproachVector);
            hash = HashStep(hash, math.asuint(config.AttackRange));
            hash = HashStep(hash, math.asuint(config.DisengageRange));
            hash = HashStep(hash, config.MaxPasses);
            hash = HashStep(hash, config.ReattackEnabled);
            hash = HashStep(hash, math.asuint(config.FormationSpacing));
            hash = HashStep(hash, math.asuint((float)config.ApproachSpeedMod));
            hash = HashStep(hash, math.asuint((float)config.AttackSpeedMod));
            return hash;
        }

        private static uint HashStep(uint current, uint value)
        {
            const uint prime = 16777619u;
            return unchecked((current ^ value) * prime);
        }

        private static FixedString128Bytes BuildBehaviorProfilePayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, uint profileHash)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            writer.AddUInt("profileHash", profileHash);
            writer.AddFloat("attackRange", config.AttackRange);
            writer.AddFloat("disengageRange", config.DisengageRange);
            writer.AddFloat("formationSpacing", config.FormationSpacing);
            writer.AddFloat("approachSpeed", (float)config.ApproachSpeedMod);
            writer.AddFloat("attackSpeed", (float)config.AttackSpeedMod);
            writer.AddInt("maxPasses", config.MaxPasses);
            writer.AddBool("reattackEnabled", config.ReattackEnabled == 1);
            return writer.Build();
        }

        private static FixedString128Bytes BuildPhaseChangePayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, AttackRunPhase previousPhase, uint duration)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("oldPhase", previousPhase.ToString());
            writer.AddString("newPhase", profile.Phase.ToString());
            writer.AddUInt("phaseDurationTicks", duration);
            writer.AddString("reason", ResolvePhaseReason(previousPhase, profile.Phase));
            writer.AddEntity("target", profile.Target);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            return writer.Build();
        }

        private static FixedString128Bytes BuildAttackRunPayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, string state, string reason)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            writer.AddString("phase", profile.Phase.ToString());
            writer.AddString("state", state);
            writer.AddString("reason", reason);
            writer.AddEntity("target", profile.Target);
            writer.AddInt("passCount", profile.PassCount);
            writer.AddBool("weaponsExpended", profile.WeaponsExpended != 0);
            return writer.Build();
        }

        private static FixedString128Bytes BuildPatrolPayload(Entity entity, in StrikeCraftProfile profile, string state, string reason)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("state", state);
            writer.AddString("reason", reason);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddEntity("target", profile.Target);
            return writer.Build();
        }

        private static FixedString128Bytes BuildWingDirectivePayload(Entity entity, in StrikeCraftProfile profile, in StrikeCraftWingDirective directive, float discipline, Entity pilot)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddEntity("pilot", pilot);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("directive", directive.Mode == 1 ? "Break" : "FormUp");
            writer.AddUInt("decisionTick", directive.LastDecisionTick);
            writer.AddFloat("discipline", discipline);
            return writer.Build();
        }

        private Entity ResolveProfileEntity(Entity craftEntity)
        {
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

        private float ComputeDiscipline(Entity entity)
        {
            if (!_outlookLookup.HasBuffer(entity))
            {
                return 0.5f;
            }

            var buffer = _outlookLookup[entity];
            var discipline = 0.5f;
            for (var i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
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

        private static string ResolvePhaseReason(AttackRunPhase previous, AttackRunPhase current)
        {
            if (previous == AttackRunPhase.Docked && current == AttackRunPhase.Launching)
                return "LaunchCommand";
            if (previous == AttackRunPhase.Launching && current == AttackRunPhase.FormUp)
                return "LaunchComplete";
            if (previous == AttackRunPhase.FormUp && current == AttackRunPhase.Approach)
                return "WingReady";
            if (previous == AttackRunPhase.Approach && current == AttackRunPhase.Execute)
                return "AttackRangeReached";
            if (previous == AttackRunPhase.Execute && current == AttackRunPhase.Disengage)
                return "PassComplete";
            if (previous == AttackRunPhase.Disengage && current == AttackRunPhase.Approach)
                return "Reattack";
            if (previous == AttackRunPhase.Disengage && current == AttackRunPhase.Return)
                return "ReturnToCarrier";
            if (previous == AttackRunPhase.Return && current == AttackRunPhase.Landing)
                return "CarrierReached";
            if (previous == AttackRunPhase.Landing && current == AttackRunPhase.Docked)
                return "Landed";
            if (current == AttackRunPhase.CombatAirPatrol)
                return "PatrolAssigned";
            if (previous == AttackRunPhase.CombatAirPatrol && current == AttackRunPhase.Launching)
                return "TargetAcquired";
            if (previous == AttackRunPhase.CombatAirPatrol && current == AttackRunPhase.Docked)
                return "Recall";
            if (current == AttackRunPhase.Disengage)
                return "LostTarget";
            return "Unknown";
        }

        private static string ResolvePatrolReason(AttackRunPhase previous, AttackRunPhase current, Entity target)
        {
            if (current == AttackRunPhase.CombatAirPatrol)
            {
                return previous == AttackRunPhase.Docked ? "NoTarget" : "PatrolAssigned";
            }

            if (previous == AttackRunPhase.CombatAirPatrol)
            {
                if (current == AttackRunPhase.Launching || current == AttackRunPhase.Approach || current == AttackRunPhase.Execute)
                {
                    return target != Entity.Null ? "TargetAcquired" : "Launch";
                }

                if (current == AttackRunPhase.Docked || current == AttackRunPhase.Landing || current == AttackRunPhase.Return)
                {
                    return "Recall";
                }
            }

            return "Transition";
        }

        private static string ResolveAttackRunOutcome(AttackRunPhase nextPhase)
        {
            return nextPhase switch
            {
                AttackRunPhase.Disengage => "PassComplete",
                AttackRunPhase.Return => "Aborted",
                AttackRunPhase.Docked => "Cancelled",
                AttackRunPhase.Landing => "RTB",
                _ => "Transition"
            };
        }
    }
}

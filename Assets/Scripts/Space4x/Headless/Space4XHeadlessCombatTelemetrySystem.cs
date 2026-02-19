using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Profile;
using Space4X.Registry;
using Space4X.StrikeCraft;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Headless
{
    /// <summary>
    /// Emits headless operator metrics for strike craft combat loop coverage.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XHeadlessCombatTelemetrySystem : ISystem
    {
        private const float CohesionStableThreshold = 0.6f;

        private struct LeaderDirectiveSample
        {
            public Entity Leader;
            public byte Mode;
        }

        private struct WingCollectiveAggregate
        {
            public int Count;
            public float ComplianceSum;
            public float FormationSum;
            public float RiskSum;
        }

        private EntityQuery _wingDirectiveQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private ComponentLookup<StrikeCraftPilotLink> _pilotLinkLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private BufferLookup<TopStance> _outlookLookup;
        private byte _done;
        private byte _sawStrikeCraft;
        private byte _sawAttackRun;
        private byte _sawCap;
        private byte _sawWingDirective;
        private int _maxStrikeCraft;
        private int _maxAttackActive;
        private int _maxCapActive;
        private int _directiveTransitionCount;
        private int _directiveBreakTransitions;
        private int _directiveFormTransitions;
        private uint _lastBreakDecisionTick;
        private int _regroupSampleCount;
        private uint _regroupLatencyTickTotal;
        private uint _regroupLatencyTickMax;
        private int _cohesionSampleCount;
        private int _cohesionStableSamples;
        private float _cohesionAverageSum;
        private int _directiveDisciplineSamples;
        private float _directiveDisciplineSum;
        private int _directiveBreakDisciplineSamples;
        private float _directiveBreakDisciplineSum;
        private int _directiveFormDisciplineSamples;
        private float _directiveFormDisciplineSum;
        private int _leaderBreakDispositionSamples;
        private int _leaderFormDispositionSamples;
        private float _leaderBreakComplianceSum;
        private float _leaderFormComplianceSum;
        private float _leaderBreakFormationSum;
        private float _leaderFormFormationSum;
        private float _leaderBreakRiskSum;
        private float _leaderFormRiskSum;
        private int _orderDecisionSamples;
        private int _orderDecisionObeyCount;
        private int _orderDecisionDisobeyCount;
        private float _orderDecisionObeyDisciplineSum;
        private float _orderDecisionDisobeyDisciplineSum;
        private float _orderDecisionObeyComplianceSum;
        private float _orderDecisionObeyFormationSum;
        private float _orderDecisionObeyRiskSum;
        private float _orderDecisionDisobeyComplianceSum;
        private float _orderDecisionDisobeyFormationSum;
        private float _orderDecisionDisobeyRiskSum;
        private int _collectiveDirectiveSamples;
        private int _collectiveBreakSamples;
        private int _collectiveFormSamples;
        private float _collectiveBreakComplianceSum;
        private float _collectiveFormComplianceSum;
        private float _collectiveBreakFormationSum;
        private float _collectiveFormFormationSum;
        private float _collectiveBreakRiskSum;
        private float _collectiveFormRiskSum;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            _wingDirectiveQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftWingDirective>().Build();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _pilotLinkLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _outlookLookup = state.GetBufferLookup<TopStance>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            var strikeCount = 0;
            var attackActive = 0;
            var capActive = 0;
            var wingMembers = 0;
            var wingCohesionTickSum = 0f;
            foreach (var profile in SystemAPI.Query<RefRO<StrikeCraftProfile>>())
            {
                strikeCount++;
                if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                {
                    attackActive++;
                }
                else if (profile.ValueRO.Phase == AttackRunPhase.CombatAirPatrol)
                {
                    capActive++;
                }
            }

            foreach (var (profile, config, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>>().WithEntityAccess())
            {
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
                    wingCohesionTickSum += cohesion;
                }
            }

            if (strikeCount > 0)
            {
                _sawStrikeCraft = 1;
            }

            if (attackActive > 0)
            {
                _sawAttackRun = 1;
            }

            if (capActive > 0)
            {
                _sawCap = 1;
            }

            if (!_wingDirectiveQuery.IsEmptyIgnoreFilter)
            {
                _sawWingDirective = 1;
            }

            var transitionsThisTick = 0;
            var breakTransitionsThisTick = 0;
            var formTransitionsThisTick = 0;
            var leaderCapacity = math.max(_wingDirectiveQuery.CalculateEntityCount(), 8);
            using var leaderDecisions = new NativeList<LeaderDirectiveSample>(Allocator.Temp);
            using var leaderSet = new NativeParallelHashSet<Entity>(leaderCapacity, Allocator.Temp);
            foreach (var (directive, leaderEntity) in SystemAPI.Query<RefRO<StrikeCraftWingDirective>>().WithEntityAccess())
            {
                var decisionTick = directive.ValueRO.LastDecisionTick;
                if (decisionTick == 0 || decisionTick != timeState.Tick)
                {
                    continue;
                }

                transitionsThisTick++;
                var profileEntity = ResolveProfileEntity(leaderEntity);
                var discipline = ComputeDiscipline(profileEntity);
                var disposition = ResolveBehaviorDisposition(profileEntity, leaderEntity);
                _directiveDisciplineSamples++;
                _directiveDisciplineSum += discipline;
                if (directive.ValueRO.Mode == 1)
                {
                    breakTransitionsThisTick++;
                    _directiveBreakDisciplineSamples++;
                    _directiveBreakDisciplineSum += discipline;
                    _leaderBreakDispositionSamples++;
                    _leaderBreakComplianceSum += disposition.Compliance;
                    _leaderBreakFormationSum += disposition.FormationAdherence;
                    _leaderBreakRiskSum += disposition.RiskTolerance;
                }
                else
                {
                    formTransitionsThisTick++;
                    _directiveFormDisciplineSamples++;
                    _directiveFormDisciplineSum += discipline;
                    _leaderFormDispositionSamples++;
                    _leaderFormComplianceSum += disposition.Compliance;
                    _leaderFormFormationSum += disposition.FormationAdherence;
                    _leaderFormRiskSum += disposition.RiskTolerance;
                }

                leaderDecisions.Add(new LeaderDirectiveSample
                {
                    Leader = leaderEntity,
                    Mode = directive.ValueRO.Mode
                });
                leaderSet.Add(leaderEntity);
            }

            if (leaderDecisions.Length > 0)
            {
                using var collectiveByLeader = new NativeParallelHashMap<Entity, WingCollectiveAggregate>(
                    math.max(leaderDecisions.Length * 2, 8),
                    Allocator.Temp);

                foreach (var (profile, craftEntity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
                {
                    var leader = Entity.Null;
                    if (leaderSet.Contains(craftEntity))
                    {
                        leader = craftEntity;
                    }
                    else if (profile.ValueRO.WingLeader != Entity.Null && leaderSet.Contains(profile.ValueRO.WingLeader))
                    {
                        leader = profile.ValueRO.WingLeader;
                    }

                    if (leader == Entity.Null)
                    {
                        continue;
                    }

                    var profileEntity = ResolveProfileEntity(craftEntity);
                    var disposition = ResolveBehaviorDisposition(profileEntity, craftEntity);

                    if (!collectiveByLeader.TryGetValue(leader, out var aggregate))
                    {
                        aggregate = default;
                    }

                    aggregate.Count++;
                    aggregate.ComplianceSum += disposition.Compliance;
                    aggregate.FormationSum += disposition.FormationAdherence;
                    aggregate.RiskSum += disposition.RiskTolerance;
                    collectiveByLeader[leader] = aggregate;
                }

                for (var i = 0; i < leaderDecisions.Length; i++)
                {
                    var decision = leaderDecisions[i];
                    if (!collectiveByLeader.TryGetValue(decision.Leader, out var aggregate) || aggregate.Count <= 0)
                    {
                        continue;
                    }

                    var avgCompliance = aggregate.ComplianceSum / aggregate.Count;
                    var avgFormation = aggregate.FormationSum / aggregate.Count;
                    var avgRisk = aggregate.RiskSum / aggregate.Count;
                    _collectiveDirectiveSamples++;

                    if (decision.Mode == 1)
                    {
                        _collectiveBreakSamples++;
                        _collectiveBreakComplianceSum += avgCompliance;
                        _collectiveBreakFormationSum += avgFormation;
                        _collectiveBreakRiskSum += avgRisk;
                    }
                    else
                    {
                        _collectiveFormSamples++;
                        _collectiveFormComplianceSum += avgCompliance;
                        _collectiveFormFormationSum += avgFormation;
                        _collectiveFormRiskSum += avgRisk;
                    }
                }
            }

            if (transitionsThisTick > 0)
            {
                _directiveTransitionCount += transitionsThisTick;
                _directiveBreakTransitions += breakTransitionsThisTick;
                _directiveFormTransitions += formTransitionsThisTick;
            }

            if (breakTransitionsThisTick > 0)
            {
                _lastBreakDecisionTick = timeState.Tick;
            }

            if (formTransitionsThisTick > 0 && _lastBreakDecisionTick > 0 && timeState.Tick >= _lastBreakDecisionTick)
            {
                var latency = timeState.Tick - _lastBreakDecisionTick;
                _regroupSampleCount += formTransitionsThisTick;
                _regroupLatencyTickTotal += latency * (uint)formTransitionsThisTick;
                if (latency > _regroupLatencyTickMax)
                {
                    _regroupLatencyTickMax = latency;
                }
            }

            if (wingMembers > 0)
            {
                var averageCohesion = wingCohesionTickSum / wingMembers;
                _cohesionSampleCount++;
                _cohesionAverageSum += averageCohesion;
                if (averageCohesion >= CohesionStableThreshold)
                {
                    _cohesionStableSamples++;
                }
            }

            foreach (var (decision, craftEntity) in SystemAPI.Query<RefRO<StrikeCraftOrderDecision>>().WithEntityAccess())
            {
                if (decision.ValueRO.LastDirectiveTick == 0 ||
                    decision.ValueRO.LastDirectiveTick != timeState.Tick)
                {
                    continue;
                }

                if (decision.ValueRO.LastDecision != 1 && decision.ValueRO.LastDecision != 2)
                {
                    continue;
                }

                var profileEntity = ResolveProfileEntity(craftEntity);
                var discipline = ComputeDiscipline(profileEntity);
                var disposition = ResolveBehaviorDisposition(profileEntity, craftEntity);
                _orderDecisionSamples++;
                if (decision.ValueRO.LastDecision == 1)
                {
                    _orderDecisionObeyCount++;
                    _orderDecisionObeyDisciplineSum += discipline;
                    _orderDecisionObeyComplianceSum += disposition.Compliance;
                    _orderDecisionObeyFormationSum += disposition.FormationAdherence;
                    _orderDecisionObeyRiskSum += disposition.RiskTolerance;
                }
                else
                {
                    _orderDecisionDisobeyCount++;
                    _orderDecisionDisobeyDisciplineSum += discipline;
                    _orderDecisionDisobeyComplianceSum += disposition.Compliance;
                    _orderDecisionDisobeyFormationSum += disposition.FormationAdherence;
                    _orderDecisionDisobeyRiskSum += disposition.RiskTolerance;
                }
            }

            if (strikeCount > _maxStrikeCraft)
            {
                _maxStrikeCraft = strikeCount;
            }

            if (attackActive > _maxAttackActive)
            {
                _maxAttackActive = attackActive;
            }

            if (capActive > _maxCapActive)
            {
                _maxCapActive = capActive;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            EmitMetrics(ref state);
            _done = 1;
        }

        private void EmitMetrics(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.strikecraft_seen"), _sawStrikeCraft != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.attack_run_seen"), _sawAttackRun != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.cap_seen"), _sawCap != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing_directive_seen"), _sawWingDirective != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.strikecraft_max"), _maxStrikeCraft);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.attack_run_max_active"), _maxAttackActive);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.cap_max_active"), _maxCapActive);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.directive_transitions"), _directiveTransitionCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.directive_break_transitions"), _directiveBreakTransitions);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.directive_form_transitions"), _directiveFormTransitions);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.regroup_samples"), _regroupSampleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.regroup_latency_ticks.max"), _regroupLatencyTickMax);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.regroup_latency_ticks.avg"),
                _regroupSampleCount > 0 ? _regroupLatencyTickTotal / (float)_regroupSampleCount : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.cohesion_samples"), _cohesionSampleCount);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.cohesion_avg"),
                _cohesionSampleCount > 0 ? _cohesionAverageSum / _cohesionSampleCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.cohesion_uptime_ratio"),
                _cohesionSampleCount > 0 ? _cohesionStableSamples / (float)_cohesionSampleCount : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.directive_discipline_samples"), _directiveDisciplineSamples);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.directive_discipline_avg"),
                _directiveDisciplineSamples > 0 ? _directiveDisciplineSum / _directiveDisciplineSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.directive_break_discipline_avg"),
                _directiveBreakDisciplineSamples > 0 ? _directiveBreakDisciplineSum / _directiveBreakDisciplineSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.directive_form_discipline_avg"),
                _directiveFormDisciplineSamples > 0 ? _directiveFormDisciplineSum / _directiveFormDisciplineSamples : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.leader_break_samples"), _leaderBreakDispositionSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.leader_form_samples"), _leaderFormDispositionSamples);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_break_compliance_avg"),
                _leaderBreakDispositionSamples > 0 ? _leaderBreakComplianceSum / _leaderBreakDispositionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_form_compliance_avg"),
                _leaderFormDispositionSamples > 0 ? _leaderFormComplianceSum / _leaderFormDispositionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_break_formation_avg"),
                _leaderBreakDispositionSamples > 0 ? _leaderBreakFormationSum / _leaderBreakDispositionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_form_formation_avg"),
                _leaderFormDispositionSamples > 0 ? _leaderFormFormationSum / _leaderFormDispositionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_break_risk_avg"),
                _leaderBreakDispositionSamples > 0 ? _leaderBreakRiskSum / _leaderBreakDispositionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.leader_form_risk_avg"),
                _leaderFormDispositionSamples > 0 ? _leaderFormRiskSum / _leaderFormDispositionSamples : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.order_decision.samples"), _orderDecisionSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.order_decision.obey"), _orderDecisionObeyCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.order_decision.disobey"), _orderDecisionDisobeyCount);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.obey_ratio"),
                _orderDecisionSamples > 0 ? _orderDecisionObeyCount / (float)_orderDecisionSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.obey_discipline_avg"),
                _orderDecisionObeyCount > 0 ? _orderDecisionObeyDisciplineSum / _orderDecisionObeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.disobey_discipline_avg"),
                _orderDecisionDisobeyCount > 0 ? _orderDecisionDisobeyDisciplineSum / _orderDecisionDisobeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.obey_compliance_avg"),
                _orderDecisionObeyCount > 0 ? _orderDecisionObeyComplianceSum / _orderDecisionObeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.obey_formation_avg"),
                _orderDecisionObeyCount > 0 ? _orderDecisionObeyFormationSum / _orderDecisionObeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.obey_risk_tolerance_avg"),
                _orderDecisionObeyCount > 0 ? _orderDecisionObeyRiskSum / _orderDecisionObeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.disobey_compliance_avg"),
                _orderDecisionDisobeyCount > 0 ? _orderDecisionDisobeyComplianceSum / _orderDecisionDisobeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.disobey_formation_avg"),
                _orderDecisionDisobeyCount > 0 ? _orderDecisionDisobeyFormationSum / _orderDecisionDisobeyCount : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.order_decision.disobey_risk_tolerance_avg"),
                _orderDecisionDisobeyCount > 0 ? _orderDecisionDisobeyRiskSum / _orderDecisionDisobeyCount : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.collective_samples"), _collectiveDirectiveSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.collective_break_samples"), _collectiveBreakSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing.collective_form_samples"), _collectiveFormSamples);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_break_compliance_avg"),
                _collectiveBreakSamples > 0 ? _collectiveBreakComplianceSum / _collectiveBreakSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_form_compliance_avg"),
                _collectiveFormSamples > 0 ? _collectiveFormComplianceSum / _collectiveFormSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_break_formation_avg"),
                _collectiveBreakSamples > 0 ? _collectiveBreakFormationSum / _collectiveBreakSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_form_formation_avg"),
                _collectiveFormSamples > 0 ? _collectiveFormFormationSum / _collectiveFormSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_break_risk_avg"),
                _collectiveBreakSamples > 0 ? _collectiveBreakRiskSum / _collectiveBreakSamples : 0f);
            AddOrUpdateMetric(
                buffer,
                new FixedString64Bytes("space4x.combat.wing.collective_form_risk_avg"),
                _collectiveFormSamples > 0 ? _collectiveFormRiskSum / _collectiveFormSamples : 0f);
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
            for (var i = 0; i < resolved.Length; i++)
            {
                if (resolved[i].Domain != domain)
                {
                    continue;
                }

                controller = resolved[i].Controller;
                return true;
            }

            return false;
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
                switch (entry.StanceId)
                {
                    case StanceId.Loyalist:
                        discipline += 0.2f * weight;
                        break;
                    case StanceId.Fanatic:
                        discipline += 0.25f * weight;
                        break;
                    case StanceId.Opportunist:
                        discipline -= 0.15f * weight;
                        break;
                    case StanceId.Mutinous:
                        discipline -= 0.3f * weight;
                        break;
                }
            }

            return math.saturate(discipline);
        }

        private BehaviorDisposition ResolveBehaviorDisposition(Entity profileEntity, Entity fallbackEntity)
        {
            if (_behaviorDispositionLookup.HasComponent(profileEntity))
            {
                return _behaviorDispositionLookup[profileEntity];
            }

            if (_behaviorDispositionLookup.HasComponent(fallbackEntity))
            {
                return _behaviorDispositionLookup[fallbackEntity];
            }

            return BehaviorDisposition.Default;
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}

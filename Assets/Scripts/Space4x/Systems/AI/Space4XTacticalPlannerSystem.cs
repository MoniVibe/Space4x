using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using PureDOTS.Systems.Groups;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(Space4XEngagementIntentSystem))]
    [UpdateBefore(typeof(SquadTacticComplianceSystem))]
    [UpdateBefore(typeof(Space4XWingFormationPlannerSystem))]
    public partial struct Space4XTacticalPlannerSystem : ISystem
    {
        private ComponentLookup<BehaviorDisposition> _dispositionLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<SquadCohesionState> _cohesionLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupTag>();

            _dispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _cohesionLookup = state.GetComponentLookup<SquadCohesionState>(true);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
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

            var config = EngagementDoctrineConfig.Default;
            if (SystemAPI.TryGetSingleton<EngagementDoctrineConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var formationConfig = WingFormationConfig.Default;
            if (SystemAPI.TryGetSingleton<WingFormationConfig>(out var formationSingleton))
            {
                formationConfig = formationSingleton;
            }

            _dispositionLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _cohesionLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            foreach (var (meta, intent, summary, tactic, planner, entity) in SystemAPI
                         .Query<RefRO<GroupMeta>, RefRO<EngagementIntent>, RefRO<EngagementThreatSummary>, RefRW<SquadTacticOrder>, RefRW<EngagementPlannerState>>()
                         .WithAll<GroupTag>()
                         .WithEntityAccess())
            {
                if (meta.ValueRO.Kind != GroupKind.StrikeWing
                    && meta.ValueRO.Kind != GroupKind.MiningWing
                    && meta.ValueRO.Kind != GroupKind.FleetTaskUnit)
                {
                    continue;
                }

                var leader = meta.ValueRO.Leader != Entity.Null && _entityInfoLookup.Exists(meta.ValueRO.Leader)
                    ? meta.ValueRO.Leader
                    : entity;
                var profileEntity = ResolveProfileEntity(leader);
                var disposition = _dispositionLookup.HasComponent(profileEntity)
                    ? _dispositionLookup[profileEntity]
                    : BehaviorDisposition.Default;

                float discipline = math.saturate((disposition.Compliance + disposition.FormationAdherence) * 0.5f);
                float aggression = disposition.Aggression;
                float risk = disposition.RiskTolerance;

                float cohesion = 0.5f;
                if (_cohesionLookup.HasComponent(entity))
                {
                    cohesion = _cohesionLookup[entity].NormalizedCohesion;
                }

                var defaults = meta.ValueRO.Kind == GroupKind.MiningWing
                    ? formationConfig.MiningDefaults
                    : formationConfig.StrikeDefaults;

                var wantsFlank = defaults.MaxSplitGroups > 1
                                 && aggression >= config.AggressionFlankThreshold
                                 && discipline >= config.DisciplineTightenThreshold
                                 && cohesion >= config.CohesionFlankThreshold;

                var newKind = ResolveTacticKind(
                    intent.ValueRO.Kind,
                    meta.ValueRO.Kind,
                    wantsFlank,
                    risk,
                    discipline,
                    config.DisciplineTightenThreshold,
                    entity,
                    timeState.Tick);
                var ackMode = ResolveAckMode(newKind, defaults);

                var intentUpdated = intent.ValueRO.LastUpdateTick > planner.ValueRO.LastTacticTick;
                var shouldUpdate = intentUpdated
                                   || timeState.Tick - planner.ValueRO.LastTacticTick >= config.TacticUpdateIntervalTicks
                                   || tactic.ValueRO.Kind != newKind;
                if (!shouldUpdate)
                {
                    continue;
                }

                tactic.ValueRW.Kind = newKind;
                tactic.ValueRW.Issuer = entity;
                tactic.ValueRW.Target = IsValidEntity(summary.ValueRO.PrimaryThreat)
                    ? summary.ValueRO.PrimaryThreat
                    : Entity.Null;
                tactic.ValueRW.FocusBudgetCost = 0f;
                tactic.ValueRW.DisciplineRequired = defaults.DisciplineRequired;
                tactic.ValueRW.AckMode = ackMode;
                tactic.ValueRW.IssueTick = timeState.Tick;

                planner.ValueRW.LastTacticTick = timeState.Tick;
            }
        }

        private Entity ResolveProfileEntity(Entity leader)
        {
            if (_strikePilotLookup.HasComponent(leader))
            {
                var pilot = _strikePilotLookup[leader].Pilot;
                if (pilot != Entity.Null && _entityInfoLookup.Exists(pilot))
                {
                    return pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(leader))
            {
                var pilot = _vesselPilotLookup[leader].Pilot;
                if (pilot != Entity.Null && _entityInfoLookup.Exists(pilot))
                {
                    return pilot;
                }
            }

            return leader;
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }

        private static SquadTacticKind ResolveTacticKind(
            EngagementIntentKind intent,
            GroupKind groupKind,
            bool wantsFlank,
            float risk,
            float discipline,
            float disciplineThreshold,
            Entity groupEntity,
            uint tick)
        {
            if (groupKind == GroupKind.MiningWing)
            {
                if (intent == EngagementIntentKind.Retreat)
                {
                    return SquadTacticKind.Retreat;
                }

                return discipline >= disciplineThreshold ? SquadTacticKind.Tighten : SquadTacticKind.Loosen;
            }

            return intent switch
            {
                EngagementIntentKind.Fight => wantsFlank ? PickFlankKind(groupEntity, tick) : SquadTacticKind.Tighten,
                EngagementIntentKind.Harass => wantsFlank && risk >= 0.55f ? PickFlankKind(groupEntity, tick) : SquadTacticKind.Loosen,
                EngagementIntentKind.BreakThrough => SquadTacticKind.Collapse,
                EngagementIntentKind.Retreat => SquadTacticKind.Retreat,
                EngagementIntentKind.Pursue => wantsFlank ? PickFlankKind(groupEntity, tick) : SquadTacticKind.Tighten,
                EngagementIntentKind.Screen => SquadTacticKind.Tighten,
                EngagementIntentKind.Rescue => SquadTacticKind.Collapse,
                EngagementIntentKind.Hold => discipline >= disciplineThreshold ? SquadTacticKind.Tighten : SquadTacticKind.Loosen,
                _ => SquadTacticKind.Loosen
            };
        }

        private static byte ResolveAckMode(SquadTacticKind kind, in WingFormationDefaults defaults)
        {
            return kind switch
            {
                SquadTacticKind.Tighten => defaults.RequireAckForTighten,
                SquadTacticKind.Collapse => defaults.RequireAckForTighten,
                SquadTacticKind.FlankLeft => defaults.RequireAckForFlank,
                SquadTacticKind.FlankRight => defaults.RequireAckForFlank,
                _ => (byte)0
            };
        }

        private static SquadTacticKind PickFlankKind(Entity groupEntity, uint tick)
        {
            var seed = (uint)math.hash(new int2(groupEntity.Index, (int)tick));
            return (seed & 1u) == 0 ? SquadTacticKind.FlankLeft : SquadTacticKind.FlankRight;
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(Space4XGroupThreatSummarySystem))]
    public partial struct Space4XEngagementIntentSystem : ISystem
    {
        private ComponentLookup<BehaviorDisposition> _dispositionLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<GroupStanceState> _groupStanceLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupTag>();

            _dispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _groupStanceLookup = state.GetComponentLookup<GroupStanceState>(false);
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

            _dispositionLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _groupStanceLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            foreach (var (intent, summary, meta, planner, entity) in SystemAPI
                         .Query<RefRW<EngagementIntent>, RefRO<EngagementThreatSummary>, RefRO<GroupMeta>, RefRW<EngagementPlannerState>>()
                         .WithAll<GroupTag>()
                         .WithEntityAccess())
            {
                if (meta.ValueRO.Kind != GroupKind.StrikeWing
                    && meta.ValueRO.Kind != GroupKind.MiningWing
                    && meta.ValueRO.Kind != GroupKind.FleetTaskUnit)
                {
                    continue;
                }

                if (timeState.Tick - intent.ValueRO.LastUpdateTick < config.IntentUpdateIntervalTicks)
                {
                    continue;
                }

                var leader = IsValidEntity(meta.ValueRO.Leader)
                    ? meta.ValueRO.Leader
                    : entity;
                var profileEntity = ResolveProfileEntity(leader);
                if (!IsValidEntity(profileEntity))
                {
                    profileEntity = entity;
                }

                var disposition = _dispositionLookup.HasComponent(profileEntity)
                    ? _dispositionLookup[profileEntity]
                    : BehaviorDisposition.Default;

                float aggression = disposition.Aggression;
                float caution = disposition.Caution;
                float risk = disposition.RiskTolerance;
                float patience = disposition.Patience;
                float discipline = math.saturate((disposition.Compliance + disposition.FormationAdherence) * 0.5f);

                if (_alignmentLookup.HasComponent(profileEntity))
                {
                    var alignment = _alignmentLookup[profileEntity];
                    aggression = math.saturate((aggression + AlignmentMath.Chaos(alignment)) * 0.5f);
                    caution = math.saturate((caution + AlignmentMath.Lawfulness(alignment)) * 0.5f);
                }

                float fightThreshold = config.FightAdvantageThreshold
                                       - (aggression - 0.5f) * config.AggressionFightBias
                                       + (caution - 0.5f) * config.CautionFightBias;
                float retreatThreshold = config.RetreatAdvantageThreshold
                                         + (caution - 0.5f) * config.CautionRetreatBias
                                         - (aggression - 0.5f) * config.AggressionRetreatBias
                                         - (risk - 0.5f) * config.RiskBreakthroughBias;
                float harassThreshold = config.HarassAdvantageThreshold
                                        - (patience - 0.5f) * config.PatienceHarassBias;

                fightThreshold = math.clamp(fightThreshold, 0.1f, 2.5f);
                retreatThreshold = math.clamp(retreatThreshold, 0.1f, 2.5f);
                harassThreshold = math.clamp(harassThreshold, 0.1f, 2.5f);

                var advantage = summary.ValueRO.AdvantageRatio;
                var hasThreat = summary.ValueRO.ThreatCount > 0;
                var canEscape = summary.ValueRO.EscapeProbability >= config.MinEscapeProbability;

                var desired = EngagementIntentKind.Hold;
                if (hasThreat)
                {
                    if (meta.ValueRO.Kind == GroupKind.MiningWing)
                    {
                        desired = canEscape ? EngagementIntentKind.Retreat : EngagementIntentKind.Hold;
                    }
                    else if (advantage <= retreatThreshold && canEscape)
                    {
                        desired = EngagementIntentKind.Retreat;
                    }
                    else if (advantage >= fightThreshold)
                    {
                        desired = EngagementIntentKind.Fight;
                    }
                    else if (advantage >= harassThreshold)
                    {
                        desired = EngagementIntentKind.Harass;
                    }
                    else if (config.AllowBreakthrough != 0
                             && advantage <= config.BreakthroughAdvantageThreshold
                             && aggression >= 0.6f
                             && risk >= 0.6f)
                    {
                        desired = EngagementIntentKind.BreakThrough;
                    }
                    else
                    {
                        desired = EngagementIntentKind.Hold;
                    }
                }

                intent.ValueRW.Kind = desired;
                intent.ValueRW.PrimaryTarget = summary.ValueRO.PrimaryThreat;
                intent.ValueRW.AdvantageRatio = summary.ValueRO.AdvantageRatio;
                intent.ValueRW.ThreatPressure = summary.ValueRO.ThreatPressure;
                intent.ValueRW.LastUpdateTick = timeState.Tick;

                planner.ValueRW.LastIntentTick = timeState.Tick;

                if (_groupStanceLookup.HasComponent(entity))
                {
                    var stance = _groupStanceLookup[entity];
                    stance.Stance = MapIntentToStance(desired);
                    stance.PrimaryTarget = summary.ValueRO.PrimaryThreat;
                    stance.Aggression = math.clamp((aggression * 2f) - 1f, -1f, 1f);
                    stance.Discipline = discipline;
                    _groupStanceLookup[entity] = stance;
                }
            }
        }

        private Entity ResolveProfileEntity(Entity leader)
        {
            if (_strikePilotLookup.HasComponent(leader))
            {
                var pilot = _strikePilotLookup[leader].Pilot;
                if (IsValidEntity(pilot))
                {
                    return pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(leader))
            {
                var pilot = _vesselPilotLookup[leader].Pilot;
                if (IsValidEntity(pilot))
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

        private static GroupStance MapIntentToStance(EngagementIntentKind intent)
        {
            return intent switch
            {
                EngagementIntentKind.Fight => GroupStance.Attack,
                EngagementIntentKind.Harass => GroupStance.Skirmish,
                EngagementIntentKind.BreakThrough => GroupStance.Attack,
                EngagementIntentKind.Retreat => GroupStance.Retreat,
                EngagementIntentKind.Pursue => GroupStance.IndependentHunt,
                EngagementIntentKind.Screen => GroupStance.Screen,
                EngagementIntentKind.Rescue => GroupStance.Screen,
                _ => GroupStance.Hold
            };
        }
    }
}

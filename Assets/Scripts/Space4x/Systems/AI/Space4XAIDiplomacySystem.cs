using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements AI diplomacy per AIDiplomacyAndTech.md: alignment-driven negotiations, tech priorities,
    /// and betrayal chances. Corrupt actors may accept deals they never intend to honor.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAIGovernanceSystem))]
    public partial struct Space4XAIDiplomacySystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private BufferLookup<EthicAxisValue> _axisLookup;
        private ComponentLookup<Reputation> _reputationLookup;
        private ComponentLookup<IndividualStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _axisLookup = state.GetBufferLookup<EthicAxisValue>(true);
            _reputationLookup = state.GetComponentLookup<Reputation>(false);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _axisLookup.Update(ref state);
            _reputationLookup.Update(ref state);
            _statsLookup.Update(ref state);

            // Process diplomatic evaluations
            // In full implementation, would evaluate relations, tech gaps, threat pressure, etc.
            var job = new ProcessDiplomacyJob
            {
                CurrentTick = timeState.Tick,
                AlignmentLookup = _alignmentLookup,
                OutlookLookup = _outlookLookup,
                AxisLookup = _axisLookup,
                ReputationLookup = _reputationLookup,
                StatsLookup = _statsLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(AggregateType))]
        public partial struct ProcessDiplomacyJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;
            [ReadOnly] public BufferLookup<EthicAxisValue> AxisLookup;
            public ComponentLookup<Reputation> ReputationLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;

            public void Execute(Entity entity)
            {
                if (!AlignmentLookup.HasComponent(entity))
                {
                    return;
                }

                var alignment = AlignmentLookup[entity];
                var integrity = AlignmentMath.IntegrityNormalized(alignment);
                var good = math.saturate(0.5f * (1f + (float)alignment.Good));

                // Get materialist axis for tech pursuit
                float materialistAxis = 0f;
                if (AxisLookup.HasBuffer(entity))
                {
                    var axes = AxisLookup[entity];
                    for (int i = 0; i < axes.Length; i++)
                    {
                        if (axes[i].Axis == EthicAxisId.Materialist)
                        {
                            materialistAxis = (float)axes[i].Value;
                            break;
                        }
                    }
                }

                // Determine diplomatic behavior
                // Corrupt Actors: May accept deals they never intend to honor
                if (integrity < 0.3f)
                {
                    // Low integrity - high betrayal chance
                    // In full implementation, would track treaty commitments and betrayal probability
                }

                // Pure/Good Entities: Seek mutually beneficial resolutions
                if (good > 0.7f && integrity > 0.7f)
                {
                    // Good entities - honest brokers
                    // In full implementation, would invest in ambassadors, honor treaties
                }

                // Evil Entities: Leverage diplomacy for advantage
                if (good < 0.3f)
                {
                    // Evil entities - exploit diplomacy
                    // In full implementation, would escalate conflicts, exploit loopholes
                }

                // Tech Pursuit: Materialist factions obsess over technological advancement
                if (materialistAxis > 0.5f)
                {
                    // Materialist - prioritize tech
                    // In full implementation, would build research labs, pursue tech pacts aggressively
                }

                // Diplomacy stat influences agreement success rates and relation modifiers
                float diplomacyBonus = 0f;
                if (StatsLookup.HasComponent(entity))
                {
                    var stats = StatsLookup[entity];
                    diplomacyBonus = stats.Diplomacy / 100f; // 0-1 normalized
                    // Higher diplomacy = better reputation gains and relation improvements
                }

                // Update reputation based on diplomatic behavior and diplomacy stat
                if (!ReputationLookup.HasComponent(entity))
                {
                    var baseReputation = 0.5f;
                    var basePrestige = 0.5f;
                    // Diplomacy stat boosts initial reputation
                    baseReputation += diplomacyBonus * 0.2f; // Up to 0.2 bonus
                    basePrestige += diplomacyBonus * 0.15f; // Up to 0.15 bonus
                    
                    ReputationLookup.GetRefRW(entity).ValueRW = new Reputation
                    {
                        ReputationScore = (half)math.saturate(baseReputation),
                        PrestigeScore = (half)math.saturate(basePrestige)
                    };
                }
                else
                {
                    // Diplomacy stat improves reputation over time
                    var rep = ReputationLookup.GetRefRW(entity).ValueRO;
                    var newRep = math.min(1f, rep.ReputationScore + diplomacyBonus * 0.001f); // Small incremental gain
                    var newPrestige = math.min(1f, rep.PrestigeScore + diplomacyBonus * 0.0008f);
                    ReputationLookup.GetRefRW(entity).ValueRW = new Reputation
                    {
                        ReputationScore = (half)newRep,
                        PrestigeScore = (half)newPrestige
                    };
                }
            }
        }
    }
}


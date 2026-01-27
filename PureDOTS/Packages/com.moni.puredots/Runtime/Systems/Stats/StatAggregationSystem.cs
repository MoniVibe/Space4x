using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Stats;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Stats
{
    /// <summary>
    /// System that updates group stat aggregates from member stats.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [BurstCompile]
    public partial struct StatAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Process groups that need aggregation
            foreach (var (aggregate, members, config, entity) in 
                SystemAPI.Query<RefRW<GroupStatAggregate>, DynamicBuffer<GroupMemberRef>, RefRO<StatAggregationConfig>>()
                    .WithEntityAccess())
            {
                // Check update interval
                if (currentTick - aggregate.ValueRW.LastUpdateTick < config.ValueRO.UpdateInterval)
                    continue;

                // Reset aggregates
                var agg = new GroupStatAggregate
                {
                    MinCommand = (half)100f,
                    MinTactics = (half)100f,
                    MinLogistics = (half)100f,
                    MinDiplomacy = (half)100f,
                    MinEngineering = (half)100f,
                    MinResolve = (half)100f,
                    LastUpdateTick = currentTick
                };

                float totalWeight = 0f;
                float weightedCommand = 0f;
                float weightedTactics = 0f;
                float weightedLogistics = 0f;
                float weightedDiplomacy = 0f;
                float weightedEngineering = 0f;
                float weightedResolve = 0f;
                float weightedPhysique = 0f;
                float weightedFinesse = 0f;
                float weightedWill = 0f;
                float totalXP = 0f;
                ushort validMembers = 0;

                // Aggregate member stats
                for (int i = 0; i < members.Length; i++)
                {
                    var memberRef = members[i];
                    
                    if (!SystemAPI.HasComponent<IndividualStats>(memberRef.MemberEntity))
                        continue;

                    var stats = SystemAPI.GetComponent<IndividualStats>(memberRef.MemberEntity);
                    float weight = config.ValueRO.UseWeightedAverage ? memberRef.Weight : 1f;
                    totalWeight += weight;
                    validMembers++;

                    float cmd = (float)stats.Command;
                    float tac = (float)stats.Tactics;
                    float log = (float)stats.Logistics;
                    float dip = (float)stats.Diplomacy;
                    float eng = (float)stats.Engineering;
                    float res = (float)stats.Resolve;

                    weightedCommand += cmd * weight;
                    weightedTactics += tac * weight;
                    weightedLogistics += log * weight;
                    weightedDiplomacy += dip * weight;
                    weightedEngineering += eng * weight;
                    weightedResolve += res * weight;

                    // Track min/max
                    if (cmd > (float)agg.MaxCommand) agg.MaxCommand = (half)cmd;
                    if (cmd < (float)agg.MinCommand) agg.MinCommand = (half)cmd;
                    if (tac > (float)agg.MaxTactics) agg.MaxTactics = (half)tac;
                    if (tac < (float)agg.MinTactics) agg.MinTactics = (half)tac;
                    if (log > (float)agg.MaxLogistics) agg.MaxLogistics = (half)log;
                    if (log < (float)agg.MinLogistics) agg.MinLogistics = (half)log;
                    if (dip > (float)agg.MaxDiplomacy) agg.MaxDiplomacy = (half)dip;
                    if (dip < (float)agg.MinDiplomacy) agg.MinDiplomacy = (half)dip;
                    if (eng > (float)agg.MaxEngineering) agg.MaxEngineering = (half)eng;
                    if (eng < (float)agg.MinEngineering) agg.MinEngineering = (half)eng;
                    if (res > (float)agg.MaxResolve) agg.MaxResolve = (half)res;
                    if (res < (float)agg.MinResolve) agg.MinResolve = (half)res;

                    // Physical attributes if available
                    if (SystemAPI.HasComponent<PhysiqueFinesseWill>(memberRef.MemberEntity))
                    {
                        var pfw = SystemAPI.GetComponent<PhysiqueFinesseWill>(memberRef.MemberEntity);
                        weightedPhysique += (float)pfw.Physique * weight;
                        weightedFinesse += (float)pfw.Finesse * weight;
                        weightedWill += (float)pfw.Will * weight;
                    }

                    // Experience from history if available
                    if (SystemAPI.HasBuffer<StatHistorySample>(memberRef.MemberEntity))
                    {
                        var history = SystemAPI.GetBuffer<StatHistorySample>(memberRef.MemberEntity);
                        if (history.Length > 0)
                        {
                            totalXP += history[history.Length - 1].GeneralXP;
                        }
                    }
                }

                // Calculate averages
                if (totalWeight > 0)
                {
                    agg.AvgCommand = (half)(weightedCommand / totalWeight);
                    agg.AvgTactics = (half)(weightedTactics / totalWeight);
                    agg.AvgLogistics = (half)(weightedLogistics / totalWeight);
                    agg.AvgDiplomacy = (half)(weightedDiplomacy / totalWeight);
                    agg.AvgEngineering = (half)(weightedEngineering / totalWeight);
                    agg.AvgResolve = (half)(weightedResolve / totalWeight);
                    agg.AvgPhysique = (half)(weightedPhysique / totalWeight);
                    agg.AvgFinesse = (half)(weightedFinesse / totalWeight);
                    agg.AvgWill = (half)(weightedWill / totalWeight);
                }

                agg.MemberCount = validMembers;
                agg.TotalExperience = totalXP;

                // Handle case where no valid members (reset mins to 0)
                if (validMembers == 0)
                {
                    agg.MinCommand = (half)0f;
                    agg.MinTactics = (half)0f;
                    agg.MinLogistics = (half)0f;
                    agg.MinDiplomacy = (half)0f;
                    agg.MinEngineering = (half)0f;
                    agg.MinResolve = (half)0f;
                }

                aggregate.ValueRW = agg;
            }
        }
    }

    /// <summary>
    /// System that records stat history samples at regular intervals.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(StatAggregationSystem))]
    [BurstCompile]
    public partial struct StatHistorySamplingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Sample stats for entities with history tracking
            foreach (var (stats, history, config, entity) in 
                SystemAPI.Query<RefRO<IndividualStats>, DynamicBuffer<StatHistorySample>, RefRO<StatHistoryConfig>>()
                    .WithEntityAccess())
            {
                // Check sample interval
                if (history.Length > 0)
                {
                    var lastSample = history[history.Length - 1];
                    if (currentTick - lastSample.Tick < config.ValueRO.SampleInterval)
                        continue;
                }

                // Remove oldest if at capacity
                if (history.Length >= config.ValueRO.MaxSamples)
                {
                    history.RemoveAt(0);
                }

                // Add new sample
                var sample = new StatHistorySample
                {
                    Tick = currentTick,
                    Command = stats.ValueRO.Command,
                    Tactics = stats.ValueRO.Tactics,
                    Logistics = stats.ValueRO.Logistics,
                    Diplomacy = stats.ValueRO.Diplomacy,
                    Engineering = stats.ValueRO.Engineering,
                    Resolve = stats.ValueRO.Resolve,
                    GeneralXP = 0f // Would be populated from progression system
                };

                history.Add(sample);
            }
        }
    }

    /// <summary>
    /// Static helpers for stat calculations.
    /// </summary>
    [BurstCompile]
    public static class StatHelpers
    {
        /// <summary>
        /// Calculates the effective stat value with available modifiers.
        /// </summary>
        public static float GetEffectiveValue(half stat)
        {
            return (float)stat;
        }

        /// <summary>
        /// Calculates a stat influence modifier (0-1 scale).
        /// </summary>
        public static float GetInfluenceModifier(float statValue, float maxStat = 100f)
        {
            return math.clamp(statValue / maxStat, 0f, 1f);
        }

        /// <summary>
        /// Calculates bonus from stat (percentage).
        /// </summary>
        public static float GetStatBonus(float statValue, float bonusPerPoint = 0.01f)
        {
            return statValue * bonusPerPoint;
        }

        /// <summary>
        /// Interpolates between two stat values.
        /// </summary>
        public static half LerpStat(half a, half b, float t)
        {
            return (half)math.lerp((float)a, (float)b, t);
        }

        /// <summary>
        /// Gets the expertise tier bonus multiplier.
        /// </summary>
        public static float GetExpertiseBonus(byte tier)
        {
            // Each tier gives 10% bonus
            return tier * 0.1f;
        }

        /// <summary>
        /// Checks if an entity has a specific service trait by Id.
        /// </summary>
        public static bool HasServiceTrait(in DynamicBuffer<ServiceTrait> traits, in FixedString32Bytes traitId)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i].Id.Equals(traitId))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the rank of a specific service trait.
        /// Rank isn't currently tracked, so presence is treated as rank 1.
        /// </summary>
        public static byte GetServiceTraitRank(in DynamicBuffer<ServiceTrait> traits, in FixedString32Bytes traitId)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i].Id.Equals(traitId))
                    return 1;
            }
            return 0;
        }

        /// <summary>
        /// Calculates formation radius bonus from command stat.
        /// </summary>
        public static float CalculateFormationRadiusBonus(float command)
        {
            return 1f + GetInfluenceModifier(command) * 0.5f; // Up to 50% bonus
        }

        /// <summary>
        /// Calculates targeting accuracy bonus from tactics stat.
        /// </summary>
        public static float CalculateTargetingAccuracyBonus(float tactics)
        {
            return GetInfluenceModifier(tactics) * 0.25f; // Up to 25% bonus
        }

        /// <summary>
        /// Calculates transfer speed bonus from logistics stat.
        /// </summary>
        public static float CalculateTransferSpeedBonus(float logistics)
        {
            return 1f + GetInfluenceModifier(logistics) * 0.3f; // Up to 30% bonus
        }

        /// <summary>
        /// Calculates repair speed bonus from engineering stat.
        /// </summary>
        public static float CalculateRepairSpeedBonus(float engineering)
        {
            return 1f + GetInfluenceModifier(engineering) * 0.4f; // Up to 40% bonus
        }

        /// <summary>
        /// Calculates engagement time bonus from resolve stat.
        /// </summary>
        public static float CalculateEngagementTimeBonus(float resolve)
        {
            return 1f + GetInfluenceModifier(resolve) * 0.2f; // Up to 20% bonus
        }
    }
}

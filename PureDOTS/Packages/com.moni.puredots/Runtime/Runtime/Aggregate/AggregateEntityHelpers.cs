using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Static helpers for aggregate entity calculations.
    /// </summary>
    [BurstCompile]
    public static class AggregateEntityHelpers
    {
        /// <summary>
        /// Calculates aggregate stats from members.
        /// </summary>
        public static AggregateMemberStats CalculateAggregateStats(
            in DynamicBuffer<AggregateMember> members,
            NativeArray<float> memberHealths,
            NativeArray<float> memberMorales,
            NativeArray<float> memberSkills,
            NativeArray<float> memberStrengths,
            uint currentTick)
        {
            if (members.Length == 0)
            {
                return new AggregateMemberStats { LastCalculatedTick = currentTick };
            }

            float totalWeight = 0;
            float weightedHealth = 0;
            float weightedMorale = 0;
            float weightedSkill = 0;
            float totalStrength = 0;
            int activeCount = 0;

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive == 0) continue;
                
                float weight = members[i].ContributionWeight;
                totalWeight += weight;
                weightedHealth += memberHealths[i] * weight;
                weightedMorale += memberMorales[i] * weight;
                weightedSkill += memberSkills[i] * weight;
                totalStrength += memberStrengths[i];
                activeCount++;
            }

            float avgHealth = totalWeight > 0 ? weightedHealth / totalWeight : 0;
            float avgMorale = totalWeight > 0 ? weightedMorale / totalWeight : 0;
            float avgSkill = totalWeight > 0 ? weightedSkill / totalWeight : 0;

            // Calculate cohesion from morale variance
            float moraleVariance = 0;
            if (activeCount > 1)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    if (members[i].IsActive == 0) continue;
                    float diff = memberMorales[i] - avgMorale;
                    moraleVariance += diff * diff;
                }
                moraleVariance /= activeCount - 1;
            }
            float cohesion = math.saturate(1f - math.sqrt(moraleVariance));

            return new AggregateMemberStats
            {
                AverageHealth = avgHealth,
                AverageMorale = avgMorale,
                AverageSkill = avgSkill,
                TotalStrength = totalStrength,
                Cohesion = cohesion,
                LastCalculatedTick = currentTick
            };
        }

        /// <summary>
        /// Calculates aggregate movement speed (slowest member).
        /// </summary>
        public static float CalculateAggregateSpeed(
            in DynamicBuffer<AggregateMember> members,
            NativeArray<float> memberSpeeds)
        {
            float minSpeed = float.MaxValue;
            
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive == 0) continue;
                minSpeed = math.min(minSpeed, memberSpeeds[i]);
            }
            
            return minSpeed == float.MaxValue ? 0 : minSpeed;
        }

        /// <summary>
        /// Calculates total upkeep for aggregate.
        /// </summary>
        public static float CalculateTotalUpkeep(
            in DynamicBuffer<AggregateMember> members,
            NativeArray<float> memberUpkeeps)
        {
            float total = 0;
            
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive == 0) continue;
                total += memberUpkeeps[i];
            }
            
            return total;
        }

        /// <summary>
        /// Propagates order to all members.
        /// </summary>
        public static int PropagateOrder(
            in AggregateOrder order,
            in DynamicBuffer<AggregateMember> members,
            NativeArray<float> memberLoyalties,
            float minLoyaltyToObey)
        {
            int obeying = 0;
            
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive == 0) continue;
                
                // Check if member will obey
                if (memberLoyalties[i] >= minLoyaltyToObey)
                {
                    obeying++;
                }
            }
            
            return obeying;
        }

        /// <summary>
        /// Calculates consensus for order.
        /// </summary>
        public static float CalculateConsensus(
            in DynamicBuffer<AggregateMember> members,
            NativeArray<float> memberApproval)
        {
            if (members.Length == 0) return 0;
            
            float totalWeight = 0;
            float weightedApproval = 0;
            
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive == 0) continue;
                
                float weight = members[i].ContributionWeight;
                totalWeight += weight;
                weightedApproval += memberApproval[i] * weight;
            }
            
            return totalWeight > 0 ? weightedApproval / totalWeight : 0;
        }

        /// <summary>
        /// Adds member to aggregate.
        /// </summary>
        public static bool TryAddMember(
            ref DynamicBuffer<AggregateMember> members,
            ref AggregateEntity aggregate,
            Entity newMember,
            float contributionWeight,
            uint currentTick)
        {
            if (aggregate.MemberCount >= aggregate.MaxMembers)
                return false;
            
            members.Add(new AggregateMember
            {
                MemberEntity = newMember,
                ContributionWeight = contributionWeight,
                Rank = 0,
                IsActive = 1,
                JoinedTick = currentTick
            });
            
            aggregate.MemberCount++;
            return true;
        }

        /// <summary>
        /// Removes member from aggregate.
        /// </summary>
        public static bool TryRemoveMember(
            ref DynamicBuffer<AggregateMember> members,
            ref AggregateEntity aggregate,
            Entity memberToRemove)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == memberToRemove)
                {
                    members.RemoveAt(i);
                    aggregate.MemberCount--;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates split distribution.
        /// </summary>
        public static void CalculateSplitDistribution(
            in DynamicBuffer<AggregateMember> members,
            in AggregateResources resources,
            float splitRatio,
            out int membersToNew,
            out AggregateResources newResources,
            out AggregateResources remainingResources)
        {
            int activeMembers = 0;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive != 0) activeMembers++;
            }
            
            membersToNew = (int)(activeMembers * splitRatio);
            
            newResources = new AggregateResources
            {
                Treasury = resources.Treasury * splitRatio,
                Supplies = resources.Supplies * splitRatio,
                Influence = resources.Influence * splitRatio * 0.5f, // Influence splits unequally
                Prestige = resources.Prestige * splitRatio * 0.3f    // Prestige stays with original
            };
            
            remainingResources = new AggregateResources
            {
                Treasury = resources.Treasury - newResources.Treasury,
                Supplies = resources.Supplies - newResources.Supplies,
                Influence = resources.Influence - newResources.Influence,
                Prestige = resources.Prestige - newResources.Prestige
            };
        }

        /// <summary>
        /// Calculates merge result.
        /// </summary>
        public static AggregateResources CalculateMergeResources(
            in AggregateResources sourceResources,
            in AggregateResources targetResources)
        {
            return new AggregateResources
            {
                Treasury = sourceResources.Treasury + targetResources.Treasury,
                Supplies = sourceResources.Supplies + targetResources.Supplies,
                Influence = math.max(sourceResources.Influence, targetResources.Influence) * 1.1f,
                Prestige = math.max(sourceResources.Prestige, targetResources.Prestige)
            };
        }

        /// <summary>
        /// Gets member with highest rank.
        /// </summary>
        public static Entity GetHighestRankMember(
            in DynamicBuffer<AggregateMember> members)
        {
            Entity best = Entity.Null;
            byte highestRank = 0;
            
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive != 0 && members[i].Rank > highestRank)
                {
                    highestRank = members[i].Rank;
                    best = members[i].MemberEntity;
                }
            }
            
            return best;
        }

        /// <summary>
        /// Counts active members.
        /// </summary>
        public static int CountActiveMembers(in DynamicBuffer<AggregateMember> members)
        {
            int count = 0;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive != 0) count++;
            }
            return count;
        }

        /// <summary>
        /// Sets member rank.
        /// </summary>
        public static bool SetMemberRank(
            ref DynamicBuffer<AggregateMember> members,
            Entity member,
            byte newRank)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == member)
                {
                    var entry = members[i];
                    entry.Rank = newRank;
                    members[i] = entry;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if entity is member of aggregate.
        /// </summary>
        public static bool IsMember(
            in DynamicBuffer<AggregateMember> members,
            Entity entity)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == entity)
                    return true;
            }
            return false;
        }
    }
}


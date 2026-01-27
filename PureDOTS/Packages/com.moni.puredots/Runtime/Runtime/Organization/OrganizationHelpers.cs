using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Organization
{
    /// <summary>
    /// Static helpers for organization management.
    /// </summary>
    [BurstCompile]
    public static class OrganizationHelpers
    {
        /// <summary>
        /// Default organization configuration.
        /// </summary>
        public static OrganizationConfig DefaultConfig => new OrganizationConfig
        {
            MinMembersToForm = 3,
            ElectionIntervalTicks = 216000, // 1 hour
            StabilityDecayRate = 0.001f,
            InfluenceGrowthRate = 0.01f,
            AllowCrossTypeRelations = 1
        };

        /// <summary>
        /// Gets member count.
        /// </summary>
        public static int GetMemberCount(in DynamicBuffer<OrganizationMember> members)
        {
            return members.Length;
        }

        /// <summary>
        /// Finds organization leader.
        /// </summary>
        public static Entity GetLeader(in DynamicBuffer<OrganizationMember> members)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsLeader != 0)
                    return members[i].MemberEntity;
            }
            return Entity.Null;
        }

        /// <summary>
        /// Adds a member to organization.
        /// </summary>
        public static bool AddMember(
            ref DynamicBuffer<OrganizationMember> members,
            Entity memberEntity,
            FixedString32Bytes rank,
            byte rankLevel,
            uint currentTick)
        {
            // Check if already member
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == memberEntity)
                    return false;
            }

            if (members.Length >= members.Capacity)
                return false;

            members.Add(new OrganizationMember
            {
                MemberEntity = memberEntity,
                Rank = rank,
                RankLevel = rankLevel,
                Standing = 50f, // Starting standing
                JoinedTick = currentTick,
                IsLeader = 0
            });

            return true;
        }

        /// <summary>
        /// Removes a member from organization.
        /// </summary>
        public static bool RemoveMember(ref DynamicBuffer<OrganizationMember> members, Entity memberEntity)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == memberEntity)
                {
                    members.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Promotes a member.
        /// </summary>
        public static bool PromoteMember(
            ref DynamicBuffer<OrganizationMember> members,
            Entity memberEntity,
            FixedString32Bytes newRank,
            byte newRankLevel)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == memberEntity)
                {
                    var member = members[i];
                    member.Rank = newRank;
                    member.RankLevel = newRankLevel;
                    members[i] = member;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates member standing.
        /// </summary>
        public static void UpdateStanding(ref DynamicBuffer<OrganizationMember> members, Entity memberEntity, float delta)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].MemberEntity == memberEntity)
                {
                    var member = members[i];
                    member.Standing = math.clamp(member.Standing + delta, 0f, 100f);
                    members[i] = member;
                    return;
                }
            }
        }

        /// <summary>
        /// Calculates organization stability.
        /// </summary>
        public static float CalculateStability(
            in DynamicBuffer<InternalFaction> factions,
            float leaderStanding,
            int memberCount)
        {
            if (memberCount <= 0) return 0f;
            
            float stability = 0.5f;
            
            // Leader standing affects stability
            stability += (leaderStanding - 50f) * 0.005f;
            
            // More factions = less stable
            stability -= factions.Length * 0.1f;
            
            // Check for dominant faction
            float maxSupport = 0;
            for (int i = 0; i < factions.Length; i++)
            {
                maxSupport = math.max(maxSupport, factions[i].Support);
            }
            
            // Clear majority = more stable
            if (maxSupport > 0.6f)
                stability += 0.2f;
            
            return math.clamp(stability, 0f, 1f);
        }

        /// <summary>
        /// Gets relation with another organization.
        /// </summary>
        public static float GetRelation(in DynamicBuffer<OrganizationRelation> relations, Entity other)
        {
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].OtherOrganization == other)
                    return relations[i].RelationScore;
            }
            return 0f; // Neutral
        }

        /// <summary>
        /// Updates relation with another organization.
        /// </summary>
        public static void UpdateRelation(
            ref DynamicBuffer<OrganizationRelation> relations,
            Entity other,
            float delta,
            uint currentTick)
        {
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].OtherOrganization == other)
                {
                    var relation = relations[i];
                    relation.RelationScore = math.clamp(relation.RelationScore + delta, -100f, 100f);
                    relation.RelationType = GetRelationType(relation.RelationScore);
                    relation.RelationChangedTick = currentTick;
                    relations[i] = relation;
                    return;
                }
            }

            // Add new relation
            if (relations.Length < relations.Capacity)
            {
                relations.Add(new OrganizationRelation
                {
                    OtherOrganization = other,
                    RelationScore = delta,
                    RelationType = GetRelationType(delta),
                    RelationChangedTick = currentTick
                });
            }
        }

        /// <summary>
        /// Gets relation type from score.
        /// </summary>
        public static FixedString32Bytes GetRelationType(float score)
        {
            if (score >= 75f) return new FixedString32Bytes("Alliance");
            if (score >= 25f) return new FixedString32Bytes("Friendly");
            if (score >= -25f) return new FixedString32Bytes("Neutral");
            if (score >= -75f) return new FixedString32Bytes("Rivalry");
            return new FixedString32Bytes("War");
        }

        /// <summary>
        /// Gets faction support total.
        /// </summary>
        public static float GetTotalFactionSupport(in DynamicBuffer<InternalFaction> factions)
        {
            float total = 0;
            for (int i = 0; i < factions.Length; i++)
            {
                total += factions[i].Support;
            }
            return total;
        }

        /// <summary>
        /// Finds ruling faction.
        /// </summary>
        public static bool TryGetRulingFaction(in DynamicBuffer<InternalFaction> factions, out InternalFaction ruling)
        {
            for (int i = 0; i < factions.Length; i++)
            {
                if (factions[i].IsRuling != 0)
                {
                    ruling = factions[i];
                    return true;
                }
            }
            ruling = default;
            return false;
        }

        /// <summary>
        /// Checks if organization can form.
        /// </summary>
        public static bool CanFormOrganization(int potentialMembers, in OrganizationConfig config)
        {
            return potentialMembers >= config.MinMembersToForm;
        }

        /// <summary>
        /// Calculates influence growth.
        /// </summary>
        public static float CalculateInfluenceGrowth(
            int memberCount,
            float wealth,
            float stability,
            in OrganizationConfig config)
        {
            float growth = config.InfluenceGrowthRate;
            growth *= memberCount * 0.1f;
            growth *= 1f + wealth * 0.001f;
            growth *= stability;
            return growth;
        }
    }
}


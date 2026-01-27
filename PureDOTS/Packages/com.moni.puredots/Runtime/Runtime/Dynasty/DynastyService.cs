using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using PureDOTS.Runtime.Succession;

namespace PureDOTS.Runtime.Dynasty
{
    /// <summary>
    /// Service for dynasty operations - creating dynasties, tracking lineage, calculating prestige.
    /// </summary>
    [BurstCompile]
    public static class DynastyService
    {
        /// <summary>
        /// Creates a new dynasty with the founder and controlled aggregate.
        /// </summary>
        public static Entity CreateDynasty(
            ref EntityCommandBuffer ecb,
            Entity founder,
            Entity aggregate,
            FixedString64Bytes dynastyName,
            uint currentTick)
        {
            var dynastyEntity = ecb.CreateEntity();

            ecb.AddComponent(dynastyEntity, new DynastyIdentity
            {
                DynastyName = dynastyName,
                FounderEntity = founder,
                ControlledAggregate = aggregate,
                FoundedTick = currentTick
            });

            ecb.AddComponent(dynastyEntity, new DynastyPrestige { LastUpdatedTick = currentTick });
            ecb.AddComponent(dynastyEntity, new DynastyWealth { LastUpdatedTick = currentTick });
            ecb.AddComponent(dynastyEntity, new DynastySuccessionRules
            {
                SuccessionType = SuccessionType.Primogeniture,
                AllowFemaleHeirs = 1,
                RequiresBloodline = 1,
                MinLineageStrength = 0.5f
            });

            // Initialize buffers before appending
            ecb.AddBuffer<DynastyMemberEntry>(dynastyEntity);
            ecb.AddBuffer<DynastyLineage>(dynastyEntity);

            // Add founder as first member
            TrackLineage(ref ecb, dynastyEntity, founder, Entity.Null, Entity.Null, currentTick, 0);
            AddMember(ref ecb, dynastyEntity, founder, DynastyRank.Founder, 1.0f, currentTick);

            return dynastyEntity;
        }

        /// <summary>
        /// Tracks a member in the dynasty lineage.
        /// </summary>
        public static void TrackLineage(
            ref EntityCommandBuffer ecb,
            Entity dynastyEntity,
            Entity member,
            Entity parentA,
            Entity parentB,
            uint birthTick,
            byte generation)
        {
            // Buffer should already exist from CreateDynasty, but add if missing
            // Note: EntityCommandBuffer doesn't support HasBuffer check, so we rely on CreateDynasty initialization
            var lineageEntry = new DynastyLineage
            {
                MemberEntity = member,
                ParentA = parentA,
                ParentB = parentB,
                BirthTick = birthTick,
                Generation = generation
            };
            ecb.AppendToBuffer(dynastyEntity, lineageEntry);
        }

        /// <summary>
        /// Adds a member to the dynasty.
        /// </summary>
        public static void AddMember(
            ref EntityCommandBuffer ecb,
            Entity dynastyEntity,
            Entity member,
            DynastyRank rank,
            float lineageStrength,
            uint currentTick)
        {
            // Add dynasty membership to the member entity
            ecb.AddComponent(member, new DynastyMember
            {
                DynastyEntity = dynastyEntity,
                Rank = rank,
                LineageStrength = lineageStrength
            });

            // Add to dynasty's member list
            var memberEntry = new DynastyMemberEntry
            {
                MemberEntity = member,
                Rank = rank,
                LineageStrength = lineageStrength,
                JoinedTick = currentTick
            };
            ecb.AppendToBuffer(dynastyEntity, memberEntry);
        }

        /// <summary>
        /// Calculates dynasty prestige based on member achievements, wealth, and reputation.
        /// </summary>
        public static float CalculateDynastyPrestige(
            in DynamicBuffer<DynastyMemberEntry> members,
            float totalWealth,
            float averageReputation)
        {
            float prestige = 0f;

            // Base prestige from wealth
            prestige += totalWealth * 0.1f;

            // Prestige from reputation
            prestige += averageReputation * 10f;

            // Prestige from member count and ranks
            int founderCount = 0;
            int heirCount = 0;
            int nobleCount = 0;

            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                switch (member.Rank)
                {
                    case DynastyRank.Founder:
                        founderCount++;
                        prestige += 100f;
                        break;
                    case DynastyRank.Heir:
                        heirCount++;
                        prestige += 50f;
                        break;
                    case DynastyRank.Noble:
                        nobleCount++;
                        prestige += 25f;
                        break;
                    case DynastyRank.Member:
                        prestige += 10f;
                        break;
                }

                // Lineage strength contributes
                prestige += member.LineageStrength * 20f;
            }

            return prestige;
        }

        /// <summary>
        /// Updates dynasty reputation based on member actions and achievements.
        /// </summary>
        public static void UpdateDynastyReputation(
            Entity dynastyEntity,
            ref DynastyPrestige prestige,
            in DynamicBuffer<DynastyMemberEntry> members,
            float totalWealth,
            float averageReputation,
            uint currentTick)
        {
            float newPrestige = CalculateDynastyPrestige(members, totalWealth, averageReputation);
            
            prestige.PrestigeScore = newPrestige;
            prestige.DynastyReputation = averageReputation;
            prestige.LastUpdatedTick = currentTick;
        }

        /// <summary>
        /// Calculates lineage strength for a member based on generation and parent lineage.
        /// </summary>
        public static float CalculateLineageStrength(
            byte generation,
            float parentALineageStrength,
            float parentBLineageStrength)
        {
            // Base strength decreases with generation
            float baseStrength = 1.0f / (1.0f + generation * 0.1f);

            // Average parent lineage strength
            float parentAverage = (parentALineageStrength + parentBLineageStrength) * 0.5f;

            // Combined strength
            return baseStrength * 0.5f + parentAverage * 0.5f;
        }
    }
}


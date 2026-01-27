using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Dynasty;
using PureDOTS.Runtime.Family;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems.Dynasty
{
    /// <summary>
    /// Promotes families to dynasties when they reach 4th generation OR meet renown threshold.
    /// Dynasties are created for renowned/glorious families or those long-running (4th generation onwards).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DynastyLineageTrackingSystem))]
    public partial struct DynastyPromotionSystem : ISystem
    {
        private const float RENOWN_THRESHOLD = 50.0f; // Family reputation threshold for promotion
        private const byte GENERATION_THRESHOLD = 4; // 4th generation (0-indexed, so generation 3 = 4th gen)
        private FixedString64Bytes _defaultDynastyName;
        private ComponentLookup<DynastyMember> _dynastyMemberLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            // Initialize default dynasty name in non-Burst context
            _defaultDynastyName = new FixedString64Bytes("Dynasty");
            _dynastyMemberLookup = state.GetComponentLookup<DynastyMember>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            _dynastyMemberLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            var job = new CheckPromotionJob
            {
                Ecb = ecb,
                CurrentTick = timeState.Tick,
                RenownThreshold = RENOWN_THRESHOLD,
                GenerationThreshold = GENERATION_THRESHOLD,
                DynastyMemberLookup = _dynastyMemberLookup,
                DefaultDynastyName = _defaultDynastyName
            };
            job.Run();
        }

        [BurstCompile]
        [WithNone(typeof(DynastyPromotionComplete))]
        partial struct CheckPromotionJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public uint CurrentTick;
            public float RenownThreshold;
            public byte GenerationThreshold;
            public FixedString64Bytes DefaultDynastyName;
            [ReadOnly] public ComponentLookup<DynastyMember> DynastyMemberLookup;

            void Execute(
                Entity familyEntity,
                in FamilyIdentity identity,
                in FamilyReputation reputation,
                in DynamicBuffer<FamilyTree> familyTree,
                in DynamicBuffer<FamilyMemberEntry> members)
            {
                // Skip if founder already has a dynasty (prevents duplicate promotions)
                if (DynastyMemberLookup.HasComponent(identity.FounderEntity))
                {
                    return;
                }

                // Check promotion criteria: 4th generation OR renown threshold
                bool meetsGenerationThreshold = false;
                bool meetsRenownThreshold = false;

                // Check generation threshold
                byte maxGeneration = FamilyService.CalculateMaxGeneration(identity.FounderEntity, familyTree);
                if (maxGeneration >= GenerationThreshold)
                {
                    meetsGenerationThreshold = true;
                }

                // Check renown threshold
                if (reputation.ReputationScore >= RenownThreshold)
                {
                    meetsRenownThreshold = true;
                }

                // Promote if either condition met
                if (meetsGenerationThreshold || meetsRenownThreshold)
                {
                    PromoteToDynasty(
                        familyEntity,
                        identity,
                        familyTree,
                        members,
                        maxGeneration);
                }
            }

            void PromoteToDynasty(
                Entity familyEntity,
                in FamilyIdentity identity,
                in DynamicBuffer<FamilyTree> familyTree,
                in DynamicBuffer<FamilyMemberEntry> members,
                byte maxGeneration)
            {
                // Create dynasty name from family name (or use default name)
                var dynastyName = identity.FamilyName;
                if (dynastyName.Length == 0)
                {
                    dynastyName = DefaultDynastyName;
                }

                // Create dynasty entity via service to ensure consistent setup
                var dynastyEntity = DynastyService.CreateDynasty(
                    ref Ecb,
                    identity.FounderEntity,
                    Entity.Null,
                    dynastyName,
                    CurrentTick);

                // Add all family members to dynasty
                // Build generation map for calculating lineage strength
                var entityGenerations = new NativeHashMap<Entity, byte>(members.Length, Allocator.Temp);
                CalculateGenerations(identity.FounderEntity, familyTree, ref entityGenerations);

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    byte generation = entityGenerations.TryGetValue(member.MemberEntity, out var gen) ? gen : (byte)0;

                    // Find member's parents from family tree
                    Entity parentA = Entity.Null;
                    Entity parentB = Entity.Null;
                    for (int j = 0; j < familyTree.Length; j++)
                    {
                        if (familyTree[j].MemberEntity == member.MemberEntity)
                        {
                            parentA = familyTree[j].ParentA;
                            parentB = familyTree[j].ParentB;
                            break;
                        }
                    }

                    // Calculate lineage strength
                    float lineageStrength = CalculateLineageStrength(generation, parentA, parentB, entityGenerations);

                    // Determine rank
                    DynastyRank rank = DetermineRank(member.MemberEntity, identity.FounderEntity, member.Role, generation);

                    // Founder already enrolled via CreateDynasty
                    if (member.MemberEntity == identity.FounderEntity)
                    {
                        continue;
                    }

                    // Track lineage
                    DynastyService.TrackLineage(
                        ref Ecb,
                        dynastyEntity,
                        member.MemberEntity,
                        parentA,
                        parentB,
                        ResolveBirthTick(member.MemberEntity, familyTree, member.JoinedTick),
                        generation);

                    // Add member to dynasty via service
                    DynastyService.AddMember(
                        ref Ecb,
                        dynastyEntity,
                        member.MemberEntity,
                        rank,
                        lineageStrength,
                        CurrentTick);
                }

                entityGenerations.Dispose();

                 // Mark family so promotion is not rerun
                Ecb.AddComponent(familyEntity, new DynastyPromotionComplete
                {
                    DynastyEntity = dynastyEntity,
                    ProcessedTick = CurrentTick
                });
            }

            static uint ResolveBirthTick(Entity member, in DynamicBuffer<FamilyTree> familyTree, uint fallbackTick)
            {
                for (int i = 0; i < familyTree.Length; i++)
                {
                    if (familyTree[i].MemberEntity == member)
                    {
                        return familyTree[i].BirthTick;
                    }
                }

                return fallbackTick;
            }

            void CalculateGenerations(
                Entity founder,
                in DynamicBuffer<FamilyTree> familyTree,
                ref NativeHashMap<Entity, byte> entityGenerations)
            {
                // Build parent-child map
                var childrenMap = new NativeParallelMultiHashMap<Entity, Entity>(familyTree.Length, Allocator.Temp);
                for (int i = 0; i < familyTree.Length; i++)
                {
                    var entry = familyTree[i];
                    if (entry.ParentA != Entity.Null)
                    {
                        childrenMap.Add(entry.ParentA, entry.MemberEntity);
                    }
                    if (entry.ParentB != Entity.Null && entry.ParentB != entry.ParentA)
                    {
                        childrenMap.Add(entry.ParentB, entry.MemberEntity);
                    }
                }

                // BFS from founder
                var queue = new NativeQueue<Entity>(Allocator.Temp);
                entityGenerations[founder] = 0;
                queue.Enqueue(founder);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    byte currentGen = entityGenerations[current];

                    if (childrenMap.TryGetFirstValue(current, out var child, out var iterator))
                    {
                        do
                        {
                            if (!entityGenerations.ContainsKey(child))
                            {
                                byte childGen = (byte)(currentGen + 1);
                                entityGenerations[child] = childGen;
                                queue.Enqueue(child);
                            }
                        }
                        while (childrenMap.TryGetNextValue(out child, ref iterator));
                    }
                }

                childrenMap.Dispose();
                queue.Dispose();
            }

            float CalculateLineageStrength(
                byte generation,
                Entity parentA,
                Entity parentB,
                in NativeHashMap<Entity, byte> entityGenerations)
            {
                float parentALineage = 0f;
                float parentBLineage = 0f;

                if (parentA != Entity.Null && entityGenerations.TryGetValue(parentA, out var genA))
                {
                    // Lineage strength decreases with generation
                    parentALineage = 1.0f / (1.0f + genA * 0.1f);
                }

                if (parentB != Entity.Null && entityGenerations.TryGetValue(parentB, out var genB))
                {
                    parentBLineage = 1.0f / (1.0f + genB * 0.1f);
                }

                // Average parent lineage strength
                float parentAverage = (parentALineage + parentBLineage) * 0.5f;
                if (parentA == Entity.Null && parentB == Entity.Null)
                {
                    parentAverage = 1.0f; // Founder has full strength
                }
                else if (parentA == Entity.Null || parentB == Entity.Null)
                {
                    parentAverage = parentALineage + parentBLineage; // Single parent
                }

                // Base strength decreases with generation
                float baseStrength = 1.0f / (1.0f + generation * 0.1f);

                // Combined strength
                return baseStrength * 0.5f + parentAverage * 0.5f;
            }

            DynastyRank DetermineRank(
                Entity member,
                Entity founder,
                FamilyRole familyRole,
                byte generation)
            {
                if (member == founder)
                {
                    return DynastyRank.Founder;
                }

                if (generation == 1)
                {
                    // First generation after founder - likely heirs
                    return DynastyRank.Heir;
                }

                if (generation <= 2)
                {
                    // Second generation - nobles
                    return DynastyRank.Noble;
                }

                // Later generations - regular members
                return DynastyRank.Member;
            }
        }
    }
}


using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Runtime.Family
{
    /// <summary>
    /// Service for family operations - creating families, managing members, calculating relationships.
    /// </summary>
    [BurstCompile]
    public static class FamilyService
    {
        /// <summary>
        /// Creates a new family with the founder as the first member.
        /// </summary>
        public static Entity CreateFamily(ref EntityCommandBuffer ecb, Entity founder, FixedString64Bytes familyName, uint currentTick)
        {
            var familyEntity = ecb.CreateEntity();
            
            ecb.AddComponent(familyEntity, new FamilyIdentity
            {
                FamilyName = familyName,
                FounderEntity = founder,
                FoundedTick = currentTick
            });

            ecb.AddComponent(familyEntity, new FamilyWealth { LastUpdatedTick = currentTick });
            ecb.AddComponent(familyEntity, new FamilyReputation { LastUpdatedTick = currentTick });

            // Initialize buffers before appending
            ecb.AddBuffer<FamilyMemberEntry>(familyEntity);
            ecb.AddBuffer<FamilyTree>(familyEntity);

            // Add founder as first member
            AddMember(ref ecb, familyEntity, founder, FamilyRole.Founder, currentTick);

            return familyEntity;
        }

        /// <summary>
        /// Adds a member to a family.
        /// </summary>
        public static void AddMember(ref EntityCommandBuffer ecb, Entity familyEntity, Entity member, FamilyRole role, uint currentTick)
        {
            // Add family membership to the member entity
            ecb.AddComponent(member, new FamilyMember
            {
                FamilyEntity = familyEntity,
                Role = role
            });

            // Add to family's member list
            var memberEntry = new FamilyMemberEntry
            {
                MemberEntity = member,
                Role = role,
                JoinedTick = currentTick
            };
            ecb.AppendToBuffer(familyEntity, memberEntry);
        }

        /// <summary>
        /// Removes a member from a family.
        /// </summary>
        public static void RemoveMember(ref EntityCommandBuffer.ParallelWriter ecb, int index, Entity familyEntity, Entity member)
        {
            ecb.RemoveComponent<FamilyMember>(index, member);
            // Note: FamilyMemberEntry removal from buffer must be done in a system that can iterate buffers
        }

        /// <summary>
        /// Calculates the relationship type between two family members based on family tree.
        /// </summary>
        public static FamilyRelationType CalculateRelationship(
            Entity memberA,
            Entity memberB,
            in DynamicBuffer<FamilyTree> familyTreeA,
            in DynamicBuffer<FamilyTree> familyTreeB)
        {
            // Find entries for both members
            FamilyTree? entryA = null;
            FamilyTree? entryB = null;

            for (int i = 0; i < familyTreeA.Length; i++)
            {
                if (familyTreeA[i].MemberEntity == memberA)
                {
                    entryA = familyTreeA[i];
                    break;
                }
            }

            for (int i = 0; i < familyTreeB.Length; i++)
            {
                if (familyTreeB[i].MemberEntity == memberB)
                {
                    entryB = familyTreeB[i];
                    break;
                }
            }

            if (!entryA.HasValue || !entryB.HasValue)
                return FamilyRelationType.None;

            var treeA = entryA.Value;
            var treeB = entryB.Value;

            // Direct parent-child
            if (treeA.MemberEntity == memberB && (treeA.ParentA == memberA || treeA.ParentB == memberA))
                return FamilyRelationType.Child;
            if (treeB.MemberEntity == memberA && (treeB.ParentA == memberB || treeB.ParentB == memberB))
                return FamilyRelationType.Parent;

            // Siblings (share at least one parent)
            if ((treeA.ParentA != Entity.Null && (treeA.ParentA == treeB.ParentA || treeA.ParentA == treeB.ParentB)) ||
                (treeA.ParentB != Entity.Null && (treeA.ParentB == treeB.ParentA || treeA.ParentB == treeB.ParentB)))
            {
                return FamilyRelationType.Sibling;
            }

            // Grandparent-grandchild
            if (treeA.ParentA != Entity.Null)
            {
                // Check if A's parent is B's grandparent
                if (treeB.ParentA == treeA.ParentA || treeB.ParentB == treeA.ParentA)
                    return FamilyRelationType.Grandparent;
            }
            if (treeA.ParentB != Entity.Null)
            {
                if (treeB.ParentA == treeA.ParentB || treeB.ParentB == treeA.ParentB)
                    return FamilyRelationType.Grandparent;
            }

            // Reverse: B's parent is A's grandparent
            if (treeB.ParentA != Entity.Null)
            {
                if (treeA.ParentA == treeB.ParentA || treeA.ParentB == treeB.ParentA)
                    return FamilyRelationType.Grandchild;
            }
            if (treeB.ParentB != Entity.Null)
            {
                if (treeA.ParentA == treeB.ParentB || treeA.ParentB == treeB.ParentB)
                    return FamilyRelationType.Grandchild;
            }

            // Cousins (parents are siblings)
            // Simplified: if both have parents and those parents are siblings
            if (treeA.ParentA != Entity.Null && treeB.ParentA != Entity.Null)
            {
                // Would need to check if parents are siblings - simplified for now
                return FamilyRelationType.Cousin;
            }

            return FamilyRelationType.None;
        }

        /// <summary>
        /// Updates the family tree when a new child is born.
        /// </summary>
        public static void AddToFamilyTree(
            ref EntityCommandBuffer ecb,
            Entity familyEntity,
            Entity child,
            Entity parentA,
            Entity parentB,
            uint birthTick)
        {
            // Buffer should already exist from CreateFamily, but add if missing
            // Note: EntityCommandBuffer doesn't support HasBuffer check, so we rely on CreateFamily initialization
            var treeEntry = new FamilyTree
            {
                MemberEntity = child,
                ParentA = parentA,
                ParentB = parentB,
                BirthTick = birthTick
            };
            ecb.AppendToBuffer(familyEntity, treeEntry);
        }

        /// <summary>
        /// Updates family tree structure - recalculates relationships.
        /// </summary>
        public static void UpdateFamilyTree(Entity familyEntity, ref DynamicBuffer<FamilyTree> familyTree)
        {
            // Tree structure is maintained by AddToFamilyTree
            // This method can be extended for validation/cleanup if needed
        }

        /// <summary>
        /// Calculates the maximum generation in a family tree.
        /// Founder is generation 0, their children are generation 1, etc.
        /// Returns the deepest generation found.
        /// </summary>
        [BurstCompile]
        public static byte CalculateMaxGeneration(
            in Entity founderEntity,
            in DynamicBuffer<FamilyTree> familyTree)
        {
            if (familyTree.Length == 0)
                return 0;

            // Find founder entry (has no parents or is the founder entity)
            Entity founder = Entity.Null;
            for (int i = 0; i < familyTree.Length; i++)
            {
                var entry = familyTree[i];
                if (entry.MemberEntity == founderEntity)
                {
                    founder = entry.MemberEntity;
                    break;
                }
            }

            // If founder not found in tree, check for entries with no parents
            if (founder == Entity.Null)
            {
                for (int i = 0; i < familyTree.Length; i++)
                {
                    var entry = familyTree[i];
                    if (entry.ParentA == Entity.Null && entry.ParentB == Entity.Null)
                    {
                        founder = entry.MemberEntity;
                        break;
                    }
                }
            }

            // If still no founder found, return 0 (assume all are generation 0)
            if (founder == Entity.Null)
                return 0;

            // Build traversal helpers
            var entityGenerations = new NativeHashMap<Entity, byte>(familyTree.Length, Allocator.Temp);
            var childrenMap = new NativeParallelMultiHashMap<Entity, Entity>(familyTree.Length, Allocator.Temp);
            var queue = new NativeQueue<Entity>(Allocator.Temp);

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

            entityGenerations[founder] = 0;
            queue.Enqueue(founder);
            byte maxGeneration = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentGeneration = entityGenerations[current];

                if (childrenMap.TryGetFirstValue(current, out var child, out var iterator))
                {
                    do
                    {
                        if (!entityGenerations.ContainsKey(child))
                        {
                            byte childGeneration = (byte)(currentGeneration + 1);
                            entityGenerations[child] = childGeneration;
                            maxGeneration = (byte)math.max((int)maxGeneration, (int)childGeneration);
                            queue.Enqueue(child);
                        }
                    }
                    while (childrenMap.TryGetNextValue(out child, ref iterator));
                }
            }

            entityGenerations.Dispose();
            childrenMap.Dispose();
            queue.Dispose();

            return maxGeneration;
        }
    }
}


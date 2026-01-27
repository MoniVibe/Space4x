using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Family;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Lifecycle;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Systems.Family
{
    /// <summary>
    /// System that calculates relationships between family members.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FamilyRelationshipCalculationSystem : ISystem
    {
        private BufferLookup<FamilyTree> _familyTreeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _familyTreeLookup = state.GetBufferLookup<FamilyTree>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            // Only update relationships periodically (every 100 ticks) to reduce cost
            if (timeState.Tick % 100 != 0)
                return;

            _familyTreeLookup.Update(ref state);

            var job = new CalculateRelationshipsJob
            {
                FamilyTreeLookup = _familyTreeLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct CalculateRelationshipsJob : IJobEntity
        {
            [ReadOnly]
            public BufferLookup<FamilyTree> FamilyTreeLookup;

            void Execute(
                Entity entity,
                in FamilyIdentity identity,
                in DynamicBuffer<FamilyMemberEntry> members,
                in DynamicBuffer<FamilyTree> familyTree)
            {
                // Calculate relationships between all pairs of members in the family
                // Store results in FamilyRelation components on member entities
                // For now, relationships are calculated on-demand via FamilyService
                // This job can be extended to cache relationship data if needed
                
                // Example: For each member pair, calculate relationship and cache it
                // This is a placeholder - full implementation would require storing
                // relationship data somewhere (component on members or family entity buffer)
            }
        }
    }

    /// <summary>
    /// System that tracks inheritance flows through families.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InheritanceTrackingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            // Integration with Succession system handles inheritance
            // This system can track family-specific inheritance rules
        }
    }

    /// <summary>
    /// System that updates family tree structure when members are added/removed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FamilyTreeUpdateSystem : ISystem
    {
        private ComponentLookup<FamilyMember> _familyMemberLookup;
        private BufferLookup<FamilyMemberEntry> _familyMemberEntryLookup;
        private BufferLookup<EntityRelation> _entityRelationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _familyMemberLookup = state.GetComponentLookup<FamilyMember>(true);
            _familyMemberEntryLookup = state.GetBufferLookup<FamilyMemberEntry>(false);
            _entityRelationLookup = state.GetBufferLookup<EntityRelation>(false); // Writable for bidirectional relation creation
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            _familyMemberLookup.Update(ref state);
            _familyMemberEntryLookup.Update(ref state);
            _entityRelationLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecbParallel = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process death events
            var deathJob = new ProcessDeathEventsJob
            {
                Ecb = ecbParallel,
                CurrentTick = timeState.Tick,
                FamilyMemberLookup = _familyMemberLookup,
                FamilyMemberEntryLookup = _familyMemberEntryLookup
            };
            deathJob.ScheduleParallel();

            // Process birth events (needs single-threaded for buffer modifications and service calls)
            var birthJob = new ProcessBirthEventsJob
            {
                Ecb = ecb,
                CurrentTick = timeState.Tick,
                FamilyMemberLookup = _familyMemberLookup,
                EntityRelationLookup = _entityRelationLookup
            };
            birthJob.Run();

            // Update family trees (cleanup, validation)
            var updateJob = new UpdateFamilyTreesJob
            {
                CurrentTick = timeState.Tick
            };
            updateJob.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessDeathEventsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public uint CurrentTick;
            [ReadOnly]
            public ComponentLookup<FamilyMember> FamilyMemberLookup;
            [NativeDisableParallelForRestriction]
            public BufferLookup<FamilyMemberEntry> FamilyMemberEntryLookup;

            void Execute([EntityIndexInQuery] int index, DynamicBuffer<DeathEvent> deathEvents)
            {
                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var deathEvent = deathEvents[i];
                    Entity deadEntity = deathEvent.DeadEntity;

                    // Check if deceased entity is a family member
                    if (!FamilyMemberLookup.HasComponent(deadEntity))
                        continue;

                    var familyMember = FamilyMemberLookup[deadEntity];
                    Entity familyEntity = familyMember.FamilyEntity;

                    if (familyEntity == Entity.Null)
                        continue;

                    // Remove FamilyMember component
                    Ecb.RemoveComponent<FamilyMember>(index, deadEntity);

                    // Remove from family member buffer
                    if (FamilyMemberEntryLookup.HasBuffer(familyEntity))
                    {
                        var members = FamilyMemberEntryLookup[familyEntity];
                        for (int j = members.Length - 1; j >= 0; j--)
                        {
                            if (members[j].MemberEntity == deadEntity)
                            {
                                members.RemoveAt(j);
                                break;
                            }
                        }
                    }

                    // If founder died, mark for succession handling (handled by succession system)
                    // Note: Family doesn't have explicit leader succession like dynasties
                }
            }
        }

        [BurstCompile]
        partial struct ProcessBirthEventsJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public uint CurrentTick;
            [ReadOnly]
            public ComponentLookup<FamilyMember> FamilyMemberLookup;
            [NativeDisableParallelForRestriction]
            public BufferLookup<EntityRelation> EntityRelationLookup;

            void Execute(
                [EntityIndexInQuery] int index,
                Entity entity,
                in LifecycleState lifecycle)
            {
                // Check if this entity was just born (BirthTick matches current tick)
                if (lifecycle.BirthTick != CurrentTick)
                    return;

                // IMPORTANT: This system requires that game-side systems (genealogy/lifecycle) create
                // Parent EntityRelation entries on the newborn entity BEFORE this handler runs.
                // If parent relations don't exist, the entity will not be added to any family.
                // Game-side systems should create bidirectional Parent/Child relations when entities are born.

                // Find parents via EntityRelation buffers
                Entity parentA = Entity.Null;
                Entity parentB = Entity.Null;

                if (EntityRelationLookup.HasBuffer(entity))
                {
                    var relations = EntityRelationLookup[entity];
                    for (int i = 0; i < relations.Length; i++)
                    {
                        if (relations[i].Type == RelationType.Parent)
                        {
                            if (parentA == Entity.Null)
                                parentA = relations[i].OtherEntity;
                            else if (parentB == Entity.Null)
                                parentB = relations[i].OtherEntity;
                        }
                    }
                }

                // If no parents found, entity cannot be added to family
                // This is expected if game-side systems haven't created parent relations yet
                if (parentA == Entity.Null && parentB == Entity.Null)
                {
                    return;
                }

                // Fallback: Ensure bidirectional Parent/Child relations exist
                // If Parent relation exists on child, ensure Child relation exists on parent
                if (parentA != Entity.Null)
                {
                    EnsureBidirectionalRelation(parentA, entity);
                }
                if (parentB != Entity.Null)
                {
                    EnsureBidirectionalRelation(parentB, entity);
                }

                // If at least one parent is in a family, add child to that family
                if (parentA != Entity.Null && FamilyMemberLookup.HasComponent(parentA))
                {
                    var parentFamily = FamilyMemberLookup[parentA];
                    FamilyService.AddToFamilyTree(
                        ref Ecb,
                        parentFamily.FamilyEntity,
                        entity,
                        parentA,
                        parentB,
                        CurrentTick);
                }
                else if (parentB != Entity.Null && FamilyMemberLookup.HasComponent(parentB))
                {
                    var parentFamily = FamilyMemberLookup[parentB];
                    FamilyService.AddToFamilyTree(
                        ref Ecb,
                        parentFamily.FamilyEntity,
                        entity,
                        parentA,
                        parentB,
                        CurrentTick);
                }
            }

            void EnsureBidirectionalRelation(Entity parent, Entity child)
            {
                // Ensure buffers exist - add via ECB if missing (will be available next frame)
                bool childBufferExists = EntityRelationLookup.HasBuffer(child);
                bool parentBufferExists = EntityRelationLookup.HasBuffer(parent);
                
                if (!childBufferExists)
                {
                    Ecb.AddBuffer<EntityRelation>(child);
                }
                if (!parentBufferExists)
                {
                    Ecb.AddBuffer<EntityRelation>(parent);
                }

                // If buffers don't exist yet, relations will be created next frame when buffers are available
                // This is acceptable since birth handlers run every frame
                if (!childBufferExists || !parentBufferExists)
                {
                    return;
                }

                // Check if relations already exist
                var childRelations = EntityRelationLookup[child];
                bool hasParentRelation = false;
                for (int i = 0; i < childRelations.Length; i++)
                {
                    if (childRelations[i].OtherEntity == parent && childRelations[i].Type == RelationType.Parent)
                    {
                        hasParentRelation = true;
                        break;
                    }
                }

                var parentRelations = EntityRelationLookup[parent];
                bool hasChildRelation = false;
                for (int i = 0; i < parentRelations.Length; i++)
                {
                    if (parentRelations[i].OtherEntity == child && parentRelations[i].Type == RelationType.Child)
                    {
                        hasChildRelation = true;
                        break;
                    }
                }

                // Create missing relations (single-threaded job allows direct buffer modification)
                if (!hasParentRelation)
                {
                    var childBuffer = EntityRelationLookup[child];
                    childBuffer.Add(new EntityRelation
                    {
                        OtherEntity = parent,
                        Type = RelationType.Parent,
                        Intensity = 80, // High positive for parent-child
                        InteractionCount = 0,
                        FirstMetTick = CurrentTick,
                        LastInteractionTick = CurrentTick,
                        Trust = 90,
                        Familiarity = 100,
                        Respect = 70,
                        Fear = 0
                    });
                }

                if (!hasChildRelation)
                {
                    var parentBuffer = EntityRelationLookup[parent];
                    parentBuffer.Add(new EntityRelation
                    {
                        OtherEntity = child,
                        Type = RelationType.Child,
                        Intensity = 80, // High positive for parent-child
                        InteractionCount = 0,
                        FirstMetTick = CurrentTick,
                        LastInteractionTick = CurrentTick,
                        Trust = 90,
                        Familiarity = 100,
                        Respect = 50,
                        Fear = 0
                    });
                }
            }
        }

        [BurstCompile]
        partial struct UpdateFamilyTreesJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<FamilyTree> familyTree,
                in FamilyIdentity identity)
            {
                // Validate and clean up family tree
                // Remove entries for deceased members (already handled by ProcessDeathEventsJob)
                // This job can be extended for relationship recalculation if needed
            }
        }
    }

    /// <summary>
    /// System that aggregates family wealth from members.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FamilyWealthAggregationSystem : ISystem
    {
        private ComponentLookup<PureDOTS.Runtime.Economy.Wealth.VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<PureDOTS.Runtime.Economy.Wealth.FamilyWealth> _familyWalletLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _villagerWealthLookup = state.GetComponentLookup<PureDOTS.Runtime.Economy.Wealth.VillagerWealth>(true);
            _familyWalletLookup = state.GetComponentLookup<PureDOTS.Runtime.Economy.Wealth.FamilyWealth>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            _villagerWealthLookup.Update(ref state);
            _familyWalletLookup.Update(ref state);

            var job = new AggregateFamilyWealthJob
            {
                CurrentTick = timeState.Tick,
                VillagerWealthLookup = _villagerWealthLookup,
                FamilyWalletLookup = _familyWalletLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct AggregateFamilyWealthJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly]
            public ComponentLookup<PureDOTS.Runtime.Economy.Wealth.VillagerWealth> VillagerWealthLookup;
            [ReadOnly]
            public ComponentLookup<PureDOTS.Runtime.Economy.Wealth.FamilyWealth> FamilyWalletLookup;

            void Execute(
                Entity entity,
                ref FamilyWealth wealth,
                in DynamicBuffer<FamilyMemberEntry> members)
            {
                float totalWealth = 0f;
                int activeMemberCount = 0;

                // Sum wealth from all active members
                for (int i = 0; i < members.Length; i++)
                {
                    var memberEntity = members[i].MemberEntity;
                    if (memberEntity != Entity.Null && VillagerWealthLookup.HasComponent(memberEntity))
                    {
                        var memberWealth = VillagerWealthLookup[memberEntity];
                        totalWealth += memberWealth.Balance;
                        activeMemberCount++;
                    }
                }

                // Read shared family wallet balance
                float sharedWealth = 0f;
                if (FamilyWalletLookup.HasComponent(entity))
                {
                    sharedWealth = FamilyWalletLookup[entity].Balance;
                }

                wealth.TotalWealth = totalWealth;
                wealth.SharedWealth = sharedWealth;
                wealth.AverageWealth = activeMemberCount > 0 ? totalWealth / activeMemberCount : 0f;
                wealth.LastUpdatedTick = CurrentTick;
            }
        }
    }
}


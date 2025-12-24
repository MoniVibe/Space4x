using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.Time;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Bridges Space4X FormationAssignment/FormationTemplate to PureDOTS FormationState/FormationIntegrity/FormationBonus.
    /// Follows projection pattern: if entity has PureDOTS FormationState, leave alone.
    /// If entity has FormationTemplate but not FormationState, project/add PureDOTS components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Combat.FormationCombatSystem))]
    public partial struct Space4XFormationCombatBridgeSystem : ISystem
    {
        private ComponentLookup<FormationAssignment> _assignmentLookup;
        private BufferLookup<FormationSlotDefinition> _slotDefinitionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _assignmentLookup = state.GetComponentLookup<FormationAssignment>(true);
            _slotDefinitionLookup = state.GetBufferLookup<FormationSlotDefinition>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.CurrentTick;

            _assignmentLookup.Update(ref state);
            _slotDefinitionLookup.Update(ref state);

            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Phase A: Add components and buffers via ECB
            // 1. PROJECT: Query formation leaders with FormationTemplate but no FormationState
            foreach (var (formationTemplate, transform, entity) in SystemAPI
                     .Query<RefRO<FormationTemplate>, RefRO<LocalTransform>>()
                     .WithNone<FormationState>()
                     .WithEntityAccess())
            {
                CreateFormationState(entity, formationTemplate.ValueRO, transform.ValueRO, currentTick, ref state, ref ecb);
            }

            // 2. UPDATE: Sync FormationState when FormationTemplate changes
            foreach (var (formationTemplate, formationState, transform, entity) in SystemAPI
                     .Query<RefRO<FormationTemplate>, RefRW<FormationState>, RefRO<LocalTransform>>()
                     .WithChangeFilter<FormationTemplate>()
                     .WithEntityAccess())
            {
                FormationType mappedType = MapFormationShape(formationTemplate.ValueRO.Shape);
                if (formationState.ValueRO.Type != mappedType)
                {
                    formationState.ValueRW.Type = mappedType;
                    formationState.ValueRW.Spacing = formationTemplate.ValueRO.Spacing;
                    formationState.ValueRW.AnchorPosition = transform.ValueRO.Position;
                    formationState.ValueRW.AnchorRotation = quaternion.LookRotationSafe(formationTemplate.ValueRO.Heading, math.up());
                    formationState.ValueRW.MaxSlots = formationTemplate.ValueRO.MaxSlots;
                    formationState.ValueRW.LastUpdateTick = currentTick;
                }
            }

            // Collect all assignments once into a map (optimize O(N²) to O(N+M))
            // Calculate assignment count first to size the map correctly
            var assignmentQuery = SystemAPI.QueryBuilder().WithAll<FormationAssignment>().Build();
            var assignmentCount = assignmentQuery.CalculateEntityCount();
            
            // Conditionally create assignment map (only if assignments exist)
            NativeMultiHashMap<Entity, (FormationAssignment Assignment, Entity VesselEntity)> assignmentMap = default;
            bool hasAssignments = assignmentCount > 0;
            if (hasAssignments)
            {
                var mapCapacity = math.max(1, assignmentCount) + 10; // Safety margin
                assignmentMap = new NativeMultiHashMap<Entity, (FormationAssignment Assignment, Entity VesselEntity)>(mapCapacity, Allocator.Temp);
                foreach (var (assignment, vesselEntity) in SystemAPI.Query<RefRO<FormationAssignment>>().WithEntityAccess())
                {
                    if (assignment.ValueRO.FormationLeader != Entity.Null)
                    {
                        assignmentMap.Add(assignment.ValueRO.FormationLeader, (assignment.ValueRO, vesselEntity));
                    }
                }
            }

            // Ensure FormationSlot buffers exist for all formation leaders
            var leaderQuery = SystemAPI.QueryBuilder()
                .WithAll<FormationTemplate, FormationState>()
                .Build();

            var leaders = leaderQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < leaders.Length; i++)
            {
                var leaderEntity = leaders[i];
                if (!state.EntityManager.HasBuffer<FormationSlot>(leaderEntity))
                {
                    ecb.AddBuffer<FormationSlot>(leaderEntity);
                }
            }
            leaders.Dispose();

            // Playback ECB to apply structural changes
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Phase B: Populate buffers via EntityManager (after playback)
            // 3. UPDATE: Sync FormationSlot buffer when FormationAssignment components change
            leaders = leaderQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < leaders.Length; i++)
            {
                var leaderEntity = leaders[i];
                if (state.EntityManager.HasBuffer<FormationSlot>(leaderEntity))
                {
                    var slots = state.EntityManager.GetBuffer<FormationSlot>(leaderEntity);
                    
                    // Check if leader has slot definitions
                    if (state.EntityManager.HasBuffer<FormationSlotDefinition>(leaderEntity))
                    {
                        var slotDefinitions = state.EntityManager.GetBuffer<FormationSlotDefinition>(leaderEntity);
                        UpdateFormationSlotsFromDefinitions(slots, slotDefinitions, leaderEntity, ref state);
                    }
                    else
                    {
                        // Otherwise, create slots from FormationAssignment components pointing to this leader
                        UpdateFormationSlotsForLeader(slots, leaderEntity, assignmentMap, hasAssignments, ref state);
                    }
                }
            }
            leaders.Dispose();
            
            // Dispose map if it was created
            if (hasAssignments)
            {
                assignmentMap.Dispose();
            }
        }

        private static void CreateFormationState(
            Entity entity,
            FormationTemplate template,
            LocalTransform transform,
            uint currentTick,
            ref SystemState state,
            ref EntityCommandBuffer ecb)
        {
            FormationType mappedType = MapFormationShape(template.Shape);

            ecb.AddComponent(entity, new FormationState
            {
                Type = mappedType,
                AnchorPosition = transform.Position,
                AnchorRotation = quaternion.LookRotationSafe(template.Heading, math.up()),
                Spacing = template.Spacing,
                Scale = 1f,
                MaxSlots = template.MaxSlots,
                FilledSlots = 0,
                IsMoving = false,
                LastUpdateTick = currentTick
            });

            ecb.AddComponent(entity, new FormationIntegrity
            {
                IntegrityPercent = 1f,
                LastCalculatedTick = currentTick,
                MembersInPosition = 0,
                TotalMembers = 0
            });

            // Ensure SquadCohesion component exists
            if (!state.EntityManager.HasComponent<SquadCohesion>(entity))
            {
                ecb.AddComponent(entity, new SquadCohesion
                {
                    CohesionLevel = 0.7f,
                    Threshold = CohesionThreshold.Cohesive,
                    LastUpdatedTick = currentTick,
                    DegradationRate = 0.1f,
                    RegenRate = 0.05f
                });
            }

            // Ensure CombatStats component exists (for morale wave application)
            // Note: These are placeholder values. TODO: Aggregate from fleet vessels/members
            if (!state.EntityManager.HasComponent<CombatStats>(entity))
            {
                ecb.AddComponent(entity, new CombatStats
                {
                    Attack = 50, // Placeholder - TODO: Aggregate from fleet vessels
                    Defense = 50, // Placeholder - TODO: Aggregate from fleet vessels
                    Morale = 50, // Placeholder - TODO: Derive from fleet aggregate stats if available
                    AttackSpeed = 50, // Placeholder - TODO: Aggregate from fleet vessels
                    AttackDamage = 10, // Placeholder - TODO: Aggregate from fleet vessels
                    Accuracy = 50, // Placeholder - TODO: Aggregate from fleet vessels
                    CriticalChance = 5, // Placeholder - TODO: Aggregate from fleet vessels
                    Health = 100, // Placeholder - TODO: Aggregate from fleet vessels
                    CurrentHealth = 100, // Placeholder
                    Stamina = 10, // Placeholder
                    CurrentStamina = 10, // Placeholder
                    SpellPower = 0, // Placeholder
                    ManaPool = 0, // Placeholder
                    CurrentMana = 0, // Placeholder
                    EquippedWeapon = Entity.Null,
                    EquippedArmor = Entity.Null,
                    EquippedShield = Entity.Null,
                    CombatExperience = 0,
                    IsInCombat = false,
                    CurrentOpponent = Entity.Null
                });
            }

            // FormationSlot buffer will be populated in Phase B after ECB playback
        }

        private static void UpdateFormationSlotsForLeader(
            DynamicBuffer<FormationSlot> slots,
            Entity leaderEntity,
            NativeMultiHashMap<Entity, (FormationAssignment Assignment, Entity VesselEntity)> assignmentMap,
            bool hasAssignments,
            ref SystemState state)
        {
            slots.Clear();

            // Defensive check: verify leader entity still exists
            if (!state.EntityManager.Exists(leaderEntity))
            {
                return;
            }

            // Handle case where no assignments exist (empty slots, but buffer still created)
            if (!hasAssignments || !assignmentMap.IsCreated)
            {
                // No assignments - slots remain empty but buffer exists
                // Update FormationState filled slots count to 0
                if (state.EntityManager.HasComponent<FormationState>(leaderEntity))
                {
                    var formationState = state.EntityManager.GetComponentData<FormationState>(leaderEntity);
                    formationState.FilledSlots = 0;
                    state.EntityManager.SetComponentData(leaderEntity, formationState);
                }
                return;
            }

            byte slotIndex = 0;
            if (assignmentMap.TryGetFirstValue(leaderEntity, out var entry, out var iterator))
            {
                do
                {
                    // Bounds check: max 20 slots per formation
                    if (slotIndex >= 20)
                        break;

                    // Defensive check: verify vessel entity still exists
                    if (!state.EntityManager.Exists(entry.VesselEntity))
                    {
                        continue;
                    }

                    FormationSlotRole role = MapFormationRole(entry.Assignment.SlotIndex);
                    slots.Add(new FormationSlot
                    {
                        SlotIndex = slotIndex,
                        LocalOffset = entry.Assignment.CurrentOffset,
                        Role = role,
                        AssignedEntity = entry.VesselEntity,
                        Priority = entry.Assignment.SlotIndex,
                        IsRequired = (slotIndex == 0) // Leader slot is required
                    });
                    slotIndex++;
                }
                while (assignmentMap.TryGetNextValue(out entry, ref iterator));
            }

            // Update FormationState filled slots count
            if (state.EntityManager.HasComponent<FormationState>(leaderEntity))
            {
                var formationState = state.EntityManager.GetComponentData<FormationState>(leaderEntity);
                formationState.FilledSlots = slotIndex;
                state.EntityManager.SetComponentData(leaderEntity, formationState);
            }
        }

        private static void UpdateFormationSlotsFromDefinitions(
            DynamicBuffer<FormationSlot> slots,
            DynamicBuffer<FormationSlotDefinition> slotDefinitions,
            Entity entity,
            ref SystemState state)
        {
            slots.Clear();

            // Defensive check: verify entity still exists
            if (!state.EntityManager.Exists(entity))
            {
                return;
            }

            // Bounds check: max 20 slots per formation
            int maxSlots = math.min(slotDefinitions.Length, 20);
            for (int i = 0; i < maxSlots; i++)
            {
                var slotDef = slotDefinitions[i];
                FormationSlotRole role = MapFormationRole(slotDef.Slot.SlotIndex);
                slots.Add(new FormationSlot
                {
                    SlotIndex = (byte)i,
                    LocalOffset = slotDef.Slot.Offset,
                    Role = role,
                    AssignedEntity = Entity.Null, // Will be filled by FormationAssignment lookup
                    Priority = slotDef.Slot.Priority,
                    IsRequired = (slotDef.Slot.SlotIndex == 0)
                });
            }

            // Update FormationState filled slots count
            if (state.EntityManager.Exists(entity) && state.EntityManager.HasComponent<FormationState>(entity))
            {
                var formationState = state.EntityManager.GetComponentData<FormationState>(entity);
                formationState.FilledSlots = (byte)maxSlots;
                state.EntityManager.SetComponentData(entity, formationState);
            }
        }

        private static FormationType MapFormationShape(FormationShape shape)
        {
            return shape switch
            {
                FormationShape.Wedge => FormationType.Wedge,
                FormationShape.Line => FormationType.Line,
                FormationShape.Circle => FormationType.Circle,
                FormationShape.Echelon => FormationType.Echelon,
                FormationShape.Cluster => FormationType.Square,
                FormationShape.Dispersed => FormationType.Skirmish,
                _ => FormationType.None
            };
        }

        private static FormationSlotRole MapFormationRole(byte slotIndex)
        {
            // Map slot index to role (0 = leader, others based on position)
            if (slotIndex == 0)
                return FormationSlotRole.Leader;
            if (slotIndex <= 3)
                return FormationSlotRole.Front;
            if (slotIndex <= 6)
                return FormationSlotRole.Flank;
            return FormationSlotRole.Rear;
        }
    }
}


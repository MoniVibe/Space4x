using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Formation
{
    /// <summary>
    /// System that calculates and updates formation slot positions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct FormationSlotUpdateSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Update formation slot positions
            foreach (var (formationState, slots, entity) in 
                SystemAPI.Query<RefRW<FormationState>, DynamicBuffer<FormationSlot>>()
                    .WithEntityAccess())
            {
                var slotsBuffer = slots;
                int totalSlots = slotsBuffer.Length;
                
                // Update each slot's local offset based on formation type
                for (int i = 0; i < totalSlots; i++)
                {
                    var slot = slotsBuffer[i];
                    
                    // Calculate new offset
                    slot.LocalOffset = FormationLayout.GetSlotOffset(
                        formationState.ValueRO.Type,
                        i,
                        totalSlots,
                        formationState.ValueRO.Spacing);
                    
                    slot.Role = FormationLayout.GetSlotRole(
                        formationState.ValueRO.Type,
                        i,
                        totalSlots);
                    
                    slotsBuffer[i] = slot;
                }

                formationState.ValueRW.LastUpdateTick = currentTick;
            }
        }
    }

    /// <summary>
    /// System that updates formation members' target positions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FormationSlotUpdateSystem))]
    [BurstCompile]
    public partial struct FormationMemberPositionSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            // Update each member's target position
            foreach (var (member, transform, entity) in 
                SystemAPI.Query<RefRW<FormationMember>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (member.ValueRO.FormationEntity == Entity.Null)
                    continue;

                if (!SystemAPI.HasComponent<FormationState>(member.ValueRO.FormationEntity))
                    continue;

                var formationState = SystemAPI.GetComponent<FormationState>(member.ValueRO.FormationEntity);
                
                if (!SystemAPI.HasBuffer<FormationSlot>(member.ValueRO.FormationEntity))
                    continue;

                var slots = SystemAPI.GetBuffer<FormationSlot>(member.ValueRO.FormationEntity);
                
                if (member.ValueRO.SlotIndex >= slots.Length)
                    continue;

                var slot = slots[member.ValueRO.SlotIndex];

                // Calculate world position for this slot
                float3 worldPos = FormationLayout.LocalToWorld(
                    slot.LocalOffset,
                    formationState.AnchorPosition,
                    formationState.AnchorRotation,
                    formationState.Scale);

                member.ValueRW.TargetPosition = worldPos;

                // Check if in position
                float distance = math.distance(transform.ValueRO.Position, worldPos);
                member.ValueRW.IsInPosition = distance <= member.ValueRO.ArrivalThreshold;
            }
        }
    }

    /// <summary>
    /// System that processes formation change requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FormationSlotUpdateSystem))]
    [BurstCompile]
    public partial struct FormationChangeSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process change requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<ChangeFormationRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (!SystemAPI.HasComponent<FormationState>(req.FormationEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var formationState = SystemAPI.GetComponent<FormationState>(req.FormationEntity);
                var oldType = formationState.Type;

                // Update formation
                formationState.Type = req.NewType;
                if (req.NewSpacing > 0)
                    formationState.Spacing = req.NewSpacing;

                SystemAPI.SetComponent(req.FormationEntity, formationState);

                // Emit change event
                if (SystemAPI.HasBuffer<FormationChangedEvent>(req.FormationEntity))
                {
                    var events = SystemAPI.GetBuffer<FormationChangedEvent>(req.FormationEntity);
                    events.Add(new FormationChangedEvent
                    {
                        OldType = oldType,
                        NewType = req.NewType,
                        Tick = currentTick
                    });
                }

                ecb.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// System that processes formation assignment requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FormationChangeSystem))]
    [BurstCompile]
    public partial struct FormationAssignSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process assignment requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<FormationAssignRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (!SystemAPI.HasBuffer<FormationSlot>(req.FormationEntity) ||
                    !SystemAPI.HasComponent<FormationState>(req.FormationEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var slots = SystemAPI.GetBuffer<FormationSlot>(req.FormationEntity);
                var formationState = SystemAPI.GetComponent<FormationState>(req.FormationEntity);

                // Find available slot
                int assignedSlot = -1;
                
                // Try preferred slot first
                if (req.PreferredSlot < 255 && req.PreferredSlot < slots.Length)
                {
                    if (slots[req.PreferredSlot].AssignedEntity == Entity.Null)
                    {
                        assignedSlot = req.PreferredSlot;
                    }
                }

                // Try to find slot with preferred role
                if (assignedSlot < 0 && req.PreferredRole != FormationSlotRole.Any)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i].AssignedEntity == Entity.Null && 
                            slots[i].Role == req.PreferredRole)
                        {
                            assignedSlot = i;
                            break;
                        }
                    }
                }

                // Find any available slot
                if (assignedSlot < 0)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i].AssignedEntity == Entity.Null)
                        {
                            assignedSlot = i;
                            break;
                        }
                    }
                }

                if (assignedSlot >= 0)
                {
                    // Assign to slot
                    var slot = slots[assignedSlot];
                    slot.AssignedEntity = req.UnitEntity;
                    slots[assignedSlot] = slot;

                    // Update formation state
                    formationState.FilledSlots++;
                    SystemAPI.SetComponent(req.FormationEntity, formationState);

                    // Add/update member component on unit
                    if (SystemAPI.HasComponent<FormationMember>(req.UnitEntity))
                    {
                        var member = SystemAPI.GetComponent<FormationMember>(req.UnitEntity);
                        member.FormationEntity = req.FormationEntity;
                        member.SlotIndex = (byte)assignedSlot;
                        member.AssignedTick = currentTick;
                        member.IsInPosition = false;
                        member.ArrivalThreshold = 1f; // Default
                        SystemAPI.SetComponent(req.UnitEntity, member);
                    }
                    else
                    {
                        ecb.AddComponent(req.UnitEntity, new FormationMember
                        {
                            FormationEntity = req.FormationEntity,
                            SlotIndex = (byte)assignedSlot,
                            AssignedTick = currentTick,
                            IsInPosition = false,
                            ArrivalThreshold = 1f
                        });
                    }
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}


using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Calculates world positions for vessels based on their formation assignments.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XFormationSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<FormationTemplate> _templateLookup;
        private BufferLookup<FormationSlotDefinition> _slotLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<FormationAssignment>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _templateLookup = state.GetComponentLookup<FormationTemplate>(true);
            _slotLookup = state.GetBufferLookup<FormationSlotDefinition>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _templateLookup.Update(ref state);
            _slotLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;

            // Update formation positions
            foreach (var (assignment, transform, entity) in SystemAPI.Query<RefRW<FormationAssignment>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                UpdateFormationPosition(
                    ref assignment.ValueRW,
                    ref transform.ValueRW,
                    ref _transformLookup,
                    ref _templateLookup,
                    ref _slotLookup,
                    deltaTime);
            }
        }

        [BurstCompile]
        private static void UpdateFormationPosition(
            ref FormationAssignment assignment,
            ref LocalTransform transform,
            ref ComponentLookup<LocalTransform> transformLookup,
            ref ComponentLookup<FormationTemplate> templateLookup,
            ref BufferLookup<FormationSlotDefinition> slotLookup,
            float deltaTime)
        {
            // Skip if no leader assigned
            if (assignment.FormationLeader == Entity.Null)
            {
                return;
            }

            // Get leader transform
            if (!transformLookup.HasComponent(assignment.FormationLeader))
            {
                return;
            }

            var leaderTransform = transformLookup[assignment.FormationLeader];

            // Get slot offset - either from template or current assignment
            float3 slotOffset = assignment.CurrentOffset;

            // Check if leader has slot definitions
            if (slotLookup.HasBuffer(assignment.FormationLeader))
            {
                var slots = slotLookup[assignment.FormationLeader];
                if (assignment.SlotIndex < slots.Length)
                {
                    slotOffset = slots[assignment.SlotIndex].Slot.Offset;
                }
            }

            // Calculate target position
            assignment.TargetPosition = FormationUtility.CalculateSlotWorldPosition(
                leaderTransform.Position,
                leaderTransform.Rotation,
                slotOffset);

            // Smoothly interpolate current offset toward slot offset
            float tightness = (float)assignment.FormationTightness;
            assignment.CurrentOffset = math.lerp(assignment.CurrentOffset, slotOffset, deltaTime * tightness * 2f);
        }
    }

    /// <summary>
    /// Applies formation target positions to vessel AI state for movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XFormationSystem))]
    public partial struct Space4XFormationMovementBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<FormationAssignment>();
            state.RequireForUpdate<VesselAIState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Update vessel AI targets based on formation
            foreach (var (assignment, aiState, entity) in SystemAPI.Query<RefRO<FormationAssignment>, RefRW<VesselAIState>>().WithEntityAccess())
            {
                // Only update if vessel is in formation-following mode
                if (aiState.ValueRO.CurrentGoal == VesselAIState.Goal.Formation)
                {
                    // Set target to formation position
                    aiState.ValueRW.TargetPosition = assignment.ValueRO.TargetPosition;
                    aiState.ValueRW.TargetEntity = assignment.ValueRO.FormationLeader;

                    // Set to moving if not at position
                    var distanceSq = math.distancesq(aiState.ValueRO.TargetPosition, float3.zero);
                    if (distanceSq > 0.01f && aiState.ValueRO.CurrentState == VesselAIState.State.Idle)
                    {
                        aiState.ValueRW.CurrentState = VesselAIState.State.MovingToTarget;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Auto-assigns vessels to formation slots based on role and proximity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XFormationSystem))]
    public partial struct Space4XFormationAssignmentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Find unassigned vessels that need formation placement
            foreach (var (assignment, transform, entity) in SystemAPI.Query<RefRW<FormationAssignment>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Skip if already assigned
                if (assignment.ValueRO.AssignedTick > 0)
                {
                    continue;
                }

                // Assign to next available slot
                if (assignment.ValueRO.FormationLeader != Entity.Null)
                {
                    assignment.ValueRW.AssignedTick = currentTick;
                }
            }
        }
    }
}


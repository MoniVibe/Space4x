using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Extraction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Extraction
{
    /// <summary>
    /// Processes extraction requests deterministically.
    /// Assigns agents to harvest slots, tracks progress, and handles timeouts.
    /// Game-agnostic: works for any resource type with harvest slots.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ExtractionQueueSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get or create queue singleton
            Entity queueEntity;
            if (!SystemAPI.TryGetSingletonEntity<ExtractionRequestQueue>(out queueEntity))
            {
                queueEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<ExtractionRequestQueue>(queueEntity);
                state.EntityManager.AddBuffer<ExtractionRequest>(queueEntity);
                state.EntityManager.AddComponentData(queueEntity, ExtractionConfig.Default);
                state.EntityManager.AddComponentData(queueEntity, new ExtractionTelemetry());
            }

            var queue = SystemAPI.GetComponentRW<ExtractionRequestQueue>(queueEntity);
            var requests = state.EntityManager.GetBuffer<ExtractionRequest>(queueEntity);
            var config = SystemAPI.HasComponent<ExtractionConfig>(queueEntity)
                ? SystemAPI.GetComponent<ExtractionConfig>(queueEntity)
                : ExtractionConfig.Default;

            var telemetry = SystemAPI.GetComponentRW<ExtractionTelemetry>(queueEntity);

            // Reset per-tick telemetry
            telemetry.ValueRW.RequestsAssigned = 0;
            telemetry.ValueRW.RequestsCompleted = 0;
            telemetry.ValueRW.RequestsTimedOut = 0;
            telemetry.ValueRW.LastUpdateTick = timeState.Tick;

            // Process pending requests
            var pendingCount = 0;
            var activeCount = 0;
            var processed = 0;

            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var request = requests[i];

                // Check for completed or cancelled
                if (request.Status == ExtractionRequestStatus.Completed ||
                    request.Status == ExtractionRequestStatus.Cancelled)
                {
                    if (request.Status == ExtractionRequestStatus.Completed)
                    {
                        telemetry.ValueRW.RequestsCompleted++;
                    }
                    requests.RemoveAt(i);
                    continue;
                }

                // Check for timeout
                var requestAge = (timeState.Tick - request.RequestTick) * timeState.FixedDeltaTime;
                if (requestAge > config.RequestTimeoutSeconds)
                {
                    // Release slot if assigned
                    if (request.AssignedSlotIndex >= 0 && 
                        state.EntityManager.HasBuffer<HarvestSlot>(request.SourceEntity))
                    {
                        var slots = state.EntityManager.GetBuffer<HarvestSlot>(request.SourceEntity);
                        if (request.AssignedSlotIndex < slots.Length)
                        {
                            var slot = slots[request.AssignedSlotIndex];
                            slot.AssignedAgent = Entity.Null;
                            slots[request.AssignedSlotIndex] = slot;
                        }
                    }

                    telemetry.ValueRW.RequestsTimedOut++;
                    requests.RemoveAt(i);
                    continue;
                }

                // Try to assign pending requests to slots
                if (request.Status == ExtractionRequestStatus.Pending &&
                    processed < config.MaxRequestsPerTick)
                {
                    if (TryAssignSlot(ref state, ref request, timeState.Tick))
                    {
                        request.Status = ExtractionRequestStatus.Assigned;
                        requests[i] = request;
                        telemetry.ValueRW.RequestsAssigned++;
                    }
                    processed++;
                }

                // Count by status
                if (request.Status == ExtractionRequestStatus.Pending)
                {
                    pendingCount++;
                }
                else if (request.Status == ExtractionRequestStatus.Assigned ||
                         request.Status == ExtractionRequestStatus.InProgress)
                {
                    activeCount++;
                }
            }

            queue.ValueRW.PendingCount = pendingCount;
            queue.ValueRW.ActiveCount = activeCount;
            queue.ValueRW.LastProcessedTick = timeState.Tick;

            // Update slot telemetry
            UpdateSlotTelemetry(ref state, ref telemetry.ValueRW);
        }

        private bool TryAssignSlot(
            ref SystemState state,
            ref ExtractionRequest request,
            uint currentTick)
        {
            if (!state.EntityManager.Exists(request.SourceEntity) ||
                !state.EntityManager.HasBuffer<HarvestSlot>(request.SourceEntity))
            {
                return false;
            }

            var slots = state.EntityManager.GetBuffer<HarvestSlot>(request.SourceEntity);
            
            // Find best available slot
            var bestSlotIndex = -1;
            var bestEfficiency = 0f;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                
                // Skip occupied or blocked slots
                if (slot.AssignedAgent != Entity.Null)
                {
                    continue;
                }

                if ((slot.Flags & HarvestSlotFlags.Blocked) != 0)
                {
                    continue;
                }

                // Pick highest efficiency slot
                if (slot.EfficiencyMultiplier > bestEfficiency)
                {
                    bestEfficiency = slot.EfficiencyMultiplier;
                    bestSlotIndex = i;
                }
            }

            if (bestSlotIndex < 0)
            {
                return false;
            }

            // Assign agent to slot
            var assignedSlot = slots[bestSlotIndex];
            assignedSlot.AssignedAgent = request.AgentEntity;
            assignedSlot.LastHarvestTick = currentTick;
            assignedSlot.Flags |= HarvestSlotFlags.PendingArrival;
            slots[bestSlotIndex] = assignedSlot;

            request.AssignedSlotIndex = (sbyte)bestSlotIndex;
            return true;
        }

        private void UpdateSlotTelemetry(
            ref SystemState state,
            ref ExtractionTelemetry telemetry)
        {
            var totalSources = 0;
            var totalSlots = 0;
            var occupiedSlots = 0;

            foreach (var slots in SystemAPI.Query<DynamicBuffer<HarvestSlot>>())
            {
                totalSources++;
                totalSlots += slots.Length;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].AssignedAgent != Entity.Null)
                    {
                        occupiedSlots++;
                    }
                }
            }

            telemetry.TotalSources = totalSources;
            telemetry.TotalSlots = totalSlots;
            telemetry.OccupiedSlots = occupiedSlots;
        }
    }

    /// <summary>
    /// Helper utilities for extraction request submission.
    /// </summary>
    [BurstCompile]
    public static class ExtractionRequestHelpers
    {
        private static bool TryGetQueueEntity(ref SystemState state, out Entity queueEntity)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<ExtractionRequestQueue>());
            return query.TryGetSingletonEntity<ExtractionRequestQueue>(out queueEntity);
        }

        /// <summary>
        /// Submits an extraction request to the queue.
        /// </summary>
        public static void SubmitRequest(
            ref SystemState state,
            Entity sourceEntity,
            Entity agentEntity,
            float amount,
            byte priority,
            uint currentTick)
        {
            if (!TryGetQueueEntity(ref state, out var queueEntity))
            {
                return;
            }

            var requests = state.EntityManager.GetBuffer<ExtractionRequest>(queueEntity);
            requests.Add(new ExtractionRequest
            {
                SourceEntity = sourceEntity,
                AgentEntity = agentEntity,
                RequestedAmount = amount,
                RequestTick = currentTick,
                Priority = priority,
                Status = ExtractionRequestStatus.Pending,
                AssignedSlotIndex = -1
            });

            // Increment submitted count
            var telemetry = state.EntityManager.GetComponentData<ExtractionTelemetry>(queueEntity);
            telemetry.RequestsSubmitted++;
            state.EntityManager.SetComponentData(queueEntity, telemetry);
        }

        /// <summary>
        /// Marks a request as completed.
        /// </summary>
        public static void CompleteRequest(
            ref SystemState state,
            Entity sourceEntity,
            Entity agentEntity)
        {
            if (!TryGetQueueEntity(ref state, out var queueEntity))
            {
                return;
            }

            var requests = state.EntityManager.GetBuffer<ExtractionRequest>(queueEntity);
            
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.SourceEntity == sourceEntity &&
                    request.AgentEntity == agentEntity &&
                    request.Status != ExtractionRequestStatus.Completed)
                {
                    // Release slot
                    if (request.AssignedSlotIndex >= 0 &&
                        state.EntityManager.HasBuffer<HarvestSlot>(sourceEntity))
                    {
                        var slots = state.EntityManager.GetBuffer<HarvestSlot>(sourceEntity);
                        if (request.AssignedSlotIndex < slots.Length)
                        {
                            var slot = slots[request.AssignedSlotIndex];
                            slot.AssignedAgent = Entity.Null;
                            slots[request.AssignedSlotIndex] = slot;
                        }
                    }

                    request.Status = ExtractionRequestStatus.Completed;
                    requests[i] = request;
                    break;
                }
            }
        }

        /// <summary>
        /// Cancels a pending request.
        /// </summary>
        public static void CancelRequest(
            ref SystemState state,
            Entity sourceEntity,
            Entity agentEntity)
        {
            if (!TryGetQueueEntity(ref state, out var queueEntity))
            {
                return;
            }

            var requests = state.EntityManager.GetBuffer<ExtractionRequest>(queueEntity);
            
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.SourceEntity == sourceEntity &&
                    request.AgentEntity == agentEntity &&
                    request.Status != ExtractionRequestStatus.Completed &&
                    request.Status != ExtractionRequestStatus.Cancelled)
                {
                    // Release slot
                    if (request.AssignedSlotIndex >= 0 &&
                        state.EntityManager.HasBuffer<HarvestSlot>(sourceEntity))
                    {
                        var slots = state.EntityManager.GetBuffer<HarvestSlot>(sourceEntity);
                        if (request.AssignedSlotIndex < slots.Length)
                        {
                            var slot = slots[request.AssignedSlotIndex];
                            slot.AssignedAgent = Entity.Null;
                            slots[request.AssignedSlotIndex] = slot;
                        }
                    }

                    request.Status = ExtractionRequestStatus.Cancelled;
                    requests[i] = request;
                    break;
                }
            }
        }
    }
}


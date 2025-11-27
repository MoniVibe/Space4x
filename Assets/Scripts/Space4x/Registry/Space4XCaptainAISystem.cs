using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Processes captain orders through the pipeline: receive, validate, execute, feedback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XMoraleSystem))]
    public partial struct Space4XCaptainOrderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainOrder>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.Time.ElapsedTime;

            foreach (var (order, captainState, entity) in
                SystemAPI.Query<RefRW<CaptainOrder>, RefRW<CaptainState>>()
                    .WithEntityAccess())
            {
                ProcessOrderPipeline(ref order.ValueRW, ref captainState.ValueRW, (uint)currentTick, entity);
            }
        }

        private void ProcessOrderPipeline(ref CaptainOrder order, ref CaptainState state, uint currentTick, Entity entity)
        {
            switch (order.Status)
            {
                case CaptainOrderStatus.Received:
                    // Advance to validation
                    order.Status = CaptainOrderStatus.Validating;
                    state.LastEvaluationTick = currentTick;
                    break;

                case CaptainOrderStatus.Validating:
                    // Check if order can be executed
                    if (state.IsReady == 1)
                    {
                        order.Status = CaptainOrderStatus.PreFlight;
                    }
                    break;

                case CaptainOrderStatus.PreFlight:
                    // Pre-flight checks handled by readiness system
                    if (state.IsReady == 1)
                    {
                        order.Status = CaptainOrderStatus.Executing;
                    }
                    break;

                case CaptainOrderStatus.Executing:
                    // Check for timeout
                    if (order.TimeoutTick > 0 && currentTick >= order.TimeoutTick)
                    {
                        order.Status = CaptainOrderStatus.Failed;
                        state.FailureCount++;
                    }
                    break;

                case CaptainOrderStatus.Completed:
                    state.SuccessCount++;
                    // Clear order for next assignment
                    order.Type = CaptainOrderType.None;
                    order.Status = CaptainOrderStatus.None;
                    break;

                case CaptainOrderStatus.Failed:
                case CaptainOrderStatus.Cancelled:
                case CaptainOrderStatus.Escalated:
                    // Clear order for next assignment
                    order.Type = CaptainOrderType.None;
                    order.Status = CaptainOrderStatus.None;
                    break;
            }
        }
    }

    /// <summary>
    /// Evaluates captain readiness with alignment-influenced thresholds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XCaptainReadinessSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainReadiness>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (readiness, captainState, entity) in
                SystemAPI.Query<RefRW<CaptainReadiness>, RefRW<CaptainState>>()
                    .WithEntityAccess())
            {
                // Get alignment if present
                AlignmentTriplet alignment = default;
                if (SystemAPI.HasComponent<AlignmentTriplet>(entity))
                {
                    alignment = SystemAPI.GetComponent<AlignmentTriplet>(entity);
                }

                // Get hull state if present
                float hullRatio = 1f;
                if (SystemAPI.HasComponent<HullIntegrity>(entity))
                {
                    var hull = SystemAPI.GetComponent<HullIntegrity>(entity);
                    hullRatio = (float)hull.Current / math.max((float)hull.Max, 0.01f);
                }

                // Get morale if present
                float morale = 0f;
                if (SystemAPI.HasComponent<MoraleState>(entity))
                {
                    morale = (float)SystemAPI.GetComponent<MoraleState>(entity).Current;
                }

                // Get resource levels if present
                float fuelRatio = 1f;
                float ammoRatio = 1f;
                if (SystemAPI.HasComponent<VesselResourceLevels>(entity))
                {
                    var resources = SystemAPI.GetComponent<VesselResourceLevels>(entity);
                    fuelRatio = (float)resources.FuelRatio;
                    ammoRatio = (float)resources.AmmoRatio;
                }

                // Evaluate readiness with alignment adjustment
                EvaluateReadiness(
                    ref readiness.ValueRW,
                    ref captainState.ValueRW,
                    alignment,
                    hullRatio,
                    morale,
                    fuelRatio,
                    ammoRatio,
                    0f // TODO: Get actual threat level from threat system
                );
            }
        }

        private void EvaluateReadiness(
            ref CaptainReadiness readiness,
            ref CaptainState state,
            in AlignmentTriplet alignment,
            float hullRatio,
            float morale,
            float fuelRatio,
            float ammoRatio,
            float threatLevel)
        {
            // Calculate risk tolerance
            state.RiskTolerance = (half)CaptainAIUtility.CalculateRiskTolerance(alignment);

            // Adjust thresholds based on risk tolerance
            float tolerance = (float)state.RiskTolerance;
            float hullThreshold = (float)readiness.MinHullRatio * (1f - tolerance * 0.5f);
            float fuelThreshold = (float)readiness.MinFuelRatio * (1f - tolerance * 0.5f);
            float ammoThreshold = (float)readiness.MinAmmoRatio * (1f - tolerance * 0.5f);
            float moraleThreshold = (float)readiness.MinMorale * (1f - tolerance * 0.5f);
            float threatThreshold = math.lerp((float)readiness.MaxThreatLevel, 1f, tolerance * 0.5f);

            // Check each threshold
            ReadinessFlags failed = ReadinessFlags.None;
            float checksPassed = 0f;
            float totalChecks = 5f;

            if (hullRatio >= hullThreshold)
            {
                checksPassed++;
            }
            else
            {
                failed |= ReadinessFlags.Hull;
            }

            if (morale >= moraleThreshold)
            {
                checksPassed++;
            }
            else
            {
                failed |= ReadinessFlags.Morale;
            }

            if (fuelRatio >= fuelThreshold)
            {
                checksPassed++;
            }
            else
            {
                failed |= ReadinessFlags.Fuel;
            }

            if (ammoRatio >= ammoThreshold)
            {
                checksPassed++;
            }
            else
            {
                failed |= ReadinessFlags.Ammo;
            }

            if (threatLevel <= threatThreshold)
            {
                checksPassed++;
            }
            else
            {
                failed |= ReadinessFlags.Threat;
            }

            // Update readiness state
            readiness.FailedChecks = failed;
            readiness.CurrentReadiness = (half)(checksPassed / totalChecks);
            state.IsReady = (byte)(failed == ReadinessFlags.None ? 1 : 0);

            // Calculate confidence based on readiness and past performance
            float performanceRatio = state.SuccessCount / math.max(state.SuccessCount + state.FailureCount, 1f);
            state.Confidence = (half)((float)readiness.CurrentReadiness * 0.6f + performanceRatio * 0.4f);
        }
    }

    /// <summary>
    /// Handles escalation requests from captains.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XCaptainEscalationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EscalationRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (escalations, order, captainState, readiness, entity) in
                SystemAPI.Query<DynamicBuffer<EscalationRequest>, RefRW<CaptainOrder>, RefRW<CaptainState>, RefRO<CaptainReadiness>>()
                    .WithEntityAccess())
            {
                // Check if captain needs to escalate
                if (order.ValueRO.Status == CaptainOrderStatus.Executing)
                {
                    CheckForEscalation(escalations, ref order.ValueRW, ref captainState.ValueRW, readiness.ValueRO, currentTick);
                }

                // Process acknowledged escalations
                ProcessAcknowledgedEscalations(escalations, ref order.ValueRW);
            }
        }

        private void CheckForEscalation(
            DynamicBuffer<EscalationRequest> escalations,
            ref CaptainOrder order,
            ref CaptainState state,
            CaptainReadiness readiness,
            uint currentTick)
        {
            // Don't escalate if autonomy doesn't allow or already escalated
            if (state.Autonomy == CaptainAutonomy.Strict)
            {
                return;
            }

            // Check for critical conditions
            if ((readiness.FailedChecks & ReadinessFlags.Hull) != 0 &&
                (float)readiness.CurrentReadiness < 0.3f)
            {
                AddEscalation(escalations, EscalationType.Repair, EscalationReason.HullCritical, currentTick);
            }

            if ((readiness.FailedChecks & ReadinessFlags.Fuel) != 0)
            {
                AddEscalation(escalations, EscalationType.Resupply, EscalationReason.ResourcesDepleted, currentTick);
            }

            if ((readiness.FailedChecks & ReadinessFlags.Threat) != 0 &&
                state.Autonomy >= CaptainAutonomy.Operational)
            {
                AddEscalation(escalations, EscalationType.Reinforcement, EscalationReason.ThreatLevelExceeded, currentTick);
            }
        }

        private void AddEscalation(DynamicBuffer<EscalationRequest> escalations, EscalationType type, EscalationReason reason, uint tick)
        {
            // Check if this type of escalation is already pending
            for (int i = 0; i < escalations.Length; i++)
            {
                if (escalations[i].Type == type && escalations[i].Acknowledged == 0)
                {
                    return;
                }
            }

            // Add new escalation
            escalations.Add(new EscalationRequest
            {
                Type = type,
                Priority = (byte)(type == EscalationType.Evacuation ? 0 : 5),
                Reason = reason,
                RequestTick = tick,
                Acknowledged = 0
            });
        }

        private void ProcessAcknowledgedEscalations(DynamicBuffer<EscalationRequest> escalations, ref CaptainOrder order)
        {
            for (int i = escalations.Length - 1; i >= 0; i--)
            {
                if (escalations[i].Acknowledged == 1)
                {
                    var escalation = escalations[i];

                    // Handle based on type
                    if (escalation.Type == EscalationType.AbortMission)
                    {
                        order.Status = CaptainOrderStatus.Cancelled;
                    }

                    // Remove processed escalation
                    escalations.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Handles alignment-influenced decision making for captains.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainReadinessSystem))]
    public partial struct Space4XCaptainAlignmentBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainState>();
            state.RequireForUpdate<AlignmentTriplet>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (captainState, order, alignment, entity) in
                SystemAPI.Query<RefRW<CaptainState>, RefRW<CaptainOrder>, RefRO<AlignmentTriplet>>()
                    .WithEntityAccess())
            {
                if (order.ValueRO.Status != CaptainOrderStatus.Executing)
                {
                    continue;
                }

                // Good captains check for allies in distress
                if ((float)alignment.ValueRO.Good > 0.3f)
                {
                    // Would check for nearby allied entities in distress
                    // For now, this is a placeholder for the logic
                }

                // Evil captains check for opportunities
                if ((float)alignment.ValueRO.Good < -0.3f)
                {
                    // Would check for opportunistic targets
                    // For now, this is a placeholder for the logic
                }
            }
        }
    }

    /// <summary>
    /// Telemetry system for captain AI metrics.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XCaptainTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalCaptains = 0;
            int readyCaptains = 0;
            int executingOrders = 0;
            int pendingEscalations = 0;
            float avgConfidence = 0f;

            foreach (var (captainState, order) in
                SystemAPI.Query<RefRO<CaptainState>, RefRO<CaptainOrder>>())
            {
                totalCaptains++;
                if (captainState.ValueRO.IsReady == 1)
                {
                    readyCaptains++;
                }
                if (order.ValueRO.Status == CaptainOrderStatus.Executing)
                {
                    executingOrders++;
                }
                avgConfidence += (float)captainState.ValueRO.Confidence;
            }

            foreach (var escalations in SystemAPI.Query<DynamicBuffer<EscalationRequest>>())
            {
                for (int i = 0; i < escalations.Length; i++)
                {
                    if (escalations[i].Acknowledged == 0)
                    {
                        pendingEscalations++;
                    }
                }
            }

            if (totalCaptains > 0)
            {
                avgConfidence /= totalCaptains;
            }

            // Emit telemetry (would integrate with TelemetryStream)
            // UnityEngine.Debug.Log($"[Captain AI] Total: {totalCaptains}, Ready: {readyCaptains}, Executing: {executingOrders}, Escalations: {pendingEscalations}, AvgConfidence: {avgConfidence:F2}");
        }
    }
}


using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Evaluates automation policies and triggers automated behaviors.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4X.Systems.AI.Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(Space4XRecallSystem))]
    public partial struct Space4XAutomationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AutomationPolicy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var resourceLevelsLookup = state.GetComponentLookup<VesselResourceLevels>(true);
            var hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            var repairLookup = state.GetComponentLookup<DockyardRepairRequest>(false);
            var miningLookup = state.GetComponentLookup<MiningVessel>(true);
            var aiLookup = state.GetComponentLookup<VesselAIState>(false);
            var stanceLookup = state.GetComponentLookup<VesselStanceComponent>(false);
            resourceLevelsLookup.Update(ref state);
            hullLookup.Update(ref state);
            repairLookup.Update(ref state);
            miningLookup.Update(ref state);
            aiLookup.Update(ref state);
            stanceLookup.Update(ref state);

            // Process vessels with automation policies
            foreach (var (policy, thresholds, automationState, entity) in
                SystemAPI.Query<RefRO<AutomationPolicy>, RefRO<AutomationThresholds>, RefRW<AutomationState>>()
                    .WithEntityAccess())
            {
                // Check for suspension expiry
                if (automationState.ValueRO.IsSuspended &&
                    automationState.ValueRO.SuspendUntilTick > 0 &&
                    currentTick >= automationState.ValueRO.SuspendUntilTick)
                {
                    automationState.ValueRW.Suspended = 0;
                    automationState.ValueRW.SuspendUntilTick = 0;
                }

                // Skip if suspended
                if (automationState.ValueRO.IsSuspended)
                {
                    continue;
                }

                EvaluateAutomation(
                    in entity,
                    policy.ValueRO,
                    thresholds.ValueRO,
                    ref automationState.ValueRW,
                    ref resourceLevelsLookup,
                    ref hullLookup,
                    ref repairLookup,
                    ref miningLookup,
                    ref aiLookup,
                    ref stanceLookup,
                    currentTick);
            }
        }

        [BurstCompile]
        private static void EvaluateAutomation(
            in Entity entity,
            in AutomationPolicy policy,
            in AutomationThresholds thresholds,
            ref AutomationState automationState,
            ref ComponentLookup<VesselResourceLevels> resourceLevelsLookup,
            ref ComponentLookup<HullIntegrity> hullLookup,
            ref ComponentLookup<DockyardRepairRequest> repairLookup,
            ref ComponentLookup<MiningVessel> miningLookup,
            ref ComponentLookup<VesselAIState> aiLookup,
            ref ComponentLookup<VesselStanceComponent> stanceLookup,
            uint currentTick)
        {
            // Check AutoEvade based on hull
            if (policy.HasFlag(AutomationFlags.AutoEvade) &&
                resourceLevelsLookup.HasComponent(entity))
            {
                var resourceLevels = resourceLevelsLookup[entity];
                if (resourceLevels.HullRatio <= (float)thresholds.EvadeThreshold)
                {
                    TriggerAutomation(ref automationState, AutomationFlags.AutoEvade, Entity.Null, currentTick);

                    // Also update stance to Evasive if the entity has one
                    if (stanceLookup.HasComponent(entity))
                    {
                        var stanceRef = stanceLookup.GetRefRW(entity);
                        stanceRef.ValueRW.CurrentStance = VesselStanceMode.Evasive;
                        stanceRef.ValueRW.DesiredStance = VesselStanceMode.Evasive;
                        stanceRef.ValueRW.StanceChangeTick = currentTick;
                    }
                }
            }

            // Check AutoRepair based on hull
            if (policy.HasFlag(AutomationFlags.AutoRepair) &&
                hullLookup.HasComponent(entity))
            {
                var hull = hullLookup[entity];
                float hullRatio = hull.Max > 0 ? hull.Current / hull.Max : 1f;
                if (hullRatio <= (float)thresholds.RepairThreshold)
                {
                    TriggerAutomation(ref automationState, AutomationFlags.AutoRepair, Entity.Null, currentTick);

                    // Set flag requesting repair
                    if (repairLookup.HasComponent(entity))
                    {
                        var repairRef = repairLookup.GetRefRW(entity);
                        repairRef.ValueRW.RequestTick = currentTick;
                        repairRef.ValueRW.Priority = 1;
                    }
                }
            }

            // Check AutoReturn based on cargo for mining vessels
            if (policy.HasFlag(AutomationFlags.AutoReturn) &&
                miningLookup.HasComponent(entity))
            {
                var vessel = miningLookup[entity];
                float cargoRatio = vessel.CargoCapacity > 0 ? vessel.CurrentCargo / vessel.CargoCapacity : 0f;

                // Return when cargo is full (inverse of threshold logic)
                if (cargoRatio >= (1f - (float)thresholds.ReturnThreshold))
                {
                    TriggerAutomation(ref automationState, AutomationFlags.AutoReturn, Entity.Null, currentTick);

                    // Update AI state to returning
                    if (aiLookup.HasComponent(entity))
                    {
                        var aiStateRef = aiLookup.GetRefRW(entity);
                        aiStateRef.ValueRW.CurrentGoal = VesselAIState.Goal.Returning;
                        aiStateRef.ValueRW.CurrentState = VesselAIState.State.Returning;
                        aiStateRef.ValueRW.StateTimer = 0f;
                        aiStateRef.ValueRW.StateStartTick = currentTick;
                    }
                }
            }

            // Check StanceEscalation based on nearby threats
            if (policy.HasFlag(AutomationFlags.StanceEscalation) &&
                stanceLookup.HasComponent(entity))
            {
                var stanceRef = stanceLookup.GetRefRW(entity);

                // For now, just ensure stance isn't already escalated
                if (stanceRef.ValueRO.CurrentStance == VesselStanceMode.Balanced)
                {
                    // Would check nearby threats here
                    // If threat detected, escalate to Defensive or Aggressive based on policy
                }
            }
        }

        [BurstCompile]
        private static void TriggerAutomation(
            ref AutomationState state,
            AutomationFlags behavior,
            in Entity target,
            uint currentTick)
        {
            state.ActiveBehavior |= behavior;
            state.BehaviorStartTick = currentTick;
            state.BehaviorTarget = target;
        }
    }

    /// <summary>
    /// Clears completed automation behaviors.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAutomationSystem))]
    public partial struct Space4XAutomationCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<AutomationState>();
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

            foreach (var (automationState, entity) in SystemAPI.Query<RefRW<AutomationState>>().WithEntityAccess())
            {
                // Check if behaviors should be cleared based on completion conditions

                // AutoEvade clears when hull is restored
                if ((automationState.ValueRO.ActiveBehavior & AutomationFlags.AutoEvade) != 0)
                {
                    if (SystemAPI.HasComponent<VesselResourceLevels>(entity))
                    {
                        var resourceLevels = SystemAPI.GetComponent<VesselResourceLevels>(entity);
                        if (resourceLevels.HullRatio > 0.5f) // Restored above 50%
                        {
                            automationState.ValueRW.ActiveBehavior &= ~AutomationFlags.AutoEvade;
                        }
                    }
                }

                // AutoReturn clears when cargo is empty
                if ((automationState.ValueRO.ActiveBehavior & AutomationFlags.AutoReturn) != 0)
                {
                    if (SystemAPI.HasComponent<MiningVessel>(entity))
                    {
                        var vessel = SystemAPI.GetComponent<MiningVessel>(entity);
                        if (vessel.CurrentCargo <= 0f)
                        {
                            automationState.ValueRW.ActiveBehavior &= ~AutomationFlags.AutoReturn;
                        }
                    }
                }

                // Clear all behaviors after timeout (5 minutes = 300 seconds * 60 ticks)
                uint behaviorTimeout = 18000;
                if (automationState.ValueRO.BehaviorStartTick > 0 &&
                    currentTick - automationState.ValueRO.BehaviorStartTick > behaviorTimeout)
                {
                    automationState.ValueRW.ActiveBehavior = AutomationFlags.None;
                    automationState.ValueRW.BehaviorTarget = Entity.Null;
                }
            }
        }
    }

    /// <summary>
    /// Logs automation events for debugging and UI.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAutomationCleanupSystem))]
    public partial struct Space4XAutomationLoggingSystem : ISystem
    {
        private AutomationFlags _lastKnownBehaviors;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _lastKnownBehaviors = AutomationFlags.None;
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

            foreach (var (automationState, logBuffer, entity) in
                SystemAPI.Query<RefRO<AutomationState>, DynamicBuffer<AutomationEventLog>>()
                    .WithEntityAccess())
            {
                var currentBehaviors = automationState.ValueRO.ActiveBehavior;

                // Log new behaviors
                var newBehaviors = currentBehaviors & ~_lastKnownBehaviors;
                if (newBehaviors != AutomationFlags.None)
                {
                    // Add log entry for each new behavior
                    for (int i = 0; i < 16; i++)
                    {
                        var flag = (AutomationFlags)(1 << i);
                        if ((newBehaviors & flag) != 0)
                        {
                            // Keep buffer size manageable
                            if (logBuffer.Length >= 16)
                            {
                                logBuffer.RemoveAt(0);
                            }

                            logBuffer.Add(new AutomationEventLog
                            {
                                Behavior = flag,
                                Tick = currentTick,
                                Success = 1,
                                Target = automationState.ValueRO.BehaviorTarget
                            });
                        }
                    }
                }

                _lastKnownBehaviors = currentBehaviors;
            }
        }
    }
}


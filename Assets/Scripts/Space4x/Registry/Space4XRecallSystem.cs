using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Checks recall thresholds and triggers vessel recall when resources are low.
    /// Runs before VesselAISystem to allow it to handle recall targets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4X.Systems.AI.Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.VesselAISystem))]
    public partial struct Space4XRecallSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RecallThresholds>();
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

            // Build lookup for carrier positions
            var carrierLookup = state.GetComponentLookup<Carrier>(true);
            var transformLookup = state.GetComponentLookup<LocalTransform>(true);
            carrierLookup.Update(ref state);
            transformLookup.Update(ref state);

            // Collect carrier entities for finding nearest
            var carriers = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                carriers.Add(entity);
            }

            // Check recall thresholds for vessels with resource levels
            foreach (var (thresholds, recallState, resourceLevels, aiState, transform, entity) in
                SystemAPI.Query<RefRO<RecallThresholds>, RefRW<RecallState>, RefRO<VesselResourceLevels>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                var carrierArray = carriers.AsArray();
                UpdateRecallState(
                    ref recallState.ValueRW,
                    ref aiState.ValueRW,
                    thresholds.ValueRO,
                    resourceLevels.ValueRO,
                    transform.ValueRO,
                    in carrierArray,
                    ref transformLookup,
                    currentTick);
            }

            // Also check vessels that just have thresholds but no resource tracking
            // (they might use MiningVessel cargo as a proxy)
            foreach (var (thresholds, recallState, vessel, aiState, transform, entity) in
                SystemAPI.Query<RefRO<RecallThresholds>, RefRW<RecallState>, RefRO<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                    .WithNone<VesselResourceLevels>()
                    .WithEntityAccess())
            {
                // Use cargo level as proxy for "ammo" (resources gathered)
                var cargoRatio = vessel.ValueRO.CargoCapacity > 0
                    ? vessel.ValueRO.CurrentCargo / vessel.ValueRO.CargoCapacity
                    : 1f;

                var proxyLevels = new VesselResourceLevels
                {
                    MaxAmmo = 100f,
                    CurrentAmmo = 100f, // Mining vessels don't run out of ammo
                    MaxFuel = 100f,
                    CurrentFuel = 100f, // Simplified - assume infinite fuel for now
                    MaxHull = 100f,
                    CurrentHull = 100f  // Hull damage tracked elsewhere
                };

                var carrierArray = carriers.AsArray();
                UpdateRecallState(
                    ref recallState.ValueRW,
                    ref aiState.ValueRW,
                    thresholds.ValueRO,
                    proxyLevels,
                    transform.ValueRO,
                    in carrierArray,
                    ref transformLookup,
                    currentTick);
            }

            carriers.Dispose();
        }

        [BurstCompile]
        private static void UpdateRecallState(
            ref RecallState recallState,
            ref VesselAIState aiState,
            in RecallThresholds thresholds,
            in VesselResourceLevels resourceLevels,
            in LocalTransform transform,
            in NativeArray<Entity> carriers,
            ref ComponentLookup<LocalTransform> transformLookup,
            uint currentTick)
        {
            // Update current resource levels
            recallState.CurrentAmmo = (half)resourceLevels.AmmoRatio;
            recallState.CurrentFuel = (half)resourceLevels.FuelRatio;
            recallState.CurrentHull = (half)resourceLevels.HullRatio;

            // Check if currently recalling
            if (recallState.IsRecalling == 1)
            {
                // Check if recall is complete (at carrier)
                if (aiState.CurrentState == VesselAIState.State.Idle && aiState.CurrentGoal == VesselAIState.Goal.Returning)
                {
                    // Recall complete, reset state
                    recallState.IsRecalling = 0;
                    recallState.Reason = RecallReason.None;
                }
                return;
            }

            // Check if we should trigger recall
            if (!RecallUtility.ShouldTriggerRecall(thresholds, resourceLevels, out var reason))
            {
                return;
            }

            // Trigger recall
            recallState.IsRecalling = 1;
            recallState.Reason = reason;
            recallState.RecallStartTick = currentTick;

            // Set AI state to return
            aiState.CurrentGoal = VesselAIState.Goal.Returning;
            aiState.CurrentState = VesselAIState.State.Returning;
            aiState.StateTimer = 0f;
            aiState.StateStartTick = currentTick;

            // Find recall target
            Entity recallTarget = thresholds.RecallTarget;

            // If no specific target, find nearest carrier
            if (recallTarget == Entity.Null && carriers.Length > 0)
            {
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < carriers.Length; i++)
                {
                    if (!transformLookup.HasComponent(carriers[i]))
                    {
                        continue;
                    }

                    var carrierTransform = transformLookup[carriers[i]];
                    var distance = math.distancesq(transform.Position, carrierTransform.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        recallTarget = carriers[i];
                    }
                }
            }

            if (recallTarget != Entity.Null)
            {
                aiState.TargetEntity = recallTarget;
                // TargetPosition will be resolved by VesselTargetingSystem
            }
            else
            {
                // No carrier, return to origin
                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = float3.zero;
            }
        }
    }

    /// <summary>
    /// Updates VesselResourceLevels from combat/damage systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XRecallSystem))]
    public partial struct Space4XResourceLevelUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<VesselResourceLevels>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;

            // Consume fuel over time for active vessels
            foreach (var (resourceLevels, aiState, entity) in SystemAPI.Query<RefRW<VesselResourceLevels>, RefRO<VesselAIState>>().WithEntityAccess())
            {
                // Only consume fuel when moving
                if (aiState.ValueRO.CurrentState == VesselAIState.State.MovingToTarget ||
                    aiState.ValueRO.CurrentState == VesselAIState.State.Returning)
                {
                    // Slow fuel consumption
                    float fuelConsumption = 0.1f * deltaTime; // 0.1 fuel per second
                    resourceLevels.ValueRW.CurrentFuel = math.max(0f, resourceLevels.ValueRO.CurrentFuel - fuelConsumption);
                }
            }
        }
    }
}


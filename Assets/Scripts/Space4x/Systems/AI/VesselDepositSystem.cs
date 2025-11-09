using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Handles vessels depositing resources to carriers when they return.
    /// Similar to ResourceDepositSystem but for vessels and carriers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselGatheringSystem))]
    public partial struct VesselDepositSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private BufferLookup<ResourceStorage> _carrierInventoryLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _carrierLookup = state.GetComponentLookup<Carrier>(false);
            _carrierInventoryLookup = state.GetBufferLookup<ResourceStorage>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningVessel>();
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

            _carrierLookup.Update(ref state);
            _carrierInventoryLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var depositDistance = 2f; // Vessels deposit when within 2 units of carrier
            var depositDistanceSq = depositDistance * depositDistance;

            foreach (var (vessel, aiState, transform, entity) in SystemAPI.Query<RefRW<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                // Only deposit if returning and has cargo
                if (aiState.ValueRO.CurrentState != VesselAIState.State.Returning ||
                    vessel.ValueRO.CurrentCargo <= 0f ||
                    aiState.ValueRO.TargetEntity == Entity.Null)
                {
                    continue;
                }

                // Check if target is a carrier
                if (!_carrierLookup.HasComponent(aiState.ValueRO.TargetEntity) ||
                    !_transformLookup.HasComponent(aiState.ValueRO.TargetEntity))
                {
                    continue;
                }

                var carrierTransform = _transformLookup[aiState.ValueRO.TargetEntity];
                var distSq = math.distancesq(transform.ValueRO.Position, carrierTransform.Position);

                if (distSq > depositDistanceSq)
                {
                    // Not close enough yet - keep moving toward carrier
                    continue;
                }

                // Arrived at carrier - stop moving
                // (VesselMovementSystem will handle stopping when in Returning state, but we ensure it here)

                // Close enough to deposit - transfer cargo to carrier
                var cargoToDeposit = vessel.ValueRO.CurrentCargo;
                
                // Add to carrier inventory (using ResourceStorage buffer)
                if (!_carrierInventoryLookup.HasBuffer(aiState.ValueRO.TargetEntity))
                {
                    continue;
                }

                var inventory = _carrierInventoryLookup[aiState.ValueRO.TargetEntity];
                
                // For now, deposit all cargo (simplified - assumes single resource type)
                // TODO: Match resource types properly
                if (inventory.Length > 0)
                {
                    var item = inventory[0];
                    item.Amount += cargoToDeposit;
                    inventory[0] = item;
                }

                // Update vessel cargo
                var vesselValue = vessel.ValueRO;
                vesselValue.CurrentCargo = 0f;
                vessel.ValueRW = vesselValue;

                // If vessel is empty, return to idle to find new target
                aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                aiState.ValueRW.TargetEntity = Entity.Null;
                aiState.ValueRW.TargetPosition = float3.zero;
            }
        }
    }
}


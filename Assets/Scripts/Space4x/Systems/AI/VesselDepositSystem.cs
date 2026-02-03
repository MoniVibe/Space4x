using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace Space4X.Systems.AI
{
    using Debug = UnityEngine.Debug;

    
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
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<MiningYield> _yieldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _carrierLookup = state.GetComponentLookup<Carrier>(false);
            _carrierInventoryLookup = state.GetBufferLookup<ResourceStorage>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _yieldLookup = state.GetComponentLookup<MiningYield>(false);

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
            _statsLookup.Update(ref state);
            _yieldLookup.Update(ref state);

            var depositDistance = 2f; // Vessels deposit when within 2 units of carrier
            var depositDistanceSq = depositDistance * depositDistance;
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            foreach (var (vessel, aiState, transform, entity) in SystemAPI.Query<RefRW<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithNone<MiningState>() // MiningOrder + MiningState pipeline uses CarrierDropoffSystem
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
                var vesselValue = vessel.ValueRO;
                var cargoToDeposit = vesselValue.CurrentCargo;
                var cargoType = vesselValue.CargoResourceType;
                
                // Logistics stat from carrier affects transfer speed/efficiency
                float logisticsBonus = 1f;
                if (_statsLookup.HasComponent(aiState.ValueRO.TargetEntity))
                {
                    var carrierStats = _statsLookup[aiState.ValueRO.TargetEntity];
                    logisticsBonus = 1f + (carrierStats.Logistics / 100f) * 0.25f; // Up to 25% faster transfer
                }
                
                // Add to carrier inventory (using ResourceStorage buffer)
                if (!_carrierInventoryLookup.HasBuffer(aiState.ValueRO.TargetEntity))
                {
                    continue;
                }

                var inventory = _carrierInventoryLookup[aiState.ValueRO.TargetEntity];
                // Apply logistics bonus: higher logistics = more efficient transfer (less waste)
                var effectiveAmount = cargoToDeposit * logisticsBonus;
                var remaining = DepositCargo(ref inventory, cargoType, effectiveAmount);
                var deposited = effectiveAmount - remaining;

                if (deposited <= 1e-4f)
                {
#if UNITY_EDITOR
                    UnityEngine.Debug.LogWarning($"[VesselDepositSystem] Carrier '{aiState.ValueRO.TargetEntity.Index}' cannot accept more cargo of type {cargoType}. Vessels will continue waiting.");
#endif
                    continue;
                }

                if (hasCommandLog)
                {
                    commandLog.Add(new MiningCommandLogEntry
                    {
                        Tick = timeState.Tick,
                        CommandType = MiningCommandType.Pickup,
                        SourceEntity = entity,
                        TargetEntity = aiState.ValueRO.TargetEntity,
                        ResourceType = cargoType,
                        Amount = deposited,
                        Position = carrierTransform.Position
                    });
                }

                // Update global player resources when cargo is deposited
                if (SystemAPI.TryGetSingletonRW<PlayerResources>(out var playerResources))
                {
                    playerResources.ValueRW.AddResource(cargoType, deposited);
                }

                vesselValue.CurrentCargo = remaining;
                if (remaining <= 1e-3f)
                {
                    vesselValue.CurrentCargo = 0f;
                    vessel.ValueRW = vesselValue;

                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    aiState.ValueRW.TargetPosition = float3.zero;
                }
                else
                {
                    vessel.ValueRW = vesselValue;
                }

                if (_yieldLookup.HasComponent(entity))
                {
                    var yield = _yieldLookup[entity];
                    yield.PendingAmount = math.max(0f, vesselValue.CurrentCargo);
                    yield.SpawnReady = yield.SpawnThreshold > 0f && yield.PendingAmount >= yield.SpawnThreshold ? (byte)1 : (byte)0;
                    _yieldLookup[entity] = yield;
                }
            }
        }

        private static float DepositCargo(ref DynamicBuffer<ResourceStorage> storage, ResourceType cargoType, float amount)
        {
            var remaining = amount;
            for (var i = 0; i < storage.Length && remaining > 1e-4f; i++)
            {
                var slot = storage[i];
                if (slot.Type != cargoType)
                {
                    continue;
                }

                remaining = slot.AddAmount(remaining);
                storage[i] = slot;

                if (remaining <= 1e-4f)
                {
                    break;
                }
            }

            return remaining;
        }
    }
}

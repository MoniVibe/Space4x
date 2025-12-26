using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Transfers miner cargo directly into the assigned carrier hold when the vessel is near the carrier.
    /// This provides a deterministic headless-friendly "gather -> return -> dropoff" loop without relying on pickup entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    [UpdateBefore(typeof(MiningResourceSpawnSystem))]
    public partial struct CarrierDropoffSystem : ISystem
    {
        private const float DropoffDistance = 3.5f;
        private const float DropoffDistanceSq = DropoffDistance * DropoffDistance;
        private const float DropoffRatePerSecond = 250f;
        private const float DockingHoldDuration = 1.2f;

        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ResourceStorage> _storageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(false);

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

            _transformLookup.Update(ref state);
            _storageLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;
            var maxTransfer = math.max(0f, DropoffRatePerSecond * deltaTime);
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            foreach (var (vessel, aiState, transform, entity) in SystemAPI
                         .Query<RefRW<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithAll<MiningState>() // Only the MiningOrder + MiningState pipeline uses this dropoff.
                         .WithEntityAccess())
            {
                var vesselValue = vessel.ValueRO;
                if (vesselValue.CarrierEntity == Entity.Null || vesselValue.CurrentCargo <= 0.01f)
                {
                    continue;
                }

                // Only drop off when returning (prevents dumping cargo at the mining site).
                if (aiState.ValueRO.CurrentState != VesselAIState.State.Returning)
                {
                    continue;
                }

                var carrierEntity = vesselValue.CarrierEntity;
                if (!_transformLookup.HasComponent(carrierEntity) || !_storageLookup.HasBuffer(carrierEntity))
                {
                    continue;
                }

                var carrierPos = _transformLookup[carrierEntity].Position;
                var distSq = math.lengthsq(transform.ValueRO.Position - carrierPos);
                if (distSq > DropoffDistanceSq)
                {
                    continue;
                }

                var transferAmount = math.min(vesselValue.CurrentCargo, maxTransfer > 0f ? maxTransfer : vesselValue.CurrentCargo);
                if (transferAmount <= 0.0001f)
                {
                    continue;
                }

                var storage = _storageLookup[carrierEntity];
                var accepted = TransferToStorage(storage, vesselValue.CargoResourceType, transferAmount);
                if (accepted <= 0.0001f)
                {
                    continue;
                }

                vesselValue.CurrentCargo = math.max(0f, vesselValue.CurrentCargo - accepted);
                vessel.ValueRW = vesselValue;

                if (hasCommandLog)
                {
                    commandLog.Add(new MiningCommandLogEntry
                    {
                        Tick = timeState.Tick,
                        CommandType = MiningCommandType.Pickup,
                        SourceEntity = entity,
                        TargetEntity = carrierEntity,
                        ResourceType = vesselValue.CargoResourceType,
                        Amount = accepted,
                        Position = carrierPos
                    });
                }

                if (SystemAPI.TryGetSingletonRW<PlayerResources>(out var playerResources))
                {
                    playerResources.ValueRW.AddResource(vesselValue.CargoResourceType, accepted);
                }

                if (vesselValue.CurrentCargo <= 0.01f)
                {
                    // Reset for another mining cycle.
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Mining;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    aiState.ValueRW.TargetPosition = float3.zero;
                    aiState.ValueRW.StateTimer = 0f;
                    aiState.ValueRW.StateStartTick = timeState.Tick;

                    if (SystemAPI.HasComponent<MiningOrder>(entity))
                    {
                        var order = SystemAPI.GetComponentRW<MiningOrder>(entity).ValueRO;
                        order.Status = MiningOrderStatus.Pending;
                        order.TargetEntity = Entity.Null;
                        order.PreferredTarget = Entity.Null;
                        order.IssuedTick = timeState.Tick;
                        SystemAPI.GetComponentRW<MiningOrder>(entity).ValueRW = order;
                    }

                    if (SystemAPI.HasComponent<MiningState>(entity))
                    {
                        var miningState = SystemAPI.GetComponentRW<MiningState>(entity).ValueRO;
                        miningState.Phase = MiningPhase.Docking;
                        miningState.ActiveTarget = Entity.Null;
                        miningState.MiningTimer = 0f;
                        miningState.PhaseTimer = DockingHoldDuration;
                        SystemAPI.GetComponentRW<MiningState>(entity).ValueRW = miningState;
                    }
                }
            }
        }

        private static float TransferToStorage(DynamicBuffer<ResourceStorage> storage, ResourceType type, float amount)
        {
            var remaining = amount;
            for (var i = 0; i < storage.Length && remaining > 1e-4f; i++)
            {
                var slot = storage[i];
                if (slot.Type != type)
                {
                    continue;
                }

                remaining = slot.AddAmount(remaining);
                storage[i] = slot;
            }

            if (remaining > 1e-4f && storage.Length < 4)
            {
                var slot = ResourceStorage.Create(type);
                remaining = slot.AddAmount(remaining);
                storage.Add(slot);
            }

            return amount - remaining;
        }
    }
}

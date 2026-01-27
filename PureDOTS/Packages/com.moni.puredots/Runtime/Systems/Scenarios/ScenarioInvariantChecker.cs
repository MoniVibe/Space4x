using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Logistics.Contracts;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Power;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using HeadlessInvariantState = PureDOTS.Runtime.Components.HeadlessInvariantState;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// System that checks for invariant violations during headless execution.
    /// Reports violations to ScenarioExitUtility and requests headless exit.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct ScenarioInvariantChecker : ISystem
    {
        private const float NegativeEpsilon = -0.0001f;
        private const float CapacityEpsilon = 0.001f;
        private const float ProgressDistanceSq = 0.0004f;
        private const float DesiredVelocitySq = 0.01f;
        private const uint ProgressTimeoutTicks = 600;
        private const float MaxAngularSpeedRad = math.PI * 4f;
        private const float MaxAngularAccelRad = math.PI * 8f;

        private uint _lastTick;
        private float _lastWorldSeconds;
        private byte _hasLastTick;
        private byte _reportedFailure;
        private EntityQuery _villagerInvariantQuery;
        private EntityQuery _movementInvariantQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            RuntimeMode.RefreshFromEnvironment();
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            _villagerInvariantQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<VillagerMovement>(),
                ComponentType.Exclude<HeadlessInvariantState>());

            _movementInvariantQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<MovementState>(),
                ComponentType.Exclude<HeadlessInvariantState>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_reportedFailure != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;
            var worldSeconds = timeState.WorldSeconds;
            var deltaTime = math.max(1e-5f, timeState.FixedDeltaTime);

            EnsureInvariantState(ref state);

            if (!CheckMonotonicTime(ref state, tick, worldSeconds))
            {
                return;
            }

            if (!CheckForNaNs(ref state, tick, worldSeconds))
            {
                return;
            }

            if (!CheckMovementInvariants(ref state, tick, worldSeconds, deltaTime))
            {
                return;
            }

            if (!CheckEnergyInvariants(ref state, tick, worldSeconds))
            {
                return;
            }

            if (!CheckInventoryInvariants(ref state, tick, worldSeconds))
            {
                return;
            }

            CheckRequiredSingletons(ref state, tick);
            CheckRewindGuards(ref state, tick);
        }

        private void EnsureInvariantState(ref SystemState state)
        {
            if (!_villagerInvariantQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<HeadlessInvariantState>(_villagerInvariantQuery);
            }

            if (!_movementInvariantQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<HeadlessInvariantState>(_movementInvariantQuery);
            }
        }

        private bool CheckMonotonicTime(ref SystemState state, uint tick, float worldSeconds)
        {
            if (_hasLastTick != 0)
            {
                if (tick < _lastTick)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/TimeTick",
                        $"TimeState.Tick regressed from {_lastTick} to {tick}");
                    return false;
                }

                if (worldSeconds + 0.0001f < _lastWorldSeconds)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/TimeSeconds",
                        $"TimeState.WorldSeconds regressed from {_lastWorldSeconds:F3} to {worldSeconds:F3}");
                    return false;
                }
            }

            _lastTick = tick;
            _lastWorldSeconds = worldSeconds;
            _hasLastTick = 1;
            return true;
        }

        private bool CheckForNaNs(ref SystemState state, uint tick, float worldSeconds)
        {
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess())
            {
                var pos = transform.ValueRO.Position;
                if (HasNaNOrInf(pos) || HasNaNOrInf(transform.ValueRO.Rotation.value))
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/NaNTransform",
                        $"NaN/Inf in LocalTransform at tick {tick} entity={entity.Index}",
                        entity, true, pos, true, default, false, transform.ValueRO.Rotation, true);
                    return false;
                }
            }

            return true;
        }

        private bool CheckMovementInvariants(ref SystemState state, uint tick, float worldSeconds, float deltaTime)
        {
            foreach (var (movement, entity) in SystemAPI.Query<RefRO<MovementState>>().WithEntityAccess())
            {
                if (HasNaNOrInf(movement.ValueRO.Vel) || HasNaNOrInf(movement.ValueRO.Desired))
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/NaNMovement",
                        $"NaN/Inf in MovementState at tick {tick} entity={entity.Index}",
                        entity, true, default, false, movement.ValueRO.Vel, true);
                    return false;
                }
            }

            foreach (var (movement, entity) in SystemAPI.Query<RefRO<VillagerMovement>>().WithEntityAccess())
            {
                if (HasNaNOrInf(movement.ValueRO.Velocity) || HasNaNOrInf(movement.ValueRO.DesiredVelocity))
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/NaNVillagerMove",
                        $"NaN/Inf in VillagerMovement at tick {tick} entity={entity.Index}",
                        entity, true, default, false, movement.ValueRO.Velocity, true);
                    return false;
                }
            }

            foreach (var (transform, movement, invariant, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<VillagerMovement>, RefRW<HeadlessInvariantState>>()
                         .WithEntityAccess())
            {
                var wantsMove = movement.ValueRO.IsMoving != 0 ||
                                math.lengthsq(movement.ValueRO.DesiredVelocity) > DesiredVelocitySq;
                if (!UpdateProgressAndRotation(ref state, tick, worldSeconds, deltaTime, entity, transform.ValueRO,
                        movement.ValueRO.DesiredVelocity, wantsMove, ref invariant.ValueRW))
                {
                    return false;
                }
            }

            foreach (var (transform, movement, invariant, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<MovementState>, RefRW<HeadlessInvariantState>>()
                         .WithEntityAccess())
            {
                var wantsMove = math.lengthsq(movement.ValueRO.Desired) > DesiredVelocitySq ||
                                math.lengthsq(movement.ValueRO.Vel) > DesiredVelocitySq;
                if (!UpdateProgressAndRotation(ref state, tick, worldSeconds, deltaTime, entity, transform.ValueRO,
                        movement.ValueRO.Desired, wantsMove, ref invariant.ValueRW))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckEnergyInvariants(ref SystemState state, uint tick, float worldSeconds)
        {
            foreach (var (battery, entity) in SystemAPI.Query<RefRO<PowerBattery>>().WithEntityAccess())
            {
                var stored = battery.ValueRO.CurrentStored;
                if (!math.isfinite(stored) || stored < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerStored",
                        $"PowerBattery.CurrentStored invalid {stored:F3} at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                if (battery.ValueRO.MaxCapacity > 0f && stored - battery.ValueRO.MaxCapacity > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerOvercap",
                        $"PowerBattery.CurrentStored {stored:F3} exceeds MaxCapacity {battery.ValueRO.MaxCapacity:F3} at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            foreach (var (distribution, entity) in SystemAPI.Query<RefRO<PowerDistribution>>().WithEntityAccess())
            {
                if (distribution.ValueRO.OutputPower < NegativeEpsilon || distribution.ValueRO.InputPower < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerDistribution",
                        $"PowerDistribution negative input/output at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                if (distribution.ValueRO.OutputPower - distribution.ValueRO.InputPower > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerCreation",
                        $"PowerDistribution output {distribution.ValueRO.OutputPower:F3} exceeds input {distribution.ValueRO.InputPower:F3} at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            foreach (var (ledger, entity) in SystemAPI.Query<RefRO<PowerLedger>>().WithEntityAccess())
            {
                if (ledger.ValueRO.BatteryStoredMWs < NegativeEpsilon || ledger.ValueRO.SurplusMW < NegativeEpsilon || ledger.ValueRO.DeficitMW < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerLedger",
                        $"PowerLedger has negative values at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                var maxAvailable = ledger.ValueRO.DistributionOutputMW + ledger.ValueRO.BatteryDischargeMW + CapacityEpsilon;
                if (ledger.ValueRO.TotalAllocatedMW - maxAvailable > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/PowerAllocation",
                        $"PowerLedger allocated {ledger.ValueRO.TotalAllocatedMW:F3} exceeds available {maxAvailable:F3} at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            return true;
        }

        private bool CheckInventoryInvariants(ref SystemState state, uint tick, float worldSeconds)
        {
            foreach (var (inventory, entity) in SystemAPI.Query<RefRO<StorehouseInventory>>().WithEntityAccess())
            {
                if (inventory.ValueRO.TotalStored < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/StorehouseNegative",
                        $"StorehouseInventory.TotalStored negative at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                if (inventory.ValueRO.TotalCapacity > 0f &&
                    inventory.ValueRO.TotalStored - inventory.ValueRO.TotalCapacity > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/StorehouseOvercap",
                        $"StorehouseInventory.TotalStored exceeds capacity at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            foreach (var (items, entity) in SystemAPI.Query<DynamicBuffer<StorehouseInventoryItem>>().WithEntityAccess())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.Amount < NegativeEpsilon || item.Reserved < NegativeEpsilon)
                    {
                        ReportInvariant(ref state, tick, worldSeconds, "Invariant/StorehouseItemNegative",
                            $"StorehouseInventoryItem negative at tick {tick} entity={entity.Index}",
                            entity, true);
                        return false;
                    }

                    if (item.Reserved - item.Amount > CapacityEpsilon)
                    {
                        ReportInvariant(ref state, tick, worldSeconds, "Invariant/StorehouseReserve",
                            $"StorehouseInventoryItem reserved exceeds amount at tick {tick} entity={entity.Index}",
                            entity, true);
                        return false;
                    }
                }
            }

            foreach (var (inventory, entity) in SystemAPI.Query<RefRO<Inventory>>().WithEntityAccess())
            {
                if (inventory.ValueRO.CurrentMass < NegativeEpsilon || inventory.ValueRO.CurrentVolume < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/InventoryNegative",
                        $"Inventory current mass/volume negative at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                if (inventory.ValueRO.MaxMass > 0f && inventory.ValueRO.CurrentMass - inventory.ValueRO.MaxMass > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/InventoryOvercap",
                        $"Inventory mass exceeds capacity at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }

                if (inventory.ValueRO.MaxVolume > 0f && inventory.ValueRO.CurrentVolume - inventory.ValueRO.MaxVolume > CapacityEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/InventoryOvercap",
                        $"Inventory volume exceeds capacity at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            foreach (var (items, entity) in SystemAPI.Query<DynamicBuffer<InventoryItem>>().WithEntityAccess())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Quantity < NegativeEpsilon)
                    {
                        ReportInvariant(ref state, tick, worldSeconds, "Invariant/InventoryItemNegative",
                            $"InventoryItem quantity negative at tick {tick} entity={entity.Index}",
                            entity, true);
                        return false;
                    }
                }
            }

            foreach (var (items, entity) in SystemAPI.Query<DynamicBuffer<VillagerInventoryItem>>().WithEntityAccess())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Amount < NegativeEpsilon)
                    {
                        ReportInvariant(ref state, tick, worldSeconds, "Invariant/VillagerInventoryNegative",
                            $"VillagerInventoryItem amount negative at tick {tick} entity={entity.Index}",
                            entity, true);
                        return false;
                    }
                }
            }

            foreach (var (items, entity) in SystemAPI.Query<DynamicBuffer<ContractInventory>>().WithEntityAccess())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Amount < 0)
                    {
                        ReportInvariant(ref state, tick, worldSeconds, "Invariant/ContractInventoryNegative",
                            $"ContractInventory amount negative at tick {tick} entity={entity.Index}",
                            entity, true);
                        return false;
                    }
                }
            }

            foreach (var (chunk, entity) in SystemAPI.Query<RefRO<ResourceChunkState>>().WithEntityAccess())
            {
                if (chunk.ValueRO.Units < NegativeEpsilon)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/ResourceChunkNegative",
                        $"ResourceChunkState.Units negative at tick {tick} entity={entity.Index}",
                        entity, true);
                    return false;
                }
            }

            return true;
        }

        private bool UpdateProgressAndRotation(
            ref SystemState state,
            uint tick,
            float worldSeconds,
            float deltaTime,
            Entity entity,
            in LocalTransform transform,
            float3 desiredVelocity,
            bool wantsMove,
            ref HeadlessInvariantState invariant)
        {
            if (invariant.Initialized == 0)
            {
                invariant.LastPosition = transform.Position;
                invariant.LastRotation = transform.Rotation;
                invariant.LastProgressTick = tick;
                invariant.LastAngularSpeed = 0f;
                invariant.Initialized = 1;
                return true;
            }

            var delta = transform.Position - invariant.LastPosition;
            if (math.lengthsq(delta) > ProgressDistanceSq)
            {
                invariant.LastPosition = transform.Position;
                invariant.LastProgressTick = tick;
            }
            else if (wantsMove && tick - invariant.LastProgressTick > ProgressTimeoutTicks)
            {
                ReportInvariant(ref state, tick, worldSeconds, "Invariant/ProgressStall",
                    $"Movement stalled for {tick - invariant.LastProgressTick} ticks at entity={entity.Index}",
                    entity, true, transform.Position, true, desiredVelocity, true, transform.Rotation, true);
                return false;
            }

            if (wantsMove)
            {
                var dot = math.abs(math.dot(invariant.LastRotation.value, transform.Rotation.value));
                dot = math.clamp(dot, -1f, 1f);
                var angle = 2f * math.acos(dot);
                var angularSpeed = angle / deltaTime;
                var angularAccel = math.abs(angularSpeed - invariant.LastAngularSpeed) / deltaTime;

                if (angularSpeed > MaxAngularSpeedRad)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/TurnRate",
                        $"Turn rate {angularSpeed:F2} rad/s exceeds limit at entity={entity.Index}",
                        entity, true, transform.Position, true, desiredVelocity, true, transform.Rotation, true);
                    return false;
                }

                if (angularAccel > MaxAngularAccelRad)
                {
                    ReportInvariant(ref state, tick, worldSeconds, "Invariant/TurnAccel",
                        $"Turn accel {angularAccel:F2} rad/s^2 exceeds limit at entity={entity.Index}",
                        entity, true, transform.Position, true, desiredVelocity, true, transform.Rotation, true);
                    return false;
                }

                invariant.LastAngularSpeed = angularSpeed;
                invariant.LastRotation = transform.Rotation;
            }

            return true;
        }

        private void CheckRequiredSingletons(ref SystemState state, uint currentTick)
        {
            if (!SystemAPI.HasSingleton<TimeState>())
            {
                ScenarioExitUtility.ReportScenarioContract("MissingSingleton",
                    $"TimeState singleton missing at tick {currentTick}");
            }

            if (!SystemAPI.HasSingleton<RewindState>())
            {
                ScenarioExitUtility.ReportScenarioContract("MissingSingleton",
                    $"RewindState singleton missing at tick {currentTick}");
            }

            if (!SystemAPI.HasSingleton<ScenarioInfo>() && currentTick > 0)
            {
                ScenarioExitUtility.ReportScenarioContract("MissingSingleton",
                    $"ScenarioInfo singleton missing at tick {currentTick}");
            }
        }

        private void CheckRewindGuards(ref SystemState state, uint currentTick)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out _))
            {
                return;
            }

            _ = currentTick;
        }

        private void ReportInvariant(
            ref SystemState state,
            uint tick,
            float worldSeconds,
            string code,
            string message,
            Entity entity = default,
            bool hasEntity = false,
            float3 position = default,
            bool hasPosition = false,
            float3 velocity = default,
            bool hasVelocity = false,
            quaternion rotation = default,
            bool hasRotation = false)
        {
            if (_reportedFailure != 0)
            {
                return;
            }

            _reportedFailure = 1;
            ScenarioExitUtility.ReportInvariant(code, message);
            HeadlessInvariantBundleWriter.TryWriteBundle(
                state.EntityManager,
                code,
                message,
                tick,
                worldSeconds,
                entity,
                hasEntity,
                position,
                hasPosition,
                velocity,
                hasVelocity,
                rotation,
                hasRotation);
            if (ScenarioExitUtility.ResolveExitPolicy() != ExitPolicy.NeverNonZero)
            {
                HeadlessExitUtility.Request(state.EntityManager, tick, 2);
            }
        }

        private static bool HasNaNOrInf(float3 value)
        {
            return math.any(math.isnan(value)) || math.any(math.isinf(value));
        }

        private static bool HasNaNOrInf(float4 value)
        {
            return math.any(math.isnan(value)) || math.any(math.isinf(value));
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XRewindGateHarnessSystem : ISystem
    {
        private static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("scenario.space4x.rewind_gate2");
        private static readonly FixedString64Bytes MetricHand = new FixedString64Bytes("gate2.space4x.actions.hand");
        private static readonly FixedString64Bytes MetricDelivery = new FixedString64Bytes("gate2.space4x.actions.delivery");
        private static readonly FixedString64Bytes MetricEntities = new FixedString64Bytes("gate2.space4x.entities.match");
        private static readonly FixedString64Bytes MetricTotals = new FixedString64Bytes("gate2.space4x.totals.match");
        private static readonly FixedString64Bytes MetricRewind = new FixedString64Bytes("gate2.space4x.rewind.match");
        private static readonly FixedString64Bytes MineralsId = new FixedString64Bytes("minerals");

        private const uint PickupTick = 60;
        private const uint ThrowTick = 120;
        private const uint DeliveryTick = 180;
        private const uint RewindCommandTick = 300;
        private const uint RewindDepthTicks = 200;

        private EntityQuery _carrierQuery;
        private EntityQuery _minerQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _storehouseQuery;

        private Entity _handEntity;
        private Entity _handTarget;
        private float3 _handTargetPosition;

        private bool _handPickupDone;
        private bool _handThrowDone;
        private bool _deliveryDone;
        private bool _baselineCaptured;
        private bool _rewindTriggered;
        private bool _rewindObserved;
        private bool _rewindChecked;

        private int _baselineCarriers;
        private int _baselineMiners;
        private int _baselineAsteroids;
        private int _baselineDeliveries;
        private float _baselineStorehouseInventory;
        private uint _rewindStartTick;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioRunnerTick>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _carrierQuery = state.GetEntityQuery(ComponentType.ReadOnly<Carrier>());
            _minerQuery = state.GetEntityQuery(ComponentType.ReadOnly<MiningVessel>());
            _asteroidQuery = state.GetEntityQuery(ComponentType.ReadOnly<Asteroid>());
            _storehouseQuery = state.GetEntityQuery(ComponentType.ReadOnly<StorehouseInventory>());
            _loggedFallbackEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_loggedFallbackEntities.IsCreated)
            {
                _loggedFallbackEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            if (!scenarioInfo.ScenarioId.Equals(ScenarioId))
            {
                return;
            }

            var scenarioTick = SystemAPI.GetSingleton<ScenarioRunnerTick>().Tick;
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused)
            {
                return;
            }

            EnsureHandEntity(ref state);

            if (_rewindTriggered && !_rewindObserved && rewindState.Mode != RewindMode.Record)
            {
                _rewindObserved = true;
            }

            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!_handPickupDone && scenarioTick >= PickupTick)
            {
                if (TryPickup(ref state))
                {
                    _handPickupDone = true;
                }
            }

            if (_handPickupDone && !_handThrowDone && scenarioTick >= ThrowTick)
            {
                if (TryThrow(ref state))
                {
                    _handThrowDone = true;
                    ScenarioMetricsUtility.SetMetric(state.EntityManager, MetricHand, 1.0);
                }
            }

            if (!_deliveryDone && scenarioTick >= DeliveryTick)
            {
                if (TryDeliver(ref state, timeState.Tick))
                {
                    _deliveryDone = true;
                    ScenarioMetricsUtility.SetMetric(state.EntityManager, MetricDelivery, 1.0);
                }
            }

            if (!_rewindTriggered && scenarioTick >= RewindCommandTick)
            {
                CaptureBaseline(ref state, timeState.Tick);
                IssueRewindCommand(ref state, timeState.Tick);
                _rewindTriggered = true;
            }

            if (_rewindTriggered && _rewindObserved && !_rewindChecked && timeState.Tick >= _rewindStartTick)
            {
                EvaluateRewind(ref state);
                _rewindChecked = true;
            }
        }

        private void EnsureHandEntity(ref SystemState state)
        {
            if (_handEntity != Entity.Null && state.EntityManager.Exists(_handEntity))
            {
                return;
            }

            _handEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(_handEntity, new Space4XGodHandTag());
            state.EntityManager.AddComponentData(_handEntity, LocalTransform.FromPosition(float3.zero));
            state.EntityManager.AddComponentData(_handEntity, new PickupState
            {
                State = PickupStateType.Empty,
                LastRaycastPosition = float3.zero,
                CursorMovementAccumulator = 0f,
                HoldTime = 0f,
                AccumulatedVelocity = float3.zero,
                IsMoving = false,
                TargetEntity = Entity.Null,
                LastHolderPosition = float3.zero
            });
        }

        private bool TryPickup(ref SystemState state)
        {
            if (_handTarget == Entity.Null || !state.EntityManager.Exists(_handTarget))
            {
                if (!TryResolveHandTarget(ref state, out _handTarget, out _handTargetPosition))
                {
                    return false;
                }
            }

            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }

            bool hasHeldByPlayer = state.EntityManager.HasComponent<HeldByPlayer>(_handTarget);
            bool hasMovementSuppressed = state.EntityManager.HasComponent<MovementSuppressed>(_handTarget);
            if ((!hasHeldByPlayer || !hasMovementSuppressed) && interactionPolicy.AllowStructuralFallback == 0)
            {
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    var missing = hasHeldByPlayer
                        ? "MovementSuppressed"
                        : hasMovementSuppressed
                            ? "HeldByPlayer"
                            : "HeldByPlayer, MovementSuppressed";
                    LogFallbackOnce(_handTarget, missing, skipped: true);
                }
                return false;
            }

            var heldByPlayer = new HeldByPlayer
            {
                Holder = _handEntity,
                LocalOffset = float3.zero,
                HoldStartPosition = _handTargetPosition,
                HoldStartTime = 0f
            };
            if (hasHeldByPlayer)
            {
                state.EntityManager.SetComponentData(_handTarget, heldByPlayer);
                state.EntityManager.SetComponentEnabled<HeldByPlayer>(_handTarget, true);
            }
            else
            {
                state.EntityManager.AddComponentData(_handTarget, heldByPlayer);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(_handTarget, "HeldByPlayer", skipped: false);
                }
            }

            if (hasMovementSuppressed)
            {
                state.EntityManager.SetComponentEnabled<MovementSuppressed>(_handTarget, true);
            }
            else
            {
                state.EntityManager.AddComponent<MovementSuppressed>(_handTarget);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(_handTarget, "MovementSuppressed", skipped: false);
                }
            }

            if (state.EntityManager.HasComponent<PickupState>(_handEntity))
            {
                var pickup = state.EntityManager.GetComponentData<PickupState>(_handEntity);
                pickup.State = PickupStateType.Holding;
                pickup.TargetEntity = _handTarget;
                pickup.LastHolderPosition = state.EntityManager.GetComponentData<LocalTransform>(_handEntity).Position;
                pickup.HoldTime = 0f;
                pickup.IsMoving = false;
                pickup.AccumulatedVelocity = float3.zero;
                state.EntityManager.SetComponentData(_handEntity, pickup);
            }

            return true;
        }

        private bool TryThrow(ref SystemState state)
        {
            if (_handTarget == Entity.Null || !state.EntityManager.Exists(_handTarget))
            {
                return false;
            }

            if (state.EntityManager.HasComponent<HeldByPlayer>(_handTarget))
            {
                state.EntityManager.SetComponentEnabled<HeldByPlayer>(_handTarget, false);
            }

            if (state.EntityManager.HasComponent<MovementSuppressed>(_handTarget))
            {
                state.EntityManager.SetComponentEnabled<MovementSuppressed>(_handTarget, false);
            }

            var throwVelocity = new float3(6f, 4f, 2f);
            if (state.EntityManager.HasComponent<Unity.Physics.PhysicsVelocity>(_handTarget))
            {
                var velocity = state.EntityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(_handTarget);
                velocity.Linear = throwVelocity;
                velocity.Angular = float3.zero;
                state.EntityManager.SetComponentData(_handTarget, velocity);
            }

            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }

            var prevPosition = float3.zero;
            var prevRotation = quaternion.identity;
            if (state.EntityManager.HasComponent<LocalTransform>(_handTarget))
            {
                var transform = state.EntityManager.GetComponentData<LocalTransform>(_handTarget);
                prevPosition = transform.Position;
                prevRotation = transform.Rotation;
            }
            var thrown = new BeingThrown
            {
                InitialVelocity = throwVelocity,
                TimeSinceThrow = 0f,
                PrevPosition = prevPosition,
                PrevRotation = prevRotation
            };
            bool hasBeingThrown = state.EntityManager.HasComponent<BeingThrown>(_handTarget);
            if (hasBeingThrown)
            {
                state.EntityManager.SetComponentData(_handTarget, thrown);
                state.EntityManager.SetComponentEnabled<BeingThrown>(_handTarget, true);
            }
            else if (interactionPolicy.AllowStructuralFallback != 0)
            {
                state.EntityManager.AddComponentData(_handTarget, thrown);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(_handTarget, "BeingThrown", skipped: false);
                }
            }
            else if (interactionPolicy.LogStructuralFallback != 0)
            {
                LogFallbackOnce(_handTarget, "BeingThrown", skipped: true);
            }

            if (state.EntityManager.HasComponent<PickupState>(_handEntity))
            {
                var pickup = state.EntityManager.GetComponentData<PickupState>(_handEntity);
                pickup.State = PickupStateType.Empty;
                pickup.TargetEntity = Entity.Null;
                pickup.HoldTime = 0f;
                pickup.AccumulatedVelocity = float3.zero;
                pickup.IsMoving = false;
                state.EntityManager.SetComponentData(_handEntity, pickup);
            }

            return true;
        }

        private bool TryDeliver(ref SystemState state, uint tick)
        {
            if (_storehouseQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            Entity storehouse = Entity.Null;
            foreach (var (inventory, entity) in SystemAPI.Query<RefRW<StorehouseInventory>>().WithEntityAccess())
            {
                storehouse = entity;
                break;
            }

            if (storehouse == Entity.Null)
            {
                return false;
            }

            var items = state.EntityManager.HasBuffer<StorehouseInventoryItem>(storehouse)
                ? state.EntityManager.GetBuffer<StorehouseInventoryItem>(storehouse)
                : state.EntityManager.AddBuffer<StorehouseInventoryItem>(storehouse);

            var added = 5f;
            var updated = false;
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].ResourceTypeId.Equals(MineralsId))
                {
                    continue;
                }

                var item = items[i];
                item.Amount += added;
                items[i] = item;
                updated = true;
                break;
            }

            if (!updated)
            {
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = MineralsId,
                    Amount = added,
                    Reserved = 0f,
                    TierId = 0,
                    AverageQuality = 0
                });
            }

            var inventoryData = state.EntityManager.GetComponentData<StorehouseInventory>(storehouse);
            inventoryData.TotalStored = math.max(inventoryData.TotalStored + added, 0f);
            inventoryData.TotalCapacity = math.max(inventoryData.TotalCapacity, inventoryData.TotalStored);
            inventoryData.LastUpdateTick = tick;
            state.EntityManager.SetComponentData(storehouse, inventoryData);

            var receipts = state.EntityManager.HasBuffer<DeliveryReceipt>(storehouse)
                ? state.EntityManager.GetBuffer<DeliveryReceipt>(storehouse)
                : state.EntityManager.AddBuffer<DeliveryReceipt>(storehouse);

            receipts.Add(new DeliveryReceipt
            {
                RequestId = tick,
                DeliveredAmount = added,
                DelivererEntity = _handEntity,
                RecipientEntity = storehouse,
                DeliveryTick = tick,
                ResourceTypeId = new FixedString32Bytes("minerals")
            });

            return true;
        }

        private void CaptureBaseline(ref SystemState state, uint tick)
        {
            _baselineCarriers = _carrierQuery.CalculateEntityCount();
            _baselineMiners = _minerQuery.CalculateEntityCount();
            _baselineAsteroids = _asteroidQuery.CalculateEntityCount();
            _baselineDeliveries = CountDeliveries(ref state);
            _baselineStorehouseInventory = SumStorehouseInventory(ref state);
            _rewindStartTick = tick;
            _baselineCaptured = true;
        }

        private void IssueRewindCommand(ref SystemState state, uint tick)
        {
            var rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                state.EntityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var buffer = state.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            var targetTick = tick > RewindDepthTicks ? tick - RewindDepthTicks : 0u;
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.StartRewind,
                UintParam = targetTick,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Scenario
            });
        }

        private void EvaluateRewind(ref SystemState state)
        {
            if (!_baselineCaptured)
            {
                return;
            }

            var currentCarriers = _carrierQuery.CalculateEntityCount();
            var currentMiners = _minerQuery.CalculateEntityCount();
            var currentAsteroids = _asteroidQuery.CalculateEntityCount();
            var currentDeliveries = CountDeliveries(ref state);
            var currentInventory = SumStorehouseInventory(ref state);

            var entitiesMatch = currentCarriers == _baselineCarriers &&
                                currentMiners == _baselineMiners &&
                                currentAsteroids == _baselineAsteroids;
            var totalsMatch = currentDeliveries == _baselineDeliveries &&
                              math.abs(currentInventory - _baselineStorehouseInventory) <= 0.001f;

            ScenarioMetricsUtility.SetMetric(state.EntityManager, MetricEntities, entitiesMatch ? 1.0 : 0.0);
            ScenarioMetricsUtility.SetMetric(state.EntityManager, MetricTotals, totalsMatch ? 1.0 : 0.0);
            ScenarioMetricsUtility.SetMetric(state.EntityManager, MetricRewind, (entitiesMatch && totalsMatch) ? 1.0 : 0.0);
        }

        private int CountDeliveries(ref SystemState state)
        {
            var total = 0;
            foreach (var receipts in SystemAPI.Query<DynamicBuffer<DeliveryReceipt>>())
            {
                total += receipts.Length;
            }

            return total;
        }

        private float SumStorehouseInventory(ref SystemState state)
        {
            var total = 0f;
            foreach (var items in SystemAPI.Query<DynamicBuffer<StorehouseInventoryItem>>())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    total += items[i].Amount;
                }
            }

            return total;
        }

        private bool TryResolveHandTarget(ref SystemState state, out Entity target, out float3 position)
        {
            foreach (var (_, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<HeldByPlayer>(entity) &&
                    state.EntityManager.IsComponentEnabled<HeldByPlayer>(entity))
                {
                    continue;
                }

                target = entity;
                position = transform.ValueRO.Position;
                return true;
            }

            foreach (var (_, transform, entity) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<HeldByPlayer>(entity) &&
                    state.EntityManager.IsComponentEnabled<HeldByPlayer>(entity))
                {
                    continue;
                }

                target = entity;
                position = transform.ValueRO.Position;
                return true;
            }

            foreach (var (_, transform, entity) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<HeldByPlayer>(entity) &&
                    state.EntityManager.IsComponentEnabled<HeldByPlayer>(entity))
                {
                    continue;
                }

                target = entity;
                position = transform.ValueRO.Position;
                return true;
            }

            target = Entity.Null;
            position = float3.zero;
            return false;
        }

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string missingComponents, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping structural fallback (strict policy)" : "using structural fallback";
            UnityEngine.Debug.LogWarning($"[Space4XRewindGateHarnessSystem] Missing {missingComponents} on entity {target.Index}:{target.Version}; {action}.");
        }
    }
}

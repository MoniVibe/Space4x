using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Mining;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using MiningStateEnum = PureDOTS.Runtime.Mining.MiningState;

namespace Space4X.Mining
{
    /// <summary>
    /// Hardened mining system for Space4X vessels with robust state machine and physics disruption handling.
    /// Integrates with PureDOTS mining components for cross-game compatibility.
    /// </summary>
    // [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierPatrolSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct Space4XMiningSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<MineableSource> _mineableSourceLookup;
        private ComponentLookup<ResourceSink> _resourceSinkLookup;
        private ComponentLookup<MiningSession> _miningSessionLookup;
        private ComponentLookup<MiningStateComponent> _miningStateLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;
        private ComponentLookup<BeingThrown> _beingThrownLookup;
        private ComponentLookup<HandHeldTag> _handHeldLookup;
        private BufferLookup<ResourceStorage> _resourceStorageLookup;
        private EntityQuery _asteroidQuery;
        private EntityQuery _carrierQuery;

        private const float MiningRange = 2f;
        private const float DeliveryRange = 3f;
        private const float MaxSearchRadius = 1000f;

#if UNITY_EDITOR
        private ComponentLookup<MiningDiagnostics> _diagnosticsLookup;
#endif

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(false);
            _mineableSourceLookup = state.GetComponentLookup<MineableSource>(false);
            _resourceSinkLookup = state.GetComponentLookup<ResourceSink>(false);
            _miningSessionLookup = state.GetComponentLookup<MiningSession>(false);
            _miningStateLookup = state.GetComponentLookup<MiningStateComponent>(false);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
            _beingThrownLookup = state.GetComponentLookup<BeingThrown>(true);
            _handHeldLookup = state.GetComponentLookup<HandHeldTag>(true);
            _resourceStorageLookup = state.GetBufferLookup<ResourceStorage>(false);

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform>()
                .Build();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, LocalTransform>()
                .Build();

#if UNITY_EDITOR
            _diagnosticsLookup = state.GetComponentLookup<MiningDiagnostics>(false);
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<Space4XLegacyMiningDisabledTag>())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _mineableSourceLookup.Update(ref state);
            _resourceSinkLookup.Update(ref state);
            _miningSessionLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);
            _beingThrownLookup.Update(ref state);
            _handHeldLookup.Update(ref state);
            _resourceStorageLookup.Update(ref state);

#if UNITY_EDITOR
            _diagnosticsLookup.Update(ref state);
#endif

            var deltaTime = timeState.FixedDeltaTime;
            var currentTick = timeState.Tick;

            // Collect available asteroids for discovery
            var asteroidList = new NativeList<(Entity entity, float3 position, Asteroid asteroid)>(Allocator.TempJob);
            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (asteroid.ValueRO.ResourceAmount > 0f)
                {
                    asteroidList.Add((entity, transform.ValueRO.Position, asteroid.ValueRO));
                }
            }

            // Collect available carriers for discovery
            var carrierList = new NativeList<(Entity entity, float3 position)>(Allocator.TempJob);
            foreach (var (carrier, transform, entity) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                carrierList.Add((entity, transform.ValueRO.Position));
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

#if UNITY_EDITOR
            Entity diagnosticsEntity = Entity.Null;
            if (SystemAPI.HasSingleton<MiningDiagnostics>())
            {
                diagnosticsEntity = SystemAPI.GetSingletonEntity<MiningDiagnostics>();
            }
#endif

            var job = new ProcessMiningJob
            {
                CarrierLookup = _carrierLookup,
                AsteroidLookup = _asteroidLookup,
                MineableSourceLookup = _mineableSourceLookup,
                ResourceSinkLookup = _resourceSinkLookup,
                MiningSessionLookup = _miningSessionLookup,
                MiningStateLookup = _miningStateLookup,
                MovementSuppressedLookup = _movementSuppressedLookup,
                BeingThrownLookup = _beingThrownLookup,
                HandHeldLookup = _handHeldLookup,
                ResourceStorageLookup = _resourceStorageLookup,
                Ecb = ecb,
                AsteroidList = asteroidList,
                CarrierList = carrierList,
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                MiningRange = MiningRange,
                DeliveryRange = DeliveryRange,
                MaxSearchRadius = MaxSearchRadius
#if UNITY_EDITOR
                ,
                DiagnosticsEntity = diagnosticsEntity,
                DiagnosticsLookup = _diagnosticsLookup
#endif
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency = asteroidList.Dispose(state.Dependency);
            state.Dependency = carrierList.Dispose(state.Dependency);

#if UNITY_EDITOR
            state.Dependency.Complete();
            UpdateDiagnostics(ref state, currentTick, deltaTime);
#endif
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private void UpdateDiagnostics(ref SystemState state, uint currentTick, float deltaTime)
        {
            if (!SystemAPI.HasSingleton<MiningDiagnostics>())
            {
                return;
            }

            var diagnosticsEntity = SystemAPI.GetSingletonEntity<MiningDiagnostics>();
            var diagnostics = _diagnosticsLookup[diagnosticsEntity];

            // Count active sessions
            var activeSessionCount = 0;
            foreach (var session in SystemAPI.Query<RefRO<MiningSession>>())
            {
                if (session.ValueRO.Source != Entity.Null)
                {
                    activeSessionCount++;
                }
            }

            diagnostics.ActiveSessionCount = activeSessionCount;
            diagnostics.TimeAccumulator += deltaTime;

            if (diagnostics.TimeAccumulator >= 1f)
            {
                diagnostics.MinedPerSecond = diagnostics.TotalMined / diagnostics.TimeAccumulator;
                diagnostics.TotalMined = 0f;
                diagnostics.TimeAccumulator = 0f;
            }

            diagnostics.LastUpdateTick = currentTick;
            _diagnosticsLookup[diagnosticsEntity] = diagnostics;
        }
#endif

        // [BurstCompile]
        [StructLayout(LayoutKind.Sequential)]
        public partial struct ProcessMiningJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Carrier> CarrierLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<Asteroid> AsteroidLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MineableSource> MineableSourceLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<ResourceSink> ResourceSinkLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningSession> MiningSessionLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningStateComponent> MiningStateLookup;
            [ReadOnly] public ComponentLookup<MovementSuppressed> MovementSuppressedLookup;
            [ReadOnly] public ComponentLookup<BeingThrown> BeingThrownLookup;
            [ReadOnly] public ComponentLookup<HandHeldTag> HandHeldLookup;
            [NativeDisableParallelForRestriction]
            public BufferLookup<ResourceStorage> ResourceStorageLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly] public NativeList<(Entity entity, float3 position, Asteroid asteroid)> AsteroidList;
            [ReadOnly] public NativeList<(Entity entity, float3 position)> CarrierList;

            public float DeltaTime;
            public uint CurrentTick;
            public float MiningRange;
            public float DeliveryRange;
            public float MaxSearchRadius;
#if UNITY_EDITOR
            public Entity DiagnosticsEntity;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningDiagnostics> DiagnosticsLookup;
#endif

            public void Execute(
                [EntityIndexInQuery] int entityInQueryIndex,
                ref MiningVessel vessel,
                ref VesselMovement movement,
                ref VesselAIState aiState,
                ref LocalTransform transform,
                in Entity entity)
            {
                // Skip mining logic if movement is suppressed or entity is being thrown/held
                if (MovementSuppressedLookup.HasComponent(entity) ||
                    BeingThrownLookup.HasComponent(entity) ||
                    HandHeldLookup.HasComponent(entity))
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }

                var hasSession = MiningSessionLookup.HasComponent(entity);
                var session = hasSession ? MiningSessionLookup[entity] : default;
                var hasState = MiningStateLookup.HasComponent(entity);
                var miningState = hasState ? MiningStateLookup[entity] : new MiningStateComponent { State = MiningStateEnum.Idle };

                var position = transform.Position;

                // Validate existing session
                if (hasSession)
                {
                    // Check if source is still valid
                    if (session.Source == Entity.Null ||
                        !AsteroidLookup.HasComponent(session.Source) ||
                        GetPositionFromAsteroidList(session.Source).Equals(float3.zero))
                    {
                        // Invalid source - clear session and reset
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                        SetMiningState(entityInQueryIndex, entity, new MiningStateComponent
                        {
                            State = MiningStateEnum.Idle,
                            LastStateChangeTick = CurrentTick
                        }, hasState);
                        miningState.State = MiningStateEnum.Idle;
                        session = default(MiningSession);
                        hasSession = false;
#if UNITY_EDITOR
                        // Track diagnostics
                        if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                        {
                            var diag = DiagnosticsLookup[DiagnosticsEntity];
                            diag.InvalidSourceResets++;
                            DiagnosticsLookup[DiagnosticsEntity] = diag;
                        }
#endif
                    }
                    else if (MineableSourceLookup.HasComponent(session.Source))
                    {
                        var source = MineableSourceLookup[session.Source];
                        if (source.CurrentAmount <= 0f)
                        {
                            // Source depleted - clear session
                            Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                            SetMiningState(entityInQueryIndex, entity, new MiningStateComponent
                            {
                                State = MiningStateEnum.Idle,
                                LastStateChangeTick = CurrentTick
                            }, hasState);
                            miningState.State = MiningStateEnum.Idle;
                            session = default(MiningSession);
                            hasSession = false;
                        }
                    }

                    // Check if carrier is still valid
                    if (hasSession && session.Carrier != Entity.Null)
                    {
                        if (!CarrierLookup.HasComponent(session.Carrier) ||
                            GetPositionFromCarrierList(session.Carrier).Equals(float3.zero))
                        {
                            // Invalid carrier - try to find a new one or clear session
                            var newCarrier = FindNearestCarrier(position);
                            if (newCarrier != Entity.Null)
                            {
                                session.Carrier = newCarrier;
                                MiningSessionLookup[entity] = session;
                            }
                            else
                            {
                                // No carrier available - clear session
                                Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                                SetMiningState(entityInQueryIndex, entity, new MiningStateComponent
                                {
                                    State = MiningStateEnum.Idle,
                                    LastStateChangeTick = CurrentTick
                                }, hasState);
                                miningState.State = MiningStateEnum.Idle;
                                session = default(MiningSession);
                                hasSession = false;
#if UNITY_EDITOR
                                if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                                {
                                    var diag = DiagnosticsLookup[DiagnosticsEntity];
                                    diag.InvalidCarrierResets++;
                                    DiagnosticsLookup[DiagnosticsEntity] = diag;
                                }
#endif
                            }
                        }
                        else if (ResourceSinkLookup.HasComponent(session.Carrier))
                        {
                            var sink = ResourceSinkLookup[session.Carrier];
                            if (sink.CurrentAmount >= sink.Capacity)
                            {
                                // Carrier full - try to find a new one
                                var newCarrier = FindNearestCarrier(position);
                                if (newCarrier != Entity.Null)
                                {
                                    session.Carrier = newCarrier;
                                    MiningSessionLookup[entity] = session;
                                }
                            }
                        }
                    }
                }

                // State machine
                if (!hasSession || session.Source == Entity.Null)
                {
                    // Idle: Find a source
                    miningState.State = MiningStateEnum.Idle;
                    var nearestAsteroid = FindNearestAsteroid(position);
                    if (nearestAsteroid != Entity.Null)
                    {
                        // Create new session
                        var newSession = new MiningSession
                        {
                            Source = nearestAsteroid,
                            Carrier = vessel.CarrierEntity != Entity.Null && CarrierLookup.HasComponent(vessel.CarrierEntity)
                                ? vessel.CarrierEntity
                                : FindNearestCarrier(position),
                            Accumulated = 0f,
                            Capacity = vessel.CargoCapacity,
                            StartTick = CurrentTick
                        };

                        if (hasSession)
                        {
                            MiningSessionLookup[entity] = newSession;
                        }
                        else
                        {
                            Ecb.AddComponent(entityInQueryIndex, entity, newSession);
                            hasSession = true;
                        }

                        session = newSession;

                        // Update MineableSource component if missing
                        if (!MineableSourceLookup.HasComponent(nearestAsteroid) && AsteroidLookup.HasComponent(nearestAsteroid))
                        {
                            var asteroid = AsteroidLookup[nearestAsteroid];
                            var newSource = new MineableSource
                            {
                                CurrentAmount = asteroid.ResourceAmount,
                                MaxAmount = asteroid.MaxResourceAmount,
                                ExtractionRate = asteroid.MiningRate
                            };
                            Ecb.AddComponent(entityInQueryIndex, nearestAsteroid, newSource);
                        }

                        miningState.State = MiningStateEnum.GoingToSource;
                        miningState.LastStateChangeTick = CurrentTick;
                        SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                    }
                }
                else
                {
                    // We have a valid session
                    var sourcePos = GetPositionFromAsteroidList(session.Source);
                    if (sourcePos.Equals(float3.zero) && session.Source != Entity.Null)
                    {
                        // Source not found in list - invalid session
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                        miningState.State = MiningStateEnum.Idle;
                        miningState.LastStateChangeTick = CurrentTick;
                        if (hasState)
                        {
                            MiningStateLookup[entity] = miningState;
                        }
                        return;
                    }
                    var distanceToSource = math.distance(position, sourcePos);

                    if (miningState.State == MiningStateEnum.GoingToSource || miningState.State == MiningStateEnum.Idle)
                    {
                        if (distanceToSource <= MiningRange)
                        {
                            // Arrived at source - start mining
                            miningState.State = MiningStateEnum.Mining;
                            miningState.LastStateChangeTick = CurrentTick;
                            SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                        }
                        else
                        {
                            // Move towards source
                            miningState.State = MiningStateEnum.GoingToSource;
                            var direction = math.normalize(sourcePos - position);
                            movement.Velocity = direction * vessel.Speed;
                            movement.IsMoving = 1;
                            aiState.TargetEntity = session.Source;
                            aiState.TargetPosition = sourcePos;
                            aiState.CurrentGoal = VesselAIState.Goal.Mining;
                            aiState.CurrentState = VesselAIState.State.MovingToTarget;
                        }
                    }
                    else if (miningState.State == MiningStateEnum.Mining)
                    {
                        // Check if still in range
                        if (distanceToSource > MiningRange * 1.5f)
                        {
                            // Too far - go back to going to source
                            miningState.State = MiningStateEnum.GoingToSource;
                            miningState.LastStateChangeTick = CurrentTick;
                            SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                        }
                        else
                        {
                            // Mine resources
                            var asteroid = AsteroidLookup[session.Source];
                            var source = MineableSourceLookup.HasComponent(session.Source)
                                ? MineableSourceLookup[session.Source]
                                : new MineableSource
                                {
                                    CurrentAmount = asteroid.ResourceAmount,
                                    MaxAmount = asteroid.MaxResourceAmount,
                                    ExtractionRate = asteroid.MiningRate
                                };

                            if (source.CurrentAmount > 0f && session.Accumulated < session.Capacity)
                            {
                                var miningRate = vessel.MiningEfficiency * source.ExtractionRate * DeltaTime;
                                var amountToMine = math.min(miningRate, source.CurrentAmount);
                                amountToMine = math.min(amountToMine, session.Capacity - session.Accumulated);

                                source.CurrentAmount = math.max(0f, source.CurrentAmount - amountToMine);
                                session.Accumulated += amountToMine;

                                // Update asteroid
                                var asteroidRef = AsteroidLookup.GetRefRW(session.Source);
                                asteroidRef.ValueRW.ResourceAmount = source.CurrentAmount;

                                // Update source component
                                MineableSourceLookup[session.Source] = source;

                                // Update session
                                MiningSessionLookup[entity] = session;

                                movement.Velocity = float3.zero;
                                movement.IsMoving = 0;

#if UNITY_EDITOR
                                if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                                {
                                    var diag = DiagnosticsLookup[DiagnosticsEntity];
                                    diag.TotalMined += amountToMine;
                                    DiagnosticsLookup[DiagnosticsEntity] = diag;
                                }
#endif
                            }

                            // Check if should return to carrier
                            if (session.Accumulated >= session.Capacity || source.CurrentAmount <= 0f)
                            {
                                if (session.Carrier != Entity.Null)
                                {
                                    miningState.State = MiningStateEnum.ReturningToCarrier;
                                    miningState.LastStateChangeTick = CurrentTick;
                                    SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                                }
                                else
                                {
                                    // No carrier - find one
                                    var newCarrier = FindNearestCarrier(position);
                                    if (newCarrier != Entity.Null)
                                    {
                                        session.Carrier = newCarrier;
                                        MiningSessionLookup[entity] = session;
                                        miningState.State = MiningStateEnum.ReturningToCarrier;
                                        miningState.LastStateChangeTick = CurrentTick;
                                        SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                                    }
                                }
                            }
                        }
                    }
                    else if (miningState.State == MiningStateEnum.ReturningToCarrier)
                    {
                        if (session.Carrier == Entity.Null)
                        {
                            // No carrier - find one or go idle
                            var newCarrier = FindNearestCarrier(position);
                            if (newCarrier != Entity.Null)
                            {
                                session.Carrier = newCarrier;
                                MiningSessionLookup[entity] = session;
                            }
                            else
                            {
                                // No carrier available - clear session
                                Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                                miningState.State = MiningStateEnum.Idle;
                                miningState.LastStateChangeTick = CurrentTick;
                                SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                                return;
                            }
                        }

                        var carrierPos = GetPositionFromCarrierList(session.Carrier);
                        if (carrierPos.Equals(float3.zero) && session.Carrier != Entity.Null)
                        {
                            // Carrier not found - reset
                            Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                            miningState.State = MiningStateEnum.Idle;
                            miningState.LastStateChangeTick = CurrentTick;
                            SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                            return;
                        }
                        var distanceToCarrier = math.distance(position, carrierPos);

                        if (distanceToCarrier <= DeliveryRange)
                        {
                            // Arrived at carrier - start delivering
                            miningState.State = MiningStateEnum.Delivering;
                            miningState.LastStateChangeTick = CurrentTick;
                            SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                        }
                        else
                        {
                            // Move towards carrier
                            var direction = math.normalize(carrierPos - position);
                            movement.Velocity = direction * vessel.Speed;
                            movement.IsMoving = 1;
                            aiState.TargetEntity = session.Carrier;
                            aiState.TargetPosition = carrierPos;
                            aiState.CurrentGoal = VesselAIState.Goal.Returning;
                            aiState.CurrentState = VesselAIState.State.Returning;
                        }
                    }
                    else if (miningState.State == MiningStateEnum.Delivering)
                    {
                        if (session.Carrier == Entity.Null || !CarrierLookup.HasComponent(session.Carrier))
                        {
                            // Carrier invalid - reset
                            Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                            miningState.State = MiningStateEnum.Idle;
                            miningState.LastStateChangeTick = CurrentTick;
                            if (hasState)
                            {
                                MiningStateLookup[entity] = miningState;
                            }
                            return;
                        }

                        var carrierPos = GetPositionFromCarrierList(session.Carrier);
                        if (carrierPos.Equals(float3.zero) && session.Carrier != Entity.Null)
                        {
                            // Carrier not found - reset
                            Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                            miningState.State = MiningStateEnum.Idle;
                            miningState.LastStateChangeTick = CurrentTick;
                            if (hasState)
                            {
                                MiningStateLookup[entity] = miningState;
                            }
                            return;
                        }
                        var distanceToCarrier = math.distance(position, carrierPos);

                        if (distanceToCarrier > DeliveryRange * 1.5f)
                        {
                            // Too far - go back to returning
                            miningState.State = MiningStateEnum.ReturningToCarrier;
                            miningState.LastStateChangeTick = CurrentTick;
                            if (hasState)
                            {
                                MiningStateLookup[entity] = miningState;
                            }
                        }
                        else
                        {
                            // Deliver resources
                            if (session.Accumulated > 0f && ResourceStorageLookup.HasBuffer(session.Carrier))
                            {
                                var resourceBuffer = ResourceStorageLookup[session.Carrier];
                                var asteroid = AsteroidLookup[session.Source];
                                var resourceType = asteroid.ResourceType;
                                var amountToTransfer = session.Accumulated;

                                // Find or create resource storage slot
                                bool foundSlot = false;
                                for (int i = 0; i < resourceBuffer.Length; i++)
                                {
                                    if (resourceBuffer[i].Type == resourceType)
                                    {
                                        var storage = resourceBuffer[i];
                                        var remaining = storage.AddAmount(amountToTransfer);
                                        resourceBuffer[i] = storage;
                                        session.Accumulated = remaining;
                                        foundSlot = true;
                                        break;
                                    }
                                }

                                if (!foundSlot && resourceBuffer.Length < 4)
                                {
                                    var newStorage = ResourceStorage.Create(resourceType);
                                    var remaining = newStorage.AddAmount(amountToTransfer);
                                    resourceBuffer.Add(newStorage);
                                    session.Accumulated = remaining;
                                }

                                MiningSessionLookup[entity] = session;
                                vessel.CurrentCargo = session.Accumulated;

                                movement.Velocity = float3.zero;
                                movement.IsMoving = 0;
                            }

                            // Check if delivery complete
                            if (session.Accumulated <= 0f)
                            {
                                // Delivery complete - reset and start new cycle
                                Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                                miningState.State = MiningStateEnum.Idle;
                                miningState.LastStateChangeTick = CurrentTick;
                                SetMiningState(entityInQueryIndex, entity, miningState, hasState);
                                vessel.CurrentCargo = 0f;
                            }
                        }
                    }
                }

                // Update transform based on movement
                if (math.lengthsq(movement.Velocity) > 0.01f)
                {
                    var newPosition = position + movement.Velocity * DeltaTime;
                    transform.Position = newPosition;

                    // Update rotation to face movement direction
                    var direction = math.normalize(movement.Velocity);
                    if (math.lengthsq(direction) > 0.01f)
                    {
                        movement.DesiredRotation = quaternion.LookRotationSafe(direction, math.up());
                        transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, DeltaTime * 2f);
                    }
                }
            }

            private void SetMiningState(int sortKey, Entity entity, MiningStateComponent state, bool hasState)
            {
                if (hasState)
                {
                    MiningStateLookup[entity] = state;
                }
                else
                {
                    Ecb.AddComponent(sortKey, entity, state);
                }
            }

            private Entity FindNearestAsteroid(float3 position)
            {
                Entity nearest = Entity.Null;
                float nearestDistance = float.MaxValue;

                for (int i = 0; i < AsteroidList.Length; i++)
                {
                    var asteroid = AsteroidList[i];
                    var distance = math.distance(position, asteroid.position);
                    if (distance < nearestDistance && distance < MaxSearchRadius)
                    {
                        nearestDistance = distance;
                        nearest = asteroid.entity;
                    }
                }

                return nearest;
            }

            private Entity FindNearestCarrier(float3 position)
            {
                Entity nearest = Entity.Null;
                float nearestDistance = float.MaxValue;

                for (int i = 0; i < CarrierList.Length; i++)
                {
                    var carrier = CarrierList[i];
                    var distance = math.distance(position, carrier.position);
                    if (distance < nearestDistance && distance < MaxSearchRadius)
                    {
                        nearestDistance = distance;
                        nearest = carrier.entity;
                    }
                }

                return nearest;
            }

            private float3 GetPositionFromAsteroidList(Entity entity)
            {
                for (int i = 0; i < AsteroidList.Length; i++)
                {
                    if (AsteroidList[i].entity == entity)
                    {
                        return AsteroidList[i].position;
                    }
                }
                return float3.zero;
            }

            private float3 GetPositionFromCarrierList(Entity entity)
            {
                for (int i = 0; i < CarrierList.Length; i++)
                {
                    if (CarrierList[i].entity == entity)
                    {
                        return CarrierList[i].position;
                    }
                }
                return float3.zero;
            }
        }
    }
}

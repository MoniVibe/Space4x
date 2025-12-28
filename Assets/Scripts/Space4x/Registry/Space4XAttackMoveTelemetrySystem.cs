using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Steering;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial struct Space4XAttackMoveTelemetryBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XAttackMoveTelemetry>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XAttackMoveTelemetry));
            state.EntityManager.SetComponentData(entity, Space4XAttackMoveTelemetry.Default);

            state.EntityManager.AddBuffer<AttackMoveStartedEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveTargetChangedEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveFiringWindowEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveCompletedEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveClarityStateEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveKiteQualityEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveSummaryEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveWeaponLeadAimEvent>(entity);
            state.EntityManager.AddBuffer<AttackMoveSample>(entity);

            state.Enabled = false;
        }
    }

    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    [UpdateBefore(typeof(VesselAttackMoveLifecycleSystem))]
    public partial struct Space4XAttackMoveTelemetrySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private ComponentLookup<AttackMoveOrigin> _originLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<TargetPriority> _priorityLookup;
        private ComponentLookup<EntityIntent> _intentLookup;
        private ComponentLookup<StrikeCraftDogfightTag> _dogfightTagLookup;
        private BufferLookup<WeaponMount> _weaponLookup;
        private BufferLookup<AttackMoveWeaponCooldownState> _cooldownLookup;
        private EntityStorageInfoLookup _entityLookup;

        private const float DefaultFireConeCos = 0.7f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(true);
            _originLookup = state.GetComponentLookup<AttackMoveOrigin>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _priorityLookup = state.GetComponentLookup<TargetPriority>(true);
            _intentLookup = state.GetComponentLookup<EntityIntent>(true);
            _dogfightTagLookup = state.GetComponentLookup<StrikeCraftDogfightTag>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
            _cooldownLookup = state.GetBufferLookup<AttackMoveWeaponCooldownState>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XAttackMoveTelemetry>(out var telemetryEntity))
            {
                return;
            }

            var telemetryConfig = SystemAPI.GetSingleton<Space4XAttackMoveTelemetry>();

            var missingState = SystemAPI.QueryBuilder()
                .WithAll<AttackMoveIntent>()
                .WithNone<AttackMoveTelemetryState>()
                .Build();
            var missingCooldown = SystemAPI.QueryBuilder()
                .WithAll<AttackMoveIntent, WeaponMount>()
                .WithNone<AttackMoveWeaponCooldownState>()
                .Build();

            if (!missingState.IsEmptyIgnoreFilter || !missingCooldown.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                if (!missingState.IsEmptyIgnoreFilter)
                {
                    using var missingEntities = missingState.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < missingEntities.Length; i++)
                    {
                        state.EntityManager.AddComponent<AttackMoveTelemetryState>(missingEntities[i]);
                    }
                }

                if (!missingCooldown.IsEmptyIgnoreFilter)
                {
                    using var missingEntities = missingCooldown.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < missingEntities.Length; i++)
                    {
                        state.EntityManager.AddBuffer<AttackMoveWeaponCooldownState>(missingEntities[i]);
                    }
                }
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _originLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _dogfightTagLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _cooldownLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var startedEvents = state.EntityManager.GetBuffer<AttackMoveStartedEvent>(telemetryEntity);
            var targetEvents = state.EntityManager.GetBuffer<AttackMoveTargetChangedEvent>(telemetryEntity);
            var windowEvents = state.EntityManager.GetBuffer<AttackMoveFiringWindowEvent>(telemetryEntity);
            var completedEvents = state.EntityManager.GetBuffer<AttackMoveCompletedEvent>(telemetryEntity);
            var clarityEvents = state.EntityManager.GetBuffer<AttackMoveClarityStateEvent>(telemetryEntity);
            var kiteEvents = state.EntityManager.GetBuffer<AttackMoveKiteQualityEvent>(telemetryEntity);
            var summaryEvents = state.EntityManager.GetBuffer<AttackMoveSummaryEvent>(telemetryEntity);
            var leadAimEvents = state.EntityManager.GetBuffer<AttackMoveWeaponLeadAimEvent>(telemetryEntity);
            var sampleEvents = state.EntityManager.GetBuffer<AttackMoveSample>(telemetryEntity);

            var dogfightConfig = StrikeCraftDogfightConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftDogfightConfig>(out var dogfightConfigSingleton))
            {
                dogfightConfig = dogfightConfigSingleton;
            }
            var dogfightFireConeCos = math.cos(math.radians(dogfightConfig.FireConeDegrees));
            var projectileSpeedMultiplier = 1f;
            if (SystemAPI.TryGetSingleton<Space4XWeaponTuningConfig>(out var weaponTuning))
            {
                projectileSpeedMultiplier = math.max(0f, weaponTuning.ProjectileSpeedMultiplier);
            }

            foreach (var (intent, transform, movement, telemetryState, entity) in SystemAPI
                         .Query<RefRO<AttackMoveIntent>, RefRO<LocalTransform>, RefRO<VesselMovement>, RefRW<AttackMoveTelemetryState>>()
                         .WithEntityAccess())
            {
                var stateData = telemetryState.ValueRW;
                var wasActive = stateData.IsActive != 0;

                var currentTarget = ResolveAimTarget(intent.ValueRO, entity, out var targetReason);
                var hasTarget = TryResolveTargetPosition(currentTarget, out var targetPosition);

                var destination = intent.ValueRO.Destination;
                var destinationRadius = intent.ValueRO.DestinationRadius;
                var distanceToDest = math.distance(transform.ValueRO.Position, destination);

                var forward = math.forward(transform.ValueRO.Rotation);
                if (_aimLookup.HasComponent(entity))
                {
                    var aim = _aimLookup[entity];
                    if (aim.AimWeight > 0f && math.lengthsq(aim.AimDirection) > 0.001f)
                    {
                        forward = math.normalizesafe(aim.AimDirection, forward);
                    }
                }
                else if (math.lengthsq(movement.ValueRO.Velocity) > 0.001f)
                {
                    forward = math.normalizesafe(movement.ValueRO.Velocity, forward);
                }

                var maxRange = ResolveMaxWeaponRange(entity);
                var directionToTarget = hasTarget
                    ? math.normalizesafe(targetPosition - transform.ValueRO.Position, forward)
                    : forward;
                var distanceToTarget = hasTarget
                    ? math.distance(transform.ValueRO.Position, targetPosition)
                    : -1f;
                var destInRange = hasTarget && maxRange > 0f && math.distance(destination, targetPosition) <= maxRange;

                var fireConeCos = _dogfightTagLookup.HasComponent(entity)
                    ? dogfightFireConeCos
                    : DefaultFireConeCos;

                var inRange = hasTarget && maxRange > 0f && distanceToTarget <= maxRange;
                var inCone = hasTarget && math.dot(forward, directionToTarget) >= fireConeCos;

                if (!wasActive)
                {
                    stateData.IsActive = 1;
                    stateData.StartTick = intent.ValueRO.StartTick != 0 ? intent.ValueRO.StartTick : timeState.Tick;
                    stateData.Source = intent.ValueRO.Source;
                    stateData.Destination = destination;
                    stateData.DestinationRadius = destinationRadius;
                    stateData.CurrentTarget = currentTarget;
                    stateData.CurrentTargetReason = targetReason;
                    stateData.InRange = (byte)(inRange ? 1 : 0);
                    stateData.InCone = (byte)(inCone ? 1 : 0);
                    stateData.DestInRange = (byte)(destInRange ? 1 : 0);
                    stateData.InFiringWindow = 0;
                    stateData.TimeInRangeTicks = 0;
                    stateData.TimeInConeTicks = 0;
                    stateData.ShotsFired = 0;
                    stateData.MountsFiredMask = 0;
                    stateData.LastArrived = 0;
                    stateData.LastArrivedTick = 0;
                    stateData.WasPatrolling = _originLookup.HasComponent(entity) ? _originLookup[entity].WasPatrolling : (byte)0;
                    stateData.ActiveTicks = 0;
                    stateData.KiteTicks = 0;
                    stateData.ConeErrorSum = 0f;
                    stateData.ConeErrorSamples = 0;
                    stateData.SpeedWhileFiringSum = 0f;
                    stateData.SpeedWhileFiringSamples = 0;
                    stateData.FirstShotTick = 0;
                    stateData.TargetChangeCount = 0;
                    stateData.DestInRangeTicks = 0;
                    stateData.DestOutOfRangeTicks = 0;
                    stateData.LastDestInRangeFlipTick = 0;

                    startedEvents.Add(new AttackMoveStartedEvent
                    {
                        Tick = timeState.Tick,
                        Ship = entity,
                        Source = stateData.Source,
                        Destination = destination,
                        DestinationRadius = destinationRadius,
                        InitialTarget = currentTarget
                    });
                    TrimBuffer(startedEvents, telemetryConfig.MaxEventEntries);
                }
                else
                {
                    stateData.Destination = destination;
                    stateData.DestinationRadius = destinationRadius;

                    if (stateData.CurrentTarget != currentTarget)
                    {
                        targetEvents.Add(new AttackMoveTargetChangedEvent
                        {
                            Tick = timeState.Tick,
                            Ship = entity,
                            PreviousTarget = stateData.CurrentTarget,
                            NewTarget = currentTarget,
                            Reason = targetReason
                        });
                        TrimBuffer(targetEvents, telemetryConfig.MaxEventEntries);

                        stateData.CurrentTarget = currentTarget;
                        stateData.CurrentTargetReason = targetReason;
                        stateData.TargetChangeCount += 1;
                    }
                }

                var arrived = ShouldCompleteAttackMove(intent.ValueRO, movement.ValueRO, transform.ValueRO.Position);
                stateData.LastArrived = (byte)(arrived ? 1 : 0);
                if (arrived)
                {
                    stateData.LastArrivedTick = timeState.Tick;
                }

                stateData.InRange = (byte)(inRange ? 1 : 0);
                stateData.InCone = (byte)(inCone ? 1 : 0);
                var prevDestInRange = stateData.DestInRange;
                stateData.DestInRange = (byte)(destInRange ? 1 : 0);

                if (destInRange)
                {
                    stateData.DestInRangeTicks += 1;
                }
                else
                {
                    stateData.DestOutOfRangeTicks += 1;
                }

                if (wasActive && prevDestInRange != stateData.DestInRange)
                {
                    stateData.LastDestInRangeFlipTick = timeState.Tick;
                    clarityEvents.Add(new AttackMoveClarityStateEvent
                    {
                        Tick = timeState.Tick,
                        Ship = entity,
                        DestInRange = (byte)(destInRange ? 1 : 0),
                        TicksTrue = stateData.DestInRangeTicks,
                        TicksFalse = stateData.DestOutOfRangeTicks,
                        LastFlipTick = stateData.LastDestInRangeFlipTick
                    });
                    TrimBuffer(clarityEvents, telemetryConfig.MaxEventEntries);
                }

                var windowActive = inRange;
                if (windowActive && stateData.InFiringWindow == 0)
                {
                    stateData.InFiringWindow = 1;
                    stateData.FiringWindowStartTick = timeState.Tick;
                    stateData.TimeInRangeTicks = 0;
                    stateData.TimeInConeTicks = 0;
                    stateData.ShotsFired = 0;
                    stateData.MountsFiredMask = 0;
                }

                if (stateData.InFiringWindow != 0)
                {
                    if (windowActive)
                    {
                        stateData.TimeInRangeTicks += 1;
                        if (inCone)
                        {
                            stateData.TimeInConeTicks += 1;
                        }
                    }
                    else
                    {
                        EmitFiringWindowEvent(windowEvents, telemetryConfig.MaxEventEntries, entity,
                            stateData.FiringWindowStartTick, timeState.Tick, stateData.TimeInRangeTicks,
                            stateData.TimeInConeTicks, stateData.ShotsFired, stateData.MountsFiredMask);
                        stateData.InFiringWindow = 0;
                    }
                }

                stateData.ActiveTicks += 1;
                if (windowActive && movement.ValueRO.IsMoving != 0)
                {
                    stateData.KiteTicks += 1;
                }

                if (windowActive && hasTarget)
                {
                    var coneError = math.degrees(math.acos(math.clamp(math.dot(forward, directionToTarget), -1f, 1f)));
                    stateData.ConeErrorSum += coneError;
                    stateData.ConeErrorSamples += 1;

                    var speed = math.length(movement.ValueRO.Velocity);
                    stateData.SpeedWhileFiringSum += speed;
                    stateData.SpeedWhileFiringSamples += 1;
                }

                UpdateShotCounters(entity, currentTarget, targetPosition, timeState.Tick, windowActive, inRange, inCone,
                    projectileSpeedMultiplier, telemetryConfig, leadAimEvents, ref stateData);

                if (telemetryConfig.SampleStrideTicks > 0 &&
                    (stateData.LastSampleTick == 0 || timeState.Tick - stateData.LastSampleTick >= telemetryConfig.SampleStrideTicks))
                {
                    var aimAngle = hasTarget
                        ? math.degrees(math.acos(math.clamp(math.dot(forward, directionToTarget), -1f, 1f)))
                        : -1f;
                    var closingSpeed = hasTarget
                        ? math.dot(movement.ValueRO.Velocity, directionToTarget)
                        : 0f;

                    sampleEvents.Add(new AttackMoveSample
                    {
                        Tick = timeState.Tick,
                        Ship = entity,
                        DistToDest = distanceToDest,
                        DistToTarget = distanceToTarget,
                        InRange = (byte)(inRange ? 1 : 0),
                        InCone = (byte)(inCone ? 1 : 0),
                        DestInRange = (byte)(destInRange ? 1 : 0),
                        AimAngleDeg = aimAngle,
                        ClosingSpeed = closingSpeed
                    });
                    TrimBuffer(sampleEvents, telemetryConfig.MaxSampleEntries);

                    stateData.LastSampleTick = timeState.Tick;
                }

                telemetryState.ValueRW = stateData;
            }

            foreach (var (telemetryState, entity) in SystemAPI.Query<RefRW<AttackMoveTelemetryState>>()
                         .WithNone<AttackMoveIntent>()
                         .WithEntityAccess())
            {
                var stateData = telemetryState.ValueRW;
                if (stateData.IsActive == 0)
                {
                    continue;
                }

                var completionTick = stateData.LastArrivedTick != 0 ? stateData.LastArrivedTick : timeState.Tick;

                if (stateData.InFiringWindow != 0)
                {
                    EmitFiringWindowEvent(windowEvents, telemetryConfig.MaxEventEntries, entity,
                        stateData.FiringWindowStartTick, completionTick, stateData.TimeInRangeTicks,
                        stateData.TimeInConeTicks, stateData.ShotsFired, stateData.MountsFiredMask);
                }

                var result = stateData.LastArrived != 0
                    ? AttackMoveCompletionResult.DestinationReached
                    : ResolveCompletionResult(entity);

                var patrolResumed = (byte)(stateData.WasPatrolling != 0 && result == AttackMoveCompletionResult.DestinationReached ? 1 : 0);
                if (patrolResumed != 0)
                {
                    stateData.PatrolResumeCount += 1;
                }

                completedEvents.Add(new AttackMoveCompletedEvent
                {
                    Tick = completionTick,
                    Ship = entity,
                    Result = result,
                    DestinationReached = (byte)(result == AttackMoveCompletionResult.DestinationReached ? 1 : 0),
                    PatrolResumed = patrolResumed,
                    PatrolResumeCount = stateData.PatrolResumeCount,
                    Destination = stateData.Destination,
                    DestinationRadius = stateData.DestinationRadius
                });
                TrimBuffer(completedEvents, telemetryConfig.MaxEventEntries);

                var mountsThatFired = CountBits(stateData.MountsFiredMask);
                var coneRatio = stateData.TimeInRangeTicks > 0
                    ? (float)stateData.TimeInConeTicks / stateData.TimeInRangeTicks
                    : 0f;
                var kiteRatio = stateData.ActiveTicks > 0
                    ? (float)stateData.KiteTicks / stateData.ActiveTicks
                    : 0f;

                var avgConeError = stateData.ConeErrorSamples > 0
                    ? stateData.ConeErrorSum / stateData.ConeErrorSamples
                    : 0f;
                var avgSpeedWhileFiring = stateData.SpeedWhileFiringSamples > 0
                    ? stateData.SpeedWhileFiringSum / stateData.SpeedWhileFiringSamples
                    : 0f;

                avgConeError = math.round(avgConeError * 10f) / 10f;
                avgSpeedWhileFiring = math.round(avgSpeedWhileFiring * 10f) / 10f;

                kiteEvents.Add(new AttackMoveKiteQualityEvent
                {
                    Tick = completionTick,
                    Ship = entity,
                    KiteTicks = stateData.KiteTicks,
                    EngagementTicks = stateData.ActiveTicks,
                    TimeInRangeTicks = stateData.TimeInRangeTicks,
                    TimeInConeTicks = stateData.TimeInConeTicks,
                    ShotsFired = stateData.ShotsFired,
                    MountsThatFiredCount = mountsThatFired,
                    AvgConeErrorDeg = avgConeError,
                    AvgSpeedWhileFiring = avgSpeedWhileFiring
                });
                TrimBuffer(kiteEvents, telemetryConfig.MaxEventEntries);

                summaryEvents.Add(new AttackMoveSummaryEvent
                {
                    Tick = completionTick,
                    Ship = entity,
                    Source = stateData.Source,
                    Result = result,
                    TimeToFirstShotTicks = stateData.FirstShotTick > 0 ? stateData.FirstShotTick - stateData.StartTick : 0,
                    ConeWhileInRangeRatio = coneRatio,
                    KiteRatio = kiteRatio,
                    TargetChangeCount = stateData.TargetChangeCount,
                    PatrolResumeCount = stateData.PatrolResumeCount
                });
                TrimBuffer(summaryEvents, telemetryConfig.MaxEventEntries);

                stateData.IsActive = 0;
                stateData.CurrentTarget = Entity.Null;
                stateData.InRange = 0;
                stateData.InCone = 0;
                stateData.InFiringWindow = 0;
                stateData.DestInRange = 0;
                stateData.TimeInRangeTicks = 0;
                stateData.TimeInConeTicks = 0;
                stateData.ShotsFired = 0;
                stateData.MountsFiredMask = 0;
                stateData.LastArrived = 0;
                stateData.LastArrivedTick = 0;
                stateData.WasPatrolling = 0;
                stateData.ActiveTicks = 0;
                stateData.KiteTicks = 0;
                stateData.ConeErrorSum = 0f;
                stateData.ConeErrorSamples = 0;
                stateData.SpeedWhileFiringSum = 0f;
                stateData.SpeedWhileFiringSamples = 0;
                stateData.FirstShotTick = 0;
                stateData.TargetChangeCount = 0;
                stateData.DestInRangeTicks = 0;
                stateData.DestOutOfRangeTicks = 0;
                stateData.LastDestInRangeFlipTick = 0;

                telemetryState.ValueRW = stateData;
            }
        }

        private Entity ResolveAimTarget(in AttackMoveIntent intent, Entity entity, out AttackMoveTargetChangeReason reason)
        {
            if (IsValidTarget(intent.EngageTarget))
            {
                reason = AttackMoveTargetChangeReason.EngageTarget;
                return intent.EngageTarget;
            }

            if (intent.AcquireTargetsAlongRoute != 0 && _priorityLookup.HasComponent(entity))
            {
                var priority = _priorityLookup[entity];
                if (IsValidTarget(priority.CurrentTarget))
                {
                    reason = AttackMoveTargetChangeReason.TargetPriority;
                    return priority.CurrentTarget;
                }
            }

            if (intent.KeepFiringWhileInRange != 0 && _engagementLookup.HasComponent(entity))
            {
                var engagement = _engagementLookup[entity];
                if (IsValidTarget(engagement.PrimaryTarget))
                {
                    reason = AttackMoveTargetChangeReason.EngagementReacquire;
                    return engagement.PrimaryTarget;
                }
            }

            reason = AttackMoveTargetChangeReason.LostTarget;
            return Entity.Null;
        }

        private bool IsValidTarget(Entity target)
        {
            return target != Entity.Null && _entityLookup.Exists(target);
        }

        private bool TryResolveTargetPosition(Entity target, out float3 position)
        {
            position = default;
            if (!IsValidTarget(target))
            {
                return false;
            }

            if (!_transformLookup.HasComponent(target))
            {
                return false;
            }

            position = _transformLookup[target].Position;
            return true;
        }

        private float ResolveMaxWeaponRange(Entity entity)
        {
            if (!_weaponLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var weapons = _weaponLookup[entity];
            var maxRange = 0f;
            for (int i = 0; i < weapons.Length; i++)
            {
                var mount = weapons[i];
                if (mount.IsEnabled == 0)
                {
                    continue;
                }
                maxRange = math.max(maxRange, mount.Weapon.MaxRange);
            }

            return maxRange;
        }

        private void UpdateShotCounters(
            Entity entity,
            Entity target,
            float3 targetPosition,
            uint tick,
            bool windowActive,
            bool inRange,
            bool inCone,
            float projectileSpeedMultiplier,
            in Space4XAttackMoveTelemetry config,
            DynamicBuffer<AttackMoveWeaponLeadAimEvent> leadAimEvents,
            ref AttackMoveTelemetryState stateData)
        {
            if (!_weaponLookup.HasBuffer(entity) || !_cooldownLookup.HasBuffer(entity))
            {
                return;
            }

            var weapons = _weaponLookup[entity];
            var cooldowns = _cooldownLookup[entity];

            if (cooldowns.Length != weapons.Length)
            {
                cooldowns.Clear();
                cooldowns.ResizeUninitialized(weapons.Length);
                for (int i = 0; i < weapons.Length; i++)
                {
                    cooldowns[i] = new AttackMoveWeaponCooldownState
                    {
                        LastCooldown = weapons[i].Weapon.CurrentCooldown
                    };
                }
                return;
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                var mount = weapons[i];
                var prev = cooldowns[i].LastCooldown;
                var current = mount.Weapon.CurrentCooldown;

                if (windowActive && target != Entity.Null && prev == 0 && current > 0 && mount.IsEnabled != 0 && mount.CurrentTarget == target)
                {
                    stateData.ShotsFired += 1;
                    if (i < 64)
                    {
                        stateData.MountsFiredMask |= 1ul << i;
                    }

                    if (stateData.FirstShotTick == 0)
                    {
                        stateData.FirstShotTick = tick;
                    }

                    if (config.EnableLeadAimDiagnostics != 0)
                    {
                        bool shouldEmit = stateData.ShotsFired == 1;
                        if (!shouldEmit && config.LeadAimSampleEveryNthShot > 0)
                        {
                            shouldEmit = (stateData.ShotsFired % config.LeadAimSampleEveryNthShot) == 0;
                        }

                        if (shouldEmit && inRange && inCone)
                        {
                            var projectileSpeed = ResolveProjectileSpeed(mount.Weapon, projectileSpeedMultiplier);
                            byte usedLeadAim = 0;
                            float predictedTof = 0f;

                            if (projectileSpeed > 0f && TryResolveTargetVelocity(target, out var targetVelocity))
                            {
                                if (SteeringPrimitives.LeadInterceptPoint(
                                        targetPosition, targetVelocity, _transformLookup[entity].Position, projectileSpeed,
                                        out var interceptPoint, out var interceptTime))
                                {
                                    usedLeadAim = 1;
                                    predictedTof = interceptTime;
                                }
                            }

                            leadAimEvents.Add(new AttackMoveWeaponLeadAimEvent
                            {
                                Tick = tick,
                                Ship = entity,
                                MountIndex = (byte)math.min(byte.MaxValue, i),
                                UsedLeadAim = usedLeadAim,
                                ProjectileSpeed = projectileSpeed,
                                PredictedTimeOfFlight = predictedTof
                            });
                            TrimBuffer(leadAimEvents, config.MaxEventEntries);
                        }
                    }
                }

                cooldowns[i] = new AttackMoveWeaponCooldownState
                {
                    LastCooldown = current
                };
            }
        }

        private bool TryResolveTargetVelocity(Entity target, out float3 velocity)
        {
            velocity = float3.zero;
            if (!_movementLookup.HasComponent(target))
            {
                return false;
            }

            velocity = _movementLookup[target].Velocity;
            return true;
        }

        private static float ResolveProjectileSpeed(in Space4XWeapon weapon, float projectileSpeedMultiplier)
        {
            var baseSpeed = weapon.Type switch
            {
                WeaponType.Laser => 400f,
                WeaponType.PointDefense => 450f,
                WeaponType.Plasma => 320f,
                WeaponType.Ion => 280f,
                WeaponType.Kinetic => 220f,
                WeaponType.Flak => 200f,
                WeaponType.Missile => 140f,
                WeaponType.Torpedo => 90f,
                _ => 200f
            };

            var sizeScale = 1f + 0.25f * (int)weapon.Size;
            return baseSpeed * sizeScale * math.max(0f, projectileSpeedMultiplier);
        }

        private static bool ShouldCompleteAttackMove(in AttackMoveIntent intent, in VesselMovement movement, float3 position)
        {
            var arrivalDistance = movement.ArrivalDistance > 0f ? movement.ArrivalDistance : 2f;
            if (intent.DestinationRadius > 0f)
            {
                arrivalDistance = math.max(arrivalDistance, intent.DestinationRadius);
            }

            var distance = math.distance(position, intent.Destination);
            var speed = math.length(movement.Velocity);
            var stopSpeed = math.max(0.05f, movement.BaseSpeed * 0.1f);
            return distance <= arrivalDistance && speed <= stopSpeed;
        }

        private AttackMoveCompletionResult ResolveCompletionResult(Entity entity)
        {
            if (_intentLookup.HasComponent(entity))
            {
                var intent = _intentLookup[entity];
                if (intent.IsValid != 0 && intent.Mode != IntentMode.Idle)
                {
                    return AttackMoveCompletionResult.SupersededByOrder;
                }
            }

            return AttackMoveCompletionResult.Cancelled;
        }

        private static void EmitFiringWindowEvent(
            DynamicBuffer<AttackMoveFiringWindowEvent> buffer,
            uint maxEntries,
            Entity entity,
            uint startTick,
            uint endTick,
            uint timeInRange,
            uint timeInCone,
            uint shotsFired,
            ulong mountsMask)
        {
            buffer.Add(new AttackMoveFiringWindowEvent
            {
                TickEnter = startTick,
                TickExit = endTick,
                Ship = entity,
                TimeInRangeTicks = timeInRange,
                TimeInConeTicks = timeInCone,
                ShotsFired = shotsFired,
                MountsThatFiredCount = CountBits(mountsMask)
            });
            TrimBuffer(buffer, maxEntries);
        }

        private static byte CountBits(ulong mask)
        {
            byte count = 0;
            while (mask != 0)
            {
                count++;
                mask &= mask - 1;
            }
            return count;
        }

        private static void TrimBuffer<T>(DynamicBuffer<T> buffer, uint maxEntries) where T : unmanaged
        {
            if (maxEntries == 0 || buffer.Length <= maxEntries)
            {
                return;
            }

            int removeCount = buffer.Length - (int)maxEntries;
            if (removeCount > 0)
            {
                buffer.RemoveRange(0, removeCount);
            }
        }
    }
}

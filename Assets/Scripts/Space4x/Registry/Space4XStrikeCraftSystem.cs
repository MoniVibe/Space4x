using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Main state machine for strike craft attack runs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XStrikeCraftSystem : ISystem
    {
        private uint _lastTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            _lastTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tick = timeState.Tick;
            var tickDelta = _lastTick == 0u ? 1u : (tick > _lastTick ? tick - _lastTick : 1u);
            _lastTick = tick;
            var deltaTime = timeState.FixedDeltaTime * tickDelta;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRW<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithNone<StrikeCraftDogfightTag>()
                    .WithEntityAccess())
            {
                var previousPhase = craftState.ValueRO.Phase;

                // Get stance from carrier or self
                VesselStanceMode stance = VesselStanceMode.Balanced;
                if (SystemAPI.HasComponent<PatrolStance>(craftState.ValueRO.Carrier))
                {
                    stance = SystemAPI.GetComponent<PatrolStance>(craftState.ValueRO.Carrier).Stance;
                }
                else if (SystemAPI.HasComponent<PatrolStance>(entity))
                {
                    stance = SystemAPI.GetComponent<PatrolStance>(entity).Stance;
                }

                ProcessPhase(ref craftState.ValueRW, config.ValueRO, ref transform.ValueRW, stance, deltaTime, entity, ref state, tickDelta);

                if (previousPhase == AttackRunPhase.Docked && craftState.ValueRO.Phase != AttackRunPhase.Docked)
                {
                    if (!SystemAPI.HasComponent<OnSortieTag>(entity))
                    {
                        ecb.AddComponent<OnSortieTag>(entity);
                    }
                }
            }
        }

        private void ProcessPhase(
            ref StrikeCraftProfile craftState,
            in AttackRunConfig config,
            ref LocalTransform transform,
            VesselStanceMode stance,
            float deltaTime,
            Entity entity,
            ref SystemState systemState,
            uint tickDelta)
        {
            // Decrement phase timer
            if (craftState.PhaseTimer > 0)
            {
                if (tickDelta < craftState.PhaseTimer)
                {
                    craftState.PhaseTimer -= (ushort)tickDelta;
                    return;
                }

                craftState.PhaseTimer = 0;
            }

            switch (craftState.Phase)
            {
                case AttackRunPhase.Docked:
                    // Waiting for launch command
                    break;

                case AttackRunPhase.Launching:
                    // Transition to form-up
                    craftState.Phase = AttackRunPhase.FormUp;
                    craftState.PhaseTimer = 30; // Form-up time
                    break;

                case AttackRunPhase.FormUp:
                    // Check if wing is ready, transition to approach
                    if (craftState.Target != Entity.Null)
                    {
                        craftState.Phase = AttackRunPhase.Approach;
                        craftState.PhaseTimer = 0;
                    }
                    break;

                case AttackRunPhase.Approach:
                    // Check distance to target
                    if (craftState.Target != Entity.Null && SystemAPI.HasComponent<LocalTransform>(craftState.Target))
                    {
                        var targetTransform = SystemAPI.GetComponent<LocalTransform>(craftState.Target);
                        float distance = math.distance(transform.Position, targetTransform.Position);

                        if (distance <= config.AttackRange)
                        {
                            craftState.Phase = AttackRunPhase.Execute;
                            craftState.PhaseTimer = 20; // Attack duration
                        }
                    }
                    else
                    {
                        // Lost target, disengage
                        craftState.Phase = AttackRunPhase.Disengage;
                    }
                    break;

                case AttackRunPhase.Execute:
                    // Attack pass complete
                    craftState.PassCount++;
                    craftState.Phase = AttackRunPhase.Disengage;
                    craftState.PhaseTimer = 15;
                    break;

                case AttackRunPhase.Disengage:
                    // Check if should re-attack or return
                    bool shouldReattack = config.ReattackEnabled == 1 &&
                                         craftState.PassCount < config.MaxPasses &&
                                         craftState.WeaponsExpended == 0;

                    // Check recall thresholds
                    if (SystemAPI.HasComponent<RecallState>(entity))
                    {
                        var recallState = SystemAPI.GetComponent<RecallState>(entity);
                        if (recallState.IsRecalling == 1)
                        {
                            shouldReattack = false;
                        }
                    }

                    if (shouldReattack)
                    {
                        craftState.Phase = AttackRunPhase.Approach;
                    }
                    else
                    {
                        craftState.Phase = AttackRunPhase.Return;
                    }
                    break;

                case AttackRunPhase.Return:
                    // Check distance to carrier
                    if (craftState.Carrier != Entity.Null && SystemAPI.HasComponent<LocalTransform>(craftState.Carrier))
                    {
                        var carrierTransform = SystemAPI.GetComponent<LocalTransform>(craftState.Carrier);
                        float distance = math.distance(transform.Position, carrierTransform.Position);

                        if (distance < 50f)
                        {
                            craftState.Phase = AttackRunPhase.Landing;
                            craftState.PhaseTimer = 10;
                        }
                    }
                    break;

                case AttackRunPhase.Landing:
                    // Complete landing
                    craftState.Phase = AttackRunPhase.Docked;
                    craftState.Target = Entity.Null;
                    craftState.PassCount = 0;
                    craftState.WeaponsExpended = 0;
                    break;

                case AttackRunPhase.CombatAirPatrol:
                    if (craftState.Target != Entity.Null)
                    {
                        craftState.Phase = AttackRunPhase.Launching;
                        craftState.PhaseTimer = 10;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Handles strike craft formation during form-up and attack.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XStrikeCraftFormationSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _alignmentLookup.Update(ref state);
            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithNone<StrikeCraftDogfightTag>()
                    .WithEntityAccess())
            {
                var lawfulness = 0.5f;
                var chaos = 0.5f;
                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    lawfulness = AlignmentMath.Lawfulness(alignment);
                    chaos = AlignmentMath.Chaos(alignment);
                }

                // Get stance
                VesselStanceMode stance = VesselStanceMode.Balanced;
                if (SystemAPI.HasComponent<PatrolStance>(craftState.ValueRO.Carrier))
                {
                    stance = SystemAPI.GetComponent<PatrolStance>(craftState.ValueRO.Carrier).Stance;
                }

                var tuning = stanceConfig.Resolve(stance);
                var maintainFormation = tuning.MaintainFormationWhenAttacking >= 0.5f;

                // Only apply formation during form-up/approach, and keep lawful wings tight into execute.
                var canHoldFormation = craftState.ValueRO.Phase == AttackRunPhase.FormUp ||
                                       craftState.ValueRO.Phase == AttackRunPhase.Approach ||
                                       (craftState.ValueRO.Phase == AttackRunPhase.Execute && (maintainFormation || lawfulness >= 0.6f));

                if (!canHoldFormation)
                {
                    continue;
                }

                // Skip if no wing leader
                if (craftState.ValueRO.WingLeader == Entity.Null || craftState.ValueRO.WingPosition == 0)
                {
                    continue;
                }

                // Get wing leader position
                if (!SystemAPI.HasComponent<LocalTransform>(craftState.ValueRO.WingLeader))
                {
                    continue;
                }

                var leaderTransform = SystemAPI.GetComponent<LocalTransform>(craftState.ValueRO.WingLeader);

                // Calculate formation offset
                var spacing = config.ValueRO.FormationSpacing * math.lerp(1.5f, 0.75f, lawfulness);
                float3 offset = StrikeCraftUtility.CalculateWingOffset(
                    stance,
                    craftState.ValueRO.WingPosition,
                    spacing
                );

                if (chaos > 0.25f)
                {
                    var jitter = math.saturate(chaos * 0.6f) * spacing * 0.15f;
                    offset += new float3(
                        math.sin((entity.Index + 1) * 1.37f) * jitter,
                        math.cos((entity.Index + 1) * 0.91f) * jitter * 0.4f,
                        math.sin((entity.Index + 1) * 1.73f) * jitter
                    );
                }

                // Transform offset by leader rotation
                float3 worldOffset = math.rotate(leaderTransform.Rotation, offset);
                float3 targetPosition = leaderTransform.Position + worldOffset;

                // Smoothly move toward formation position
                transform.ValueRW.Position = math.lerp(transform.ValueRO.Position, targetPosition, 0.1f);
                transform.ValueRW.Rotation = leaderTransform.Rotation;
            }
        }
    }

    /// <summary>
    /// Handles strike craft movement during approach and return.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftFormationSystem))]
    public partial struct Space4XStrikeCraftMovementSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<StrikeCraftKinematics> _kinematicsLookup;
        private ComponentLookup<VesselMovement> _vesselMovementLookup;
        private uint _lastTick;
        private const float PnNavConstant = 3.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            _lastTick = 0;
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _kinematicsLookup = state.GetComponentLookup<StrikeCraftKinematics>(true);
            _vesselMovementLookup = state.GetComponentLookup<VesselMovement>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tick = timeState.Tick;
            var tickDelta = _lastTick == 0u ? 1u : (tick > _lastTick ? tick - _lastTick : 1u);
            _lastTick = tick;
            var deltaTime = timeState.FixedDeltaTime * tickDelta;
            var worldSeconds = timeState.WorldSeconds;

            var missingKinematics = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftProfile, LocalTransform>()
                .WithNone<StrikeCraftKinematics>()
                .Build();
            if (!missingKinematics.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                state.EntityManager.AddComponent<StrikeCraftKinematics>(missingKinematics);
            }

            _alignmentLookup.Update(ref state);
            _kinematicsLookup.Update(ref state);
            _vesselMovementLookup.Update(ref state);

            foreach (var (craftState, config, transform, kinematics, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>, RefRW<StrikeCraftKinematics>>()
                    .WithNone<StrikeCraftDogfightTag>()
                    .WithEntityAccess())
            {
                float speed = 50f; // Base speed
                var chaos = 0.5f;
                if (_alignmentLookup.HasComponent(entity))
                {
                    chaos = AlignmentMath.Chaos(_alignmentLookup[entity]);
                }

                if (craftState.ValueRO.Phase == AttackRunPhase.Docked ||
                    craftState.ValueRO.Phase == AttackRunPhase.FormUp ||
                    craftState.ValueRO.Phase == AttackRunPhase.Launching ||
                    craftState.ValueRO.Phase == AttackRunPhase.Landing)
                {
                    kinematics.ValueRW.Velocity = float3.zero;
                    continue;
                }

                var velocity = kinematics.ValueRO.Velocity;
                var accel = speed * 1.8f;
                var decel = speed * 2.4f;

                switch (craftState.ValueRO.Phase)
                {
                    case AttackRunPhase.Approach:
                        // Move toward target
                        if (craftState.ValueRO.Target != Entity.Null &&
                            SystemAPI.HasComponent<LocalTransform>(craftState.ValueRO.Target))
                        {
                            var targetTransform = SystemAPI.GetComponent<LocalTransform>(craftState.ValueRO.Target);
                            float3 direction = StrikeCraftUtility.CalculateApproachVector(
                                config.ValueRO.ApproachVector,
                                targetTransform.Position,
                                transform.ValueRO.Position
                            );
                            var toTarget = targetTransform.Position - transform.ValueRO.Position;
                            var distance = math.length(toTarget);

                            if (chaos > 0.4f)
                            {
                                var sway = math.sin((entity.Index + 1) * 0.12f + worldSeconds * 0.7f) * chaos * 0.35f;
                                var right = math.normalize(math.cross(direction, math.up()));
                                direction = math.normalize(direction + right * sway);
                            }

                            var approachSpeed = speed * (float)config.ValueRO.ApproachSpeedMod;
                            var targetVelocity = ResolveTargetVelocity(craftState.ValueRO.Target, _vesselMovementLookup, _kinematicsLookup);
                            var pnAccel = ComputePnAcceleration(toTarget, targetVelocity - velocity);
                            var desiredVelocity = direction * approachSpeed + pnAccel * deltaTime;
                            var slowdownDistance = math.max(8f, config.ValueRO.AttackRange * 0.2f);

                            ApplyArrivalBraking(ref desiredVelocity, ref direction, velocity, toTarget, distance, slowdownDistance, decel, config.ValueRO.BrakeLeadFactor);
                            ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, direction, deltaTime, accel, decel);
                        }
                        else
                        {
                            velocity = float3.zero;
                        }
                        break;

                    case AttackRunPhase.Execute:
                        // Continue through target
                        {
                            var forward = math.normalizesafe(velocity, math.forward(transform.ValueRO.Rotation));
                            var attackSpeed = speed * (float)config.ValueRO.AttackSpeedMod;
                            var desiredVelocity = forward * attackSpeed;
                            ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, forward, deltaTime, accel, decel);
                        }
                        break;

                    case AttackRunPhase.Disengage:
                        // Break away from target
                        {
                            var breakDir = math.normalizesafe(velocity, math.forward(transform.ValueRO.Rotation));
                            var desiredVelocity = breakDir * speed * 1.2f;
                            ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, breakDir, deltaTime, accel, decel);
                        }
                        break;

                    case AttackRunPhase.Return:
                        // Return to carrier
                        if (craftState.ValueRO.Carrier != Entity.Null &&
                            SystemAPI.HasComponent<LocalTransform>(craftState.ValueRO.Carrier))
                        {
                            var carrierTransform = SystemAPI.GetComponent<LocalTransform>(craftState.ValueRO.Carrier);
                            var toCarrier = carrierTransform.Position - transform.ValueRO.Position;
                            var distance = math.length(toCarrier);
                            var direction = math.normalizesafe(toCarrier);
                            var desiredSpeed = speed;
                            var desiredVelocity = direction * desiredSpeed;
                            var slowdownDistance = 50f;
                            ApplyArrivalBraking(ref desiredVelocity, ref direction, velocity, toCarrier, distance, slowdownDistance, decel, config.ValueRO.BrakeLeadFactor);
                            ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, direction, deltaTime, accel, decel);
                        }
                        else
                        {
                            velocity = float3.zero;
                        }
                        break;

                    case AttackRunPhase.CombatAirPatrol:
                        if (craftState.ValueRO.Carrier != Entity.Null &&
                            SystemAPI.HasComponent<LocalTransform>(craftState.ValueRO.Carrier))
                        {
                            var carrierTransform = SystemAPI.GetComponent<LocalTransform>(craftState.ValueRO.Carrier);
                            var role = craftState.ValueRO.Role;
                            var isNonCombat = role == StrikeCraftRole.Recon || role == StrikeCraftRole.EWar;
                            var baseRadius = isNonCombat ? 25f : 65f;
                            var orbitRate = isNonCombat ? 0.35f : 0.8f;

                            var angle = (entity.Index % 360) * math.radians(1f) + worldSeconds * orbitRate;
                            var orbitOffset = new float3(math.cos(angle), 0f, math.sin(angle)) * baseRadius;

                            if (isNonCombat)
                            {
                                var anchor = carrierTransform.Position + new float3(0f, 0f, -baseRadius * 0.4f);
                                var toAnchor = anchor - transform.ValueRO.Position;
                                if (math.lengthsq(toAnchor) > 0.01f)
                                {
                                    var direction = math.normalizesafe(toAnchor);
                                    var desiredVelocity = direction * speed * 0.35f;
                                    ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, direction, deltaTime, accel, decel);
                                }
                                else
                                {
                                    velocity = float3.zero;
                                    transform.ValueRW.Rotation = carrierTransform.Rotation;
                                }
                            }
                            else
                            {
                                var targetPosition = carrierTransform.Position + orbitOffset;
                                var toTarget = targetPosition - transform.ValueRO.Position;
                                var patrolSpeed = speed * 0.45f;
                                if (math.lengthsq(toTarget) > 0.01f)
                                {
                                    var direction = math.normalizesafe(toTarget);
                                    var desiredVelocity = direction * patrolSpeed;
                                    ApplySteering(ref transform.ValueRW, ref velocity, desiredVelocity, direction, deltaTime, accel, decel);
                                }
                                else
                                {
                                    velocity = float3.zero;
                                }
                            }
                        }
                        break;
                }

                kinematics.ValueRW.Velocity = velocity;
            }
        }

        private static float3 ResolveTargetVelocity(
            Entity target,
            in ComponentLookup<VesselMovement> vesselMovementLookup,
            in ComponentLookup<StrikeCraftKinematics> kinematicsLookup)
        {
            if (vesselMovementLookup.HasComponent(target))
            {
                return vesselMovementLookup[target].Velocity;
            }

            if (kinematicsLookup.HasComponent(target))
            {
                return kinematicsLookup[target].Velocity;
            }

            return float3.zero;
        }

        private static float3 ComputePnAcceleration(float3 relativePosition, float3 relativeVelocity)
        {
            var distanceSq = math.lengthsq(relativePosition);
            if (distanceSq < 1e-4f)
            {
                return float3.zero;
            }

            var distance = math.sqrt(distanceSq);
            var los = relativePosition / distance;
            var closingSpeed = math.max(0f, -math.dot(relativeVelocity, los));
            if (closingSpeed <= 0f)
            {
                return float3.zero;
            }

            var losRate = math.cross(relativePosition, relativeVelocity) / distanceSq;
            return PnNavConstant * closingSpeed * math.cross(losRate, los);
        }

        private static void ApplyArrivalBraking(
            ref float3 desiredVelocity,
            ref float3 desiredDirection,
            float3 currentVelocity,
            float3 toTarget,
            float distance,
            float slowdownDistance,
            float deceleration,
            float brakeLeadFactor)
        {
            if (slowdownDistance <= 0f || distance <= 0f)
            {
                return;
            }

            if (distance < slowdownDistance)
            {
                var speedScale = math.saturate(distance / slowdownDistance);
                desiredVelocity *= speedScale;
            }

            var currentSpeedSq = math.lengthsq(currentVelocity);
            if (currentSpeedSq < 1e-4f)
            {
                return;
            }

            if (brakeLeadFactor > 0f && deceleration > 0f)
            {
                var leadDistance = (currentSpeedSq / (2f * deceleration)) * brakeLeadFactor;
                if (leadDistance > 0.001f && distance < leadDistance)
                {
                    desiredVelocity *= math.saturate(distance / leadDistance);
                }
            }

            var overshoot = math.dot(currentVelocity, toTarget) < 0f;
            var retrogradeWeight = overshoot
                ? 1f
                : math.saturate((slowdownDistance - distance) / math.max(1e-4f, slowdownDistance));

            if (retrogradeWeight <= 0f)
            {
                return;
            }

            var retroDir = math.normalizesafe(-currentVelocity, desiredDirection);
            var retroVelocity = retroDir * math.length(desiredVelocity);
            desiredVelocity = math.lerp(desiredVelocity, retroVelocity, retrogradeWeight);
            desiredDirection = math.normalizesafe(math.lerp(desiredDirection, retroDir, retrogradeWeight), retroDir);
        }

        private static void ApplySteering(
            ref LocalTransform transform,
            ref float3 velocity,
            float3 desiredVelocity,
            float3 desiredDirection,
            float deltaTime,
            float acceleration,
            float deceleration)
        {
            var currentSpeed = math.length(velocity);
            var desiredSpeed = math.length(desiredVelocity);
            var accelLimit = desiredSpeed > currentSpeed ? acceleration : deceleration;
            var maxDelta = accelLimit * deltaTime;
            var deltaV = desiredVelocity - velocity;
            var deltaSq = math.lengthsq(deltaV);
            if (maxDelta > 0f && deltaSq > maxDelta * maxDelta)
            {
                deltaV = math.normalizesafe(deltaV) * maxDelta;
            }

            velocity += deltaV;
            transform.Position += velocity * deltaTime;

            if (math.lengthsq(velocity) > 0.001f)
            {
                var forward = math.normalizesafe(velocity, desiredDirection);
                transform.Rotation = quaternion.LookRotationSafe(forward, math.up());
            }
        }
    }

    /// <summary>
    /// Updates strike craft experience after sorties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XStrikeCraftExperienceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftExperience>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (craftState, experience, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRW<StrikeCraftExperience>>()
                    .WithNone<StrikeCraftDogfightTag>()
                    .WithEntityAccess())
            {
                // Check for sortie completion
                if (craftState.ValueRO.Phase == AttackRunPhase.Docked)
                {
                    // Check for OnSortieTag removal (sortie just completed)
                    bool wasOnSortie = SystemAPI.HasComponent<OnSortieTag>(entity);
                    if (!wasOnSortie)
                    {
                        continue;
                    }

                    // Update stats
                    experience.ValueRW.SortieCount++;
                    experience.ValueRW.SurvivalCount++;

                    // Grant XP
                    uint xp = StrikeCraftUtility.CalculateSortieXP(true, 0, false);
                    experience.ValueRW.ExperiencePoints += xp;

                    // Check for level up
                    uint xpNeeded = StrikeCraftUtility.XPForLevel(experience.ValueRO.Level);
                    if (experience.ValueRO.ExperiencePoints >= xpNeeded && experience.ValueRO.Level < 5)
                    {
                        experience.ValueRW.Level++;

                        // Grant trait based on level
                        experience.ValueRW.Traits |= experience.ValueRO.Level switch
                        {
                            1 => StrikeCraftTraits.EvasiveManeuvers,
                            2 => StrikeCraftTraits.FormationDiscipline,
                            3 => StrikeCraftTraits.TargetPrioritization,
                            4 => StrikeCraftTraits.PrecisionStrike,
                            5 => StrikeCraftTraits.AceStatus,
                            _ => StrikeCraftTraits.None
                        };
                    }

                    ecb.RemoveComponent<OnSortieTag>(entity);
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for strike craft operations.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XStrikeCraftTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalCraft = 0;
            int dockedCraft = 0;
            int approachingCraft = 0;
            int executingCraft = 0;
            int returningCraft = 0;
            int totalSorties = 0;
            int aceCount = 0;

            foreach (var (craftState, experience) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<StrikeCraftExperience>>()
                    .WithNone<StrikeCraftDogfightTag>())
            {
                totalCraft++;
                totalSorties += (int)experience.ValueRO.SortieCount;

                if ((experience.ValueRO.Traits & StrikeCraftTraits.AceStatus) != 0)
                {
                    aceCount++;
                }

                switch (craftState.ValueRO.Phase)
                {
                    case AttackRunPhase.Docked:
                    case AttackRunPhase.Launching:
                    case AttackRunPhase.Landing:
                        dockedCraft++;
                        break;
                    case AttackRunPhase.FormUp:
                    case AttackRunPhase.Approach:
                        approachingCraft++;
                        break;
                    case AttackRunPhase.Execute:
                        executingCraft++;
                        break;
                    case AttackRunPhase.Disengage:
                    case AttackRunPhase.Return:
                        returningCraft++;
                        break;
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Strike Craft] Total: {totalCraft}, Docked: {dockedCraft}, Approaching: {approachingCraft}, Executing: {executingCraft}, Returning: {returningCraft}, Aces: {aceCount}");
        }
    }
}

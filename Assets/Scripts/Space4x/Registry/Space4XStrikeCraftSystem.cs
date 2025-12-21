using PureDOTS.Runtime.Components;
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

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
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

                // Only apply formation during form-up/approach, and keep lawful wings tight into execute.
                var canHoldFormation = craftState.ValueRO.Phase == AttackRunPhase.FormUp ||
                                       craftState.ValueRO.Phase == AttackRunPhase.Approach ||
                                       (craftState.ValueRO.Phase == AttackRunPhase.Execute && lawfulness >= 0.6f);

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

                // Get stance
                VesselStanceMode stance = VesselStanceMode.Balanced;
                if (SystemAPI.HasComponent<PatrolStance>(craftState.ValueRO.Carrier))
                {
                    stance = SystemAPI.GetComponent<PatrolStance>(craftState.ValueRO.Carrier).Stance;
                }

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
        private uint _lastTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            _lastTick = 0;
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
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

            _alignmentLookup.Update(ref state);

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                float speed = 50f; // Base speed
                var chaos = 0.5f;
                if (_alignmentLookup.HasComponent(entity))
                {
                    chaos = AlignmentMath.Chaos(_alignmentLookup[entity]);
                }

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

                            if (chaos > 0.4f)
                            {
                                var sway = math.sin((entity.Index + 1) * 0.12f + worldSeconds * 0.7f) * chaos * 0.35f;
                                var right = math.normalize(math.cross(direction, math.up()));
                                direction = math.normalize(direction + right * sway);
                            }

                            float approachSpeed = speed * (float)config.ValueRO.ApproachSpeedMod;
                            transform.ValueRW.Position += direction * approachSpeed * deltaTime;
                            transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, new float3(0, 1, 0));
                        }
                        break;

                    case AttackRunPhase.Execute:
                        // Continue through target
                        float3 forward = math.forward(transform.ValueRO.Rotation);
                        float attackSpeed = speed * (float)config.ValueRO.AttackSpeedMod;
                        transform.ValueRW.Position += forward * attackSpeed * deltaTime;
                        break;

                    case AttackRunPhase.Disengage:
                        // Break away from target
                        float3 breakDir = math.forward(transform.ValueRO.Rotation);
                        transform.ValueRW.Position += breakDir * speed * 1.2f * deltaTime;
                        break;

                    case AttackRunPhase.Return:
                        // Return to carrier
                        if (craftState.ValueRO.Carrier != Entity.Null &&
                            SystemAPI.HasComponent<LocalTransform>(craftState.ValueRO.Carrier))
                        {
                            var carrierTransform = SystemAPI.GetComponent<LocalTransform>(craftState.ValueRO.Carrier);
                            float3 toCarrier = math.normalize(carrierTransform.Position - transform.ValueRO.Position);
                            transform.ValueRW.Position += toCarrier * speed * deltaTime;
                            transform.ValueRW.Rotation = quaternion.LookRotationSafe(toCarrier, new float3(0, 1, 0));
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
                                transform.ValueRW.Position = math.lerp(transform.ValueRO.Position, anchor, math.saturate(deltaTime * 2f));
                                transform.ValueRW.Rotation = carrierTransform.Rotation;
                            }
                            else
                            {
                                var targetPosition = carrierTransform.Position + orbitOffset;
                                var toTarget = targetPosition - transform.ValueRO.Position;
                                var patrolSpeed = speed * 0.45f;
                                if (math.lengthsq(toTarget) > 0.01f)
                                {
                                    var direction = math.normalize(toTarget);
                                    transform.ValueRW.Position += direction * patrolSpeed * deltaTime;
                                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, new float3(0, 1, 0));
                                }
                            }
                        }
                        break;
                }
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
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<StrikeCraftExperience>>())
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

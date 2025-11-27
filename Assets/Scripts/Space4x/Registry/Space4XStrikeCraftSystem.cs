using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Main state machine for strike craft attack runs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XStrikeCraftSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRW<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
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

                ProcessPhase(ref craftState.ValueRW, config.ValueRO, ref transform.ValueRW, stance, deltaTime, entity, ref state);
            }
        }

        private void ProcessPhase(
            ref StrikeCraftProfile craftState,
            in AttackRunConfig config,
            ref LocalTransform transform,
            VesselStanceMode stance,
            float deltaTime,
            Entity entity,
            ref SystemState systemState)
        {
            // Decrement phase timer
            if (craftState.PhaseTimer > 0)
            {
                craftState.PhaseTimer--;
                return;
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
                    // Patrol behavior - check for targets
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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Only apply formation during form-up and approach
                if (craftState.ValueRO.Phase != AttackRunPhase.FormUp &&
                    craftState.ValueRO.Phase != AttackRunPhase.Approach)
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
                float3 offset = StrikeCraftUtility.CalculateWingOffset(
                    stance,
                    craftState.ValueRO.WingPosition,
                    config.ValueRO.FormationSpacing
                );

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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (craftState, config, transform, entity) in
                SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                float speed = 50f; // Base speed

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


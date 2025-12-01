using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates carrier presentation based on simulation state.
    /// Reads carrier sim data and writes visual state and material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XCarrierPresentationSystem : ISystem
    {
        // Pre-defined color constants for Burst compatibility
        private static readonly float4 IdleColorMod = new float4(1f, 1f, 1f, 1f);
        private static readonly float4 PatrolColorMod = new float4(1f, 1f, 1.1f, 1f);
        private static readonly float4 MiningColorMod = new float4(1.2f, 0.9f, 0.7f, 1f);
        private static readonly float4 CombatColorMod = new float4(1.3f, 0.6f, 0.6f, 1f);
        private static readonly float4 RetreatingColorMod = new float4(0.7f, 0.8f, 1.2f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update carriers with FleetMovementBroadcast (if available)
            new UpdateCarrierWithMovementJob
            {
                DeltaTime = deltaTime,
                IdleColorMod = IdleColorMod,
                PatrolColorMod = PatrolColorMod,
                MiningColorMod = MiningColorMod,
                CombatColorMod = CombatColorMod,
                RetreatingColorMod = RetreatingColorMod
            }.ScheduleParallel();

            // Update carriers without FleetMovementBroadcast
            new UpdateCarrierPresentationJob
            {
                DeltaTime = deltaTime,
                IdleColorMod = IdleColorMod,
                PatrolColorMod = PatrolColorMod,
                MiningColorMod = MiningColorMod,
                CombatColorMod = CombatColorMod,
                RetreatingColorMod = RetreatingColorMod
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CarrierPresentationTag))]
        [WithNone(typeof(FleetMovementBroadcast))]
        private partial struct UpdateCarrierPresentationJob : IJobEntity
        {
            public float DeltaTime;
            public float4 IdleColorMod;
            public float4 PatrolColorMod;
            public float4 MiningColorMod;
            public float4 CombatColorMod;
            public float4 RetreatingColorMod;

            public void Execute(
                ref CarrierVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in Carrier carrier,
                in FactionColor factionColor,
                in PresentationLOD lod,
                in LocalTransform transform)
            {
                // Skip hidden entities
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update state timer
                visualState.StateTimer += DeltaTime;

                // Determine visual state from carrier data
                // Use patrol radius and position to determine if patrolling
                float distanceToPatrolCenter = math.distance(transform.Position, carrier.PatrolCenter);
                bool isPatrolling = carrier.PatrolRadius > 0f && distanceToPatrolCenter <= carrier.PatrolRadius * 1.1f;

                if (isPatrolling)
                {
                    visualState.State = CarrierVisualStateType.Patrolling;
                }
                else
                {
                    visualState.State = CarrierVisualStateType.Idle;
                }

                // Apply color modifiers based on state
                float4 colorMod = visualState.State switch
                {
                    CarrierVisualStateType.Idle => IdleColorMod,
                    CarrierVisualStateType.Patrolling => PatrolColorMod,
                    CarrierVisualStateType.Mining => MiningColorMod,
                    CarrierVisualStateType.Combat => CombatColorMod,
                    CarrierVisualStateType.Retreating => RetreatingColorMod,
                    _ => IdleColorMod
                };

                // Calculate pulse effect for patrolling state
                float pulse = 1f;
                if (visualState.State == CarrierVisualStateType.Patrolling)
                {
                    pulse = 0.9f + 0.1f * math.sin(visualState.StateTimer * 2f);
                }
                else if (visualState.State == CarrierVisualStateType.Combat)
                {
                    // Faster flashing for combat
                    pulse = 0.7f + 0.3f * math.sin(visualState.StateTimer * 8f);
                }

                // Apply faction color with state modifier
                materialProps.BaseColor = factionColor.Value * colorMod * pulse;
                materialProps.Alpha = 1f;
                materialProps.PulsePhase = visualState.StateTimer;

                // Add emissive for combat state
                if (visualState.State == CarrierVisualStateType.Combat)
                {
                    materialProps.EmissiveColor = new float4(1f, 0.2f, 0.2f, 1f) * 0.5f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(CarrierPresentationTag))]
        [WithAll(typeof(FleetMovementBroadcast))]
        private partial struct UpdateCarrierWithMovementJob : IJobEntity
        {
            public float DeltaTime;
            public float4 IdleColorMod;
            public float4 PatrolColorMod;
            public float4 MiningColorMod;
            public float4 CombatColorMod;
            public float4 RetreatingColorMod;

            public void Execute(
                ref CarrierVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in Carrier carrier,
                in FactionColor factionColor,
                in PresentationLOD lod,
                in LocalTransform transform,
                in FleetMovementBroadcast movementBroadcast)
            {
                // Skip hidden entities
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update state timer
                visualState.StateTimer += DeltaTime;

                // Determine visual state from carrier data and movement
                float distanceToPatrolCenter = math.distance(transform.Position, carrier.PatrolCenter);
                bool isPatrolling = carrier.PatrolRadius > 0f && distanceToPatrolCenter <= carrier.PatrolRadius * 1.1f;
                bool isMoving = math.lengthsq(movementBroadcast.Velocity) > 0.01f;

                if (isPatrolling && isMoving)
                {
                    visualState.State = CarrierVisualStateType.Patrolling;
                }
                else if (isMoving)
                {
                    visualState.State = CarrierVisualStateType.Patrolling; // Moving counts as patrolling
                }
                else
                {
                    visualState.State = CarrierVisualStateType.Idle;
                }

                // Apply color modifiers based on state
                float4 colorMod = visualState.State switch
                {
                    CarrierVisualStateType.Idle => IdleColorMod,
                    CarrierVisualStateType.Patrolling => PatrolColorMod,
                    CarrierVisualStateType.Mining => MiningColorMod,
                    CarrierVisualStateType.Combat => CombatColorMod,
                    CarrierVisualStateType.Retreating => RetreatingColorMod,
                    _ => IdleColorMod
                };

                // Calculate pulse effect for patrolling state
                float pulse = 1f;
                if (visualState.State == CarrierVisualStateType.Patrolling)
                {
                    pulse = 0.9f + 0.1f * math.sin(visualState.StateTimer * 2f);
                }
                else if (visualState.State == CarrierVisualStateType.Combat)
                {
                    // Faster flashing for combat
                    pulse = 0.7f + 0.3f * math.sin(visualState.StateTimer * 8f);
                }

                // Apply faction color with state modifier
                materialProps.BaseColor = factionColor.Value * colorMod * pulse;
                materialProps.Alpha = 1f;
                materialProps.PulsePhase = visualState.StateTimer;

                // Add emissive for combat state
                if (visualState.State == CarrierVisualStateType.Combat)
                {
                    materialProps.EmissiveColor = new float4(1f, 0.2f, 0.2f, 1f) * 0.5f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }
            }
        }
    }

    /// <summary>
    /// System that updates carrier visual state based on fleet posture (if available).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Space4XCarrierPresentationSystem))]
    public partial struct Space4XCarrierStateFromFleetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Update carrier visual state based on fleet posture
            foreach (var (visualState, fleet) in SystemAPI
                         .Query<RefRW<CarrierVisualState>, RefRO<Space4XFleet>>()
                         .WithAll<CarrierPresentationTag>())
            {
                visualState.ValueRW.State = fleet.ValueRO.Posture switch
                {
                    Space4XFleetPosture.Idle => CarrierVisualStateType.Idle,
                    Space4XFleetPosture.Patrol => CarrierVisualStateType.Patrolling,
                    Space4XFleetPosture.Engaging => CarrierVisualStateType.Combat,
                    Space4XFleetPosture.Retreating => CarrierVisualStateType.Retreating,
                    Space4XFleetPosture.Docked => CarrierVisualStateType.Idle,
                    _ => CarrierVisualStateType.Idle
                };
            }
        }
    }
}


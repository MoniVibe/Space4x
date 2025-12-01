using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates craft/mining vessel presentation based on simulation state.
    /// Reads vessel AI state and writes visual state and material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XCraftPresentationSystem : ISystem
    {
        // Pre-defined color constants for Burst compatibility
        private static readonly float4 IdleColor = new float4(0.5f, 0.5f, 0.5f, 1f);      // Gray (docked)
        private static readonly float4 MiningColor = new float4(1f, 0.6f, 0.2f, 1f);      // Orange
        private static readonly float4 ReturningColor = new float4(0.2f, 1f, 0.4f, 1f);   // Green
        private static readonly float4 MovingColorMod = new float4(1f, 1f, 1f, 1f);       // Faction color

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CraftPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update crafts with VesselAIState (full AI state available)
            new UpdateCraftWithAIStateJob
            {
                DeltaTime = deltaTime,
                IdleColor = IdleColor,
                MiningColor = MiningColor,
                ReturningColor = ReturningColor,
                MovingColorMod = MovingColorMod
            }.ScheduleParallel();

            // Update crafts with MiningVessel but no VesselAIState (simpler state)
            new UpdateCraftWithMiningVesselJob
            {
                DeltaTime = deltaTime,
                IdleColor = IdleColor,
                MiningColor = MiningColor,
                ReturningColor = ReturningColor
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CraftPresentationTag))]
        private partial struct UpdateCraftWithAIStateJob : IJobEntity
        {
            public float DeltaTime;
            public float4 IdleColor;
            public float4 MiningColor;
            public float4 ReturningColor;
            public float4 MovingColorMod;

            public void Execute(
                ref CraftVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in VesselAIState aiState,
                in FactionColor factionColor,
                in PresentationLOD lod)
            {
                // Skip hidden entities
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update state timer
                visualState.StateTimer += DeltaTime;

                // Map AI state to visual state
                visualState.State = aiState.CurrentState switch
                {
                    VesselAIState.State.Idle => CraftVisualStateType.Idle,
                    VesselAIState.State.Mining => CraftVisualStateType.Mining,
                    VesselAIState.State.Returning => CraftVisualStateType.Returning,
                    VesselAIState.State.MovingToTarget => CraftVisualStateType.Moving,
                    _ => CraftVisualStateType.Idle
                };

                // Apply color based on state
                float4 baseColor = visualState.State switch
                {
                    CraftVisualStateType.Idle => IdleColor,
                    CraftVisualStateType.Mining => MiningColor,
                    CraftVisualStateType.Returning => ReturningColor,
                    CraftVisualStateType.Moving => factionColor.Value * MovingColorMod,
                    _ => IdleColor
                };

                // Calculate pulse effect for mining state
                float pulse = 1f;
                if (visualState.State == CraftVisualStateType.Mining)
                {
                    pulse = 0.8f + 0.2f * math.sin(visualState.StateTimer * 4f);
                }

                materialProps.BaseColor = baseColor * pulse;
                materialProps.Alpha = 1f;
                materialProps.PulsePhase = visualState.StateTimer;

                // Add emissive for mining state
                if (visualState.State == CraftVisualStateType.Mining)
                {
                    materialProps.EmissiveColor = MiningColor * 0.3f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(CraftPresentationTag))]
        [WithNone(typeof(VesselAIState))]
        private partial struct UpdateCraftWithMiningVesselJob : IJobEntity
        {
            public float DeltaTime;
            public float4 IdleColor;
            public float4 MiningColor;
            public float4 ReturningColor;

            public void Execute(
                ref CraftVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in MiningVessel miningVessel,
                in FactionColor factionColor,
                in PresentationLOD lod)
            {
                // Skip hidden entities
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update state timer
                visualState.StateTimer += DeltaTime;

                // Determine state based on MiningVessel data
                // For now, use simple heuristics (could be expanded with MiningState component)
                if (miningVessel.CurrentCargo > 0f && miningVessel.CurrentCargo >= miningVessel.CargoCapacity * 0.8f)
                {
                    visualState.State = CraftVisualStateType.Returning;
                }
                else if (miningVessel.CurrentCargo > 0f)
                {
                    visualState.State = CraftVisualStateType.Mining;
                }
                else
                {
                    visualState.State = CraftVisualStateType.Idle;
                }

                // Apply color based on state
                float4 baseColor = visualState.State switch
                {
                    CraftVisualStateType.Idle => IdleColor,
                    CraftVisualStateType.Mining => MiningColor,
                    CraftVisualStateType.Returning => ReturningColor,
                    _ => IdleColor
                };

                // Calculate pulse effect for mining state
                float pulse = 1f;
                if (visualState.State == CraftVisualStateType.Mining)
                {
                    pulse = 0.8f + 0.2f * math.sin(visualState.StateTimer * 4f);
                }

                materialProps.BaseColor = baseColor * pulse;
                materialProps.Alpha = 1f;
                materialProps.PulsePhase = visualState.StateTimer;
            }
        }
    }
}


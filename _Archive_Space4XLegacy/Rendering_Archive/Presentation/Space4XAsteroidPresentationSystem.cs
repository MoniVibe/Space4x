using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates asteroid presentation based on simulation state.
    /// Reads asteroid resource state and writes visual state and material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XAsteroidPresentationSystem : ISystem
    {
        // Pre-defined color constants for Burst compatibility
        private static readonly float4 DepletedColor = new float4(0.3f, 0.3f, 0.3f, 0.5f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AsteroidPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update asteroids with full Asteroid component
            new UpdateAsteroidPresentationJob
            {
                DeltaTime = deltaTime,
                DepletedColor = DepletedColor
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AsteroidPresentationTag))]
        private partial struct UpdateAsteroidPresentationJob : IJobEntity
        {
            public float DeltaTime;
            public float4 DepletedColor;

            public void Execute(
                ref AsteroidVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in Asteroid asteroid,
                in ResourceTypeColor resourceColor,
                in PresentationLOD lod)
            {
                // Skip hidden entities
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update state timer
                visualState.StateTimer += DeltaTime;

                // Calculate depletion ratio
                float maxResources = asteroid.MaxResourceAmount;
                float currentResources = asteroid.ResourceAmount;
                float depletionRatio = maxResources > 0f ? 1f - (currentResources / maxResources) : 1f;
                visualState.DepletionRatio = depletionRatio;

                // Determine visual state based on depletion
                // Note: MiningActive state would require additional tracking of active miners
                // For now, we use depletion ratio to determine state
                if (depletionRatio >= 0.95f)
                {
                    visualState.State = AsteroidVisualStateType.Depleted;
                }
                else if (depletionRatio > 0.05f)
                {
                    // If partially depleted, assume mining is active
                    visualState.State = AsteroidVisualStateType.MiningActive;
                }
                else
                {
                    visualState.State = AsteroidVisualStateType.Full;
                }

                // Apply color based on state
                float4 baseColor;
                float pulse = 1f;

                switch (visualState.State)
                {
                    case AsteroidVisualStateType.Full:
                        baseColor = resourceColor.Value;
                        break;

                    case AsteroidVisualStateType.MiningActive:
                        baseColor = resourceColor.Value;
                        // Pulsing effect when being mined
                        pulse = 0.7f + 0.3f * math.sin(visualState.StateTimer * 3f);
                        break;

                    case AsteroidVisualStateType.Depleted:
                        baseColor = DepletedColor;
                        break;

                    default:
                        baseColor = resourceColor.Value;
                        break;
                }

                // Lerp towards depleted color based on depletion ratio
                baseColor = math.lerp(baseColor, DepletedColor, depletionRatio * 0.5f);

                materialProps.BaseColor = baseColor * pulse;
                materialProps.Alpha = 1f - (depletionRatio * 0.5f); // More depleted = more transparent
                materialProps.PulsePhase = visualState.StateTimer;

                // Add emissive for mining active state
                if (visualState.State == AsteroidVisualStateType.MiningActive)
                {
                    materialProps.EmissiveColor = resourceColor.Value * 0.2f * pulse;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }
            }
        }
    }

    /// <summary>
    /// System that updates resource pickup presentation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XResourcePickupPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePickupPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new UpdateResourcePickupJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ResourcePickupPresentationTag))]
        private partial struct UpdateResourcePickupJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref MaterialPropertyOverride materialProps,
                in SpawnResource spawnResource,
                in ResourceTypeColor resourceColor,
                in PresentationLOD lod)
            {
                // Resource pickups are always full detail when visible
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Calculate pulsing effect
                float time = materialProps.PulsePhase + DeltaTime;
                float pulse = 0.8f + 0.2f * math.sin(time * 5f);

                materialProps.BaseColor = resourceColor.Value * pulse;
                materialProps.EmissiveColor = resourceColor.Value * 0.3f;
                materialProps.Alpha = 1f;
                materialProps.PulsePhase = time;
            }
        }
    }
}


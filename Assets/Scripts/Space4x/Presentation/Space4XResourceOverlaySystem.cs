using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates resource overlay data for asteroids.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XAsteroidPresentationSystem))]
    public partial struct Space4XResourceOverlaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AsteroidPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Presentation can be driven by wall-clock simulation time
            double currentTime = SystemAPI.Time.ElapsedTime;
            double recentMiningWindowSeconds = 1.0; // Consider "recent" if mined within last 1 second

            new UpdateResourceOverlayJob
            {
                CurrentTime = currentTime,
                RecentMiningWindowSeconds = recentMiningWindowSeconds
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AsteroidPresentationTag))]
        private partial struct UpdateResourceOverlayJob : IJobEntity
        {
            public double CurrentTime;
            public double RecentMiningWindowSeconds;

            public void Execute(
                ref ResourceOverlayData overlayData,
                ref MaterialPropertyOverride materialProps,
                in Asteroid asteroid,
                in PresentationLOD lod)
            {
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Calculate richness level
                float maxResources = asteroid.MaxResourceAmount;
                float currentResources = asteroid.ResourceAmount;
                overlayData.RichnessLevel = maxResources > 0f ? currentResources / maxResources : 0f;

                // Check if recently mined (would need ResourceSourceState.LastHarvestTick)
                // For now, use depletion ratio as proxy
                overlayData.RecentlyMined = overlayData.RichnessLevel < 0.95f && overlayData.RichnessLevel > 0.05f;

                // Update material properties for overlay visualization
                // Rich asteroids get bright halo, depleted get dim
                float4 baseColor = materialProps.BaseColor;
                float haloIntensity = overlayData.RichnessLevel;

                // Add emissive for rich asteroids
                if (overlayData.RichnessLevel > 0.5f)
                {
                    materialProps.EmissiveColor = baseColor * haloIntensity * 0.3f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }

                // Pulse effect for recently mined asteroids
                if (overlayData.RecentlyMined)
                {
                    float pulse = 0.7f + 0.3f * math.sin((float)CurrentTime * 6f); // Pulse at ~1 Hz
                    materialProps.EmissiveColor *= pulse;
                }
            }
        }
    }
}


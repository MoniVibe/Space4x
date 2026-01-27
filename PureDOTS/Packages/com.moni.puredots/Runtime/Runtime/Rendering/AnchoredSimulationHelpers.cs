using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Rendering
{
    /// <summary>
    /// Helper utilities for anchored character simulation rate management.
    /// Use these in AI/behavior systems to determine if an anchored character
    /// should update this tick based on distance and configuration.
    /// </summary>
    [BurstCompile]
    public static class AnchoredSimulationHelpers
    {
        /// <summary>
        /// Determines if an anchored character should update this tick.
        /// </summary>
        /// <param name="config">The entity's simulation configuration.</param>
        /// <param name="distanceFromCamera">Distance from the active camera.</param>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <returns>True if the entity should update this tick.</returns>
        [BurstCompile]
        public static bool ShouldUpdateThisTick(
            in AnchoredSimConfig config,
            float distanceFromCamera,
            uint currentTick)
        {
            // Always update if configured for full simulation
            if (config.AlwaysFullSimulation)
            {
                return true;
            }

            // Always update if within reduced-rate distance threshold
            if (distanceFromCamera < config.DistanceForReduced)
            {
                return true;
            }

            // At distance, use tick rate divisor (1=every tick, 2=every 2nd, 4=every 4th)
            int divisorInt = math.max((int)config.TickRateDivisor, 1); // explicit ints avoid overload ambiguity
            byte divisor = (byte)divisorInt;
            return (currentTick % divisor) == 0;
        }

        /// <summary>
        /// Gets the effective tick rate for an anchored character.
        /// </summary>
        /// <param name="config">The entity's simulation configuration.</param>
        /// <param name="distanceFromCamera">Distance from the active camera.</param>
        /// <returns>Effective updates per base tick (1.0 = full rate, 0.5 = half, 0.25 = quarter).</returns>
        [BurstCompile]
        public static float GetEffectiveTickRate(
            in AnchoredSimConfig config,
            float distanceFromCamera)
        {
            if (config.AlwaysFullSimulation)
            {
                return 1.0f;
            }

            if (distanceFromCamera < config.DistanceForReduced)
            {
                return 1.0f;
            }

            int divisorInt = math.max((int)config.TickRateDivisor, 1);
            byte divisor = (byte)divisorInt;
            return 1.0f / divisor;
        }

        /// <summary>
        /// Determines if an anchored character should render at a given distance.
        /// </summary>
        /// <param name="config">The entity's render configuration.</param>
        /// <param name="distanceFromCamera">Distance from the active camera.</param>
        /// <returns>True if the entity should render (anchored characters almost always render).</returns>
        [BurstCompile]
        public static bool ShouldRender(
            in AnchoredRenderConfig config,
            float distanceFromCamera)
        {
            // MaxRenderDistance of 0 means infinite (always render)
            if (config.MaxRenderDistance <= 0f)
            {
                return true;
            }

            return distanceFromCamera <= config.MaxRenderDistance;
        }

        /// <summary>
        /// Gets the minimum LOD level for an anchored character.
        /// </summary>
        /// <param name="config">The entity's render configuration.</param>
        /// <param name="defaultLOD">The LOD that would normally be used at this distance.</param>
        /// <returns>The LOD to use (clamped to minimum configured level).</returns>
        [BurstCompile]
        public static byte GetClampedLOD(
            in AnchoredRenderConfig config,
            byte defaultLOD)
        {
            // Anchored characters never go below their minimum LOD
            // Lower number = higher quality (0 = full, 1 = medium, 2 = low)
            int clamped = math.max((int)config.MinLODLevel, (int)defaultLOD);
            return (byte)clamped;
        }

        /// <summary>
        /// Checks if shadows should be rendered for an anchored character at distance.
        /// </summary>
        /// <param name="config">The entity's render configuration.</param>
        /// <param name="distanceFromCamera">Distance from the active camera.</param>
        /// <param name="normalShadowDistance">Normal shadow culling distance.</param>
        /// <returns>True if shadows should render.</returns>
        [BurstCompile]
        public static bool ShouldRenderShadows(
            in AnchoredRenderConfig config,
            float distanceFromCamera,
            float normalShadowDistance)
        {
            // If configured to always cast shadows, ignore distance
            if (config.AlwaysCastShadows)
            {
                return true;
            }

            // Otherwise use normal shadow distance culling
            return distanceFromCamera <= normalShadowDistance;
        }

        /// <summary>
        /// Checks if VFX should be rendered for an anchored character at distance.
        /// </summary>
        /// <param name="config">The entity's render configuration.</param>
        /// <param name="distanceFromCamera">Distance from the active camera.</param>
        /// <param name="normalVFXDistance">Normal VFX culling distance.</param>
        /// <returns>True if VFX should render.</returns>
        [BurstCompile]
        public static bool ShouldRenderVFX(
            in AnchoredRenderConfig config,
            float distanceFromCamera,
            float normalVFXDistance)
        {
            // If configured to always render VFX, ignore distance
            if (config.AlwaysRenderVFX)
            {
                return true;
            }

            // Otherwise use normal VFX distance culling
            return distanceFromCamera <= normalVFXDistance;
        }
    }
}


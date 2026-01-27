using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Rendering
{
    /// <summary>
    /// Provides distance and importance scores for rendering LOD decisions.
    /// Games consume this to decide rendering quality/visibility.
    /// Updated by game-side camera systems (non-deterministic, frame-time).
    /// </summary>
    public struct RenderLODData : IComponentData
    {
        /// <summary>
        /// Distance to camera (updated by game-side camera system).
        /// </summary>
        public float CameraDistance;

        /// <summary>
        /// Importance score (0-1, higher = more important).
        /// Can be based on entity type, player focus, or gameplay relevance.
        /// </summary>
        public float ImportanceScore;

        /// <summary>
        /// LOD level recommendation (0=full detail, 1=reduced, 2=impostor, 3=hidden).
        /// </summary>
        public byte RecommendedLOD;

        /// <summary>
        /// Last update tick (for stale data detection).
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Tag component indicating entity can be culled at distance thresholds.
    /// </summary>
    public struct RenderCullable : IComponentData
    {
        /// <summary>
        /// Distance at which to cull (0 = never cull).
        /// </summary>
        public float CullDistance;

        /// <summary>
        /// Priority for culling (0-255, higher = cull later when reducing render count).
        /// </summary>
        public byte Priority;
    }

    /// <summary>
    /// Provides stable sampling index for render density control.
    /// Allows games to render only 1 in N entities consistently.
    /// </summary>
    public struct RenderSampleIndex : IComponentData
    {
        /// <summary>
        /// Stable hash-based index (0 to SampleModulus-1).
        /// Assigned at entity creation based on entity ID hash.
        /// </summary>
        public ushort SampleIndex;

        /// <summary>
        /// Modulus for sampling (e.g., 10 = render 1 in 10).
        /// </summary>
        public ushort SampleModulus;

        /// <summary>
        /// Whether this entity should be rendered at current density.
        /// Computed: (SampleIndex % CurrentDensity) == 0
        /// </summary>
        public byte ShouldRender;
    }

    /// <summary>
    /// Global render density settings singleton.
    /// Controls how many entities are rendered based on scale.
    /// </summary>
    public struct RenderDensitySettings : IComponentData
    {
        /// <summary>
        /// Current render density (1 = all, 2 = half, 10 = 1 in 10, etc.).
        /// </summary>
        public ushort CurrentDensity;

        /// <summary>
        /// Maximum entities to render per frame (0 = unlimited).
        /// </summary>
        public int MaxRenderCount;

        /// <summary>
        /// Last update tick.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// LOD level thresholds configuration.
    /// </summary>
    public struct LODThresholds : IComponentData
    {
        /// <summary>
        /// Distance threshold for LOD 1 (reduced detail).
        /// </summary>
        public float LOD1Distance;

        /// <summary>
        /// Distance threshold for LOD 2 (impostor).
        /// </summary>
        public float LOD2Distance;

        /// <summary>
        /// Distance threshold for LOD 3 (hidden/culled).
        /// </summary>
        public float LOD3Distance;

        /// <summary>
        /// Hysteresis to prevent LOD flickering.
        /// </summary>
        public float Hysteresis;
    }

    /// <summary>
    /// Helper methods for render LOD calculations.
    /// </summary>
    public static class RenderLODHelpers
    {
        /// <summary>
        /// Calculates recommended LOD level based on distance and thresholds.
        /// </summary>
        public static byte CalculateLOD(float distance, in LODThresholds thresholds)
        {
            if (distance < thresholds.LOD1Distance)
                return 0; // Full detail
            if (distance < thresholds.LOD2Distance)
                return 1; // Reduced detail
            if (distance < thresholds.LOD3Distance)
                return 2; // Impostor
            return 3; // Hidden
        }

        /// <summary>
        /// Calculates stable sample index from entity index.
        /// Uses a simple hash to distribute entities evenly.
        /// </summary>
        public static ushort CalculateSampleIndex(int entityIndex, ushort modulus)
        {
            if (modulus == 0) return 0;
            
            // Simple hash to distribute evenly
            uint hash = (uint)entityIndex;
            hash = hash * 2654435761u; // Knuth's multiplicative hash
            return (ushort)(hash % modulus);
        }

        /// <summary>
        /// Determines if entity should render at current density.
        /// </summary>
        public static bool ShouldRenderAtDensity(ushort sampleIndex, ushort currentDensity)
        {
            if (currentDensity <= 1) return true;
            return (sampleIndex % currentDensity) == 0;
        }
    }
}


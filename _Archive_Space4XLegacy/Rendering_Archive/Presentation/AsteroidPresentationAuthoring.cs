using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Authoring component for asteroid presentation.
    /// Adds presentation-layer components to asteroid entities for rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class AsteroidPresentationAuthoring : MonoBehaviour
    {
        [Header("Resource Type")]
        [Tooltip("Resource type determines the asteroid color")]
        public ResourceType ResourceTypeValue = ResourceType.Minerals;

        [Header("Visual Settings")]
        [Tooltip("Optional mesh override (uses default if null)")]
        public Mesh AsteroidMesh;

        [Tooltip("Optional material override (uses default if null)")]
        public Material AsteroidMaterial;

        [Header("Initial State")]
        [Tooltip("Initial visual state")]
        public AsteroidVisualStateType InitialState = AsteroidVisualStateType.Full;

        [Tooltip("Initial depletion ratio (0 = full, 1 = empty)")]
        [Range(0f, 1f)]
        public float InitialDepletionRatio = 0f;
    }

    /// <summary>
    /// Baker for AsteroidPresentationAuthoring.
    /// </summary>
    public class AsteroidPresentationBaker : Baker<AsteroidPresentationAuthoring>
    {
        public override void Bake(AsteroidPresentationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add presentation tag
            AddComponent(entity, new AsteroidPresentationTag());

            // Add resource type color based on resource type
            var resourceColor = GetResourceColor(authoring.ResourceTypeValue);
            AddComponent(entity, new ResourceTypeColor { Value = resourceColor });

            // Add visual state
            AddComponent(entity, new AsteroidVisualState
            {
                State = authoring.InitialState,
                DepletionRatio = authoring.InitialDepletionRatio,
                StateTimer = 0f
            });

            // Add PureDOTS-compatible LOD components
            AddComponent(entity, new RenderLODData
            {
                RecommendedLOD = 0, // Full detail
                DistanceToCamera = 0f,
                Importance = 0.7f
            });
            AddComponent(entity, new RenderCullable
            {
                CullDistance = 1800f,
                Priority = 90
            });

            // Add material property override
            AddComponent(entity, new MaterialPropertyOverride
            {
                BaseColor = resourceColor,
                EmissiveColor = float4.zero,
                Alpha = 1f - (authoring.InitialDepletionRatio * 0.5f), // Depleted asteroids are more transparent
                PulsePhase = 0f
            });

            // Add render sample index for density sampling (PureDOTS-compatible)
            AddComponent(entity, new RenderSampleIndex
            {
                Index = (uint)entity.Index,
                ShouldRender = 1 // Render by default
            });

            // Add should render tag (all entities render by default)
            AddComponent(entity, new ShouldRenderTag());
        }

        private static float4 GetResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Minerals => new float4(0.6f, 0.6f, 0.6f, 1f),      // Gray
                ResourceType.RareMetals => new float4(0.8f, 0.7f, 0.2f, 1f),    // Gold
                ResourceType.EnergyCrystals => new float4(0.2f, 0.8f, 1f, 1f),  // Cyan
                ResourceType.OrganicMatter => new float4(0.2f, 0.8f, 0.3f, 1f), // Green
                ResourceType.Ore => new float4(0.5f, 0.3f, 0.2f, 1f),           // Brown
                _ => new float4(1f, 1f, 1f, 1f)                                  // White
            };
        }
    }
}


using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Authoring component for craft/mining vessel presentation.
    /// Adds presentation-layer components to craft entities for rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftPresentationAuthoring : MonoBehaviour
    {
        [Header("Identification")]
        [Tooltip("Craft type ID for visual variant selection")]
        public string CraftTypeId = "mining_craft_basic";

        [Header("Parent Reference")]
        [Tooltip("Reference to parent carrier (optional - can be set at runtime)")]
        public GameObject ParentCarrierObject;

        [Header("Visual Settings")]
        [Tooltip("Faction color for this craft (usually inherited from carrier)")]
        public Color FactionColorValue = Color.blue;

        [Tooltip("Optional mesh override (uses default if null)")]
        public Mesh CraftMesh;

        [Tooltip("Optional material override (uses default if null)")]
        public Material CraftMaterial;

        [Header("Initial State")]
        [Tooltip("Initial visual state")]
        public CraftVisualStateType InitialState = CraftVisualStateType.Idle;
    }

    /// <summary>
    /// Baker for CraftPresentationAuthoring.
    /// </summary>
    public class CraftPresentationBaker : Baker<CraftPresentationAuthoring>
    {
        public override void Bake(CraftPresentationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add presentation tag
            AddComponent(entity, new CraftPresentationTag());

            // Add faction color
            AddComponent(entity, new FactionColor
            {
                Value = new float4(
                    authoring.FactionColorValue.r,
                    authoring.FactionColorValue.g,
                    authoring.FactionColorValue.b,
                    authoring.FactionColorValue.a)
            });

            // Add parent carrier reference if specified
            if (authoring.ParentCarrierObject != null)
            {
                var parentEntity = GetEntity(authoring.ParentCarrierObject, TransformUsageFlags.Dynamic);
                AddComponent(entity, new ParentCarrier { Value = parentEntity });
            }

            // Add visual state
            AddComponent(entity, new CraftVisualState
            {
                State = authoring.InitialState,
                StateTimer = 0f
            });

            // Add PureDOTS-compatible LOD components
            AddComponent(entity, new RenderLODData
            {
                RecommendedLOD = 0, // Full detail
                DistanceToCamera = 0f,
                Importance = 0.6f
            });
            AddComponent(entity, new RenderCullable
            {
                CullDistance = 1500f,
                Priority = 80
            });

            // Add material property override
            AddComponent(entity, new MaterialPropertyOverride
            {
                BaseColor = new float4(
                    authoring.FactionColorValue.r,
                    authoring.FactionColorValue.g,
                    authoring.FactionColorValue.b,
                    authoring.FactionColorValue.a),
                EmissiveColor = float4.zero,
                Alpha = 1f,
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
    }
}


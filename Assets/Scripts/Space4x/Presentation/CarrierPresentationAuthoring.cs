using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Shared.Demo;

namespace Space4X.Presentation
{
    /// <summary>
    /// Authoring component for carrier presentation.
    /// Adds presentation-layer components to carrier entities for rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarrierPresentationAuthoring : MonoBehaviour
    {
        [Header("Identification")]
        [Tooltip("Hull ID for visual variant selection")]
        public string HullId = "carrier_basic";

        [Header("Visual Settings")]
        [Tooltip("Faction color for this carrier")]
        public Color FactionColorValue = Color.blue;

        [Tooltip("Optional mesh override (uses default if null)")]
        public Mesh CarrierMesh;

        [Tooltip("Optional material override (uses default if null)")]
        public Material CarrierMaterial;

        [Header("Initial State")]
        [Tooltip("Initial visual state")]
        public CarrierVisualStateType InitialState = CarrierVisualStateType.Idle;
    }

    /// <summary>
    /// Baker for CarrierPresentationAuthoring.
    /// </summary>
    public class CarrierPresentationBaker : Baker<CarrierPresentationAuthoring>
    {
        public override void Bake(CarrierPresentationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add presentation tag
            AddComponent(entity, new CarrierPresentationTag());

            // Add faction color
            AddComponent(entity, new FactionColor
            {
                Value = new float4(
                    authoring.FactionColorValue.r,
                    authoring.FactionColorValue.g,
                    authoring.FactionColorValue.b,
                    authoring.FactionColorValue.a)
            });

            // Add visual state
            AddComponent(entity, new CarrierVisualState
            {
                State = authoring.InitialState,
                StateTimer = 0f
            });

            // Add PureDOTS-compatible LOD components
            AddComponent(entity, new RenderLODData
            {
                RecommendedLOD = 0, // Full detail
                DistanceToCamera = 0f,
                Importance = 0.8f
            });
            AddComponent(entity, new RenderCullable
            {
                CullDistance = 2000f,
                Priority = 100
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

            // Note: Render components (MaterialMeshInfo and RenderMeshArray) are added at runtime
            // by Space4XPresentationLifecycleSystem using the shared RenderMeshArray from DemoRenderReady.
            // This ensures all entities use the same render mesh array for efficient batching.
        }
    }
}


using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Render system for carriers following PureDOTS canonical recipe.
    /// Reads LocalTransform, Carrier, RenderLODData, RenderCullable, RenderSampleIndex, FleetMemberRef.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XCarrierRenderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            // Query matches PureDOTS canonical recipe: LocalTransform, Carrier, RenderLODData, RenderCullable, RenderSampleIndex
            // FleetMemberRef is optional (only present if carrier is in a fleet)
            foreach (var (transform, carrier, lodData, cullable, sampleIndex, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Carrier>, RefRO<RenderLODData>, 
                                    RefRO<RenderCullable>, RefRO<RenderSampleIndex>>()
                     .WithAll<CarrierPresentationTag>()
                     .WithEntityAccess())
            {
                // Skip if density-culled (per PureDOTS guide)
                if (sampleIndex.ValueRO.ShouldRender == 0) continue;
                
                // Skip if LOD-culled (per PureDOTS guide: RecommendedLOD >= 4 means cull)
                if (lodData.ValueRO.RecommendedLOD >= 4) continue;

                // Read position/rotation from LocalTransform (sim-driven, don't modify)
                float3 position = transform.ValueRO.Position;
                quaternion rotation = transform.ValueRO.Rotation; // Heading/orientation

                // Get or create MaterialPropertyOverride for color coding
                if (!SystemAPI.HasComponent<MaterialPropertyOverride>(entity))
                {
                    state.EntityManager.AddComponent<MaterialPropertyOverride>(entity);
                }
                var materialOverride = SystemAPI.GetComponentRW<MaterialPropertyOverride>(entity);

                // Get base faction color (default to blue if not present)
                float4 baseColor = new float4(0.2f, 0.4f, 1f, 1f); // Default blue
                if (SystemAPI.HasComponent<FactionColor>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<FactionColor>(entity).ValueRO.Value;
                }

                // Get visual state and apply state-based tint
                CarrierVisualStateType visualStateType = CarrierVisualStateType.Idle;
                float stateTimer = 0f;
                if (SystemAPI.HasComponent<CarrierVisualState>(entity))
                {
                    var visualState = SystemAPI.GetComponentRW<CarrierVisualState>(entity);
                    visualState.ValueRW.StateTimer += dt;
                    visualStateType = visualState.ValueRO.State;
                    stateTimer = visualState.ValueRO.StateTimer;
                }

                // Apply state-based color tint
                float4 stateTint = new float4(1f, 1f, 1f, 1f); // Default: no tint
                switch (visualStateType)
                {
                    case CarrierVisualStateType.Idle:
                        stateTint = new float4(1f, 1f, 1f, 1f); // White (normal)
                        break;
                    case CarrierVisualStateType.Patrolling:
                        stateTint = new float4(0.7f, 1f, 1f, 1f); // Cyan tint
                        break;
                    case CarrierVisualStateType.Mining:
                        stateTint = new float4(1f, 0.8f, 0.6f, 1f); // Orange tint
                        break;
                    case CarrierVisualStateType.Combat:
                        stateTint = new float4(1f, 0.6f, 0.6f, 1f); // Red tint
                        break;
                    case CarrierVisualStateType.Retreating:
                        stateTint = new float4(1f, 0.7f, 1f, 1f); // Purple tint
                        break;
                }

                // Combine base color with state tint
                float4 finalColor = baseColor * stateTint;
                materialOverride.ValueRW.BaseColor = finalColor;

                // Visual feedback for selected entities (enhanced with pulsing)
                bool isSelected = SystemAPI.HasComponent<SelectedTag>(entity);
                if (isSelected)
                {
                    // Pulsing emissive glow (intensity varies with sin wave)
                    float pulseIntensity = 0.5f + 0.5f * math.sin(stateTimer * 3f); // Pulse at 3 Hz
                    materialOverride.ValueRW.EmissiveColor = new float4(0.2f, 0.4f, 1f, 1f) * pulseIntensity;
                    materialOverride.ValueRW.PulsePhase = stateTimer;
                }
                else
                {
                    // Clear emissive if not selected
                    materialOverride.ValueRW.EmissiveColor = float4.zero;
                    materialOverride.ValueRW.PulsePhase = 0f;
                }

                // Render carrier at appropriate LOD
                // Note: Actual rendering is handled by Entities Graphics based on LocalTransform
                // This system mainly handles LOD/density filtering and visual state updates
            }
        }
    }
}

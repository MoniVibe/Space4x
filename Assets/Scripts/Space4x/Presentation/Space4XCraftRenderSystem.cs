using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Render system for crafts following PureDOTS canonical recipe.
    /// Reads LocalTransform, RenderLODData, RenderCullable, RenderSampleIndex, FleetMemberRef.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XCraftRenderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CraftPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            // Query matches PureDOTS canonical recipe pattern: LocalTransform, RenderLODData, RenderCullable, RenderSampleIndex, FleetMemberRef
            foreach (var (transform, lodData, cullable, sampleIndex, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<RenderLODData>, 
                                    RefRO<RenderCullable>, RefRO<RenderSampleIndex>>()
                     .WithAll<CraftPresentationTag>()
                     .WithEntityAccess())
            {
                // Skip if density-culled (per PureDOTS guide)
                if (sampleIndex.ValueRO.ShouldRender == 0) continue;
                
                // Fleet LOD strategy: Hide individual crafts at LOD 2+ (show fleet impostor instead)
                // LOD 0/1: Show individual crafts + fleet marker
                // LOD 2+: Hide individual crafts, show only fleet marker
                if (lodData.ValueRO.RecommendedLOD >= 2) continue;

                // Read position/rotation from LocalTransform (sim-driven, don't modify)
                float3 position = transform.ValueRO.Position;
                quaternion rotation = transform.ValueRO.Rotation; // Heading/orientation

                // Get or create MaterialPropertyOverride for color coding
                if (!SystemAPI.HasComponent<MaterialPropertyOverride>(entity))
                {
                    state.EntityManager.AddComponent<MaterialPropertyOverride>(entity);
                }
                var materialOverride = SystemAPI.GetComponentRW<MaterialPropertyOverride>(entity);

                // Get faction color from parent carrier (inherit) or default
                float4 baseColor = new float4(0.5f, 0.5f, 0.5f, 1f); // Default gray
                if (SystemAPI.HasComponent<FactionColor>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<FactionColor>(entity).ValueRO.Value;
                }
                else if (SystemAPI.HasComponent<ParentCarrier>(entity))
                {
                    var parentCarrierEntity = SystemAPI.GetComponentRO<ParentCarrier>(entity).ValueRO.Value;
                    if (parentCarrierEntity != Entity.Null && SystemAPI.HasComponent<FactionColor>(parentCarrierEntity))
                    {
                        baseColor = SystemAPI.GetComponentRO<FactionColor>(parentCarrierEntity).ValueRO.Value;
                    }
                }

                // Get visual state and apply state-based tint
                CraftVisualStateType visualStateType = CraftVisualStateType.Idle;
                float stateTimer = 0f;
                if (SystemAPI.HasComponent<CraftVisualState>(entity))
                {
                    var visualState = SystemAPI.GetComponentRW<CraftVisualState>(entity);
                    visualState.ValueRW.StateTimer += dt;
                    visualStateType = visualState.ValueRO.State;
                    stateTimer = visualState.ValueRO.StateTimer;
                }

                // Apply state-based color tint (crafts are dimmer than carriers)
                float4 stateTint = new float4(0.8f, 0.8f, 0.8f, 1f); // Default: dimmed
                switch (visualStateType)
                {
                    case CraftVisualStateType.Idle:
                        stateTint = new float4(0.7f, 0.7f, 0.7f, 1f); // Dimmed gray
                        break;
                    case CraftVisualStateType.Mining:
                        stateTint = new float4(1f, 0.9f, 0.7f, 1f); // Orange glow
                        break;
                    case CraftVisualStateType.Returning:
                        stateTint = new float4(0.7f, 1f, 0.7f, 1f); // Green tint
                        break;
                    case CraftVisualStateType.Moving:
                        stateTint = new float4(0.7f, 1f, 1f, 1f); // Cyan tint
                        break;
                }

                // Combine base color with state tint (crafts are smaller, so slightly dimmer)
                float4 finalColor = baseColor * stateTint * 0.9f; // 0.9f makes crafts visually distinct from carriers
                materialOverride.ValueRW.BaseColor = finalColor;

                // Visual feedback for selected entities (enhanced with pulsing)
                bool isSelected = SystemAPI.HasComponent<SelectedTag>(entity);
                if (isSelected)
                {
                    // Pulsing emissive glow
                    float pulseIntensity = 0.5f + 0.5f * math.sin(stateTimer * 3f);
                    materialOverride.ValueRW.EmissiveColor = new float4(0.2f, 0.8f, 1f, 1f) * pulseIntensity;
                    materialOverride.ValueRW.PulsePhase = stateTimer;
                }
                else
                {
                    // Clear emissive if not selected
                    materialOverride.ValueRW.EmissiveColor = float4.zero;
                    materialOverride.ValueRW.PulsePhase = 0f;
                }

                // Render craft at appropriate LOD
                // Note: Actual rendering is handled by Entities Graphics based on LocalTransform
                // This system mainly handles LOD/density filtering and visual state updates
            }
        }
    }
}

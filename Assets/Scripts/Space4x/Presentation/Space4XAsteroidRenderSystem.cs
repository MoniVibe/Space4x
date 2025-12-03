using Space4X.Registry;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Render system for asteroids following PureDOTS canonical recipe.
    /// Reads LocalTransform, ResourceSourceState, ResourceTypeId, RenderLODData, RenderCullable, RenderSampleIndex.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XAsteroidRenderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AsteroidPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            // Query matches PureDOTS canonical recipe: LocalTransform, ResourceSourceState, ResourceTypeId, RenderLODData, RenderCullable, RenderSampleIndex
            // Using fully qualified PureDOTS types to avoid collision with Space4X.Registry types
            foreach (var (transform, sourceState, resourceType, lodData, cullable, sampleIndex, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, 
                                    RefRO<PureDOTS.Runtime.Components.ResourceSourceState>, 
                                    RefRO<PureDOTS.Runtime.Components.ResourceTypeId>,
                                    RefRO<RenderLODData>, RefRO<RenderCullable>, RefRO<RenderSampleIndex>>()
                     .WithAll<AsteroidPresentationTag>()
                     .WithEntityAccess())
            {
                // Skip if density-culled (per PureDOTS guide)
                if (sampleIndex.ValueRO.ShouldRender == 0) continue;
                
                // Skip if LOD-culled (per PureDOTS guide: RecommendedLOD >= 4 means cull)
                if (lodData.ValueRO.RecommendedLOD >= 4) continue;

                // Read position/rotation from LocalTransform (sim-driven, don't modify)
                float3 position = transform.ValueRO.Position;
                quaternion rotation = transform.ValueRO.Rotation; // Heading/orientation
                float unitsRemaining = sourceState.ValueRO.UnitsRemaining;

                // Get or create MaterialPropertyOverride for color coding
                if (!SystemAPI.HasComponent<MaterialPropertyOverride>(entity))
                {
                    state.EntityManager.AddComponent<MaterialPropertyOverride>(entity);
                }
                var materialOverride = SystemAPI.GetComponentRW<MaterialPropertyOverride>(entity);

                // Get resource type color (default to gray if not present)
                float4 resourceColor = new float4(0.6f, 0.6f, 0.6f, 1f); // Default gray
                if (SystemAPI.HasComponent<ResourceTypeColor>(entity))
                {
                    resourceColor = SystemAPI.GetComponentRO<ResourceTypeColor>(entity).ValueRO.Value;
                }

                // Update visual state and get depletion ratio
                float depletionRatio = 0f;
                AsteroidVisualStateType visualStateType = AsteroidVisualStateType.Full;
                float stateTimer = 0f;
                if (SystemAPI.HasComponent<AsteroidVisualState>(entity))
                {
                    var visualState = SystemAPI.GetComponentRW<AsteroidVisualState>(entity);
                    visualState.ValueRW.StateTimer += dt;
                    stateTimer = visualState.ValueRO.StateTimer;
                    
                    // Update depletion ratio based on resource state
                    if (SystemAPI.HasComponent<Asteroid>(entity))
                    {
                        var asteroid = SystemAPI.GetComponentRO<Asteroid>(entity);
                        depletionRatio = 1f - (asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount));
                        visualState.ValueRW.DepletionRatio = depletionRatio;
                        
                        // Update visual state type based on depletion
                        if (depletionRatio >= 1f)
                        {
                            visualState.ValueRW.State = AsteroidVisualStateType.Depleted;
                        }
                        else if (depletionRatio > 0f)
                        {
                            visualState.ValueRW.State = AsteroidVisualStateType.MiningActive;
                        }
                        else
                        {
                            visualState.ValueRW.State = AsteroidVisualStateType.Full;
                        }
                        visualStateType = visualState.ValueRO.State;
                    }
                }

                // Apply depletion-based color and scale
                // Rich asteroids: bright and saturated, Depleted: dim and gray
                float4 finalColor = resourceColor;
                float alpha = 1f;
                float scale = 1f;
                
                if (depletionRatio >= 1f)
                {
                    // Depleted: gray and small
                    finalColor = new float4(0.3f, 0.3f, 0.3f, 1f);
                    alpha = 0.5f;
                    scale = 0.3f;
                }
                else if (depletionRatio > 0f)
                {
                    // Partially depleted: desaturate and reduce brightness
                    float saturation = 1f - depletionRatio * 0.7f; // Reduce saturation as depleted
                    float brightness = 1f - depletionRatio * 0.5f; // Reduce brightness
                    finalColor = resourceColor * brightness;
                    finalColor = math.lerp(new float4(0.5f, 0.5f, 0.5f, 1f), finalColor, saturation);
                    scale = 0.5f + (1f - depletionRatio) * 0.5f; // Scale from 0.5 to 1.0
                }
                else
                {
                    // Full: bright and saturated
                    finalColor = resourceColor;
                    scale = 1f;
                }

                materialOverride.ValueRW.BaseColor = finalColor;
                materialOverride.ValueRW.Alpha = alpha;

                // Update scale for depleted asteroids (via LocalTransform if we can modify it)
                // Note: In ECS, we typically don't modify LocalTransform from presentation systems
                // This would be handled by a separate system or via a Scale component if available

                // Visual feedback for selected entities (enhanced with pulsing)
                bool isSelected = SystemAPI.HasComponent<SelectedTag>(entity);
                if (isSelected)
                {
                    // Pulsing emissive glow
                    float pulseIntensity = 0.5f + 0.5f * math.sin(stateTimer * 3f);
                    materialOverride.ValueRW.EmissiveColor = new float4(1f, 1f, 0.2f, 1f) * pulseIntensity;
                    materialOverride.ValueRW.PulsePhase = stateTimer;
                }
                else
                {
                    // Clear emissive if not selected
                    materialOverride.ValueRW.EmissiveColor = float4.zero;
                    materialOverride.ValueRW.PulsePhase = 0f;
                }

                // Render asteroid at appropriate LOD
                // Note: Actual rendering is handled by Entities Graphics based on LocalTransform
                // This system mainly handles LOD/density filtering and visual state updates
            }
        }
    }
}

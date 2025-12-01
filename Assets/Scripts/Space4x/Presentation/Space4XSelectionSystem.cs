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
    /// System that handles entity selection based on input.
    /// Adds/removes SelectedTag component on entities.
    /// Uses proper camera raycast for accurate selection.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XSelectionSystem : ISystem
    {
        private EntityQuery _selectableQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SelectionInput>();
            state.RequireForUpdate<Space4XCameraState>();

            // Create query for all selectable entities
            _selectableQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PresentationLOD>()
            );
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SelectionInput>(out var selectionInput))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Handle deselect
            if (selectionInput.DeselectRequested)
            {
                foreach (var (_, entity) in SystemAPI.Query<RefRO<SelectedTag>>().WithEntityAccess())
                {
                    ecb.RemoveComponent<SelectedTag>(entity);
                }

                // Update selection state
                UpdateSelectionState(ref state, ecb, Entity.Null, 0, SelectionType.None);
            }
            // Handle click selection
            else if (selectionInput.ClickPressed)
            {
                // Clear previous selection if shift not held
                if (!selectionInput.ShiftHeld)
                {
                    foreach (var (_, entity) in SystemAPI.Query<RefRO<SelectedTag>>().WithEntityAccess())
                    {
                        ecb.RemoveComponent<SelectedTag>(entity);
                    }
                }

                // Find entity at click position using simple distance check
                // In a real implementation, this would use proper raycasting
                var clickedEntity = FindEntityAtPosition(ref state, selectionInput.ClickPosition, cameraState);

                if (clickedEntity != Entity.Null)
                {
                    if (!state.EntityManager.HasComponent<SelectedTag>(clickedEntity))
                    {
                        ecb.AddComponent<SelectedTag>(clickedEntity);
                    }

                    int selectedCount = selectionInput.ShiftHeld ? CountSelectedEntities(ref state) + 1 : 1;
                    var selectionType = selectedCount > 1 ? SelectionType.Multi : SelectionType.Single;
                    UpdateSelectionState(ref state, ecb, clickedEntity, selectedCount, selectionType);
                }
            }
            // Handle box selection
            else if (selectionInput.BoxActive && !selectionInput.ClickHeld)
            {
                // Box selection completed
                if (!selectionInput.ShiftHeld)
                {
                    foreach (var (_, entity) in SystemAPI.Query<RefRO<SelectedTag>>().WithEntityAccess())
                    {
                        ecb.RemoveComponent<SelectedTag>(entity);
                    }
                }

                int selectedCount = SelectEntitiesInBox(ref state, ecb, selectionInput.BoxStart, selectionInput.BoxEnd, cameraState);

                if (selectedCount > 0)
                {
                    UpdateSelectionState(ref state, ecb, Entity.Null, selectedCount, SelectionType.Box);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private Entity FindEntityAtPosition(ref SystemState state, float2 screenPosition, Space4XCameraState cameraState)
        {
            // Get Unity Camera for proper raycast
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                // Fallback to simple distance-based selection
                return FindEntityAtPositionSimple(ref state, screenPosition, cameraState);
            }

            // Convert screen position to world ray
            Ray ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            float3 rayOrigin = ray.origin;
            float3 rayDirection = ray.direction;

            Entity closestEntity = Entity.Null;
            float closestDistance = float.MaxValue;
            float maxSelectionDistance = 10000f; // Max selection range

            // Selection priority: Fleet impostors > Carriers > Crafts > Asteroids
            // Check fleet impostors first (when at Impostor LOD)
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<FleetImpostorTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level != PresentationLODLevel.Impostor) continue;

                float3 entityPos = transform.ValueRO.Position;
                float dist = DistanceToRay(rayOrigin, rayDirection, entityPos);
                
                // Use larger selection radius for fleet impostors (they're bigger targets)
                float selectionRadius = 20f;
                if (dist < selectionRadius)
                {
                    // Check if ray actually hits the entity (distance along ray)
                    float3 toEntity = entityPos - rayOrigin;
                    float t = math.dot(toEntity, rayDirection);
                    if (t > 0f && t < maxSelectionDistance && dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestEntity = entity;
                    }
                }
            }

            // Check carriers (if no fleet impostor selected and at FullDetail/ReducedDetail)
            if (closestEntity == Entity.Null)
            {
                foreach (var (transform, lod, entity) in SystemAPI
                             .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                             .WithAll<CarrierPresentationTag>()
                             .WithEntityAccess())
                {
                    if (lod.ValueRO.Level == PresentationLODLevel.Hidden || 
                        lod.ValueRO.Level == PresentationLODLevel.Impostor) continue;

                    float3 entityPos = transform.ValueRO.Position;
                    float dist = DistanceToRay(rayOrigin, rayDirection, entityPos);
                    
                    float selectionRadius = 10f; // Carrier selection radius
                    if (dist < selectionRadius)
                    {
                        float3 toEntity = entityPos - rayOrigin;
                        float t = math.dot(toEntity, rayDirection);
                        if (t > 0f && t < maxSelectionDistance && dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestEntity = entity;
                        }
                    }
                }
            }

            // Check crafts (if no carrier selected and at FullDetail/ReducedDetail)
            if (closestEntity == Entity.Null)
            {
                foreach (var (transform, lod, entity) in SystemAPI
                             .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                             .WithAll<CraftPresentationTag>()
                             .WithEntityAccess())
                {
                    if (lod.ValueRO.Level == PresentationLODLevel.Hidden || 
                        lod.ValueRO.Level == PresentationLODLevel.Impostor) continue;

                    float3 entityPos = transform.ValueRO.Position;
                    float dist = DistanceToRay(rayOrigin, rayDirection, entityPos);
                    
                    float selectionRadius = 5f; // Craft selection radius
                    if (dist < selectionRadius)
                    {
                        float3 toEntity = entityPos - rayOrigin;
                        float t = math.dot(toEntity, rayDirection);
                        if (t > 0f && t < maxSelectionDistance && dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestEntity = entity;
                        }
                    }
                }
            }

            // Check asteroids (always selectable)
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden) continue;

                float3 entityPos = transform.ValueRO.Position;
                float dist = DistanceToRay(rayOrigin, rayDirection, entityPos);
                
                // Asteroids can be larger, use scale-based selection radius
                float selectionRadius = math.max(5f, transform.ValueRO.Scale * 2f);
                if (dist < selectionRadius)
                {
                    float3 toEntity = entityPos - rayOrigin;
                    float t = math.dot(toEntity, rayDirection);
                    if (t > 0f && t < maxSelectionDistance && dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestEntity = entity;
                    }
                }
            }

            return closestEntity;
        }

        // Fallback simple selection when camera is not available
        private Entity FindEntityAtPositionSimple(ref SystemState state, float2 screenPosition, Space4XCameraState cameraState)
        {
            float3 cameraPos = cameraState.Position;
            float3 cameraForward = math.forward(cameraState.Rotation);

            Entity closestEntity = Entity.Null;
            float closestDistance = float.MaxValue;
            float selectionRadius = 5f;

            // Check carriers
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden) continue;

                float dist = DistanceToRay(cameraPos, cameraForward, transform.ValueRO.Position);
                if (dist < selectionRadius && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestEntity = entity;
                }
            }

            // Check crafts
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden) continue;

                float dist = DistanceToRay(cameraPos, cameraForward, transform.ValueRO.Position);
                if (dist < selectionRadius && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestEntity = entity;
                }
            }

            // Check asteroids
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden) continue;

                float dist = DistanceToRay(cameraPos, cameraForward, transform.ValueRO.Position);
                if (dist < selectionRadius && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestEntity = entity;
                }
            }

            return closestEntity;
        }

        [BurstDiscard]
        private int SelectEntitiesInBox(ref SystemState state, EntityCommandBuffer ecb, float2 boxStart, float2 boxEnd, Space4XCameraState cameraState)
        {
            int count = 0;
            float2 boxMin = math.min(boxStart, boxEnd);
            float2 boxMax = math.max(boxStart, boxEnd);

            // Get Unity Camera for proper screen-to-world conversion
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            // Select fleet impostors in box (when at Impostor LOD)
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<FleetImpostorTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level != PresentationLODLevel.Impostor) continue;

                float2 screenPos = camera != null 
                    ? WorldToScreenAccurate(transform.ValueRO.Position, camera)
                    : WorldToScreen(transform.ValueRO.Position, cameraState);
                    
                if (IsInBox(screenPos, boxMin, boxMax))
                {
                    if (!state.EntityManager.HasComponent<SelectedTag>(entity))
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                    count++;
                }
            }

            // Select carriers in box (if at FullDetail/ReducedDetail)
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden || 
                    lod.ValueRO.Level == PresentationLODLevel.Impostor) continue;

                float2 screenPos = camera != null 
                    ? WorldToScreenAccurate(transform.ValueRO.Position, camera)
                    : WorldToScreen(transform.ValueRO.Position, cameraState);
                    
                if (IsInBox(screenPos, boxMin, boxMax))
                {
                    if (!state.EntityManager.HasComponent<SelectedTag>(entity))
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                    count++;
                }
            }

            // Select crafts in box (if at FullDetail/ReducedDetail)
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden || 
                    lod.ValueRO.Level == PresentationLODLevel.Impostor) continue;

                float2 screenPos = camera != null 
                    ? WorldToScreenAccurate(transform.ValueRO.Position, camera)
                    : WorldToScreen(transform.ValueRO.Position, cameraState);
                    
                if (IsInBox(screenPos, boxMin, boxMax))
                {
                    if (!state.EntityManager.HasComponent<SelectedTag>(entity))
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                    count++;
                }
            }

            // Select asteroids in box
            foreach (var (transform, lod, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationLOD>>()
                         .WithAll<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                if (lod.ValueRO.Level == PresentationLODLevel.Hidden) continue;

                float2 screenPos = camera != null 
                    ? WorldToScreenAccurate(transform.ValueRO.Position, camera)
                    : WorldToScreen(transform.ValueRO.Position, cameraState);
                    
                if (IsInBox(screenPos, boxMin, boxMax))
                {
                    if (!state.EntityManager.HasComponent<SelectedTag>(entity))
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                    count++;
                }
            }

            return count;
        }

        [BurstDiscard]
        private static float2 WorldToScreenAccurate(float3 worldPos, Camera camera)
        {
            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
            return new float2(screenPos.x, screenPos.y);
        }

        private int CountSelectedEntities(ref SystemState state)
        {
            int count = 0;
            foreach (var _ in SystemAPI.Query<RefRO<SelectedTag>>())
            {
                count++;
            }
            return count;
        }

        private void UpdateSelectionState(ref SystemState state, EntityCommandBuffer ecb, Entity primaryEntity, int count, SelectionType type)
        {
            // Find or create selection state singleton
            if (SystemAPI.TryGetSingletonEntity<SelectionState>(out var stateEntity))
            {
                state.EntityManager.SetComponentData(stateEntity, new SelectionState
                {
                    SelectedCount = count,
                    PrimarySelected = primaryEntity,
                    Type = type
                });
            }
            else
            {
                var newEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(newEntity, new SelectionState
                {
                    SelectedCount = count,
                    PrimarySelected = primaryEntity,
                    Type = type
                });
            }
        }

        private static float DistanceToRay(float3 rayOrigin, float3 rayDirection, float3 point)
        {
            float3 v = point - rayOrigin;
            float t = math.dot(v, rayDirection);
            float3 closestPoint = rayOrigin + rayDirection * math.max(0, t);
            return math.distance(point, closestPoint);
        }

        private static float2 WorldToScreen(float3 worldPos, Space4XCameraState cameraState)
        {
            // Simple orthographic-like projection (for proper implementation, use camera matrices)
            float3 relativePos = worldPos - cameraState.Position;
            float3 right = math.mul(cameraState.Rotation, new float3(1, 0, 0));
            float3 up = math.mul(cameraState.Rotation, new float3(0, 1, 0));

            return new float2(
                math.dot(relativePos, right) * 0.01f + 0.5f,
                math.dot(relativePos, up) * 0.01f + 0.5f
            );
        }

        private static bool IsInBox(float2 point, float2 boxMin, float2 boxMax)
        {
            return point.x >= boxMin.x && point.x <= boxMax.x &&
                   point.y >= boxMin.y && point.y <= boxMax.y;
        }
    }

    /// <summary>
    /// System that highlights selected entities by modifying their material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XSelectionSystem))]
    public partial struct Space4XSelectionHighlightSystem : ISystem
    {
        // Selection highlight color
        private static readonly float4 SelectionHighlight = new float4(1f, 1f, 0.5f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SelectedTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            new HighlightSelectedJob
            {
                Time = time,
                SelectionHighlight = SelectionHighlight
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(SelectedTag))]
        private partial struct HighlightSelectedJob : IJobEntity
        {
            public float Time;
            public float4 SelectionHighlight;

            public void Execute(ref MaterialPropertyOverride materialProps)
            {
                // Add selection highlight effect
                float pulse = 0.8f + 0.2f * math.sin(Time * 4f);

                // Brighten the base color
                materialProps.BaseColor = materialProps.BaseColor * 1.2f * pulse;

                // Add selection emissive
                materialProps.EmissiveColor = SelectionHighlight * 0.3f * pulse;
            }
        }
    }
}


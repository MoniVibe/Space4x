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
    /// Selection system that handles entity selection via raycast.
    /// Finds closest carrier/craft/fleet marker under cursor and adds SelectedTag.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct Space4XSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SelectionInput>();
            state.RequireForUpdate<SelectionState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var input = SystemAPI.GetSingleton<SelectionInput>();
            
            if (!input.IsSelectPressed) return;

            // Get camera state for raycast
            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Clear old selection tags
            foreach (var (_, entity) in SystemAPI.Query<RefRO<SelectedTag>>().WithEntityAccess())
            {
                ecb.RemoveComponent<SelectedTag>(entity);
            }

            // Convert screen point to world ray (simplified - assumes camera is looking down)
            // In a full implementation, we'd use Camera.ScreenPointToRay, but for ECS we'll do a simple distance check
            float3 cameraPos = cameraState.Position;
            float3 cameraForward = math.forward(cameraState.Rotation);
            
            // For now, find closest entity to camera forward direction
            // In a full implementation, we'd project screen point to world plane and find closest entity
            Entity selectedEntity = Entity.Null;
            float closestDistance = float.MaxValue;

            // Query carriers
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                float3 toEntity = transform.ValueRO.Position - cameraPos;
                float distance = math.length(toEntity);
                float3 direction = math.normalize(toEntity);
                float dot = math.dot(direction, cameraForward);
                
                // Simple selection: closest entity in front of camera
                if (dot > 0.5f && distance < closestDistance && distance < 1000f)
                {
                    closestDistance = distance;
                    selectedEntity = entity;
                }
            }

            // Query crafts
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                float3 toEntity = transform.ValueRO.Position - cameraPos;
                float distance = math.length(toEntity);
                float3 direction = math.normalize(toEntity);
                float dot = math.dot(direction, cameraForward);
                
                if (dot > 0.5f && distance < closestDistance && distance < 1000f)
                {
                    closestDistance = distance;
                    selectedEntity = entity;
                }
            }

            // Query fleet markers
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<FleetImpostorTag>()
                         .WithEntityAccess())
            {
                float3 toEntity = transform.ValueRO.Position - cameraPos;
                float distance = math.length(toEntity);
                float3 direction = math.normalize(toEntity);
                float dot = math.dot(direction, cameraForward);
                
                if (dot > 0.5f && distance < closestDistance && distance < 1000f)
                {
                    closestDistance = distance;
                    selectedEntity = entity;
                }
            }

            // Add SelectedTag to chosen entity and update SelectionState
            if (selectedEntity != Entity.Null)
            {
                ecb.AddComponent(selectedEntity, new SelectedTag());
                
                var selectionState = SystemAPI.GetSingletonRW<SelectionState>();
                selectionState.ValueRW.PrimarySelected = selectedEntity;
                selectionState.ValueRW.SelectedCount = 1;
                
                // Determine selection type
                if (SystemAPI.HasComponent<CarrierPresentationTag>(selectedEntity))
                {
                    selectionState.ValueRW.Type = SelectionType.Carrier;
                }
                else if (SystemAPI.HasComponent<CraftPresentationTag>(selectedEntity))
                {
                    selectionState.ValueRW.Type = SelectionType.Craft;
                }
                else if (SystemAPI.HasComponent<FleetImpostorTag>(selectedEntity))
                {
                    selectionState.ValueRW.Type = SelectionType.Fleet;
                }
            }
            else
            {
                var selectionState = SystemAPI.GetSingletonRW<SelectionState>();
                selectionState.ValueRW.PrimarySelected = Entity.Null;
                selectionState.ValueRW.SelectedCount = 0;
                selectionState.ValueRW.Type = SelectionType.None;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

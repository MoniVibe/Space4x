using PureDOTS.Input;
using PureDOTS.Systems.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Tag component marking a rock as breakable.
    /// </summary>
    public struct BreakableRockTag : IComponentData { }

    /// <summary>
    /// Processes rock break events (double-click RMB on rock) and spawns smaller rocks.
    /// Game-specific: Rock spawning logic should be implemented by games.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct RockBreakSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<RockBreakEvent>(rtsInputEntity))
            {
                return;
            }

            var rockBreakBuffer = state.EntityManager.GetBuffer<RockBreakEvent>(rtsInputEntity);

            for (int i = 0; i < rockBreakBuffer.Length; i++)
            {
                var breakEvent = rockBreakBuffer[i];
                ProcessRockBreak(ref state, breakEvent);
            }

            rockBreakBuffer.Clear();
        }

        private void ProcessRockBreak(ref SystemState state, RockBreakEvent breakEvent)
        {
            Entity rockEntity = breakEvent.RockEntity;
            float3 hitPosition = breakEvent.HitPosition;

            // If rock entity is null, try to find rock at hit position
            if (rockEntity == Entity.Null)
            {
                // TODO: Query for BreakableRockTag entities near hit position
                // For now, skip if no entity provided
                return;
            }

            // Verify it's a breakable rock
            if (!state.EntityManager.HasComponent<BreakableRockTag>(rockEntity))
            {
                return;
            }

            // Get rock position and scale
            float3 rockPosition = hitPosition;
            float rockScale = 1f;

            if (state.EntityManager.HasComponent<LocalTransform>(rockEntity))
            {
                var transform = state.EntityManager.GetComponentData<LocalTransform>(rockEntity);
                rockPosition = transform.Position;
                rockScale = transform.Scale;
            }

            // Destroy original rock
            state.EntityManager.DestroyEntity(rockEntity);

            // Spawn 2 smaller rocks
            // Game-specific: Games should implement actual rock spawning/prefab instantiation
            // For now, create placeholder entities
            float3 offset1 = new float3(-0.5f, 0f, 0f);
            float3 offset2 = new float3(0.5f, 0f, 0f);
            float newScale = rockScale * 0.7f; // 70% of original size

            // Create first smaller rock
            Entity rock1 = state.EntityManager.CreateEntity(
                typeof(BreakableRockTag),
                typeof(LocalTransform));

            state.EntityManager.SetComponentData(rock1, LocalTransform.FromPositionRotationScale(
                rockPosition + offset1,
                quaternion.identity,
                newScale));

            // Create second smaller rock
            Entity rock2 = state.EntityManager.CreateEntity(
                typeof(BreakableRockTag),
                typeof(LocalTransform));

            state.EntityManager.SetComponentData(rock2, LocalTransform.FromPositionRotationScale(
                rockPosition + offset2,
                quaternion.identity,
                newScale));

            // TODO: Games should add physics components, visual components, etc.
        }
    }
}


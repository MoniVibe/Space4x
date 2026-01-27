using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Processes TerrainChangeEvent buffers and increments TerrainVersion singleton.
    /// Future terraforming systems will add events to the buffer, and this system handles version tracking.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct TerrainChangeProcessorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TerrainVersion>(out var terrainVersionEntity))
            {
                return;
            }

            // Check if any entity has TerrainChangeEvent buffer
            // In the future, terraforming systems will add events to a buffer on the TerrainVersion entity
            if (!state.EntityManager.HasBuffer<TerrainChangeEvent>(terrainVersionEntity))
            {
                state.EntityManager.AddBuffer<TerrainChangeEvent>(terrainVersionEntity);
            }

            var events = state.EntityManager.GetBuffer<TerrainChangeEvent>(terrainVersionEntity);
            if (events.Length == 0)
            {
                return;
            }

            // Process events and increment terrain version
            var terrainVersion = SystemAPI.GetComponentRW<TerrainVersion>(terrainVersionEntity);
            uint maxVersion = terrainVersion.ValueRO.Value;

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Version > maxVersion)
                {
                    maxVersion = evt.Version;
                }
            }

            // Increment version if events indicate terrain changed
            if (maxVersion > terrainVersion.ValueRO.Value)
            {
                terrainVersion.ValueRW.Value = maxVersion;
            }
            else if (events.Length > 0)
            {
                // Events were processed but version already updated, just increment
                terrainVersion.ValueRW.Value++;
            }

            // Clear processed events
            events.Clear();
        }
    }
}



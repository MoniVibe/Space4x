using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using PureDOTS.Demo.Orbit;

namespace Space4X.Demo
{
    /// <summary>
    /// Debug system that queries for OrbitCubeTag entities from PureDOTS
    /// and logs their count, positions, and render component status.
    /// Useful for verifying that OrbitCubeSystem is spawning entities correctly.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XOrbitDebugSystem : ISystem
    {
        private bool _loggedOnce;

        public void OnCreate(ref SystemState state)
        {
            // Only run if OrbitCubeTag exists (from PureDOTS)
            state.RequireForUpdate<OrbitCubeTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only log once to avoid spam
            if (_loggedOnce)
            {
                return;
            }

            _loggedOnce = true;

            var worldName = state.WorldUnmanaged.Name.ToString();
            var query = state.GetEntityQuery(ComponentType.ReadOnly<OrbitCubeTag>(), ComponentType.ReadOnly<LocalTransform>());
            var count = query.CalculateEntityCount();

            Debug.Log($"[Space4XOrbitDebugSystem] World '{worldName}': Found {count} OrbitCubeTag entities.");

            if (count == 0)
            {
                Debug.LogWarning("[Space4XOrbitDebugSystem] No OrbitCubeTag entities found. OrbitCubeSystem may not have run yet.");
                return;
            }

            // Log details for first few entities
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            int logCount = math.min(count, 5);
            for (int i = 0; i < logCount; i++)
            {
                var entity = entities[i];
                var transform = transforms[i];

                bool hasMMI = state.EntityManager.HasComponent<MaterialMeshInfo>(entity);
                bool hasRMA = state.EntityManager.HasComponent<RenderMeshArray>(entity);
                bool hasColor = state.EntityManager.HasComponent<URPMaterialPropertyBaseColor>(entity);

                Debug.Log($"[Space4XOrbitDebugSystem] Entity {entity.Index}: " +
                    $"Pos={transform.Position}, Scale={transform.Scale}, " +
                    $"HasMMI={hasMMI}, HasRMA={hasRMA}, HasColor={hasColor}");
            }

            entities.Dispose();
            transforms.Dispose();
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

//#define SPACE4X_DEBUG_CUBES

namespace Space4X.Demo
{
    /// <summary>
    /// Diagnostic system to verify debug cube entities exist and have render components.
    /// Logs entity counts, positions, and render component status.
    /// </summary>
#if SPACE4X_DEBUG_CUBES
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDebugCubeSpawnerSystem))]
    public partial struct Space4XDebugCubeDiagnosticSystem : ISystem
    {
        private bool _loggedOnce;

        public void OnCreate(ref SystemState state)
        {
            _loggedOnce = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only log once per session to avoid spam
            if (_loggedOnce)
            {
                return;
            }

            _loggedOnce = true;

            // Count all entities with LocalTransform (baseline)
            var allTransformQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform>()
                .Build();
            int totalEntityCount = allTransformQuery.CalculateEntityCount();

            // Count orbiting cubes specifically
            var cubeQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, DebugOrbitTag>()
                .Build();
            int cubeCount = cubeQuery.CalculateEntityCount();

            Debug.Log($"[Space4XDebugCubeDiagnosticSystem] Total entities with LocalTransform: {totalEntityCount}");
            Debug.Log($"[Space4XDebugCubeDiagnosticSystem] Orbiting cubes (with DebugOrbitTag): {cubeCount}");

            if (cubeCount == 0)
            {
                Debug.LogWarning("[Space4XDebugCubeDiagnosticSystem] ⚠ NO ORBITING CUBES FOUND!");
                Debug.LogWarning("   Possible causes:");
                Debug.LogWarning("   1. Space4XDebugCubeSpawnerSystem didn't run (check it's enabled)");
                Debug.LogWarning("   2. DemoRenderReady singleton missing (check SharedDemoRenderBootstrap ran)");
                Debug.LogWarning("   3. Entities were destroyed or created in wrong world");
                return;
            }

            // Check render components for each cube
            int cubesWithMaterialMeshInfo = 0;
            int cubesWithRenderBounds = 0;
            int cubesWithRenderMeshArray = 0;

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                .WithAll<DebugOrbitTag>()
                .WithEntityAccess())
            {
                var em = state.EntityManager;
                bool hasMMI = em.HasComponent<MaterialMeshInfo>(entity);
                bool hasRB = em.HasComponent<RenderBounds>(entity);
                bool hasRMA = em.HasComponent<RenderMeshArray>(entity);

                if (hasMMI) cubesWithMaterialMeshInfo++;
                if (hasRB) cubesWithRenderBounds++;
                if (hasRMA) cubesWithRenderMeshArray++;

                Debug.Log($"[Space4XDebugCubeDiagnosticSystem] Cube {entity.Index}: " +
                    $"pos={transform.ValueRO.Position}, " +
                    $"hasMaterialMeshInfo={hasMMI}, " +
                    $"hasRenderBounds={hasRB}, " +
                    $"hasRenderMeshArray={hasRMA}");

                // If missing render components, log details
                if (!hasMMI)
                {
                    Debug.LogError($"[Space4XDebugCubeDiagnosticSystem] ✗ Cube {entity.Index} MISSING MaterialMeshInfo!");
                }
                if (!hasRB)
                {
                    Debug.LogWarning($"[Space4XDebugCubeDiagnosticSystem] ⚠ Cube {entity.Index} MISSING RenderBounds (may be auto-computed)");
                }
                if (!hasRMA)
                {
                    Debug.LogError($"[Space4XDebugCubeDiagnosticSystem] ✗ Cube {entity.Index} MISSING RenderMeshArray!");
                }
            }

            Debug.Log($"[Space4XDebugCubeDiagnosticSystem] Render component summary: " +
                $"MaterialMeshInfo={cubesWithMaterialMeshInfo}/{cubeCount}, " +
                $"RenderBounds={cubesWithRenderBounds}/{cubeCount}, " +
                $"RenderMeshArray={cubesWithRenderMeshArray}/{cubeCount}");

            if (cubesWithMaterialMeshInfo < cubeCount)
            {
                Debug.LogError("[Space4XDebugCubeDiagnosticSystem] ✗ Some cubes missing MaterialMeshInfo - they won't render!");
            }
            if (cubesWithRenderMeshArray < cubeCount)
            {
                Debug.LogError("[Space4XDebugCubeDiagnosticSystem] ✗ Some cubes missing RenderMeshArray - they won't render!");
            }
        }
    }
#endif
}


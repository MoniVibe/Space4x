#if UNITY_EDITOR && PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Rendering
{
    /// <summary>
    /// Legacy scenario: One-shot debug rendering system for scenario entities.
    /// Automatically assigns render components to any entity with LocalToWorld but no MaterialMeshInfo.
    ///
    /// IMPORTANT: This is an example implementation for PureDOTS testing purposes.
    /// Real games should use proper render assignment systems with game-specific RenderKeys.
    ///
    /// Only runs when legacy scenario gates are enabled.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SharedRenderBootstrap))]
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "UniversalDebugRenderSetupSystem")]
    public partial struct UniversalDebugRenderSetupSystem : ISystem
    {
        private EntityQuery _noRenderQuery;
        private EntityQuery _rmaQuery;
        private bool _initialized;
        private bool _warnedNoRma;
        private bool _warnedEmptyRma;

        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            _noRenderQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalToWorld>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });

            _rmaQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderMeshArraySingleton>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;

            var em = state.EntityManager;

            // If no RenderMeshArraySingleton exists, bail.
            if (_rmaQuery.IsEmptyIgnoreFilter)
            {
                if (!_warnedNoRma)
                {
                    Debug.LogWarning("[UniversalDebugRenderSetupSystem] Missing RenderMeshArraySingleton; legacy scenario debug rendering disabled for this world.");
                    _warnedNoRma = true;
                }
                return;
            }

            _warnedNoRma = false;

            var rmaEntity = _rmaQuery.GetSingletonEntity();
            var renderArraySingleton = em.GetSharedComponentManaged<RenderMeshArraySingleton>(rmaEntity);
            var renderArray = renderArraySingleton.Value;

            if (renderArray == null ||
                renderArray.MeshReferences == null || renderArray.MeshReferences.Length == 0 ||
                renderArray.MaterialReferences == null || renderArray.MaterialReferences.Length == 0)
            {
                if (!_warnedEmptyRma)
                {
                    Debug.LogWarning("[UniversalDebugRenderSetupSystem] RenderMeshArraySingleton has no meshes/materials; legacy scenario debug rendering disabled for this world.");
                    _warnedEmptyRma = true;
                }
                return;
            }

            _warnedEmptyRma = false;

            using var entities = _noRenderQuery.ToEntityArray(state.WorldUpdateAllocator);
            if (entities.Length == 0) return;

            var desc = new RenderMeshDescription();
            foreach (var e in entities)
            {
                // Structural changes are intentional: attach render components using mesh/material index 0.
                RenderMeshUtility.AddComponents(
                    e,
                    em,
                    desc,
                    renderArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }

            // One-shot setup; disable after first run.
            _initialized = true;
            state.Enabled = false;
        }
    }
}

#endif

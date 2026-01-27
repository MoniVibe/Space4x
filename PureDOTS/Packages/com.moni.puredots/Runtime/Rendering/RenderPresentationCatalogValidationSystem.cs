using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Optional strict validation controls for render catalog enforcement.
    /// Add this singleton in headless scenarios when you want to hard-fail on violations.
    /// </summary>
    public struct RenderCatalogValidationSettings : IComponentData
    {
        /// <summary>If non-zero, validation failures throw (headless strict mode).</summary>
        public byte Strict;
    }

    /// <summary>
    /// Dev-only metrics snapshot for render batching stability. (Pure validation; no presentation hacks.)
    /// </summary>
    public struct RenderBatchingDiagnostics : IComponentData
    {
        public int SemanticKeyCount;
        public int LodCount;
        public int Theme0MissingSlots;
        public float Theme0Coverage01;
        public int RenderMeshCount;
        public int RenderMaterialCount;
    }

    /// <summary>
    /// Validates Theme 0 completeness once per catalog version and optionally records metrics.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct RenderPresentationCatalogValidationSystem : ISystem
    {
        private uint _lastCatalogVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalog = SystemAPI.GetSingleton<RenderPresentationCatalog>();
            if (!catalog.Blob.IsCreated)
                return;

            var hasVersion = SystemAPI.TryGetSingleton<RenderCatalogVersion>(out var version);
            var currentVersion = hasVersion ? version.Value : 0u;
            if (currentVersion == _lastCatalogVersion)
                return;
            _lastCatalogVersion = currentVersion;

            var strict = SystemAPI.TryGetSingleton<RenderCatalogValidationSettings>(out var settings) && settings.Strict != 0;

            // Validate Theme 0 completeness (ThemeId == 0).
            RenderPresentationCatalogValidation.ValidateTheme0OrLog(state.EntityManager, catalog, strict);

            // Optional metrics snapshot (only populated when the game provides RequiredRenderSemanticKey universe).
            var diagnostics = default(RenderBatchingDiagnostics);
            var haveMetrics = false;
            if (state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>())
                .TryGetSingletonBuffer(out DynamicBuffer<RenderPresentationCatalogValidation.RequiredRenderSemanticKey> required))
            {
                var requiredKeys = new Unity.Collections.NativeArray<ushort>(required.Length, Unity.Collections.Allocator.Temp);
                try
                {
                    for (int i = 0; i < required.Length; i++)
                        requiredKeys[i] = required[i].Value;

                    if (RenderPresentationCatalogValidation.TryComputeTheme0RequiredCoverage(catalog.Blob, requiredKeys, out var metrics, out _))
                    {
                        var meshCount = 0;
                        var materialCount = 0;
                        RenderPresentationCatalogValidation.TryGetRenderMeshArrayCounts(state.EntityManager, catalog.RenderMeshArrayEntity, out meshCount, out materialCount);

                        diagnostics = new RenderBatchingDiagnostics
                        {
                            SemanticKeyCount = metrics.SemanticKeyCount,
                            LodCount = metrics.LodCount,
                            Theme0MissingSlots = metrics.MissingRequiredSlots,
                            Theme0Coverage01 = metrics.Coverage01,
                            RenderMeshCount = meshCount,
                            RenderMaterialCount = materialCount
                        };
                        haveMetrics = true;
                    }
                }
                finally
                {
                    requiredKeys.Dispose();
                }
            }

            if (haveMetrics)
            {
                if (SystemAPI.HasSingleton<RenderBatchingDiagnostics>())
                {
                    SystemAPI.SetSingleton(diagnostics);
                }
                else
                {
                    var e = state.EntityManager.CreateEntity(typeof(RenderBatchingDiagnostics));
                    state.EntityManager.SetComponentData(e, diagnostics);
                    state.EntityManager.SetName(e, "RenderBatchingDiagnostics");
                }
            }
        }
    }
}


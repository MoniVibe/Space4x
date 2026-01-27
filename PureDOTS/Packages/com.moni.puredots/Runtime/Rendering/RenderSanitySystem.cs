using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Emits warnings for render pipeline health issues:
    /// - Missing MaterialMeshInfo when RenderKey entities exist
    /// - Material count spikes (batch breaking risk)
    /// - Draw command count thresholds (performance risk)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    public partial struct RenderSanitySystem : ISystem
    {
        private EntityQuery _materialMeshInfoQuery;
        private EntityQuery _renderKeyQuery;
        private bool _warned;
        private bool _warnedNoKeys;
        private bool _warnedNoVisible;
        private uint _lastMaterialCountCheckTick;
        private int _lastMaterialCount;
        private const uint MaterialCountCheckInterval = 300; // Check every 300 ticks (~5 seconds at 60fps)
        private const int MaterialCountWarningThreshold = 20; // Warn if >20 unique materials
        private const int MaterialCountSpikeThreshold = 5; // Warn if count increases by >5 since last check

        public void OnCreate(ref SystemState state)
        {
            _materialMeshInfoQuery = state.GetEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
            _renderKeyQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderKey>());
        }


        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            var diagnosticsEnabled = RenderSanityDebugSettings.LogsEnabled;
#else
            const bool diagnosticsEnabled = false;
#endif

            if (_renderKeyQuery.IsEmptyIgnoreFilter)
            {
#if UNITY_EDITOR
                if (diagnosticsEnabled && !_warnedNoKeys)
                {
                    Debug.LogWarning("[PureDOTS.Rendering] No RenderKey entities found; render pipeline is idle.");
                    _warnedNoKeys = true;
                }
#endif
                _warned = false;
                return;
            }

            _warnedNoKeys = false;

            int visibleSimEntities = 0;
            foreach (var flags in SystemAPI.Query<RefRO<RenderFlags>>().WithAll<RenderKey>())
            {
                if (flags.ValueRO.Visible != 0)
                {
                    visibleSimEntities++;
                }
            }

            if (visibleSimEntities == 0)
            {
#if UNITY_EDITOR
                if (diagnosticsEnabled && !_warnedNoVisible)
                {
                    Debug.LogError("[PureDOTS.Rendering] RenderKey entities exist but none are marked visible (RenderFlags.Visible == 0).");
                    _warnedNoVisible = true;
                }
#endif
                return;
            }

            _warnedNoVisible = false;

            if (!_warned)
            {
                var renderableCount = _materialMeshInfoQuery.CalculateEntityCount();
                if (renderableCount == 0)
                {
                    _warned = true;
                    Debug.LogWarning("[PureDOTS.Rendering] Visible RenderKey entities detected but no MaterialMeshInfo present. Check ApplyRenderCatalogSystem and render bootstrap.");
                }
            }

            // Check material count for batch breaking risks (dev-only, rate-limited)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (diagnosticsEnabled && SystemAPI.TryGetSingleton<RenderPresentationCatalog>(out var catalog) && catalog.RenderMeshArrayEntity != Entity.Null)
            {
                var currentTick = SystemAPI.TryGetSingleton<PureDOTS.Runtime.Components.TimeState>(out var timeState) ? timeState.Tick : 0u;
                if (currentTick - _lastMaterialCountCheckTick >= MaterialCountCheckInterval)
                {
                    _lastMaterialCountCheckTick = currentTick;

                    if (state.EntityManager.HasComponent<RenderMeshArray>(catalog.RenderMeshArrayEntity))
                    {
                        var renderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalog.RenderMeshArrayEntity);
                        var materialCount = renderMeshArray.MaterialReferences?.Length ?? 0;

                        if (materialCount > MaterialCountWarningThreshold)
                        {
                            Debug.LogWarning($"[PureDOTS.Rendering] High material count detected: {materialCount} materials in RenderMeshArray (threshold: {MaterialCountWarningThreshold}). This may break batching efficiency. Consider consolidating materials.");
                        }

                        if (_lastMaterialCount > 0 && materialCount - _lastMaterialCount > MaterialCountSpikeThreshold)
                        {
                            Debug.LogWarning($"[PureDOTS.Rendering] Material count spike detected: increased from {_lastMaterialCount} to {materialCount} (+{materialCount - _lastMaterialCount}). This may indicate a batching regression.");
                        }

                        _lastMaterialCount = materialCount;
                    }
                }
            }
#endif
        }

    }

#if UNITY_EDITOR
    static class RenderSanityDebugSettings
    {
        const string MenuPath = "PureDOTS/Debug/Rendering/Enable Render Sanity Logs";
        const string EditorPrefKey = "PureDOTS.Rendering.RenderSanity.EnableLogs";

        static bool _initialized;
        static bool _enabled;

        static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _enabled = EditorPrefs.GetBool(EditorPrefKey, false);
            Menu.SetChecked(MenuPath, _enabled);
            _initialized = true;
        }

        public static bool LogsEnabled
        {
            get
            {
                EnsureInitialized();
                return _enabled;
            }
            set
            {
                EnsureInitialized();
                _enabled = value;
                EditorPrefs.SetBool(EditorPrefKey, value);
                Menu.SetChecked(MenuPath, value);
            }
        }

        [MenuItem(MenuPath)]
        static void ToggleLogs()
        {
            LogsEnabled = !LogsEnabled;
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateToggle()
        {
            EnsureInitialized();
            Menu.SetChecked(MenuPath, _enabled);
            return true;
        }
    }
#endif
}

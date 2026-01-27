using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Runs once after <see cref="CoreSingletonBootstrapSystem"/> and reports any missing bootstrap singletons.
    /// Helps scene authors spot missing <c>PureDotsConfigAuthoring</c> / <c>SpatialPartitionAuthoring</c> wiring without
    /// waiting for downstream systems to throw <see cref="InvalidOperationException"/>.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BootstrapDiagnosticsSystem : ISystem
    {
        private bool _reported;

        public void OnCreate(ref SystemState state)
        {
            _reported = false;
        }

        public void OnUpdate(ref SystemState state)
        {
#if !UNITY_EDITOR
            state.Enabled = false;
            return;
#else
            if (Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }
#endif
            if (_reported)
            {
                state.Enabled = false;
                return;
            }

            var warnings = new NativeList<FixedString128Bytes>(Allocator.Temp);

            CheckSingleton<TimeState>(ref state, "TimeState singleton missing. Ensure PureDotsConfigAuthoring baked into the SubScene.", ref warnings);
            CheckSingleton<RewindState>(ref state, "RewindState singleton missing. Ensure CoreSingletonBootstrapSystem ran before gameplay systems.", ref warnings);
            CheckSingleton<ResourceTypeIndex>(ref state, "ResourceTypeIndex singleton missing. Confirm PureDotsRuntimeConfigLoader has a ResourceTypes catalog.", ref warnings);
            CheckSingleton<ResourceRecipeSet>(ref state, "ResourceRecipeSet singleton missing. Assign a RecipeCatalog on PureDotsRuntimeConfig.", ref warnings);
            CheckSingleton<SpatialGridConfig>(ref state, "SpatialGridConfig singleton missing. Add SpatialPartitionAuthoring with a profile asset.", ref warnings);
            CheckSingleton<SpatialGridState>(ref state, "SpatialGridState singleton missing. Spatial grid buffers were not created during bake.", ref warnings);
            CheckSingleton<TelemetryStream>(ref state, "TelemetryStream singleton missing. Add TelemetryStreamAuthoring to the bootstrap SubScene.", ref warnings);

#if UNITY_EDITOR
            if (warnings.Length == 0)
            {
                UnityEngine.Debug.Log("[BootstrapDiagnostics] Core DOTS bootstrap validated.");
            }
            else
            {
                for (int i = 0; i < warnings.Length; i++)
                {
                    UnityEngine.Debug.LogWarning($"[BootstrapDiagnostics] {warnings[i]}");
                }
            }
#endif

            warnings.Dispose();
            _reported = true;
            state.Enabled = false;
        }

        private static void CheckSingleton<T>(ref SystemState state, string message, ref NativeList<FixedString128Bytes> warnings)
            where T : unmanaged, IComponentData
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                warnings.Add(new FixedString128Bytes(message));
            }
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// DEV-only probe that logs high-level render counts once at runtime to simplify visibility debugging.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XRenderWorldProbeSystem : ISystem
    {
        private bool _logged;

        public void OnCreate(ref SystemState state)
        {
            _logged = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                state.Enabled = false;
                return;
            }

            if (_logged)
            {
                state.Enabled = false;
                return;
            }

            var worldName = state.WorldUnmanaged.Name.ToString();
            if (!string.Equals(worldName, "Game World", StringComparison.Ordinal))
            {
                state.Enabled = false;
                return;
            }

            var em = state.EntityManager;
            bool hasCatalog = SystemAPI.TryGetSingleton<RenderPresentationCatalog>(out _);
            int semanticCount = CountEntities<RenderSemanticKey>(ref state);
            int materialMeshCount = CountEntities<MaterialMeshInfo>(ref state);
            int asteroidCount = CountEntities<Asteroid>(ref state);
            int resourceStateCount = CountEntities<ResourceSourceState>(ref state);
            int resourceConfigCount = CountEntities<ResourceSourceConfig>(ref state);

            UnityEngine.Debug.Log(
                $"[Space4XRenderWorldProbe] World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={materialMeshCount} Asteroid={asteroidCount} ResourceSourceState={resourceStateCount} ResourceSourceConfig={resourceConfigCount}");

            _logged = true;
            state.Enabled = false;
        }

        private static int CountEntities<T>(ref SystemState state) where T : unmanaged, IComponentData
        {
            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            var count = query.CalculateEntityCount();
            query.Dispose();
            return count;
        }
    }
}
#endif

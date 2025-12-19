#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Rendering;
using Space4X.Registry;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Logs key entity counts once so smoke tests can distinguish missing SubScene content from presentation errors.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XSmokeWorldCountsSystem : ISystem
    {
        private bool _loggedOnce;
        private bool _loggedResolved;
        private double _startTime;

        public void OnCreate(ref SystemState state)
        {
            // Always run once; disable immediately after logging.
        }

        public void OnUpdate(ref SystemState state)
        {
            var worldName = state.WorldUnmanaged.Name.ToString();
            if (!string.Equals(worldName, "Game World", StringComparison.Ordinal))
            {
                state.Enabled = false;
                return;
            }

            var em = state.EntityManager;
            var hasCatalog = SystemAPI.HasSingleton<RenderPresentationCatalog>();
            var semanticCount = Count<RenderSemanticKey>(em);
            var meshInfoCount = Count<MaterialMeshInfo>(em);
            var carrierCount = Count<Carrier>(em);
            var miningCount = Count<MiningVessel>(em);
            var asteroidCount = Count<Asteroid>(em);
            var resourceConfigCount = Count<ResourceSourceConfig>(em);
            var resourceStateCount = Count<ResourceSourceState>(em);
            var sectionEntityAvailable = TryCountComponent(em, "Unity.Scenes.ResolvedSectionEntity, Unity.Scenes", out var sectionEntityCount);

            if (!_loggedOnce)
            {
                _startTime = UnityTime.realtimeSinceStartup;
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Initial World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")}");
                _loggedOnce = true;
            }

            var elapsed = UnityTime.realtimeSinceStartup - _startTime;
            var hasGameplayEntities = carrierCount > 0 || miningCount > 0 || asteroidCount > 0;
            var subSceneResolved = sectionEntityAvailable && sectionEntityCount > 0;
            if (subSceneResolved && !_loggedResolved)
            {
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Resolved World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} ElapsedSeconds={elapsed:0.0}");
                _loggedResolved = true;
            }

            if (hasGameplayEntities || elapsed >= 10.0)
            {
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Final World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} ElapsedSeconds={elapsed:0.0}");
                state.Enabled = false;
            }

        }

        private static int Count<T>(EntityManager em) where T : IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool TryCountComponent(EntityManager em, string assemblyQualifiedName, out int count)
        {
            var componentType = Type.GetType(assemblyQualifiedName);
            if (componentType == null)
            {
                count = 0;
                return false;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(componentType));
            count = query.CalculateEntityCount();
            return true;
        }
    }
}
#endif

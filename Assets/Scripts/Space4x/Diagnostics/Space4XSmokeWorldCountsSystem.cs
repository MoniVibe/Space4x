#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Rendering;
using Space4X.Registry;
using Unity.Collections;
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
            var strikeCraftCount = TryCountComponent(em, "Space4X.Registry.StrikeCraftProfile, Space4X.Gameplay", out var strikeCraft) ? strikeCraft : 0;
            var sectionEntityAvailable = TryCountComponent(em, "Unity.Scenes.ResolvedSectionEntity, Unity.Scenes", out var sectionEntityCount);
            
            // Check for fallback entities (should not exist per "no illusions" rule)
            var fallbackCarrierCount = CountFallbackEntities<Carrier>(em, "FALLBACK-CARRIER", c => c.CarrierId);
            var fallbackMinerCount = CountFallbackEntities<MiningVessel>(em, "FALLBACK-MINER", m => m.VesselId);

            if (!_loggedOnce)
            {
                _startTime = UnityTime.realtimeSinceStartup;
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Initial World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} StrikeCraft={strikeCraftCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")}");
                
                if (fallbackCarrierCount > 0 || fallbackMinerCount > 0)
                {
                    Debug.LogWarning($"[Space4XSmokeWorldCounts] PARITY VIOLATION: Fallback entities detected (Carriers={fallbackCarrierCount} Miners={fallbackMinerCount}). These should not exist - presentation must reflect headless progress only.");
                }
                
                // Vision check: carriers deploying miners, strike craft when enemies nearby
                var hasVisionEntities = carrierCount > 0 && miningCount > 0;
                if (!hasVisionEntities)
                {
                    Debug.LogWarning($"[Space4XSmokeWorldCounts] WARNING: Expected carriers ({carrierCount}) and mining vessels ({miningCount}) from space4x_smoke.json scenario. Strike craft ({strikeCraftCount}) should deploy when enemies are nearby.");
                }
                
                _loggedOnce = true;
            }

            var elapsed = UnityTime.realtimeSinceStartup - _startTime;
            var hasGameplayEntities = carrierCount > 0 || miningCount > 0 || asteroidCount > 0;
            var subSceneResolved = sectionEntityAvailable && sectionEntityCount > 0;
            if (subSceneResolved && !_loggedResolved)
            {
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Resolved World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} StrikeCraft={strikeCraftCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} ElapsedSeconds={elapsed:0.0}");
                _loggedResolved = true;
            }

            if (hasGameplayEntities || elapsed >= 10.0)
            {
                Debug.Log(
                    $"[Space4XSmokeWorldCounts] Phase=Final World='{worldName}' Catalog={hasCatalog} RenderSemanticKey={semanticCount} MaterialMeshInfo={meshInfoCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} StrikeCraft={strikeCraftCount} ResourceSourceConfig={resourceConfigCount} ResourceSourceState={resourceStateCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} ElapsedSeconds={elapsed:0.0}");
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
        
        private static int CountFallbackEntities<T>(EntityManager em, string fallbackId, System.Func<T, FixedString64Bytes> getId) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }
            
            using var components = query.ToComponentDataArray<T>(Allocator.Temp);
            var fallbackIdFixed = new FixedString64Bytes(fallbackId);
            var count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (getId(components[i]).Equals(fallbackIdFixed))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
#endif

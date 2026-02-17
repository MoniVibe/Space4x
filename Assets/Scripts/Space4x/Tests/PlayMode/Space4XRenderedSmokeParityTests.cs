#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PureDOTS.Rendering;
using Space4X.Registry;
using Space4X.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.Tests.PlayMode
{
    internal static class RenderedSmokeParityBatchEnvBootstrap
    {
        private const string RenderedSmokeParityFilter = "Space4X.Tests.PlayMode.Space4XRenderedSmokeParityTests";
        private const string DefaultScenarioPath = "Assets/Scenarios/space4x_smoke.json";
        private const string EnvScenarioPath = "SPACE4X_SCENARIO_PATH";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void PrimeBatchRunEnvironment()
        {
            ApplyDeterministicParityEnvironment(refreshRuntimeMode: false, reason: "AfterAssembliesLoaded");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ConfigureBatchRunEnvironment()
        {
            ApplyDeterministicParityEnvironment(refreshRuntimeMode: true, reason: "BeforeSplash");
        }

        private static bool IsRenderedSmokeParityRun()
        {
            var args = global::System.Environment.GetCommandLineArgs();
            if (args == null || args.Length < 2)
            {
                return false;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-testFilter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return args[i + 1].IndexOf(RenderedSmokeParityFilter, StringComparison.Ordinal) >= 0;
            }

            return false;
        }

        internal static void ApplyDeterministicParityEnvironment(bool refreshRuntimeMode, string reason)
        {
            if (!Application.isBatchMode || !IsRenderedSmokeParityRun())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(global::System.Environment.GetEnvironmentVariable(EnvScenarioPath)))
            {
                global::System.Environment.SetEnvironmentVariable(EnvScenarioPath, DefaultScenarioPath);
            }

            global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "1");
            global::System.Environment.SetEnvironmentVariable("PUREDOTS_RENDERING", "1");
            global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", "0");
            global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
            global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0");
            global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", "0");

            if (refreshRuntimeMode)
            {
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();
            }

            var scenario = global::System.Environment.GetEnvironmentVariable(EnvScenarioPath);
            UnityEngine.Debug.Log($"[RenderedSmokeParityBatchEnvBootstrap] applied=1 reason={reason} scenario={scenario}");
        }
    }

    /// <summary>
    /// Scene-level parity test to catch runtime render/presentation failures in automated runs.
    /// </summary>
    public class Space4XRenderedSmokeParityTests
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const int MaxTicks = 1200; // ~20 seconds at 60 fps equivalent update count

        [Test]
        public void SmokeScene_ResolvesPresentation_WithoutRuntimeExceptions()
        {
            var runtimeErrors = new List<string>();
            var previousScenario = global::System.Environment.GetEnvironmentVariable("SPACE4X_SCENARIO_PATH");
            var previousForceRender = global::System.Environment.GetEnvironmentVariable("PUREDOTS_FORCE_RENDER");
            var previousLegacyRendering = global::System.Environment.GetEnvironmentVariable("PUREDOTS_RENDERING");
            var previousHeadlessPresentation = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION");
            var previousHeadless = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS");
            var previousRewindProof = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF");
            var previousTimeProof = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF");
            var previousMiningProof = global::System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF");

            static void SetEnvironmentDefault(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(global::System.Environment.GetEnvironmentVariable(name)))
                {
                    global::System.Environment.SetEnvironmentVariable(name, value);
                }
            }

#if UNITY_EDITOR
            var originalBuildScenes = UnityEditor.EditorBuildSettings.scenes;
#endif

            void CaptureLog(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                {
                    return;
                }

                if (!IsFatalRuntimeSignal(condition, stackTrace))
                {
                    return;
                }

                runtimeErrors.Add($"{type}: {condition}");
            }

            Application.logMessageReceived += CaptureLog;
            try
            {
                RenderedSmokeParityBatchEnvBootstrap.ApplyDeterministicParityEnvironment(refreshRuntimeMode: true, reason: "TestStart");
                SetEnvironmentDefault("SPACE4X_SCENARIO_PATH", "Assets/Scenarios/space4x_smoke.json");
                // Force rendered play-mode parity regardless of outer pipeline env.
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_RENDERING", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", "0");
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();

#if UNITY_EDITOR
                EnsureSmokeSceneInBuildSettings(originalBuildScenes);
#endif

                Scene loadedScene = default;
#if UNITY_EDITOR
                try
                {
                    var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                        SmokeScenePath,
                        new LoadSceneParameters(LoadSceneMode.Single));

                    var spin = 0;
                    while (loadOp != null && !loadOp.isDone && spin < 240)
                    {
                        UpdateWorlds(runtimeErrors);
                        spin++;
                    }

                    var loadedByPath = SceneManager.GetSceneByPath(SmokeScenePath);
                    if (loadedByPath.IsValid() && loadedByPath.isLoaded)
                    {
                        SceneManager.SetActiveScene(loadedByPath);
                        loadedScene = loadedByPath;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XRenderedSmokeParityTests] LoadSceneAsyncInPlayMode failed ({ex.GetType().Name}): {ex.Message}");
                }
#else
                try
                {
                    SceneManager.LoadScene(SmokeSceneName, LoadSceneMode.Single);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XRenderedSmokeParityTests] Failed to load scene '{SmokeSceneName}' ({ex.GetType().Name}): {ex.Message}. Continuing with active scene.");
                }
                loadedScene = SceneManager.GetSceneByName(SmokeSceneName);
#endif

                if (!(loadedScene.IsValid() && loadedScene.isLoaded))
                {
                    loadedScene = SceneManager.GetSceneByName(SmokeSceneName);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                    {
                        SceneManager.SetActiveScene(loadedScene);
                    }
                }

                if (!(loadedScene.IsValid() && loadedScene.isLoaded))
                {
                    UnityEngine.Debug.LogWarning($"[Space4XRenderedSmokeParityTests] Scene '{SmokeSceneName}' not active after load call; continuing with currently active scene.");
                }

                EnsureCatalogBootstrapAwake(runtimeErrors);

                var hasCatalog = false;
                var hasMainCamera = false;
                var catalogWorld = "<none>";
                var semanticCount = 0;
                var materialMeshInfoCount = 0;
                var gameplayWorld = "<none>";
                var gameplayWorldSeen = false;
                var peakCarrierCount = 0;
                var peakMiningCount = 0;
                var peakAsteroidCount = 0;
                var peakGameplayMaterialMeshInfo = 0;
                var readyTicks = 0;

                for (var tick = 0; tick < MaxTicks; tick++)
                {
                    UpdateWorlds(runtimeErrors);

                    if (!hasCatalog)
                    {
                        EnsureCatalogBootstrapAwake(runtimeErrors);
                    }

                    hasMainCamera = HasEnabledCamera();
                    hasCatalog = TryFindCatalogWorld(out catalogWorld);
                    semanticCount = CountEntitiesAcrossWorlds<RenderSemanticKey>();
                    materialMeshInfoCount = CountEntitiesAcrossWorlds<MaterialMeshInfo>();
                    if (TryFindGameplayWorldStats(
                            out var gameplayCandidateWorld,
                            out var carrierCount,
                            out var miningCount,
                            out var asteroidCount,
                            out var gameplayMaterialMeshInfo))
                    {
                        gameplayWorldSeen = true;
                        gameplayWorld = gameplayCandidateWorld;
                        peakCarrierCount = Math.Max(peakCarrierCount, carrierCount);
                        peakMiningCount = Math.Max(peakMiningCount, miningCount);
                        peakAsteroidCount = Math.Max(peakAsteroidCount, asteroidCount);
                        peakGameplayMaterialMeshInfo = Math.Max(peakGameplayMaterialMeshInfo, gameplayMaterialMeshInfo);
                    }

                    if (hasCatalog && gameplayWorldSeen && peakCarrierCount > 0 && peakMiningCount > 0 && peakGameplayMaterialMeshInfo > 0)
                    {
                        readyTicks++;
                        if (readyTicks >= 120)
                        {
                            break;
                        }
                    }
                    else
                    {
                        readyTicks = 0;
                    }
                }

                Assert.IsTrue(hasCatalog, $"RenderPresentationCatalog singleton was not present in any active world (catalogWorld={catalogWorld}, semantic={semanticCount}, meshInfo={materialMeshInfoCount}).");
                Assert.Greater(materialMeshInfoCount, 0, $"MaterialMeshInfo stayed at zero, presentation did not resolve (catalogWorld={catalogWorld}, semantic={semanticCount}).");
                Assert.IsTrue(gameplayWorldSeen, $"Gameplay world was never observed (catalogWorld={catalogWorld}, semantic={semanticCount}).");
                Assert.Greater(peakCarrierCount + peakMiningCount, 0, $"No carrier/mining vessels were observed in gameplay world '{gameplayWorld}'.");
                Assert.Greater(peakGameplayMaterialMeshInfo, 0, $"MaterialMeshInfo stayed at zero in gameplay world '{gameplayWorld}' (carriers={peakCarrierCount}, miners={peakMiningCount}, asteroids={peakAsteroidCount}).");
                if (!hasMainCamera)
                {
                    UnityEngine.Debug.LogWarning("[Space4XRenderedSmokeParityTests] Smoke scene did not provide an enabled camera in this batch context.");
                }
                AppendMissingPresenterDiagnostics(runtimeErrors);
                AssertRuntimeErrorFree(runtimeErrors);
            }
            finally
            {
                Application.logMessageReceived -= CaptureLog;
#if UNITY_EDITOR
                UnityEditor.EditorBuildSettings.scenes = originalBuildScenes;
#endif
                global::System.Environment.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", previousScenario);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", previousForceRender);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_RENDERING", previousLegacyRendering);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", previousHeadlessPresentation);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", previousHeadless);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", previousRewindProof);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", previousTimeProof);
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", previousMiningProof);
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();
            }
        }

        private static void UpdateWorlds(List<string> runtimeErrors)
        {
            PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();
            for (var worldIndex = 0; worldIndex < World.All.Count; worldIndex++)
            {
                var world = World.All[worldIndex];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                try
                {
                    world.Update();
                }
                catch (Exception ex)
                {
                    runtimeErrors.Add($"Exception: World update failed in '{world.Name}' ({ex.GetType().Name}): {ex.Message}");
                }
            }
        }

        private static void EnsureCatalogBootstrapAwake(List<string> runtimeErrors)
        {
            var bootstraps = Resources.FindObjectsOfTypeAll<RenderPresentationCatalogRuntimeBootstrap>();
            var awake = typeof(RenderPresentationCatalogRuntimeBootstrap).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null)
            {
                return;
            }

            if (bootstraps != null)
            {
                for (var i = 0; i < bootstraps.Length; i++)
                {
                    var bootstrap = bootstraps[i];
                    if (bootstrap == null || !bootstrap.isActiveAndEnabled)
                    {
                        continue;
                    }

                    var bootstrapScene = bootstrap.gameObject.scene;
                    if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
                    {
                        continue;
                    }

                    try
                    {
                        awake.Invoke(bootstrap, null);
                    }
                    catch (Exception ex)
                    {
                        runtimeErrors.Add($"Exception: Render catalog bootstrap invoke failed ({ex.GetType().Name}): {ex.Message}");
                    }
                }
            }

            if (TryFindCatalogWorld(out _))
            {
                return;
            }

#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>("Assets/Data/Space4XRenderCatalog_v2.asset");
            if (catalog == null)
            {
                return;
            }

            var tempName = "__Space4XParityBootstrap";
            var tempGo = global::UnityEngine.GameObject.Find(tempName);
            var createdTemp = false;
            if (tempGo == null)
            {
                tempGo = new global::UnityEngine.GameObject(tempName);
                tempGo.SetActive(false);
                createdTemp = true;
            }

            var tempBootstrap = tempGo.GetComponent<RenderPresentationCatalogRuntimeBootstrap>();
            if (tempBootstrap == null)
            {
                tempBootstrap = tempGo.AddComponent<RenderPresentationCatalogRuntimeBootstrap>();
            }

            tempBootstrap.CatalogDefinition = catalog;
            if (createdTemp)
            {
                tempGo.SetActive(true);
            }
            try
            {
                awake.Invoke(tempBootstrap, null);
            }
            catch (Exception ex)
            {
                runtimeErrors.Add($"Exception: Temporary render catalog bootstrap invoke failed ({ex.GetType().Name}): {ex.Message}");
            }
#endif
        }

        private static void AssertRuntimeErrorFree(List<string> runtimeErrors)
        {
            if (runtimeErrors.Count == 0)
            {
                return;
            }

            var max = runtimeErrors.Count < 30 ? runtimeErrors.Count : 30;
            var lines = new string[max];
            for (var i = 0; i < max; i++)
            {
                lines[i] = runtimeErrors[i];
            }

            Assert.Fail("Runtime errors detected:\n" + string.Join("\n", lines));
        }

        private static void AppendMissingPresenterDiagnostics(List<string> runtimeErrors)
        {
            var remainingDetails = 5;
            for (var worldIndex = 0; worldIndex < World.All.Count; worldIndex++)
            {
                var world = World.All[worldIndex];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                    None = new[]
                    {
                        ComponentType.ReadOnly<MeshPresenter>(),
                        ComponentType.ReadOnly<SpritePresenter>(),
                        ComponentType.ReadOnly<DebugPresenter>(),
                        ComponentType.ReadOnly<TracerPresenter>()
                    },
                    Options = EntityQueryOptions.IgnoreComponentEnabledState
                });

                var missingCount = query.CalculateEntityCount();
                if (missingCount <= 0)
                {
                    continue;
                }

                runtimeErrors.Add($"Error: [RenderPresentationValidation] Missing presenter components persisted in world '{world.Name}' count={missingCount}");

                if (remainingDetails <= 0)
                {
                    continue;
                }

                using var entities = query.ToEntityArray(Allocator.Temp);
                var detailLimit = Math.Min(remainingDetails, entities.Length);
                for (var i = 0; i < detailLimit; i++)
                {
                    var entity = entities[i];
                    var semantic = em.GetComponentData<RenderSemanticKey>(entity).Value;
                    var renderKey = em.HasComponent<RenderKey>(entity) ? em.GetComponentData<RenderKey>(entity).ArchetypeId : (ushort)0;
                    var isSpawn = em.HasComponent<SpawnResource>(entity);
                    var isDebris = em.HasComponent<Space4XDebrisTag>(entity);
                    var isCargoVisual = em.HasComponent<CargoPresentationTag>(entity);
                    var isStorageMarker = em.HasComponent<StorageMarkerPresentationTag>(entity);
                    var isResourceMarker = em.HasComponent<ResourceMarkerPresentationTag>(entity);
                    var isResourcePickupPresenter = em.HasComponent<ResourcePickupPresentationTag>(entity);
                    runtimeErrors.Add($"Diag MissingPresenter World='{world.Name}' Entity={entity} Semantic={semantic} RenderKey={renderKey} SpawnResource={isSpawn} Debris={isDebris} CargoVisual={isCargoVisual} StorageMarker={isStorageMarker} ResourceMarker={isResourceMarker} ResourcePickupPresenter={isResourcePickupPresenter}");
                }

                remainingDetails -= detailLimit;
            }
        }

#if UNITY_EDITOR
        private static void EnsureSmokeSceneInBuildSettings(UnityEditor.EditorBuildSettingsScene[] originalBuildScenes)
        {
            var hasSmokeScene = false;
            for (var i = 0; i < originalBuildScenes.Length; i++)
            {
                if (string.Equals(originalBuildScenes[i].path, SmokeScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    hasSmokeScene = true;
                    break;
                }
            }

            if (hasSmokeScene)
            {
                return;
            }

            var updatedScenes = new List<UnityEditor.EditorBuildSettingsScene>(originalBuildScenes)
            {
                new UnityEditor.EditorBuildSettingsScene(SmokeScenePath, true)
            };
            UnityEditor.EditorBuildSettings.scenes = updatedScenes.ToArray();
        }
#endif

        private static bool HasEnabledCamera()
        {
            var mainCamera = global::UnityEngine.Camera.main;
            if (mainCamera != null && mainCamera.enabled)
            {
                return true;
            }

            var allCameras = global::UnityEngine.Camera.allCameras;
            for (var cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                var camera = allCameras[cameraIndex];
                if (camera != null && camera.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountEntitiesAcrossWorlds<T>() where T : unmanaged, IComponentData
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var count = CountEntities<T>(world.EntityManager);
                if (count > 0)
                {
                    return count;
                }
            }

            return 0;
        }

        private static bool TryFindCatalogWorld(out string worldName)
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());
                if (!query.IsEmptyIgnoreFilter)
                {
                    worldName = world.Name;
                    return true;
                }
            }

            worldName = "<none>";
            return false;
        }

        private static bool TryFindGameplayWorldStats(out string worldName, out int carrierCount, out int miningCount, out int asteroidCount, out int materialMeshInfoCount)
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var entityManager = world.EntityManager;
                carrierCount = CountEntities<Carrier>(entityManager);
                miningCount = CountEntities<MiningVessel>(entityManager);
                asteroidCount = CountEntities<Asteroid>(entityManager);
                if (carrierCount <= 0 && miningCount <= 0 && asteroidCount <= 0)
                {
                    continue;
                }

                materialMeshInfoCount = CountEntities<MaterialMeshInfo>(entityManager);
                worldName = world.Name;
                return true;
            }

            worldName = "<none>";
            carrierCount = 0;
            miningCount = 0;
            asteroidCount = 0;
            materialMeshInfoCount = 0;
            return false;
        }

        private static int CountEntities<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool IsFatalRuntimeSignal(string condition, string stackTrace)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return false;
            }

            var text = (condition + "\n" + stackTrace).ToLowerInvariant();
            if (text.Contains("there are 2 audio listeners in the scene"))
            {
                return false;
            }

            if (text.Contains("[renderpresentationvalidation] entity has rendersemantickey but no presenter component"))
            {
                return false;
            }

            return text.Contains("resolverendervariantjob.jobdata.requiredsemantickeys")
                   || text.Contains("objectdisposedexception")
                   || text.Contains("invalidoperationexception")
                   || text.Contains("nullreferenceexception")
                   || text.Contains("missingreferenceexception")
                   || text.Contains("[renderpresentationvalidation]")
                   || text.Contains("parity violation")
                   || text.Contains("completeness violation")
                   || text.Contains("error cs");
        }
    }
}
#endif

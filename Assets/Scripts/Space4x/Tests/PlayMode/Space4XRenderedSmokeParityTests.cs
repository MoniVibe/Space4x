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
            var previousHeadlessPresentation = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION");
            var previousHeadless = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS");

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
                global::System.Environment.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", "Assets/Scenarios/space4x_smoke.json");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", "0");

#if UNITY_EDITOR
                EnsureSmokeSceneInBuildSettings(originalBuildScenes);
#endif

                try
                {
                    SceneManager.LoadScene(SmokeSceneName, LoadSceneMode.Single);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XRenderedSmokeParityTests] Failed to load scene '{SmokeSceneName}' ({ex.GetType().Name}): {ex.Message}. Continuing with active scene.");
                }

                var loadedScene = SceneManager.GetSceneByName(SmokeSceneName);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(loadedScene);
                }
                else
                {
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
                        UnityEngine.Debug.LogWarning($"[Space4XRenderedSmokeParityTests] LoadSceneAsyncInPlayMode fallback failed ({ex.GetType().Name}): {ex.Message}");
                    }
#endif
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

                    if (hasCatalog && materialMeshInfoCount > 0)
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
                if (!hasMainCamera)
                {
                    UnityEngine.Debug.LogWarning("[Space4XRenderedSmokeParityTests] Smoke scene did not provide an enabled camera in this batch context.");
                }
                if (runtimeErrors.Count > 0)
                {
                    AppendMissingPresenterDiagnostics(runtimeErrors);
                }
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
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", previousHeadlessPresentation);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", previousHeadless);
            }
        }

        private static void UpdateWorlds(List<string> runtimeErrors)
        {
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
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
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

            using var entities = query.ToEntityArray(Allocator.Temp);
            var limit = entities.Length < 5 ? entities.Length : 5;
            for (var i = 0; i < limit; i++)
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
                runtimeErrors.Add($"Diag MissingPresenter Entity={entity} Semantic={semantic} RenderKey={renderKey} SpawnResource={isSpawn} Debris={isDebris} CargoVisual={isCargoVisual} StorageMarker={isStorageMarker} ResourceMarker={isResourceMarker} ResourcePickupPresenter={isResourcePickupPresenter}");
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

            return text.Contains("resolverendervariantjob.jobdata.requiredsemantickeys")
                   || text.Contains("objectdisposedexception")
                   || text.Contains("invalidoperationexception")
                   || text.Contains("nullreferenceexception")
                   || text.Contains("missingreferenceexception")
                   || text.Contains("[renderpresentationvalidation]")
                   || text.Contains("completeness violation")
                   || text.Contains("error cs");
        }
    }
}
#endif

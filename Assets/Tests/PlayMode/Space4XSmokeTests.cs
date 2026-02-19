#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Entities;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Rendering;
using UnityEngine.Rendering;

public class Space4XSmokeTests
{
    private const string SmokeSceneName = "TRI_Space4X_Smoke";
    private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
    private const float SceneLoadTimeoutSeconds = 30f;
    private const float SubSceneConversionTimeoutSeconds = 45f;
    private const float RenderAssignmentTimeoutSeconds = 45f;
    private const int FramesBeforeRenderCheck = 3;
    private const int WarmupFramesForMaterialMeshInfo = 30;
    private const string SubSceneMissingMessage = "SubScene not loaded/baked; no ECS entities exist.";

    [UnityTest]
    public IEnumerator SmokeScene_HasCoreSingletonsAndRenderables()
    {
        yield return LoadSmokeSceneAndWaitForWorld();
        AssertSrpConfigured();

        var em = RequireDefaultEntityManager();

        for (int i = 0; i < FramesBeforeRenderCheck; i++)
        {
            yield return null;
        }

        int semanticCount = 0;
        yield return WaitForCondition(
            () =>
            {
                semanticCount = CountEntities<RenderSemanticKey>(em);
                return semanticCount > 0;
            },
            SubSceneConversionTimeoutSeconds,
            SubSceneMissingMessage);

        int catalogCount = 0;
        yield return WaitForCondition(
            () =>
            {
                catalogCount = CountEntities<RenderPresentationCatalog>(em);
                return catalogCount > 0;
            },
            SubSceneConversionTimeoutSeconds,
            () => BuildRenderGateFailure(
                "Catalog=False",
                em,
                $"RenderPresentationCatalog count stayed at {catalogCount}."));

        for (int i = 0; i < FramesBeforeRenderCheck; i++)
        {
            yield return null;
        }

        yield return WaitForMaterialMeshInfoAfterWarmup(em);

        int carrierCount = 0;
        int miningVesselCount = 0;
        yield return WaitForCondition(
            () =>
            {
                carrierCount = CountEntities<Carrier>(em);
                miningVesselCount = CountEntities<MiningVessel>(em);
                return carrierCount > 0 || miningVesselCount > 0;
            },
            SubSceneConversionTimeoutSeconds,
            () => $"SubScene '{SmokeSceneName}' content missing: Carrier={carrierCount}, MiningVessel={miningVesselCount}. Scene likely failed to bake or load.");

        int asteroidCount = 0;
        yield return WaitForCondition(
            () =>
            {
                carrierCount = CountEntities<Carrier>(em);
                asteroidCount = CountEntities<Asteroid>(em);
                return carrierCount > 0 || asteroidCount > 0;
            },
            SubSceneConversionTimeoutSeconds,
            "Carrier or Asteroid entities never appeared; SubScene likely not loaded.");

        AssertCoreTimeSingletons(em);
    }

    [UnityTest]
    public IEnumerator MiningLoop_AdvancesTick()
    {
        yield return LoadSmokeSceneAndWaitForWorld();

        var em = RequireDefaultEntityManager();

        yield return WaitForCondition(
            () => CountEntities<TickTimeState>(em) > 0,
            SubSceneConversionTimeoutSeconds,
            "TickTimeState entity never appeared; core time system missing.");

        using var tickQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
        using var beforeTicks = tickQuery.ToComponentDataArray<TickTimeState>(Allocator.Temp);
        var startTick = beforeTicks.Length > 0 ? beforeTicks[0].Tick : 0u;

        yield return null;
        yield return null;

        using var afterTicks = tickQuery.ToComponentDataArray<TickTimeState>(Allocator.Temp);
        var endTick = afterTicks.Length > 0 ? afterTicks[0].Tick : startTick;

        Assert.Greater(endTick, startTick, "TickTimeState did not advance across frames");
    }

    private static IEnumerator LoadSmokeSceneAndWaitForWorld()
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(SmokeScenePath);
        Assert.IsTrue(buildIndex >= 0, $"Smoke scene path '{SmokeScenePath}' is not included in Build Settings.");

        var loadOperation = SceneManager.LoadSceneAsync(SmokeSceneName, LoadSceneMode.Single);
        Assert.IsNotNull(loadOperation, $"Failed to start loading scene '{SmokeSceneName}'.");

        float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
        while (!loadOperation.isDone)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Assert.Fail($"Timed out while loading scene '{SmokeSceneName}'.");
            }

            yield return null;
        }

        yield return WaitForCondition(
            () =>
            {
                var world = World.DefaultGameObjectInjectionWorld;
                return world != null && world.IsCreated;
            },
            SceneLoadTimeoutSeconds,
            "Default GameObject Injection World failed to initialize after loading the smoke scene.");

        var loadedScene = SceneManager.GetSceneByName(SmokeSceneName);
        Assert.IsTrue(loadedScene.IsValid() && loadedScene.isLoaded, $"Scene '{SmokeSceneName}' did not load.");
        SceneManager.SetActiveScene(loadedScene);
    }

    private static EntityManager RequireDefaultEntityManager()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        Assert.IsNotNull(world, "Default world missing");
        Assert.IsTrue(world.IsCreated, "Default world was not created");
        var entityManager = world.EntityManager;
        return entityManager;
    }

    private static void AssertSrpConfigured()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline ?? QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
        Assert.IsNotNull(pipeline, "Pure-green gate failed: SRP missing (GraphicsSettings.currentRenderPipeline is null).");
    }

    private static void AssertCoreTimeSingletons(EntityManager em)
    {
        Assert.Greater(CountEntities<TimeState>(em), 0, "Missing TimeState singleton");
        Assert.Greater(CountEntities<TickTimeState>(em), 0, "Missing TickTimeState singleton");
        Assert.Greater(CountEntities<RewindState>(em), 0, "Missing RewindState singleton");
    }

    private static int CountEntities<T>(EntityManager em) where T : IComponentData
    {
        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
        return query.CalculateEntityCount();
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, float timeoutSeconds, string failureMessage)
    {
        yield return WaitForCondition(predicate, timeoutSeconds, () => failureMessage);
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, float timeoutSeconds, Func<string> failureMessageFactory)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup <= deadline)
        {
            if (predicate())
            {
                yield break;
            }

            yield return null;
        }

        var message = failureMessageFactory != null ? failureMessageFactory() : "WaitForCondition failed.";
        Assert.Fail(message);
    }

    private static IEnumerator WaitForMaterialMeshInfoAfterWarmup(EntityManager em)
    {
        float deadline = Time.realtimeSinceStartup + RenderAssignmentTimeoutSeconds;
        while (Time.realtimeSinceStartup <= deadline)
        {
            int materialMeshCount = CountEntities<MaterialMeshInfo>(em);
            if (materialMeshCount > 0)
            {
                yield break;
            }

            for (int i = 0; i < WarmupFramesForMaterialMeshInfo; i++)
            {
                yield return null;
            }

            materialMeshCount = CountEntities<MaterialMeshInfo>(em);
            if (materialMeshCount <= 0)
            {
                Assert.Fail(BuildRenderGateFailure(
                    "MaterialMeshInfo=0 after warmup",
                    em,
                    $"MaterialMeshInfo count remained 0 after {WarmupFramesForMaterialMeshInfo} warmup frames."));
            }
        }

        Assert.Fail(BuildRenderGateFailure(
            "MaterialMeshInfo=0 after warmup",
            em,
            $"MaterialMeshInfo did not appear within {RenderAssignmentTimeoutSeconds:0.0}s."));
    }

    private static string BuildRenderGateFailure(string gate, EntityManager em, string detail)
    {
        int semanticCount = CountEntities<RenderSemanticKey>(em);
        int catalogCount = CountEntities<RenderPresentationCatalog>(em);
        int materialMeshCount = CountEntities<MaterialMeshInfo>(em);
        int carrierCount = CountEntities<Carrier>(em);
        int minerCount = CountEntities<MiningVessel>(em);
        var srpName = GraphicsSettings.currentRenderPipeline?.GetType().Name ?? "None";
        return $"Pure-green gate failed: {gate}. {detail} Snapshot: SRP={srpName}, Catalog={(catalogCount > 0)}, RenderSemanticKey={semanticCount}, MaterialMeshInfo={materialMeshCount}, Carrier={carrierCount}, MiningVessel={minerCount}.";
    }
}
#endif

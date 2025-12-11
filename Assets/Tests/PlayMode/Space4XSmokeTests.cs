using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using Space4X.Rendering;

public class Space4XSmokeTests
{
    [UnityTest]
    public IEnumerator SmokeScene_HasCoreSingletonsAndRenderables()
    {
        yield return SceneManager.LoadSceneAsync("TRI_Space4X_Smoke", LoadSceneMode.Single);
        yield return null;

        var world = World.DefaultGameObjectInjectionWorld;
        Assert.IsNotNull(world, "Default world missing");
        var em = world.EntityManager;

        bool hasTime = em.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()).CalculateEntityCount() > 0;
        bool hasTick = em.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>()).CalculateEntityCount() > 0;
        bool hasRewind = em.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()).CalculateEntityCount() > 0;

        Assert.IsTrue(hasTime && hasTick && hasRewind, "Missing time/rewind singletons");

        var renderQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<RenderKey>(),
            ComponentType.ReadOnly<LocalTransform>());
        Assert.Greater(renderQuery.CalculateEntityCount(), 0, "No renderable entities with RenderKey + Transform");
    }

    [UnityTest]
    public IEnumerator MiningLoop_AdvancesTick()
    {
        yield return SceneManager.LoadSceneAsync("TRI_Space4X_Smoke", LoadSceneMode.Single);
        yield return null;

        var world = World.DefaultGameObjectInjectionWorld;
        Assert.IsNotNull(world, "Default world missing");
        var em = world.EntityManager;

        var tickQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
        using var beforeTicks = tickQuery.ToComponentDataArray<TickTimeState>(Allocator.Temp);
        var startTick = beforeTicks.Length > 0 ? beforeTicks[0].Tick : 0u;

        yield return null;
        yield return null;

        using var afterTicks = tickQuery.ToComponentDataArray<TickTimeState>(Allocator.Temp);
        var endTick = afterTicks.Length > 0 ? afterTicks[0].Tick : startTick;

        Assert.Greater(endTick, startTick, "TickTimeState did not advance across frames");
    }
}


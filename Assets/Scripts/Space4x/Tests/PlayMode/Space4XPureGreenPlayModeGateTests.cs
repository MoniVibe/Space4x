#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Space4X.Tests.PlayMode
{
    public class Space4XPureGreenPlayModeGateTests
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const float SceneLoadTimeoutSec = 30f;
        private const float EntitiesTimeoutSec = 45f;

        private bool _sawErrorLog;
        private string _firstErrorLog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _sawErrorLog = false;
            _firstErrorLog = null;
            Application.logMessageReceived += OnLogMessageReceived;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SmokeScene_BootstrapsWorld_AndBindsRenderables()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SmokeScenePath);
            Assert.GreaterOrEqual(buildIndex, 0, $"Scene '{SmokeScenePath}' must be in Build Settings.");

            var load = SceneManager.LoadSceneAsync(SmokeSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, $"Failed to load '{SmokeSceneName}'.");

            float loadDeadline = Time.realtimeSinceStartup + SceneLoadTimeoutSec;
            while (!load.isDone)
            {
                if (Time.realtimeSinceStartup > loadDeadline)
                {
                    Assert.Fail($"Timed out loading '{SmokeSceneName}'.");
                }
                yield return null;
            }

            yield return WaitFor(
                () =>
                {
                    var world = World.DefaultGameObjectInjectionWorld;
                    return world != null && world.IsCreated;
                },
                SceneLoadTimeoutSec,
                "Default world was not created after loading smoke scene.");

            var worldRef = World.DefaultGameObjectInjectionWorld;
            var em = worldRef.EntityManager;

            yield return WaitFor(
                () => CountEntities<MaterialMeshInfo>(em) > 0,
                EntitiesTimeoutSec,
                "MaterialMeshInfo entities never appeared; render binding likely failed.");

            yield return null;
            yield return null;

            if (_sawErrorLog)
            {
                Assert.Fail($"Unexpected runtime error during smoke playmode: {_firstErrorLog}");
            }
        }

        private static int CountEntities<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static IEnumerator WaitFor(System.Func<bool> predicate, float timeoutSec, string timeoutMessage)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSec;
            while (Time.realtimeSinceStartup <= deadline)
            {
                if (predicate())
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail(timeoutMessage);
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_firstErrorLog))
            {
                _firstErrorLog = condition;
            }
            _sawErrorLog = true;
        }
    }
}
#endif

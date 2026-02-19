#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Space4X.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UCamera = UnityEngine.Camera;

namespace Space4X.Tests.PlayMode
{
    /// <summary>
    /// Headless-friendly contract check for Mode 1 follow alignment across a mode-cycle.
    /// </summary>
    public class Space4XMode1CameraFollowContractTests
    {
        private const string SmokeScenePath = "Assets/Scenes/TRI_Space4X_Smoke.unity";
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string SmokeScenarioPath = "Assets/Scenarios/space4x_smoke.json";
        private const int WarmupFrames = 120;
        private const int Mode1FramesInitial = 60;
        private const int Mode2Frames = 30;
        private const int Mode3Frames = 30;
        private const int Mode1FramesFinal = 120;
        private const int MinEligibleSamples = 20;
        private const float MaxUnalignedRatio = 0.02f;
        private const float MaxYawDeltaP95Deg = 50f;
        private const float MaxSpinDegPerS = 140f;

        [Timeout(180000)]
        [Test]
        public void ModeCycle_Mode1FollowAlignment_StaysWithinContract()
        {
            var previousEnv = CaptureEnvironment(new[]
            {
                "SPACE4X_SCENARIO_PATH",
                "PUREDOTS_FORCE_RENDER",
                "PUREDOTS_HEADLESS_PRESENTATION",
                "PUREDOTS_HEADLESS",
                "PUREDOTS_HEADLESS_REWIND_PROOF",
                "PUREDOTS_HEADLESS_TIME_PROOF",
                "SPACE4X_HEADLESS_MINING_PROOF"
            });
            var previousMode = Space4XControlModeState.CurrentMode;

            try
            {
                global::System.Environment.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", SmokeScenarioPath);
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_PRESENTATION", "1");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0");
                global::System.Environment.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", "0");
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();

                LoadSmokeScene();
                TickFrames(WarmupFrames);

                Assert.IsTrue(
                    TryFindCameraRig(out var camera, out var follow, out var controller),
                    "Mode1 contract test could not find camera + follow/controller components in smoke scene.");

                var stats = new Mode1Stats();
                var sampleClock = 0f;

                Space4XControlModeState.SetMode(Space4XControlMode.CursorOrient);
                CaptureFrames(Mode1FramesInitial, camera, follow, controller, stats, ref sampleClock);

                Space4XControlModeState.SetMode(Space4XControlMode.CruiseLook);
                CaptureFrames(Mode2Frames, camera, follow, controller, stats, ref sampleClock);

                Space4XControlModeState.SetMode(Space4XControlMode.Rts);
                CaptureFrames(Mode3Frames, camera, follow, controller, stats, ref sampleClock);

                Space4XControlModeState.SetMode(Space4XControlMode.CursorOrient);
                CaptureFrames(Mode1FramesFinal, camera, follow, controller, stats, ref sampleClock);

                var unalignedRatio = stats.EligibleSamples > 0
                    ? stats.UnalignedSamples / (float)stats.EligibleSamples
                    : 1f;
                var yawP95 = Percentile(stats.YawDeltas, 0.95f);
                var maxSpin = stats.MaxSpinDegPerS;

                UnityEngine.Debug.Log(
                    $"[Space4XMode1CameraFollowContractTests] mode1_samples={stats.Mode1Samples} eligible={stats.EligibleSamples} " +
                    $"unaligned={stats.UnalignedSamples} unaligned_ratio={unalignedRatio:F4} yaw_p95={yawP95:F2} max_spin={maxSpin:F2}");

                Assert.GreaterOrEqual(
                    stats.EligibleSamples,
                    MinEligibleSamples,
                    $"Insufficient Mode 1 samples with controlled+target entities (eligible={stats.EligibleSamples}).");
                Assert.LessOrEqual(
                    unalignedRatio,
                    MaxUnalignedRatio,
                    $"Mode 1 follow target drift exceeded threshold (unaligned_ratio={unalignedRatio:F4}).");
                Assert.LessOrEqual(
                    yawP95,
                    MaxYawDeltaP95Deg,
                    $"Mode 1 yaw delta p95 exceeded threshold (yaw_p95={yawP95:F2}).");
                Assert.LessOrEqual(
                    maxSpin,
                    MaxSpinDegPerS,
                    $"Mode 1 camera spin exceeded threshold (max_spin={maxSpin:F2}).");
            }
            finally
            {
                RestoreEnvironment(previousEnv);
                PureDOTS.Runtime.Core.RuntimeMode.RefreshFromEnvironment();
                Space4XControlModeState.SetMode(previousMode);
            }
        }

        private static void LoadSmokeScene()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                SmokeScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            var spin = 0;
            while (op != null && !op.isDone && spin < 300)
            {
                UpdateWorlds();
                spin++;
            }
#else
            SceneManager.LoadScene(SmokeSceneName, LoadSceneMode.Single);
#endif

            var scene = SceneManager.GetSceneByPath(SmokeScenePath);
            if (!(scene.IsValid() && scene.isLoaded))
            {
                scene = SceneManager.GetSceneByName(SmokeSceneName);
            }

            Assert.IsTrue(
                scene.IsValid() && scene.isLoaded,
                $"Could not load smoke scene '{SmokeSceneName}' for Mode 1 contract test.");
            SceneManager.SetActiveScene(scene);
        }

        private static void TickFrames(int frameCount)
        {
            for (var i = 0; i < frameCount; i++)
            {
                UpdateWorlds();
            }
        }

        private static void CaptureFrames(
            int frameCount,
            UCamera camera,
            Space4XFollowPlayerVessel follow,
            Space4XPlayerFlagshipController controller,
            Mode1Stats stats,
            ref float sampleClock)
        {
            for (var i = 0; i < frameCount; i++)
            {
                UpdateWorlds();
                if (Space4XControlModeState.CurrentMode != Space4XControlMode.CursorOrient)
                {
                    sampleClock += 1f / 60f;
                    continue;
                }

                stats.Mode1Samples++;

                if (!controller.TryGetControlledFlagship(out var controlled))
                {
                    continue;
                }
                if (!follow.TryGetDebugTarget(out var target))
                {
                    continue;
                }

                stats.EligibleSamples++;
                if (controlled != target)
                {
                    stats.UnalignedSamples++;
                }

                if (TryComputeYawDelta(camera, target, out var yawDeltaDeg))
                {
                    stats.YawDeltas.Add(Mathf.Abs(yawDeltaDeg));
                }

                stats.PushCameraYaw(camera.transform.eulerAngles.y, sampleClock);
                sampleClock += 1f / 60f;
            }
        }

        private static void UpdateWorlds()
        {
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
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
                    UnityEngine.Debug.LogWarning($"[Space4XMode1CameraFollowContractTests] world update failure: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static bool TryFindCameraRig(
            out UCamera camera,
            out Space4XFollowPlayerVessel follow,
            out Space4XPlayerFlagshipController controller)
        {
            camera = UCamera.main;
            if (camera == null)
            {
                camera = UnityEngine.Object.FindFirstObjectByType<UCamera>();
            }

            follow = null;
            controller = null;
            if (camera != null)
            {
                follow = camera.GetComponent<Space4XFollowPlayerVessel>();
                controller = camera.GetComponent<Space4XPlayerFlagshipController>();
            }

            if (follow == null)
            {
                follow = UnityEngine.Object.FindFirstObjectByType<Space4XFollowPlayerVessel>();
            }
            if (controller == null)
            {
                controller = UnityEngine.Object.FindFirstObjectByType<Space4XPlayerFlagshipController>();
            }

            return camera != null && follow != null && controller != null;
        }

        private static bool TryComputeYawDelta(UCamera camera, Entity targetEntity, out float yawDeltaDeg)
        {
            yawDeltaDeg = 0f;
            if (camera == null)
            {
                return false;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var em = world.EntityManager;
            if (!em.Exists(targetEntity) || !em.HasComponent<LocalTransform>(targetEntity))
            {
                return false;
            }

            var localTransform = em.GetComponentData<LocalTransform>(targetEntity);
            var shipForward3 = math.mul(localTransform.Rotation, new float3(0f, 0f, 1f));
            var cameraForward3 = (float3)camera.transform.forward;

            var shipForward2 = new Vector2(shipForward3.x, shipForward3.z);
            var cameraForward2 = new Vector2(cameraForward3.x, cameraForward3.z);
            if (shipForward2.sqrMagnitude < 1e-6f || cameraForward2.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            shipForward2.Normalize();
            cameraForward2.Normalize();
            yawDeltaDeg = Mathf.Abs(Vector2.SignedAngle(cameraForward2, shipForward2));
            return true;
        }

        private static float Percentile(List<float> values, float p)
        {
            if (values == null || values.Count == 0)
            {
                return 0f;
            }

            values.Sort();
            var clamped = Mathf.Clamp01(p);
            var index = Mathf.FloorToInt((values.Count - 1) * clamped);
            index = Mathf.Clamp(index, 0, values.Count - 1);
            return values[index];
        }

        private static Dictionary<string, string> CaptureEnvironment(string[] names)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++)
            {
                var key = names[i];
                snapshot[key] = global::System.Environment.GetEnvironmentVariable(key);
            }
            return snapshot;
        }

        private static void RestoreEnvironment(Dictionary<string, string> snapshot)
        {
            foreach (var pair in snapshot)
            {
                global::System.Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        private sealed class Mode1Stats
        {
            public int Mode1Samples;
            public int EligibleSamples;
            public int UnalignedSamples;
            public readonly List<float> YawDeltas = new List<float>(256);
            public float MaxSpinDegPerS;

            private bool _hasSpinBaseline;
            private float _lastYaw;
            private float _lastSampleAt;

            public void PushCameraYaw(float yawDegrees, float sampleTime)
            {
                if (_hasSpinBaseline)
                {
                    var dt = Mathf.Max(1e-4f, sampleTime - _lastSampleAt);
                    var spin = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yawDegrees)) / dt;
                    if (spin > MaxSpinDegPerS)
                    {
                        MaxSpinDegPerS = spin;
                    }
                }

                _lastYaw = yawDegrees;
                _lastSampleAt = sampleTime;
                _hasSpinBaseline = true;
            }
        }
    }
}
#endif

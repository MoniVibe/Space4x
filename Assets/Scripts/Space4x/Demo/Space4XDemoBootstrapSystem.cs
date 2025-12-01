using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Space4X.Registry;
using PlayEffectRequest = Space4X.Registry.PlayEffectRequest;
using Space4X.CameraSystem;

namespace Space4X.Demo
{
    /// <summary>
    /// Stores FixedString tokens for use in Bursted paths.
    /// </summary>
    public struct Space4XDemoIds : IComponentData
    {
        public FixedString64Bytes DefaultScenario;
        public FixedString64Bytes StartFxId;
        public FixedString64Bytes PingFxId;
        public byte PingQueued;
    }

    /// <summary>
    /// Tracks the bound presentation binding asset (managed object reference lives alongside this).
    /// </summary>
    public struct PresentationBindingReference : IComponentData
    {
        public FixedString128Bytes BindingPath;
        public byte HasBinding;
    }

    /// <summary>
    /// Managed companion that carries the binding asset reference.
    /// </summary>
    public sealed class PresentationBindingManaged : IComponentData
    {
        public Registry.Space4XPresentationBinding Binding;
    }

    /// <summary>
    /// Bootstraps demo singletons (DemoOptions, DemoBootstrapState) and integrates with TimeState/RewindState.
    /// Runs after core singletons are created.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    // Space4XCoreSingletonGuardSystem is also in InitializationSystemGroup, so UpdateAfter is valid within the group.
    // However, to be safe and avoid warnings if groups change, we can use OrderLast or just keep it as is if they are in the same group.
    // The user said: "Defer the [UpdateAfter]/[Before] warnings by putting demo systems in the right group... or by using OrderFirst/OrderLast."
    // Since they are in the same group, the warning might be about something else or just general best practice.
    // Let's try to be explicit about ordering.
    [UpdateAfter(typeof(Registry.Space4XCoreSingletonGuardSystem))]
    public partial struct Space4XDemoBootstrapSystem : ISystem
    {
        // Build FixedStrings from strings in a non-Burst context only
        [BurstDiscard]
        static FixedString64Bytes FS(string s)
        {
            var fs = default(FixedString64Bytes);
            for (int i = 0; i < s.Length; i++) fs.Append(s[i]);
            return fs;
        }

        [BurstDiscard]
        static FixedString128Bytes FS128(string s)
        {
            var fs = default(FixedString128Bytes);
            for (int i = 0; i < s.Length; i++) fs.Append(s[i]);
            return fs;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            var em = state.EntityManager;
            if (!SystemAPI.TryGetSingleton<Space4XDemoIds>(out _))
            {
                var idsEntity = em.CreateEntity(typeof(Space4XDemoIds));
                em.SetComponentData(idsEntity, new Space4XDemoIds
                {
                    DefaultScenario = FS("Scenarios/space4x_demo_mining_combat.json"),
                    StartFxId = FS("FX.Demo.Start"),
                    PingFxId = FS("FX.Demo.Ping"),
                    PingQueued = 0
                });
            }

            // Ensure DemoOptions singleton exists with fallback defaults
            if (!SystemAPI.HasSingleton<DemoOptions>())
            {
                var optionsEntity = em.CreateEntity();
                em.AddComponent<DemoOptions>(optionsEntity);
                em.SetComponentData(optionsEntity, new DemoOptions
                {
                    ScenarioPath = FS("Scenarios/space4x_demo_mining_combat.json"),
                    BindingsSet = 0, // Minimal
                    Veteran = 0
                });
            }

            // Ensure DemoBootstrapState singleton exists
            if (!SystemAPI.HasSingleton<DemoBootstrapState>())
            {
                var stateEntity = em.CreateEntity();
                em.AddComponent<DemoBootstrapState>(stateEntity);
                em.SetComponentData(stateEntity, new DemoBootstrapState
                {
                    Paused = 0,
                    TimeScale = 1.0f,
                    RewindEnabled = 0,
                    RngSeed = 12345u
                });
            }

            // Ensure DemoReporterState singleton exists
            if (!SystemAPI.HasSingleton<DemoReporterState>())
            {
                var reporterEntity = em.CreateEntity();
                em.AddComponent<DemoReporterState>(reporterEntity);
                em.SetComponentData(reporterEntity, new DemoReporterState
                {
                    ReportStarted = 0,
                    ReportCompleted = 0
                });
            }

            if (!SystemAPI.HasSingleton<DemoHotkeyConfig>())
            {
                var hotkeyConfigEntity = em.CreateEntity();
                em.AddComponentData(hotkeyConfigEntity, new DemoHotkeyConfig
                {
                    EnableEcsHotkeys = 0 // HUD handles hotkeys by default
                });
            }

            // Ensure effect request stream exists for VFX pings
            Entity effectStreamEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<Space4XEffectRequestStream>().WithEntityAccess())
            {
                effectStreamEntity = entity;
                break;
            }

            if (effectStreamEntity == Entity.Null)
            {
                effectStreamEntity = em.CreateEntity();
                em.AddComponent<Space4XEffectRequestStream>(effectStreamEntity);
                em.AddBuffer<Space4X.Registry.PlayEffectRequest>(effectStreamEntity);
            }

            // Ensure presentation binding reference exists (Managed component holds the asset)
            if (!SystemAPI.HasSingleton<PresentationBindingReference>())
            {
                var bindingEntity = em.CreateEntity();
                var bindingPath = "Assets/Resources/Space4X/Bindings/Space4XPresentationBinding_Minimal.asset";
                var bindingAsset = LoadBindingAsset(bindingPath);
                byte hasBinding = (byte)(bindingAsset != null ? 1 : 0);

                em.AddComponentData(bindingEntity, new PresentationBindingReference
                {
                    BindingPath = FS128(bindingPath),
                    HasBinding = hasBinding
                });

                if (bindingAsset != null)
                {
                    em.AddComponentObject(bindingEntity, new PresentationBindingManaged
                    {
                        Binding = bindingAsset
                    });
                }
                else
                {
                    UnityEngine.Debug.LogError($"[Space4XDemoBootstrapSystem] Presentation binding not found at {bindingPath}. Demo visuals may be missing.");
                    CreateBindingSentinel();
                }
            }

            // Ensure a camera exists for rendering
            EnsureCameraExists();

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ids = SystemAPI.GetSingletonRW<Space4XDemoIds>();

            // Apply demo state to TimeState and RewindState
            if (SystemAPI.TryGetSingletonRW<TimeState>(out var timeState) &&
                SystemAPI.TryGetSingletonRW<RewindState>(out var rewindState) &&
                SystemAPI.TryGetSingleton<DemoBootstrapState>(out var demoState))
            {
                // Apply pause state
                if (demoState.Paused == 1)
                {
                    timeState.ValueRW.IsPaused = true;
                }
                else
                {
                    timeState.ValueRW.IsPaused = false;
                }

                // Apply time scale
                timeState.ValueRW.CurrentSpeedMultiplier = demoState.TimeScale;

                // Apply rewind mode
                if (demoState.RewindEnabled == 1)
                {
                    rewindState.ValueRW.Mode = RewindMode.Record;
                }
                else
                {
                    rewindState.ValueRW.Mode = RewindMode.Playback;
                }
            }

            // One-shot ping to prove render is active
            if (ids.ValueRO.PingQueued == 0)
            {
                Entity effectStreamEntity = Entity.Null;
                foreach (var (_, entity) in SystemAPI.Query<Space4XEffectRequestStream>().WithEntityAccess())
                {
                    effectStreamEntity = entity;
                    break;
                }

                if (effectStreamEntity != Entity.Null && state.EntityManager.HasBuffer<Space4X.Registry.PlayEffectRequest>(effectStreamEntity))
                {
                    var buffer = state.EntityManager.GetBuffer<Space4X.Registry.PlayEffectRequest>(effectStreamEntity);
                    buffer.Add(new Space4X.Registry.PlayEffectRequest
                    {
                        EffectId = ids.ValueRO.PingFxId,
                        AttachTo = Entity.Null,
                        Lifetime = 0f
                    });
                    ids.ValueRW.PingQueued = 1;
                }
            }
        }

        [BurstDiscard]
        private static Registry.Space4XPresentationBinding LoadBindingAsset(string path)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Registry.Space4XPresentationBinding>(path);
            if (asset != null)
                return asset;
#endif
            // Attempt Resources load if someone moved it there
            var resourcePath = path.Replace("Assets/Resources/", string.Empty);
            resourcePath = resourcePath.Replace(".asset", string.Empty);
            return UnityEngine.Resources.Load<Registry.Space4XPresentationBinding>(resourcePath);
        }

        [BurstDiscard]
        private static void EnsureCameraExists()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            if (existing != null)
            {
                EnsureAudioListenerGuard(existing.gameObject);
                return;
            }

            var go = new UnityEngine.GameObject("Space4X Camera");
            var cam = go.AddComponent<UnityEngine.Camera>();
            go.tag = "MainCamera";
            cam.transform.position = new UnityEngine.Vector3(0, 50, -70.71f); // Better starting position for Space4X camera
            cam.transform.LookAt(UnityEngine.Vector3.zero);
            if (go.GetComponent<UnityEngine.AudioListener>() == null)
            {
                go.AddComponent<UnityEngine.AudioListener>();
            }

            EnsureAudioListenerGuard(go);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Add new Space4X camera system components
            if (go.GetComponent<Space4X.CameraSystem.Space4XCameraController>() == null)
            {
                var controller = go.AddComponent<Space4X.CameraSystem.Space4XCameraController>();
                UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] Added Space4XCameraController");

                // Configure initial state for demo
                var state = controller.GetCurrentState();
                state.Position = new UnityEngine.Vector3(0, 50, -70.71f);
                state.Yaw = 0f; // radians
                state.Pitch = Unity.Mathematics.math.radians(45f);
                state.Distance = 70.71f;
                state.PerspectiveMode = false;
                controller.SetState(state);
            }
            else
            {
                UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] Space4XCameraController already exists");
            }

            if (go.GetComponent<PureDOTS.Runtime.Camera.CameraRigApplier>() == null)
            {
                go.AddComponent<PureDOTS.Runtime.Camera.CameraRigApplier>();
                UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] Added CameraRigApplier");
            }
            else
            {
                UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] CameraRigApplier already exists");
            }

            // Ensure input bridge exists (it will create itself as singleton)
            Space4X.CameraSystem.Space4XCameraInputBridge.TryGetSnapshot(out _);
#endif
            UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] Spawned Space4X camera with new rig system.");
        }

        private static void EnsureAudioListenerGuard(UnityEngine.GameObject go)
        {
            if (go.GetComponent<EnsureSingleAudioListener>() == null)
            {
                go.AddComponent<EnsureSingleAudioListener>();
            }
        }

        [BurstDiscard]
        private static void CreateBindingSentinel()
        {
            var sentinel = new UnityEngine.GameObject("MissingPresentationBinding");
            sentinel.transform.position = UnityEngine.Vector3.zero;
            var cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            cube.transform.SetParent(sentinel.transform, false);
            cube.transform.localScale = new UnityEngine.Vector3(3f, 3f, 3f);
            var renderer = cube.GetComponent<UnityEngine.Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = UnityEngine.Color.magenta
                };
            }
            UnityEngine.Debug.LogWarning("[Space4XDemoBootstrapSystem] Spawned sentinel cube to indicate missing presentation binding.");
        }

        [BurstDiscard]
        private static void EnsureCameraInputAuthoring()
        {
            const string prefabResourcePath = "Space4X_Input";
            var existing = GameObject.Find("Space4X_Input");
            if (existing != null)
                return;

            var prefab = Resources.Load<GameObject>(prefabResourcePath);
            if (prefab != null)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = "Space4X_Input";
                UnityEngine.Debug.Log("[Space4XDemoBootstrapSystem] Instantiated Space4X_Input prefab for camera input.");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[Space4XDemoBootstrapSystem] Space4X_Input prefab not found in Resources/{prefabResourcePath}. Camera input system will use defaults.");
            }
        }
    }
}

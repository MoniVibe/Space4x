using System;
using System.IO;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Rendering;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UDebug = UnityEngine.Debug;
using UTime = UnityEngine.Time;
using SystemEnv = System.Environment;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Emits bounded JSONL telemetry for flagship ownership, movement/render writer hints,
    /// and probable disappearance causes.
    /// </summary>
    [DefaultExecutionOrder(-9365)]
    [DisallowMultipleComponent]
    public sealed class Space4XFlagshipPipelineProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_FLAGSHIP_PIPELINE_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_FLAGSHIP_PIPELINE_PROBE_OUT";
        private const string ProbeOutDirEnv = "SPACE4X_PROBE_OUT_DIR";
        private const string ProbeEchoEnv = "SPACE4X_FLAGSHIP_PIPELINE_PROBE_ECHO";
        private const string DefaultFileName = "space4x_flagship_pipeline_probe.jsonl";

        [SerializeField] private bool enabledByDefault;
        [SerializeField] private Key toggleKey = Key.F9;
        [SerializeField] private float sampleIntervalSeconds = 0.2f;
        [SerializeField] private float heartbeatIntervalSeconds = 2f;
        [SerializeField] private bool emitEverySample;
        [SerializeField] private bool echoEventsToUnityLog;

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private float _nextHeartbeatAt;

        private string _lastControlled = "Entity.Null";
        private string _lastTarget = "Entity.Null";
        private string _lastTracked = "Entity.Null";
        private bool _lastAligned;
        private string _lastDisappearanceReason = string.Empty;
        private string _lastMovementHints = string.Empty;
        private string _lastRenderHints = string.Empty;

        [Serializable]
        private sealed class ProbeRecord
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public string kind = string.Empty;
            public string mode = string.Empty;
            public string note = string.Empty;
            public string preset = string.Empty;

            public string controlled = "Entity.Null";
            public string target = "Entity.Null";
            public string tracked = "Entity.Null";
            public int aligned;

            public int has_controlled;
            public int has_target;
            public int tracked_exists;
            public int tracked_has_carrier;
            public int tracked_has_mining_vessel;
            public int tracked_has_player_flagship_tag;

            public int tracked_has_local_transform;
            public int tracked_has_local_to_world;
            public int tracked_has_sim_pose_snapshot;
            public int tracked_has_frame_membership;
            public int tracked_has_frame_driven_tag;

            public float local_x;
            public float local_y;
            public float local_z;
            public float world_x;
            public float world_y;
            public float world_z;
            public float ltw_gap;

            public int tracked_has_material_mesh_info;
            public int tracked_material_raw;
            public int tracked_mesh_raw;
            public int tracked_sub_mesh;
            public int tracked_material_index;
            public int tracked_mesh_index;
            public int tracked_has_material_mesh_index_range;
            public string tracked_material_mesh_index_range = string.Empty;

            public int tracked_has_render_semantic_key;
            public int tracked_render_semantic_key;
            public int tracked_has_render_variant_key;
            public int tracked_render_variant_key;
            public int tracked_has_render_variant_override;
            public int tracked_render_variant_override_enabled;
            public int tracked_render_variant_override;
            public int has_render_catalog;
            public int render_catalog_variant_count;
            public int tracked_variant_in_catalog_range;
            public int tracked_override_in_catalog_range;

            public int tracked_has_mesh_presenter;
            public int tracked_mesh_presenter_enabled;
            public int tracked_mesh_presenter_def;
            public int tracked_has_render_flags;
            public int tracked_render_visible;
            public int tracked_has_disable_rendering;
            public int tracked_has_render_bounds;
            public int tracked_has_world_render_bounds;
            public float tracked_world_bounds_extent_mag;
            public int tracked_world_bounds_nonfinite;
            public int tracked_world_bounds_tiny;

            public int tracked_has_render_cullable;
            public float tracked_cull_distance;
            public float tracked_distance_to_camera;
            public int tracked_beyond_cull_distance;
            public int tracked_has_render_sample_index;
            public int tracked_render_should_sample;

            public int tracked_has_movement_suppressed;
            public int tracked_movement_suppressed_enabled;
            public int tracked_has_orbit_anchor;
            public int tracked_has_orbit_anchor_state;
            public int tracked_has_rogue_orbit_tag;
            public int tracked_has_micro_impulse_tag;

            public int tracked_has_player_input;
            public float input_forward;
            public float input_strafe;
            public float input_vertical;
            public float input_roll;
            public int input_movement_enabled;
            public int input_boost;
            public int input_retro_brake;

            public int tracked_has_vessel_movement;
            public float vessel_speed;
            public int vessel_is_moving;
            public int tracked_has_ship_runtime_state;
            public float runtime_speed;
            public int runtime_dampeners;

            public string movement_writer_hints = string.Empty;
            public string render_writer_hints = string.Empty;
            public string disappearance_reason = string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindAnyObjectByType<Space4XFlagshipPipelineProbe>() != null)
                return;

            var go = new GameObject("Space4X Flagship Pipeline Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XFlagshipPipelineProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            echoEventsToUnityLog = ResolveEchoFromEnvOrDefault();
            _nextSampleAt = 0f;
            _nextHeartbeatAt = 0f;
            ResetState();
            UDebug.Log($"[Space4XFlagshipPipelineProbe] boot active={_active} echo={echoEventsToUnityLog} path='{_outputPath}'");
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                _active = !_active;
                UDebug.Log($"[Space4XFlagshipPipelineProbe] active={_active} path='{_outputPath}'");
                if (!_active)
                {
                    ResetState();
                }
            }

            if (!_active)
                return;

            if (UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = UTime.unscaledTime + Mathf.Max(0.05f, sampleIntervalSeconds);
            SampleAndEmit();
        }

        private void SampleAndEmit()
        {
            var now = UTime.unscaledTime;
            var record = BuildRecord();
            var heartbeatDue = now >= _nextHeartbeatAt;

            var transition =
                record.controlled != _lastControlled ||
                record.target != _lastTarget ||
                record.tracked != _lastTracked;

            var aligned = record.aligned != 0;
            var alignmentChanged = aligned != _lastAligned;
            var hintsChanged =
                !string.Equals(record.movement_writer_hints, _lastMovementHints, StringComparison.Ordinal) ||
                !string.Equals(record.render_writer_hints, _lastRenderHints, StringComparison.Ordinal);
            var disappearanceChanged = !string.Equals(record.disappearance_reason, _lastDisappearanceReason, StringComparison.Ordinal);
            var hasDisappearance = !string.Equals(record.disappearance_reason, "none", StringComparison.Ordinal);
            var hasMismatch = record.has_controlled != 0 && record.has_target != 0 && !aligned;

            var shouldEmit =
                emitEverySample ||
                heartbeatDue ||
                transition ||
                alignmentChanged ||
                hintsChanged ||
                disappearanceChanged ||
                hasMismatch ||
                hasDisappearance;

            if (!shouldEmit)
                return;

            if (heartbeatDue)
            {
                record.kind = "heartbeat";
                record.note = "state_snapshot";
                _nextHeartbeatAt = now + Mathf.Max(0.25f, heartbeatIntervalSeconds);
            }
            else if (hasDisappearance)
            {
                record.kind = "disappearance";
                record.note = record.disappearance_reason;
            }
            else if (hasMismatch)
            {
                record.kind = "mismatch";
                record.note = "controlled_target_diverged";
            }
            else if (transition)
            {
                record.kind = "transition";
                record.note = "entity_binding_changed";
            }
            else
            {
                record.kind = "sample";
                record.note = "state_changed";
            }

            if (!TryAppendLine(_outputPath, JsonUtility.ToJson(record)))
                return;

            if (echoEventsToUnityLog)
            {
                UDebug.LogWarning(
                    $"[Space4XFlagshipPipelineProbe] kind={record.kind} note='{record.note}' controlled={record.controlled} target={record.target} tracked={record.tracked} aligned={record.aligned} disappearance='{record.disappearance_reason}' movementHints='{record.movement_writer_hints}' renderHints='{record.render_writer_hints}'");
            }

            _lastControlled = record.controlled;
            _lastTarget = record.target;
            _lastTracked = record.tracked;
            _lastAligned = aligned;
            _lastDisappearanceReason = record.disappearance_reason;
            _lastMovementHints = record.movement_writer_hints;
            _lastRenderHints = record.render_writer_hints;
        }

        private ProbeRecord BuildRecord()
        {
            var record = new ProbeRecord
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                mode = Space4XControlModeState.CurrentMode.ToString(),
                preset = Space4XRunStartSelection.ShipPresetId ?? string.Empty,
                disappearance_reason = "none"
            };

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                record.disappearance_reason = "world_unavailable";
                return record;
            }

            var em = world.EntityManager;
            var camera = UCamera.main ?? FindAnyObjectByType<UCamera>();
            var follow = camera != null ? camera.GetComponent<Space4XFollowPlayerVessel>() : FindAnyObjectByType<Space4XFollowPlayerVessel>();
            var controller = camera != null ? camera.GetComponent<Space4XPlayerFlagshipController>() : FindAnyObjectByType<Space4XPlayerFlagshipController>();
            var catalogVariantCount = -1;

            using (var catalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>()))
            {
                if (!catalogQuery.IsEmptyIgnoreFilter)
                {
                    var catalog = catalogQuery.GetSingleton<RenderPresentationCatalog>();
                    if (catalog.Blob.IsCreated)
                    {
                        catalogVariantCount = catalog.Blob.Value.Variants.Length;
                        record.has_render_catalog = 1;
                        record.render_catalog_variant_count = catalogVariantCount;
                    }
                }
            }

            var controlled = Entity.Null;
            var hasControlled = controller != null && controller.TryGetControlledFlagship(out controlled);
            var target = Entity.Null;
            var hasTarget = follow != null && follow.TryGetDebugTarget(out target);

            record.has_controlled = hasControlled ? 1 : 0;
            record.has_target = hasTarget ? 1 : 0;
            record.controlled = FormatEntity(hasControlled ? controlled : Entity.Null);
            record.target = FormatEntity(hasTarget ? target : Entity.Null);
            record.aligned = hasControlled && hasTarget && controlled == target ? 1 : 0;

            var tracked = hasControlled ? controlled : (hasTarget ? target : Entity.Null);
            record.tracked = FormatEntity(tracked);
            if (tracked == Entity.Null)
            {
                record.disappearance_reason = "no_controlled_or_target";
                return record;
            }

            if (!em.Exists(tracked))
            {
                record.disappearance_reason = "tracked_entity_missing";
                return record;
            }

            record.tracked_exists = 1;
            record.tracked_has_carrier = em.HasComponent<Carrier>(tracked) ? 1 : 0;
            record.tracked_has_mining_vessel = em.HasComponent<MiningVessel>(tracked) ? 1 : 0;
            record.tracked_has_player_flagship_tag = em.HasComponent<PlayerFlagshipTag>(tracked) ? 1 : 0;
            record.tracked_has_sim_pose_snapshot = em.HasComponent<SimPoseSnapshot>(tracked) ? 1 : 0;
            record.tracked_has_frame_membership = em.HasComponent<Space4XFrameMembership>(tracked) ? 1 : 0;
            record.tracked_has_frame_driven_tag = em.HasComponent<Space4XFrameDrivenTransformTag>(tracked) ? 1 : 0;

            var hasLocalTransform = em.HasComponent<LocalTransform>(tracked);
            var hasLocalToWorld = em.HasComponent<LocalToWorld>(tracked);
            record.tracked_has_local_transform = hasLocalTransform ? 1 : 0;
            record.tracked_has_local_to_world = hasLocalToWorld ? 1 : 0;

            var localPos = Vector3.zero;
            var worldPos = Vector3.zero;
            if (hasLocalTransform)
            {
                var local = em.GetComponentData<LocalTransform>(tracked);
                localPos = new Vector3(local.Position.x, local.Position.y, local.Position.z);
                record.local_x = localPos.x;
                record.local_y = localPos.y;
                record.local_z = localPos.z;
            }

            if (hasLocalToWorld)
            {
                var ltw = em.GetComponentData<LocalToWorld>(tracked);
                worldPos = new Vector3(ltw.Position.x, ltw.Position.y, ltw.Position.z);
                record.world_x = worldPos.x;
                record.world_y = worldPos.y;
                record.world_z = worldPos.z;
            }
            else
            {
                worldPos = localPos;
                record.world_x = worldPos.x;
                record.world_y = worldPos.y;
                record.world_z = worldPos.z;
            }

            record.ltw_gap = hasLocalTransform && hasLocalToWorld ? Vector3.Distance(localPos, worldPos) : 0f;

            record.tracked_has_material_mesh_info = em.HasComponent<MaterialMeshInfo>(tracked) ? 1 : 0;
            if (record.tracked_has_material_mesh_info != 0)
            {
                var materialMesh = em.GetComponentData<MaterialMeshInfo>(tracked);
                record.tracked_material_raw = materialMesh.Material;
                record.tracked_mesh_raw = materialMesh.Mesh;
                record.tracked_sub_mesh = materialMesh.SubMesh;
                record.tracked_material_index = materialMesh.Material < 0 ? MaterialMeshInfo.StaticIndexToArrayIndex(materialMesh.Material) : materialMesh.Material;
                record.tracked_mesh_index = materialMesh.Mesh < 0 ? MaterialMeshInfo.StaticIndexToArrayIndex(materialMesh.Mesh) : materialMesh.Mesh;
                record.tracked_has_material_mesh_index_range = materialMesh.HasMaterialMeshIndexRange ? 1 : 0;
                record.tracked_material_mesh_index_range = materialMesh.HasMaterialMeshIndexRange
                    ? materialMesh.MaterialMeshIndexRange.ToString()
                    : "n/a";
            }

            record.tracked_has_render_semantic_key = em.HasComponent<RenderSemanticKey>(tracked) ? 1 : 0;
            if (record.tracked_has_render_semantic_key != 0)
            {
                record.tracked_render_semantic_key = em.GetComponentData<RenderSemanticKey>(tracked).Value;
            }

            record.tracked_has_render_variant_key = em.HasComponent<RenderVariantKey>(tracked) ? 1 : 0;
            if (record.tracked_has_render_variant_key != 0)
            {
                record.tracked_render_variant_key = em.GetComponentData<RenderVariantKey>(tracked).Value;
                record.tracked_variant_in_catalog_range = catalogVariantCount > 0 &&
                                                         record.tracked_render_variant_key >= 0 &&
                                                         record.tracked_render_variant_key < catalogVariantCount
                    ? 1
                    : 0;
            }

            record.tracked_has_render_variant_override = em.HasComponent<RenderVariantOverride>(tracked) ? 1 : 0;
            if (record.tracked_has_render_variant_override != 0)
            {
                record.tracked_render_variant_override = em.GetComponentData<RenderVariantOverride>(tracked).Value;
                record.tracked_render_variant_override_enabled = em.IsComponentEnabled<RenderVariantOverride>(tracked) ? 1 : 0;
                record.tracked_override_in_catalog_range = catalogVariantCount > 0 &&
                                                           record.tracked_render_variant_override >= 0 &&
                                                           record.tracked_render_variant_override < catalogVariantCount
                    ? 1
                    : 0;
            }

            record.tracked_has_mesh_presenter = em.HasComponent<MeshPresenter>(tracked) ? 1 : 0;
            if (record.tracked_has_mesh_presenter != 0)
            {
                var meshPresenter = em.GetComponentData<MeshPresenter>(tracked);
                record.tracked_mesh_presenter_def = meshPresenter.DefIndex;
                record.tracked_mesh_presenter_enabled = em.IsComponentEnabled<MeshPresenter>(tracked) ? 1 : 0;
            }

            record.tracked_has_render_flags = em.HasComponent<RenderFlags>(tracked) ? 1 : 0;
            if (record.tracked_has_render_flags != 0)
            {
                var flags = em.GetComponentData<RenderFlags>(tracked);
                record.tracked_render_visible = flags.Visible != 0 ? 1 : 0;
            }
            record.tracked_has_disable_rendering = em.HasComponent<DisableRendering>(tracked) ? 1 : 0;
            record.tracked_has_render_bounds = em.HasComponent<RenderBounds>(tracked) ? 1 : 0;
            record.tracked_has_world_render_bounds = em.HasComponent<WorldRenderBounds>(tracked) ? 1 : 0;
            if (record.tracked_has_world_render_bounds != 0)
            {
                var worldBounds = em.GetComponentData<WorldRenderBounds>(tracked).Value;
                var extentLength = math.length(worldBounds.Extents);
                record.tracked_world_bounds_extent_mag = extentLength;
                record.tracked_world_bounds_nonfinite = math.any(!math.isfinite(worldBounds.Center)) || !math.isfinite(extentLength) ? 1 : 0;
                record.tracked_world_bounds_tiny = extentLength <= 1e-4f ? 1 : 0;
            }

            record.tracked_has_render_cullable = em.HasComponent<RenderCullable>(tracked) ? 1 : 0;
            record.tracked_distance_to_camera = camera != null
                ? Vector3.Distance(camera.transform.position, hasLocalToWorld ? worldPos : localPos)
                : 0f;
            if (record.tracked_has_render_cullable != 0)
            {
                var cullable = em.GetComponentData<RenderCullable>(tracked);
                record.tracked_cull_distance = cullable.CullDistance;
                record.tracked_beyond_cull_distance =
                    cullable.CullDistance > 0f && record.tracked_distance_to_camera > cullable.CullDistance ? 1 : 0;
            }

            record.tracked_has_render_sample_index = em.HasComponent<RenderSampleIndex>(tracked) ? 1 : 0;
            if (record.tracked_has_render_sample_index != 0)
            {
                var sampleIndex = em.GetComponentData<RenderSampleIndex>(tracked);
                record.tracked_render_should_sample = sampleIndex.ShouldRender != 0 ? 1 : 0;
            }

            record.tracked_has_movement_suppressed = em.HasComponent<MovementSuppressed>(tracked) ? 1 : 0;
            if (record.tracked_has_movement_suppressed != 0)
            {
                record.tracked_movement_suppressed_enabled = em.IsComponentEnabled<MovementSuppressed>(tracked) ? 1 : 0;
            }

            record.tracked_has_orbit_anchor = em.HasComponent<Space4XOrbitAnchor>(tracked) ? 1 : 0;
            record.tracked_has_orbit_anchor_state = em.HasComponent<Space4XOrbitAnchorState>(tracked) ? 1 : 0;
            record.tracked_has_rogue_orbit_tag = em.HasComponent<Space4XRogueOrbitTag>(tracked) ? 1 : 0;
            record.tracked_has_micro_impulse_tag = em.HasComponent<Space4XMicroImpulseTag>(tracked) ? 1 : 0;

            record.tracked_has_player_input = em.HasComponent<PlayerFlagshipFlightInput>(tracked) ? 1 : 0;
            if (record.tracked_has_player_input != 0)
            {
                var input = em.GetComponentData<PlayerFlagshipFlightInput>(tracked);
                record.input_forward = input.Forward;
                record.input_strafe = input.Strafe;
                record.input_vertical = input.Vertical;
                record.input_roll = input.Roll;
                record.input_movement_enabled = input.MovementEnabled != 0 ? 1 : 0;
                record.input_boost = input.BoostPressed != 0 ? 1 : 0;
                record.input_retro_brake = input.RetroBrakePressed != 0 ? 1 : 0;
            }

            record.tracked_has_vessel_movement = em.HasComponent<VesselMovement>(tracked) ? 1 : 0;
            if (record.tracked_has_vessel_movement != 0)
            {
                var movement = em.GetComponentData<VesselMovement>(tracked);
                record.vessel_speed = movement.CurrentSpeed > 0f ? movement.CurrentSpeed : math.length(movement.Velocity);
                record.vessel_is_moving = movement.IsMoving != 0 ? 1 : 0;
            }

            record.tracked_has_ship_runtime_state = em.HasComponent<ShipFlightRuntimeState>(tracked) ? 1 : 0;
            if (record.tracked_has_ship_runtime_state != 0)
            {
                var runtime = em.GetComponentData<ShipFlightRuntimeState>(tracked);
                record.runtime_speed = math.length(runtime.VelocityWorld);
                record.runtime_dampeners = runtime.InertialDampenersEnabled != 0 ? 1 : 0;
            }

            record.movement_writer_hints = ResolveMovementHints(record);
            record.render_writer_hints = ResolveRenderHints(record);
            record.disappearance_reason = ResolveDisappearanceReason(record);
            return record;
        }

        private static string ResolveMovementHints(ProbeRecord record)
        {
            var hints = string.Empty;
            AppendHint(ref hints, record.tracked_has_player_input != 0, "player_flagship_input");
            AppendHint(ref hints, record.tracked_has_vessel_movement != 0, "vessel_movement");
            AppendHint(ref hints, record.tracked_has_ship_runtime_state != 0, "ship_runtime_state");
            AppendHint(ref hints, record.tracked_has_movement_suppressed != 0 && record.tracked_movement_suppressed_enabled != 0, "movement_suppressed_enabled");
            AppendHint(ref hints, record.tracked_has_orbit_anchor != 0 || record.tracked_has_orbit_anchor_state != 0, "orbit_anchor");
            AppendHint(ref hints, record.tracked_has_rogue_orbit_tag != 0, "rogue_orbit_tag");
            AppendHint(ref hints, record.tracked_has_micro_impulse_tag != 0, "micro_impulse_tag");
            AppendHint(ref hints, record.tracked_has_frame_membership != 0, "frame_membership");
            AppendHint(ref hints, record.tracked_has_frame_driven_tag != 0, "frame_driven_transform");
            AppendHint(ref hints, record.tracked_has_sim_pose_snapshot != 0, "sim_pose_snapshot");
            return string.IsNullOrWhiteSpace(hints) ? "none" : hints;
        }

        private static string ResolveRenderHints(ProbeRecord record)
        {
            var hints = string.Empty;
            AppendHint(ref hints, record.tracked_has_render_semantic_key != 0, "semantic_key");
            AppendHint(ref hints, record.tracked_has_render_variant_key != 0, "variant_key");
            AppendHint(ref hints, record.tracked_has_render_variant_override != 0 && record.tracked_render_variant_override_enabled != 0, "variant_override_enabled");
            AppendHint(ref hints, record.has_render_catalog != 0, "catalog_ready");
            AppendHint(ref hints, record.tracked_has_render_variant_key != 0 && record.has_render_catalog != 0 && record.tracked_variant_in_catalog_range == 0, "variant_key_out_of_catalog_range");
            AppendHint(ref hints, record.tracked_has_render_variant_override != 0 && record.tracked_render_variant_override_enabled != 0 && record.has_render_catalog != 0 && record.tracked_override_in_catalog_range == 0, "variant_override_out_of_catalog_range");
            AppendHint(ref hints, record.tracked_has_mesh_presenter != 0 && record.tracked_mesh_presenter_enabled != 0, "mesh_presenter_enabled");
            AppendHint(ref hints, record.tracked_has_material_mesh_info != 0, "material_mesh_info");
            AppendHint(ref hints, record.tracked_has_render_bounds != 0, "render_bounds");
            AppendHint(ref hints, record.tracked_has_world_render_bounds != 0, "world_render_bounds");
            AppendHint(ref hints, record.tracked_has_disable_rendering != 0, "disable_rendering_tag");
            AppendHint(ref hints, record.tracked_has_render_sample_index != 0 && record.tracked_render_should_sample == 0, "density_sample_suppressed_candidate");
            AppendHint(ref hints, record.tracked_has_render_cullable != 0 && record.tracked_beyond_cull_distance != 0, "beyond_cull_distance_candidate");
            AppendHint(ref hints, record.tracked_has_carrier != 0, "carrier_presentation_lifecycle_writer");
            AppendHint(ref hints, record.tracked_has_player_flagship_tag != 0, "flagship_controller_writer");
            AppendHint(ref hints,
                record.tracked_has_carrier != 0 &&
                record.tracked_has_player_flagship_tag != 0 &&
                record.tracked_has_render_variant_override != 0 &&
                record.tracked_render_variant_override_enabled != 0,
                "variant_writer_conflict_possible");
            return string.IsNullOrWhiteSpace(hints) ? "none" : hints;
        }

        private static string ResolveDisappearanceReason(ProbeRecord record)
        {
            if (record.tracked_exists == 0)
                return "tracked_entity_missing";
            if (record.tracked_has_local_transform == 0 && record.tracked_has_local_to_world == 0)
                return "missing_transform_components";
            if (record.tracked_has_render_semantic_key == 0)
                return "missing_render_semantic_key";
            if (record.tracked_has_render_variant_key != 0 && record.has_render_catalog != 0 && record.tracked_variant_in_catalog_range == 0)
                return "variant_key_out_of_catalog_range";
            if (record.tracked_has_render_variant_override != 0 && record.tracked_render_variant_override_enabled != 0 && record.has_render_catalog != 0 && record.tracked_override_in_catalog_range == 0)
                return "variant_override_out_of_catalog_range";
            if (record.tracked_has_material_mesh_info == 0)
                return "missing_material_mesh_info";
            if (record.tracked_has_mesh_presenter == 0)
                return "missing_mesh_presenter";
            if (record.tracked_mesh_presenter_enabled == 0)
                return "mesh_presenter_disabled";
            if (record.tracked_has_disable_rendering != 0)
                return "disabled_by_disable_rendering";
            if (record.tracked_has_render_flags != 0 && record.tracked_render_visible == 0)
                return "hidden_by_render_flags";
            if (record.tracked_has_world_render_bounds != 0 && record.tracked_world_bounds_nonfinite != 0)
                return "invalid_world_render_bounds";
            if (record.tracked_has_world_render_bounds != 0 && record.tracked_world_bounds_tiny != 0)
                return "degenerate_world_render_bounds";
            return "none";
        }

        private static void AppendHint(ref string hints, bool condition, string hint)
        {
            if (!condition)
                return;

            if (string.IsNullOrWhiteSpace(hints))
            {
                hints = hint;
            }
            else
            {
                hints += "|" + hint;
            }
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null
                ? "Entity.Null"
                : $"Entity({entity.Index}:{entity.Version})";
        }

        private static bool TryAppendLine(string path, string line)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(path, line + SystemEnv.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                UDebug.LogWarning($"[Space4XFlagshipPipelineProbe] write failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveOutputPath()
        {
            var env = SystemEnv.GetEnvironmentVariable(ProbeOutputEnv);
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var outDir = SystemEnv.GetEnvironmentVariable(ProbeOutDirEnv);
            if (!string.IsNullOrWhiteSpace(outDir))
                return Path.Combine(outDir, DefaultFileName);

            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        private bool ResolveActiveFromEnvOrDefault()
        {
            if (!TryReadEnvironmentEnabled(out var enabled))
                return enabledByDefault;

            return enabled;
        }

        private bool ResolveEchoFromEnvOrDefault()
        {
            if (!TryReadEnvironmentFlag(ProbeEchoEnv, out var enabled))
                return echoEventsToUnityLog;

            return enabled;
        }

        private static bool IsEnabledByEnvironment()
        {
            return TryReadEnvironmentEnabled(out var enabled) && enabled;
        }

        private static bool TryReadEnvironmentEnabled(out bool enabled)
        {
            return TryReadEnvironmentFlag(ProbeEnabledEnv, out enabled);
        }

        private static bool TryReadEnvironmentFlag(string key, out bool enabled)
        {
            enabled = false;
            var env = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(env))
                return false;

            var token = env.Trim().ToLowerInvariant();
            if (token == "1" || token == "true" || token == "yes" || token == "on")
            {
                enabled = true;
                return true;
            }

            if (token == "0" || token == "false" || token == "no" || token == "off")
            {
                enabled = false;
                return true;
            }

            return false;
        }

        private void ResetState()
        {
            _lastControlled = "Entity.Null";
            _lastTarget = "Entity.Null";
            _lastTracked = "Entity.Null";
            _lastAligned = false;
            _lastDisappearanceReason = string.Empty;
            _lastMovementHints = string.Empty;
            _lastRenderHints = string.Empty;
        }
    }
}

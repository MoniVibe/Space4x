using System;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Interaction;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;

namespace Space4X.UI
{
    /// <summary>
    /// Claims and drives a single flagship entity from keyboard input for the presentation slice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XPlayerFlagshipController : MonoBehaviour
    {
        [Header("Flight Model")]
        [SerializeField] private float moveSpeed = 28f;
        [SerializeField] private float maxReverseSpeed = 16f;
        [SerializeField] private float maxStrafeSpeed = 18f;
        [SerializeField] private float boostMultiplier = 2f;
        [SerializeField] private float verticalSpeed = 14f;
        [SerializeField] private float forwardThrustAcceleration = 32f;
        [SerializeField] private float reverseThrustAcceleration = 26f;
        [SerializeField] private float strafeThrustAcceleration = 20f;
        [SerializeField] private float verticalThrustAcceleration = 18f;
        [SerializeField] private float passiveDriftDrag = 0.03f;
        [SerializeField] private bool inertialDampeners = false;
        [SerializeField] private float dampenerDeceleration = 12f;
        [SerializeField] private float retroBrakeAcceleration = 45f;
        [SerializeField] private Key retroBrakeKey = Key.X;
        [SerializeField] private Key toggleDampenersKey = Key.Z;

        [Header("Vessel-Driven Flight Tuning")]
        [SerializeField] private bool inheritMovementFromClaimedVessel = true;
        [SerializeField] private float forwardSpeedFromVesselMultiplier = 1.25f;
        [SerializeField] private float reverseSpeedFromVesselMultiplier = 0.85f;
        [SerializeField] private float strafeSpeedFromVesselMultiplier = 0.75f;
        [SerializeField] private float verticalSpeedFromVesselMultiplier = 0.65f;
        [SerializeField] private float forwardAccelFromVesselMultiplier = 1.6f;
        [SerializeField] private float reverseAccelFromVesselMultiplier = 1.35f;
        [SerializeField] private float strafeAccelFromVesselMultiplier = 1.15f;
        [SerializeField] private float verticalAccelFromVesselMultiplier = 1f;
        [SerializeField] private float dampenerFromVesselMultiplier = 1.25f;
        [SerializeField] private float retroBrakeFromVesselMultiplier = 1.65f;
        [SerializeField] private float minInheritedSpeed = 14f;
        [SerializeField] private float maxInheritedSpeed = 220f;
        [SerializeField] private float minInheritedAcceleration = 18f;
        [SerializeField] private float maxInheritedAcceleration = 280f;

        [Header("Mode Hotkeys")]
        [SerializeField] private Key cursorModeHotkey = Key.Digit1;
        [SerializeField] private Key cruiseModeHotkey = Key.Digit2;
        [SerializeField] private Key rtsModeHotkey = Key.Digit3;

        [Header("Attitude")]
        [SerializeField] private float rollSpeedDegrees = 75f;
        [SerializeField] private float cursorTurnSharpness = 12f;
        [SerializeField] private float maxCursorPitchDegrees = 65f;
        [SerializeField] private Color highlightColor = new Color(0.25f, 0.95f, 0.65f, 1f);

        [Header("Mode 1 Mouse Steering Gate")]
        [SerializeField] private bool requireMouseHoldForCursorSteering = true;
        [SerializeField] private bool cursorSteerWithLeftMouse = true;
        [SerializeField] private bool cursorSteerWithRightMouse = true;
        [SerializeField] private bool useClickAnchorDeadZoneSteering = true;
        [SerializeField] private float cursorSteerDeadZonePixels = 22f;
        [SerializeField] private float cursorSteerMaxOffsetPixels = 320f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _playerFlagshipQuery;
        private EntityQuery _carrierAnyQuery;
        private EntityQuery _miningAnyQuery;
        private EntityQuery _carrierRenderableQuery;
        private EntityQuery _miningRenderableQuery;
        private EntityQuery _fallbackRenderableQuery;
        private Entity _flagship;
        private float3 _flagshipVelocityWorld;
        private UCamera _drivingCamera;
        private bool _queriesReady;
        private bool _cursorSteerAnchorActive;
        private bool _cursorSteerDeadZoneUnlocked;
        private Vector2 _cursorSteerAnchorPointer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _loggedClaim;
#endif

        private void OnEnable()
        {
            _flagship = Entity.Null;
            _flagshipVelocityWorld = float3.zero;
            _drivingCamera = GetComponent<UCamera>();
            _queriesReady = false;
            _cursorSteerAnchorActive = false;
            _cursorSteerDeadZoneUnlocked = false;
            _cursorSteerAnchorPointer = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _loggedClaim = false;
#endif
            EnsureQueries();
        }

        private void OnDisable()
        {
            if (_queriesReady &&
                _world != null &&
                _world.IsCreated &&
                IsValidTarget(_flagship) &&
                _entityManager.HasComponent<PlayerFlagshipFlightInput>(_flagship))
            {
                _entityManager.SetComponentData(_flagship, PlayerFlagshipFlightInput.Disabled);
            }

            _flagship = Entity.Null;
            _flagshipVelocityWorld = float3.zero;
            _drivingCamera = null;
            _queriesReady = false;
            _cursorSteerAnchorActive = false;
            _cursorSteerDeadZoneUnlocked = false;
            _cursorSteerAnchorPointer = Vector2.zero;
        }

        public void SnapClaimNow()
        {
            EnsureClaimedFlagship();
        }

        public bool TryGetControlledFlagship(out Entity entity)
        {
            entity = Entity.Null;
            if (!EnsureQueries())
                return false;

            if (!EnsureClaimedFlagship())
                return false;

            entity = _flagship;
            return IsValidTarget(entity);
        }

        public bool DebugCursorSteerAnchorActive => _cursorSteerAnchorActive;
        public bool DebugCursorSteerDeadZoneUnlocked => _cursorSteerDeadZoneUnlocked;
        public float DebugCursorSteerDeadZonePixels => cursorSteerDeadZonePixels;

        private void Update()
        {
            if (!EnsureQueries())
                return;

            var keyboard = Keyboard.current;
            HandleModeHotkeys(keyboard);

            if (!EnsureClaimedFlagship())
                return;

            if (Space4XControlModeState.CurrentMode == Space4XControlMode.Rts)
            {
                SuppressFlagshipMovement();
                MaintainHighlight(_flagship);
                return;
            }

            ApplyInput(keyboard);
            MaintainHighlight(_flagship);
        }

        private bool EnsureClaimedFlagship()
        {
            if (IsValidTarget(_flagship))
            {
                ApplyFlagshipVariantFromSelection(_flagship);
                return true;
            }

            _flagship = ClaimFlagshipFromSelection();
            if (IsValidTarget(_flagship))
            {
                ApplyFlightTuningFromEntity(_flagship);
                return true;
            }

            _flagship = PickNearestToCamera(_playerFlagshipQuery);
            if (IsValidTarget(_flagship))
            {
                ApplyFlightTuningFromEntity(_flagship);
                ApplyFlagshipVariantFromSelection(_flagship);
                return true;
            }

            return false;
        }

        private Entity ClaimFlagshipFromSelection()
        {
            var preferCarrier = PreferCarrierSelection();

            var candidate = preferCarrier
                ? PickNearestToCamera(_carrierRenderableQuery)
                : PickNearestToCamera(_miningRenderableQuery);

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCamera(_carrierAnyQuery)
                    : PickNearestToCamera(_miningAnyQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCamera(_miningRenderableQuery)
                    : PickNearestToCamera(_carrierRenderableQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCamera(_miningAnyQuery)
                    : PickNearestToCamera(_carrierAnyQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = PickNearestToCamera(_fallbackRenderableQuery);
            }

            if (candidate == Entity.Null)
                return Entity.Null;

            ClearOtherFlagshipTags(candidate);

            if (!_entityManager.HasComponent<PlayerFlagshipTag>(candidate))
            {
                _entityManager.AddComponent<PlayerFlagshipTag>(candidate);
            }

            if (!_entityManager.HasComponent<MovementSuppressed>(candidate))
            {
                _entityManager.AddComponent<MovementSuppressed>(candidate);
            }

            if (_entityManager.HasComponent<MovementSuppressed>(candidate))
            {
                _entityManager.SetComponentEnabled<MovementSuppressed>(candidate, true);
            }

            if (_entityManager.HasComponent<VesselMovement>(candidate))
            {
                var movement = _entityManager.GetComponentData<VesselMovement>(candidate);
                movement.Velocity = float3.zero;
                movement.CurrentSpeed = 0f;
                movement.IsMoving = 0;
                _entityManager.SetComponentData(candidate, movement);
            }

            DetachFromAmbientOrbit(candidate);
            ApplyFlightTuningFromEntity(candidate);
            ApplyFlagshipVariantFromSelection(candidate);
            _flagshipVelocityWorld = float3.zero;

            MaintainHighlight(candidate);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedClaim)
            {
                var hasCarrier = _entityManager.HasComponent<Carrier>(candidate);
                var hasMiningVessel = _entityManager.HasComponent<MiningVessel>(candidate);
                var hasMaterialMesh = _entityManager.HasComponent<MaterialMeshInfo>(candidate);
                UnityEngine.Debug.Log($"[Space4XPlayerFlagshipController] Claimed={candidate} HasCarrier={hasCarrier} HasMiningVessel={hasMiningVessel} HasMaterialMeshInfo={hasMaterialMesh} Preset='{Space4XRunStartSelection.ShipPresetId}'");
                _loggedClaim = true;
            }
#endif

            return candidate;
        }

        private void DetachFromAmbientOrbit(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            if (_entityManager.HasComponent<Space4XOrbitAnchor>(entity))
            {
                _entityManager.RemoveComponent<Space4XOrbitAnchor>(entity);
            }

            if (_entityManager.HasComponent<Space4XOrbitAnchorState>(entity))
            {
                _entityManager.RemoveComponent<Space4XOrbitAnchorState>(entity);
            }

            if (_entityManager.HasComponent<Space4XMicroImpulseTag>(entity))
            {
                _entityManager.RemoveComponent<Space4XMicroImpulseTag>(entity);
            }

            if (!_entityManager.HasComponent<Space4XRogueOrbitTag>(entity))
            {
                _entityManager.AddComponent<Space4XRogueOrbitTag>(entity);
            }
        }

        private void ApplyInput(Keyboard keyboard)
        {
            if (!IsValidTarget(_flagship))
                return;

            if (keyboard == null)
                return;

            var profile = ResolveFlightProfile(_flagship);
            _ = ResolveFlightRuntimeState(_flagship, profile);
            var input = ResolveFlightInputIntent(_flagship);
            input.MovementEnabled = 1;

            var forwardInput = 0f;
            var strafeInput = 0f;
            var verticalInput = 0f;
            var rollInput = 0f;

            if (keyboard.wKey.isPressed) forwardInput += 1f;
            if (keyboard.sKey.isPressed) forwardInput -= 1f;
            if (keyboard.dKey.isPressed) strafeInput += 1f;
            if (keyboard.aKey.isPressed) strafeInput -= 1f;
            if (keyboard.spaceKey.isPressed) verticalInput += 1f;
            if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed) verticalInput -= 1f;
            if (keyboard.qKey.isPressed) rollInput += 1f;
            if (keyboard.eKey.isPressed) rollInput -= 1f;

            input.Forward = Mathf.Clamp(forwardInput, -1f, 1f);
            input.Strafe = Mathf.Clamp(strafeInput, -1f, 1f);
            input.Vertical = Mathf.Clamp(verticalInput, -1f, 1f);
            input.Roll = Mathf.Clamp(rollInput, -1f, 1f);
            input.TranslationBasisOverride = 0;
            input.AutoAlignToTranslation = 0;
            input.TranslationForward = new float3(0f, 0f, 1f);
            input.TranslationUp = new float3(0f, 1f, 0f);
            input.BoostPressed = keyboard.leftShiftKey.isPressed ? (byte)1 : (byte)0;
            input.RetroBrakePressed = retroBrakeKey != Key.None && keyboard[retroBrakeKey].isPressed ? (byte)1 : (byte)0;
            if (toggleDampenersKey != Key.None && keyboard[toggleDampenersKey].wasPressedThisFrame)
            {
                input.ToggleDampenersRequested = 1;
            }

            var controlMode = Space4XControlModeState.CurrentMode;
            if (controlMode == Space4XControlMode.CruiseLook &&
                TryGetCameraMovementBasis(out var translationForward, out var translationUp))
            {
                input.TranslationBasisOverride = 1;
                input.AutoAlignToTranslation = 1;
                input.TranslationForward = new float3(translationForward.x, translationForward.y, translationForward.z);
                input.TranslationUp = new float3(translationUp.x, translationUp.y, translationUp.z);
            }

            var cursorSteeringHeld = controlMode == Space4XControlMode.CursorOrient && IsCursorSteeringHeld();
            UpdateCursorSteeringAnchor(cursorSteeringHeld);
            var cursorSteeringActive = cursorSteeringHeld && IsCursorSteeringOutsideDeadZone();

            if (cursorSteeringActive &&
                TryGetCursorLookDirection(out var lookDirection, out var upDirection))
            {
                input.CursorSteeringActive = 1;
                input.CursorLookDirection = new float3(lookDirection.x, lookDirection.y, lookDirection.z);
                input.CursorUpDirection = new float3(upDirection.x, upDirection.y, upDirection.z);
            }
            else
            {
                input.CursorSteeringActive = 0;
                input.CursorLookDirection = new float3(0f, 0f, 1f);
                input.CursorUpDirection = new float3(0f, 1f, 0f);
            }

            SetFlightInputIntent(_flagship, input);
        }

        private bool IsCursorSteeringHeld()
        {
            if (!requireMouseHoldForCursorSteering)
                return true;

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
                return false;

            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            var leftHeld = cursorSteerWithLeftMouse && mouse.leftButton.isPressed;
            var rightHeld = cursorSteerWithRightMouse && mouse.rightButton.isPressed;
            return leftHeld || rightHeld;
        }

        private bool IsCursorSteeringOutsideDeadZone()
        {
            // Anchor click is neutral; steering only engages once pointer exits deadzone radius.
            if (!useClickAnchorDeadZoneSteering ||
                !requireMouseHoldForCursorSteering ||
                !_cursorSteerAnchorActive)
            {
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (_cursorSteerDeadZoneUnlocked)
            {
                return true;
            }

            var deadZone = Mathf.Max(0f, cursorSteerDeadZonePixels);
            var delta = mouse.position.ReadValue() - _cursorSteerAnchorPointer;
            if (delta.sqrMagnitude <= (deadZone * deadZone))
            {
                return false;
            }

            // Unlock on a neutral frame to avoid a directional throw when leaving deadzone.
            _cursorSteerDeadZoneUnlocked = true;
            return false;
        }

        private void UpdateCursorSteeringAnchor(bool steeringHeld)
        {
            if (!useClickAnchorDeadZoneSteering || !requireMouseHoldForCursorSteering)
            {
                _cursorSteerAnchorActive = false;
                _cursorSteerDeadZoneUnlocked = false;
                return;
            }

            var mouse = Mouse.current;
            if (!steeringHeld || mouse == null)
            {
                _cursorSteerAnchorActive = false;
                _cursorSteerDeadZoneUnlocked = false;
                return;
            }

            if (!_cursorSteerAnchorActive)
            {
                _cursorSteerAnchorPointer = mouse.position.ReadValue();
                _cursorSteerAnchorActive = true;
                _cursorSteerDeadZoneUnlocked = false;
            }
        }

        private bool TryGetCursorLookDirection(out Vector3 lookDirection, out Vector3 upDirection)
        {
            lookDirection = default;
            upDirection = Vector3.up;
            var camera = ResolveDrivingCamera();
            if (camera == null)
                return false;

            if (camera.transform.up.sqrMagnitude > 0.0001f)
            {
                upDirection = camera.transform.up.normalized;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                if (TryResolveCursorSteerPointer(pointer, out var steerPointer))
                {
                    pointer = steerPointer;
                }

                var ray = camera.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));
                if (ray.direction.sqrMagnitude > 0.0001f)
                {
                    lookDirection = ray.direction.normalized;
                    return true;
                }
            }

            if (camera.transform.forward.sqrMagnitude > 0.0001f)
            {
                lookDirection = camera.transform.forward.normalized;
                return true;
            }

            return false;
        }

        private bool TryResolveCursorSteerPointer(Vector2 pointer, out Vector2 steerPointer)
        {
            steerPointer = pointer;
            if (!useClickAnchorDeadZoneSteering ||
                !requireMouseHoldForCursorSteering ||
                !_cursorSteerAnchorActive)
            {
                return false;
            }

            if (!_cursorSteerDeadZoneUnlocked)
            {
                steerPointer = _cursorSteerAnchorPointer;
                return true;
            }

            var deadZone = Mathf.Max(0f, cursorSteerDeadZonePixels);
            var maxOffset = Mathf.Max(deadZone + 1f, cursorSteerMaxOffsetPixels);
            var delta = pointer - _cursorSteerAnchorPointer;
            var magnitude = delta.magnitude;
            if (magnitude <= deadZone || magnitude < 0.0001f)
            {
                steerPointer = _cursorSteerAnchorPointer;
                return true;
            }

            var direction = delta / magnitude;
            var mappedMagnitude = Mathf.Clamp(magnitude - deadZone, 0f, maxOffset - deadZone);
            // Map relative drag around screen center so click location does not bias steering direction.
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var mappedPointer = screenCenter + (direction * mappedMagnitude);
            var maxX = Mathf.Max(0f, Screen.width - 1f);
            var maxY = Mathf.Max(0f, Screen.height - 1f);
            steerPointer = new Vector2(
                Mathf.Clamp(mappedPointer.x, 0f, maxX),
                Mathf.Clamp(mappedPointer.y, 0f, maxY));
            return true;
        }

        private bool TryGetCameraMovementBasis(out Vector3 forward, out Vector3 up)
        {
            forward = Vector3.forward;
            up = Vector3.up;

            var camera = ResolveDrivingCamera();
            if (camera == null)
                return false;

            var cameraForward = camera.transform.forward;
            if (cameraForward.sqrMagnitude < 0.0001f)
                return false;

            cameraForward.Normalize();
            var cameraUp = camera.transform.up;
            if (cameraUp.sqrMagnitude < 0.0001f)
            {
                cameraUp = Vector3.up;
            }
            else
            {
                cameraUp.Normalize();
            }

            var right = Vector3.Cross(cameraUp, cameraForward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.Cross(Vector3.up, cameraForward);
            }

            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            var correctedUp = Vector3.Cross(cameraForward, right);
            if (correctedUp.sqrMagnitude < 0.0001f)
            {
                correctedUp = Vector3.up;
            }
            else
            {
                correctedUp.Normalize();
            }

            forward = cameraForward;
            up = correctedUp;
            return true;
        }

        private UCamera ResolveDrivingCamera()
        {
            if (_drivingCamera != null)
                return _drivingCamera;

            _drivingCamera = GetComponent<UCamera>();
            if (_drivingCamera != null)
                return _drivingCamera;

            _drivingCamera = UCamera.main;
            if (_drivingCamera != null)
                return _drivingCamera;

            _drivingCamera = UnityEngine.Object.FindFirstObjectByType<UCamera>();
            return _drivingCamera;
        }

        private void SuppressFlagshipMovement()
        {
            if (!IsValidTarget(_flagship))
                return;

            var input = ResolveFlightInputIntent(_flagship);
            input.MovementEnabled = 0;
            input.Forward = 0f;
            input.Strafe = 0f;
            input.Vertical = 0f;
            input.Roll = 0f;
            input.BoostPressed = 0;
            input.RetroBrakePressed = 0;
            input.TranslationBasisOverride = 0;
            input.AutoAlignToTranslation = 0;
            input.TranslationForward = new float3(0f, 0f, 1f);
            input.TranslationUp = new float3(0f, 1f, 0f);
            input.CursorSteeringActive = 0;
            input.CursorLookDirection = new float3(0f, 0f, 1f);
            input.CursorUpDirection = new float3(0f, 1f, 0f);
            input.ToggleDampenersRequested = 0;
            SetFlightInputIntent(_flagship, input);
            _flagshipVelocityWorld = float3.zero;
        }

        private void HandleModeHotkeys(Keyboard keyboard)
        {
            if (keyboard == null)
                return;

            if (cursorModeHotkey != Key.None && keyboard[cursorModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetMode(Space4XControlMode.CursorOrient);
            }
            else if (cruiseModeHotkey != Key.None && keyboard[cruiseModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetMode(Space4XControlMode.CruiseLook);
            }
            else if (rtsModeHotkey != Key.None && keyboard[rtsModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetMode(Space4XControlMode.Rts);
            }
        }

        private void MaintainHighlight(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            if (!_entityManager.HasComponent<RenderTint>(entity))
                return;

            var tint = _entityManager.GetComponentData<RenderTint>(entity);
            tint.Value = new float4(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a);
            _entityManager.SetComponentData(entity, tint);
        }

        private bool EnsureQueries()
        {
            if (_queriesReady && _world != null && _world.IsCreated)
                return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
                return false;

            _entityManager = _world.EntityManager;
            _playerFlagshipQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PlayerFlagshipTag>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });
            _carrierAnyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });
            _miningAnyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });
            _carrierRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _miningRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _fallbackRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });

            _queriesReady = true;
            return true;
        }

        private bool IsValidTarget(Entity entity)
        {
            return entity != Entity.Null
                   && _entityManager.Exists(entity)
                   && _entityManager.HasComponent<LocalTransform>(entity);
        }

        private Entity PickNearestToCamera(EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
                return Entity.Null;

            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return Entity.Null;

            var cameraPosition = transform.position;

            var bestDistanceSq = float.MaxValue;
            var bestEntity = Entity.Null;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!_entityManager.HasComponent<LocalToWorld>(entity))
                    continue;

                var ltw = _entityManager.GetComponentData<LocalToWorld>(entity);
                var worldPos = new Vector3(ltw.Position.x, ltw.Position.y, ltw.Position.z);
                var distanceSq = (worldPos - cameraPosition).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestEntity = entity;
                }
            }

            return bestEntity;
        }

        private static bool PreferCarrierSelection()
        {
            var presetId = Space4XRunStartSelection.ShipPresetId;
            if (string.IsNullOrWhiteSpace(presetId))
                return true;

            if (presetId.Contains("square", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("carrier", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (presetId.Contains("sphere", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("capsule", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("interceptor", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("frigate", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void ApplyFlightTuningFromEntity(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            var profile = Space4XRunStartSelection.FlightProfile;
            if (!profile.IsConfigured)
            {
                profile = BuildProfileFromCurrentFields();
            }

            if (inheritMovementFromClaimedVessel && _entityManager.HasComponent<VesselMovement>(entity))
            {
                var movement = _entityManager.GetComponentData<VesselMovement>(entity);
                var baseSpeed = Mathf.Clamp(movement.BaseSpeed, minInheritedSpeed, maxInheritedSpeed);
                var baseAcceleration = movement.Acceleration > 0f
                    ? movement.Acceleration
                    : Mathf.Max(minInheritedAcceleration, baseSpeed * 2f);
                var baseDeceleration = movement.Deceleration > 0f
                    ? movement.Deceleration
                    : Mathf.Max(minInheritedAcceleration, baseSpeed * 2.5f);

                var reverseRatio = Mathf.Max(0f, reverseSpeedFromVesselMultiplier);
                var strafeRatio = Mathf.Max(0f, strafeSpeedFromVesselMultiplier);
                var verticalRatio = Mathf.Max(0f, verticalSpeedFromVesselMultiplier);

                if (_entityManager.HasComponent<VesselMobilityProfile>(entity))
                {
                    var mobility = _entityManager.GetComponentData<VesselMobilityProfile>(entity);
                    reverseRatio = Mathf.Max(reverseRatio, Mathf.Max(0f, mobility.ReverseSpeedMultiplier));
                    strafeRatio = Mathf.Max(strafeRatio, Mathf.Max(0f, mobility.StrafeSpeedMultiplier));
                    if (mobility.ThrustMode == VesselThrustMode.ForwardOnly)
                    {
                        strafeRatio = Mathf.Min(strafeRatio, 0.25f);
                        verticalRatio = Mathf.Min(verticalRatio, 0.25f);
                    }
                }

                profile.MaxForwardSpeed = Mathf.Clamp(baseSpeed * Mathf.Max(0.01f, forwardSpeedFromVesselMultiplier), minInheritedSpeed, maxInheritedSpeed);
                profile.MaxReverseSpeed = Mathf.Clamp(baseSpeed * reverseRatio, minInheritedSpeed * 0.5f, maxInheritedSpeed);
                profile.MaxStrafeSpeed = Mathf.Clamp(baseSpeed * strafeRatio, minInheritedSpeed * 0.5f, maxInheritedSpeed);
                profile.MaxVerticalSpeed = Mathf.Clamp(baseSpeed * verticalRatio, minInheritedSpeed * 0.5f, maxInheritedSpeed);

                profile.ForwardAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, forwardAccelFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
                profile.ReverseAcceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, reverseAccelFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
                profile.StrafeAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, strafeAccelFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
                profile.VerticalAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, verticalAccelFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
                profile.DampenerDeceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, dampenerFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
                profile.RetroBrakeAcceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, retroBrakeFromVesselMultiplier), minInheritedAcceleration, maxInheritedAcceleration);
            }

            profile = profile.Sanitized();
            UpsertFlightProfile(entity, profile);
            ApplyFlagshipVariantFromSelection(entity);

            var runtimeState = ResolveFlightRuntimeState(entity, profile);
            runtimeState.VelocityWorld = float3.zero;
            SetFlightRuntimeState(entity, runtimeState);
            ResolveFlightInputIntent(entity);

            // Keep legacy inspector fields aligned for debugging while profile data owns runtime behavior.
            moveSpeed = profile.MaxForwardSpeed;
            maxReverseSpeed = profile.MaxReverseSpeed;
            maxStrafeSpeed = profile.MaxStrafeSpeed;
            verticalSpeed = profile.MaxVerticalSpeed;
            forwardThrustAcceleration = profile.ForwardAcceleration;
            reverseThrustAcceleration = profile.ReverseAcceleration;
            strafeThrustAcceleration = profile.StrafeAcceleration;
            verticalThrustAcceleration = profile.VerticalAcceleration;
            boostMultiplier = profile.BoostMultiplier;
            passiveDriftDrag = profile.PassiveDriftDrag;
            dampenerDeceleration = profile.DampenerDeceleration;
            retroBrakeAcceleration = profile.RetroBrakeAcceleration;
            rollSpeedDegrees = profile.RollSpeedDegrees;
            cursorTurnSharpness = profile.CursorTurnSharpness;
            maxCursorPitchDegrees = profile.MaxCursorPitchDegrees;
            inertialDampeners = runtimeState.InertialDampenersEnabled != 0;
        }

        private void ApplyFlagshipVariantFromSelection(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            var variantIndex = ResolveFlagshipVariantIndexFromPreset();
            if (_entityManager.HasComponent<RenderVariantOverride>(entity))
            {
                _entityManager.SetComponentData(entity, new RenderVariantOverride { Value = variantIndex });
            }
            else
            {
                _entityManager.AddComponentData(entity, new RenderVariantOverride { Value = variantIndex });
            }

            _entityManager.SetComponentEnabled<RenderVariantOverride>(entity, true);
        }

        private static int ResolveFlagshipVariantIndexFromPreset()
        {
            const int squareVariant = 0;
            const int capsuleVariant = 1;
            const int sphereVariant = 2;

            var presetId = Space4XRunStartSelection.ShipPresetId;
            if (string.IsNullOrWhiteSpace(presetId))
                return squareVariant;

            if (presetId.Contains("sphere", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("frigate", StringComparison.OrdinalIgnoreCase))
            {
                return sphereVariant;
            }

            if (presetId.Contains("capsule", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("interceptor", StringComparison.OrdinalIgnoreCase) ||
                presetId.Contains("cylinder", StringComparison.OrdinalIgnoreCase))
            {
                return capsuleVariant;
            }

            return squareVariant;
        }

        private ShipFlightProfile ResolveFlightProfile(Entity entity)
        {
            if (_entityManager.HasComponent<ShipFlightProfile>(entity))
            {
                var profile = _entityManager.GetComponentData<ShipFlightProfile>(entity).Sanitized();
                _entityManager.SetComponentData(entity, profile);
                return profile;
            }

            var fallback = Space4XRunStartSelection.FlightProfile;
            if (!fallback.IsConfigured)
            {
                fallback = BuildProfileFromCurrentFields();
            }

            fallback = fallback.Sanitized();
            UpsertFlightProfile(entity, fallback);
            return fallback;
        }

        private ShipFlightRuntimeState ResolveFlightRuntimeState(Entity entity, in ShipFlightProfile profile)
        {
            if (_entityManager.HasComponent<ShipFlightRuntimeState>(entity))
            {
                var runtime = _entityManager.GetComponentData<ShipFlightRuntimeState>(entity);
                runtime.InertialDampenersEnabled = runtime.InertialDampenersEnabled != 0 ? (byte)1 : (byte)0;
                return runtime;
            }

            var created = new ShipFlightRuntimeState
            {
                VelocityWorld = _flagshipVelocityWorld,
                InertialDampenersEnabled = profile.DefaultInertialDampenersEnabled != 0 ? (byte)1 : (byte)0
            };
            _entityManager.AddComponentData(entity, created);
            return created;
        }

        private void SetFlightRuntimeState(Entity entity, in ShipFlightRuntimeState runtimeState)
        {
            if (_entityManager.HasComponent<ShipFlightRuntimeState>(entity))
            {
                _entityManager.SetComponentData(entity, runtimeState);
                return;
            }

            _entityManager.AddComponentData(entity, runtimeState);
        }

        private PlayerFlagshipFlightInput ResolveFlightInputIntent(Entity entity)
        {
            if (_entityManager.HasComponent<PlayerFlagshipFlightInput>(entity))
            {
                return _entityManager.GetComponentData<PlayerFlagshipFlightInput>(entity);
            }

            var created = PlayerFlagshipFlightInput.Disabled;
            _entityManager.AddComponentData(entity, created);
            return created;
        }

        private void SetFlightInputIntent(Entity entity, in PlayerFlagshipFlightInput input)
        {
            if (_entityManager.HasComponent<PlayerFlagshipFlightInput>(entity))
            {
                _entityManager.SetComponentData(entity, input);
                return;
            }

            _entityManager.AddComponentData(entity, input);
        }

        private void UpsertFlightProfile(Entity entity, in ShipFlightProfile profile)
        {
            if (_entityManager.HasComponent<ShipFlightProfile>(entity))
            {
                _entityManager.SetComponentData(entity, profile);
                return;
            }

            _entityManager.AddComponentData(entity, profile);
        }

        private ShipFlightProfile BuildProfileFromCurrentFields()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = moveSpeed,
                MaxReverseSpeed = maxReverseSpeed,
                MaxStrafeSpeed = maxStrafeSpeed,
                MaxVerticalSpeed = verticalSpeed,
                ForwardAcceleration = forwardThrustAcceleration,
                ReverseAcceleration = reverseThrustAcceleration,
                StrafeAcceleration = strafeThrustAcceleration,
                VerticalAcceleration = verticalThrustAcceleration,
                BoostMultiplier = boostMultiplier,
                PassiveDriftDrag = passiveDriftDrag,
                DampenerDeceleration = dampenerDeceleration,
                RetroBrakeAcceleration = retroBrakeAcceleration,
                RollSpeedDegrees = rollSpeedDegrees,
                CursorTurnSharpness = cursorTurnSharpness,
                MaxCursorPitchDegrees = maxCursorPitchDegrees,
                DefaultInertialDampenersEnabled = inertialDampeners ? (byte)1 : (byte)0
            }.Sanitized();
        }

        private void ClearOtherFlagshipTags(Entity keepEntity)
        {
            if (_playerFlagshipQuery.IsEmptyIgnoreFilter)
                return;

            using var tagged = _playerFlagshipQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < tagged.Length; i++)
            {
                var entity = tagged[i];
                if (entity == keepEntity || !_entityManager.Exists(entity))
                    continue;

                _entityManager.RemoveComponent<PlayerFlagshipTag>(entity);
                if (_entityManager.HasComponent<MovementSuppressed>(entity))
                {
                    _entityManager.SetComponentEnabled<MovementSuppressed>(entity, false);
                }
            }
        }
    }
}

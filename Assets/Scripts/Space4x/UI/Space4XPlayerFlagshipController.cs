using System;
using PureDOTS.Input;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Spatial;
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
using UTime = UnityEngine.Time;

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
        [SerializeField] private float minInheritedSpeed = 0.5f;
        [SerializeField] private float maxInheritedSpeed = 220f;
        [SerializeField] private float minInheritedAcceleration = 0.1f;
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
        [SerializeField] private bool cursorSteerTowardCameraFacing = true;
        [SerializeField] private bool cursorSteerWithLeftMouse = true;
        [SerializeField] private bool cursorSteerWithRightMouse = true;
        [SerializeField] private bool useClickAnchorDeadZoneSteering = true;
        [SerializeField] private float cursorSteerDeadZonePixels = 22f;
        [SerializeField] private float cursorSteerMaxOffsetPixels = 320f;
        [SerializeField] private bool lockCursorInMode1Fighter = true;

        [Header("Prototype Abilities")]
        [SerializeField] private Key cycleAbilityKey = Key.None;
        [SerializeField] private float shiftTapWindowSeconds = 0.35f;
        [SerializeField] private float timeshipStopDurationSeconds = 2.5f;
        [SerializeField] private float timeshipSlowDurationSeconds = 5f;
        [SerializeField] private float timeshipSlowTimeScale = 0.25f;
        [SerializeField] private float timeshipCooldownSeconds = 8f;
        [SerializeField] private bool timeshipAllowSlowFallback = true;
        [SerializeField] private bool timeshipGlobalStop = true;
        [SerializeField] private float skipshipMinRange = 18f;
        [SerializeField] private float skipshipMaxRange = 68f;
        [SerializeField] private float skipshipCooldownSeconds = 2.5f;

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
        private Space4XFollowPlayerVessel _followPlayerVessel;
        private bool _queriesReady;
        private bool _cursorSteerAnchorActive;
        private bool _cursorSteerDeadZoneUnlocked;
        private Vector2 _cursorSteerAnchorPointer;
        private bool _fighterCursorLockOwned;
        private bool _cursorVisibleBeforeFighter;
        private CursorLockMode _cursorLockBeforeFighter;
        private Entity _rtsAutoSelectedFlagship;
        private bool _shiftPressActive;
        private float _shiftPressStartTime;
        private bool _shiftHoldActivated;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _loggedClaim;
#endif

        private void OnEnable()
        {
            _flagship = Entity.Null;
            _flagshipVelocityWorld = float3.zero;
            _drivingCamera = GetComponent<UCamera>();
            _followPlayerVessel = GetComponent<Space4XFollowPlayerVessel>();
            _queriesReady = false;
            _cursorSteerAnchorActive = false;
            _cursorSteerDeadZoneUnlocked = false;
            _cursorSteerAnchorPointer = Vector2.zero;
            _fighterCursorLockOwned = false;
            _cursorVisibleBeforeFighter = true;
            _cursorLockBeforeFighter = CursorLockMode.None;
            _rtsAutoSelectedFlagship = Entity.Null;
            _shiftPressActive = false;
            _shiftPressStartTime = 0f;
            _shiftHoldActivated = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _loggedClaim = false;
#endif
            EnsureQueries();
        }

        private void OnDisable()
        {
            RestoreFighterCursorStateIfNeeded();
            if (_queriesReady &&
                _world != null &&
                _world.IsCreated &&
                IsValidTarget(_flagship) &&
                _entityManager.HasComponent<PlayerFlagshipFlightInput>(_flagship))
            {
                _entityManager.SetComponentData(_flagship, PlayerFlagshipFlightInput.Disabled);
            }

            if (_queriesReady &&
                _world != null &&
                _world.IsCreated &&
                IsValidTarget(_flagship) &&
                _entityManager.HasComponent<MovementSuppressed>(_flagship))
            {
                _entityManager.SetComponentEnabled<MovementSuppressed>(_flagship, true);
            }

            _flagship = Entity.Null;
            _flagshipVelocityWorld = float3.zero;
            _drivingCamera = null;
            _followPlayerVessel = null;
            _queriesReady = false;
            _cursorSteerAnchorActive = false;
            _cursorSteerDeadZoneUnlocked = false;
            _cursorSteerAnchorPointer = Vector2.zero;
            _fighterCursorLockOwned = false;
            _rtsAutoSelectedFlagship = Entity.Null;
            _shiftPressActive = false;
            _shiftHoldActivated = false;
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
            if (ShouldHandleModeHotkeys())
            {
                HandleModeHotkeys(keyboard);
            }
            UpdateFighterCursorState();

            if (!EnsureClaimedFlagship())
                return;

            if (Space4XControlModeState.CurrentMode == Space4XControlMode.Rts)
            {
                PrepareFlagshipForRtsOrders();
                SuppressFlagshipMovement();
                MaintainHighlight(_flagship);
                return;
            }

            PrepareFlagshipForManualFlight();
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
            var translationForward = new float3(0f, 0f, 1f);
            var translationUp = new float3(0f, 1f, 0f);
            var translationOverride = false;
            var shiftHeld = keyboard.leftShiftKey.isPressed;
            var shiftPressed = keyboard.leftShiftKey.wasPressedThisFrame;
            var shiftReleased = keyboard.leftShiftKey.wasReleasedThisFrame;
            if (shiftPressed)
            {
                _shiftPressActive = true;
                _shiftPressStartTime = UTime.unscaledTime;
                _shiftHoldActivated = false;
            }

            if (cycleAbilityKey != Key.None && keyboard[cycleAbilityKey].wasPressedThisFrame)
            {
                TryCycleAbilitySelection();
            }
            else if (shiftReleased && _shiftPressActive && !_shiftHoldActivated)
            {
                TryCycleAbilitySelection();
            }
            if (shiftReleased)
            {
                _shiftPressActive = false;
                _shiftHoldActivated = false;
            }

            var shiftAbility = ResolveShiftAbility(_flagship);
            var shiftTapWindow = Mathf.Max(0.05f, shiftTapWindowSeconds);
            var shiftHeldDuration = _shiftPressActive ? UTime.unscaledTime - _shiftPressStartTime : 0f;
            var shiftHoldReady = shiftHeld && _shiftPressActive && shiftHeldDuration >= shiftTapWindow;
            var shiftHoldActivatedThisFrame = shiftHoldReady && !_shiftHoldActivated;
            if (shiftHoldActivatedThisFrame)
            {
                _shiftHoldActivated = true;
            }

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
            input.TranslationForward = translationForward;
            input.TranslationUp = translationUp;
            input.BoostPressed = shiftAbility == ShipAbilityKind.BoostDrive && shiftHoldReady ? (byte)1 : (byte)0;
            input.RetroBrakePressed = retroBrakeKey != Key.None && keyboard[retroBrakeKey].isPressed ? (byte)1 : (byte)0;
            if (toggleDampenersKey != Key.None && keyboard[toggleDampenersKey].wasPressedThisFrame)
            {
                input.ToggleDampenersRequested = 1;
            }

            var controlMode = Space4XControlModeState.CurrentMode;
            var fighterModeActive =
                controlMode == Space4XControlMode.CursorOrient &&
                Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient);
            var cruiseHeadingHoldEnabled =
                controlMode == Space4XControlMode.CruiseLook &&
                Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CruiseLook);
            if (controlMode == Space4XControlMode.CruiseLook &&
                TryGetCameraMovementBasis(out var cameraForward, out var cameraUp))
            {
                translationOverride = true;
                translationForward = new float3(cameraForward.x, cameraForward.y, cameraForward.z);
                translationUp = new float3(cameraUp.x, cameraUp.y, cameraUp.z);
                input.TranslationBasisOverride = 1;
                // Mode 2 always uses camera-relative translation.
                // Variant toggle disables course correction while preserving heading.
                input.AutoAlignToTranslation = cruiseHeadingHoldEnabled ? (byte)0 : (byte)1;
                input.TranslationForward = translationForward;
                input.TranslationUp = translationUp;
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
            input.FighterSteeringMode = fighterModeActive ? (byte)1 : (byte)0;

            if (shiftHoldActivatedThisFrame)
            {
                switch (shiftAbility)
                {
                    case ShipAbilityKind.SkipDrive:
                        TryQueueSkipJump(forwardInput, strafeInput, translationOverride, translationForward, translationUp);
                        break;
                    case ShipAbilityKind.TimeCore:
                        TryQueueTimeStop();
                        break;
                }
            }

            SetFlightInputIntent(_flagship, input);
        }

        private bool IsCursorSteeringHeld()
        {
            if (!IsCursorHoldGateEnabled())
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
            if (ShouldUseCameraFacingCursorSteering())
            {
                return true;
            }

            // Anchor click is neutral; steering only engages once pointer exits deadzone radius.
            if (!useClickAnchorDeadZoneSteering ||
                !IsCursorHoldGateEnabled() ||
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
            if (!useClickAnchorDeadZoneSteering || !IsCursorHoldGateEnabled())
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

            if (ShouldUseCameraFacingCursorSteering() &&
                camera.transform.forward.sqrMagnitude > 0.0001f)
            {
                lookDirection = camera.transform.forward.normalized;
                return true;
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
                !IsCursorHoldGateEnabled() ||
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

        private bool IsCursorHoldGateEnabled()
        {
            if (!requireMouseHoldForCursorSteering)
            {
                return false;
            }

            // Mode 1 default and fighter variant are always-on steering.
            return false;
        }

        private bool ShouldUseCameraFacingCursorSteering()
        {
            if (Space4XControlModeState.CurrentMode != Space4XControlMode.CursorOrient ||
                !cursorSteerTowardCameraFacing)
            {
                return false;
            }

            if (Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient))
            {
                // Fighter mode ignores world cursor ray and steers directly by camera facing.
                return true;
            }

            if (_followPlayerVessel == null)
            {
                _followPlayerVessel = GetComponent<Space4XFollowPlayerVessel>();
            }

            // Only force camera-facing steering when mode 1 camera is running in independent orbit mode.
            return _followPlayerVessel == null || _followPlayerVessel.CursorModeUsesIndependentCamera;
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
            input.FighterSteeringMode = 0;
            input.ToggleDampenersRequested = 0;
            SetFlightInputIntent(_flagship, input);
            _flagshipVelocityWorld = float3.zero;
        }

        private void PrepareFlagshipForRtsOrders()
        {
            if (!IsValidTarget(_flagship))
            {
                _rtsAutoSelectedFlagship = Entity.Null;
                return;
            }

            if (_entityManager.HasComponent<MovementSuppressed>(_flagship))
            {
                _entityManager.SetComponentEnabled<MovementSuppressed>(_flagship, false);
            }

            if (!_entityManager.HasComponent<SelectableTag>(_flagship))
            {
                _entityManager.AddComponent<SelectableTag>(_flagship);
            }

            if (_entityManager.HasComponent<SelectionOwner>(_flagship))
            {
                var owner = _entityManager.GetComponentData<SelectionOwner>(_flagship);
                if (owner.PlayerId != 0)
                {
                    owner.PlayerId = 0;
                    _entityManager.SetComponentData(_flagship, owner);
                }
            }
            else
            {
                _entityManager.AddComponentData(_flagship, new SelectionOwner { PlayerId = 0 });
            }

            // Auto-select once when entering RTS for immediate command usability.
            if (_rtsAutoSelectedFlagship != _flagship)
            {
                SeedRtsMomentumFromManualFlight();

                if (!_entityManager.HasComponent<SelectedTag>(_flagship))
                {
                    _entityManager.AddComponent<SelectedTag>(_flagship);
                }

                _rtsAutoSelectedFlagship = _flagship;
            }
            else
            {
                // Keep flagship commandable if selection was cleared and nothing else is selected.
                using var selectedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SelectedTag>());
                if (selectedQuery.IsEmptyIgnoreFilter && !_entityManager.HasComponent<SelectedTag>(_flagship))
                {
                    _entityManager.AddComponent<SelectedTag>(_flagship);
                }
            }

            ApplyRtsDefaultHoldBehavior();
        }

        private void PrepareFlagshipForManualFlight()
        {
            _rtsAutoSelectedFlagship = Entity.Null;
            if (!IsValidTarget(_flagship))
            {
                return;
            }

            if (_entityManager.HasComponent<MovementSuppressed>(_flagship))
            {
                _entityManager.SetComponentEnabled<MovementSuppressed>(_flagship, true);
            }
        }

        private void SeedRtsMomentumFromManualFlight()
        {
            if (!IsValidTarget(_flagship))
            {
                return;
            }

            if (!_entityManager.HasComponent<ShipFlightRuntimeState>(_flagship) ||
                !_entityManager.HasComponent<VesselMovement>(_flagship))
            {
                return;
            }

            var runtime = _entityManager.GetComponentData<ShipFlightRuntimeState>(_flagship);
            var movement = _entityManager.GetComponentData<VesselMovement>(_flagship);
            var seedVelocity = runtime.VelocityWorld;
            if (math.lengthsq(seedVelocity) <= 1e-6f)
            {
                // Fallback when runtime velocity was cleared during a mode edge.
                seedVelocity = movement.Velocity;
            }

            movement.Velocity = seedVelocity;
            movement.CurrentSpeed = math.length(seedVelocity);
            movement.IsMoving = movement.CurrentSpeed > 0.001f ? (byte)1 : (byte)0;
            _entityManager.SetComponentData(_flagship, movement);
        }

        private void ApplyRtsDefaultHoldBehavior()
        {
            if (!IsValidTarget(_flagship))
            {
                return;
            }

            if (HasPendingRtsOrders(_flagship) || _entityManager.HasComponent<AttackMoveIntent>(_flagship))
            {
                return;
            }

            var holdPosition = _entityManager.HasComponent<LocalTransform>(_flagship)
                ? _entityManager.GetComponentData<LocalTransform>(_flagship).Position
                : float3.zero;

            if (_entityManager.HasComponent<EntityIntent>(_flagship))
            {
                var intent = _entityManager.GetComponentData<EntityIntent>(_flagship);
                intent.Mode = IntentMode.MoveTo;
                intent.TargetEntity = Entity.Null;
                intent.TargetPosition = holdPosition;
                intent.TriggeringInterrupt = InterruptType.None;
                intent.Priority = InterruptPriority.Normal;
                intent.IsValid = 1;
                _entityManager.SetComponentData(_flagship, intent);
            }

            if (_entityManager.HasComponent<VesselAIState>(_flagship))
            {
                var aiState = _entityManager.GetComponentData<VesselAIState>(_flagship);
                // Model hold-position as an explicit world-space move directive so movement stays module-driven
                // instead of falling into no-target hard stop.
                aiState.CurrentGoal = VesselAIState.Goal.Patrol;
                aiState.CurrentState = VesselAIState.State.MovingToTarget;
                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = holdPosition;
                aiState.StateTimer = 0f;
                _entityManager.SetComponentData(_flagship, aiState);
            }

            if (_entityManager.HasComponent<MovementCommand>(_flagship))
            {
                var command = _entityManager.GetComponentData<MovementCommand>(_flagship);
                command.TargetPosition = holdPosition;
                command.ArrivalThreshold = 1f;
                _entityManager.SetComponentData(_flagship, command);
            }
        }

        private bool HasPendingRtsOrders(Entity entity)
        {
            return _entityManager.HasBuffer<OrderQueueElement>(entity) &&
                   _entityManager.GetBuffer<OrderQueueElement>(entity).Length > 0;
        }

        private void UpdateFighterCursorState()
        {
            var fighterActive = lockCursorInMode1Fighter &&
                Space4XControlModeState.CurrentMode == Space4XControlMode.CursorOrient &&
                Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient);
            if (fighterActive)
            {
                if (!_fighterCursorLockOwned)
                {
                    _cursorVisibleBeforeFighter = Cursor.visible;
                    _cursorLockBeforeFighter = Cursor.lockState;
                    _fighterCursorLockOwned = true;
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            RestoreFighterCursorStateIfNeeded();
        }

        private void RestoreFighterCursorStateIfNeeded()
        {
            if (!_fighterCursorLockOwned)
            {
                return;
            }

            Cursor.lockState = _cursorLockBeforeFighter;
            Cursor.visible = _cursorVisibleBeforeFighter;
            _fighterCursorLockOwned = false;
        }

        private void HandleModeHotkeys(Keyboard keyboard)
        {
            if (keyboard == null)
                return;

            if (cursorModeHotkey != Key.None && keyboard[cursorModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetModeOrToggleVariant(Space4XControlMode.CursorOrient);
            }
            else if (cruiseModeHotkey != Key.None && keyboard[cruiseModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetModeOrToggleVariant(Space4XControlMode.CruiseLook);
            }
            else if (rtsModeHotkey != Key.None && keyboard[rtsModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetModeOrToggleVariant(Space4XControlMode.Rts);
            }
        }

        private bool ShouldHandleModeHotkeys()
        {
            if (_followPlayerVessel == null)
            {
                _followPlayerVessel = GetComponent<Space4XFollowPlayerVessel>();
            }

            // Follow camera owns mode hotkeys when present to avoid duplicate 1/2/3 processing.
            return _followPlayerVessel == null || !_followPlayerVessel.isActiveAndEnabled;
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
                var minSpeedFloor = Mathf.Min(minInheritedSpeed, 0.1f);
                var minAccelFloor = Mathf.Min(minInheritedAcceleration, 0.05f);
                var baseSpeed = Mathf.Clamp(movement.BaseSpeed, minSpeedFloor, maxInheritedSpeed);
                var baseAcceleration = movement.Acceleration > 0f
                    ? movement.Acceleration
                    : Mathf.Max(minAccelFloor, baseSpeed * 0.5f);
                var baseDeceleration = movement.Deceleration > 0f
                    ? movement.Deceleration
                    : Mathf.Max(minAccelFloor, baseSpeed * 0.8f);

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

                profile.MaxForwardSpeed = Mathf.Clamp(baseSpeed * Mathf.Max(0.01f, forwardSpeedFromVesselMultiplier), minSpeedFloor, maxInheritedSpeed);
                profile.MaxReverseSpeed = Mathf.Clamp(baseSpeed * reverseRatio, minSpeedFloor * 0.5f, maxInheritedSpeed);
                profile.MaxStrafeSpeed = Mathf.Clamp(baseSpeed * strafeRatio, minSpeedFloor * 0.5f, maxInheritedSpeed);
                profile.MaxVerticalSpeed = Mathf.Clamp(baseSpeed * verticalRatio, minSpeedFloor * 0.5f, maxInheritedSpeed);

                profile.ForwardAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, forwardAccelFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
                profile.ReverseAcceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, reverseAccelFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
                profile.StrafeAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, strafeAccelFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
                profile.VerticalAcceleration = Mathf.Clamp(baseAcceleration * Mathf.Max(0.01f, verticalAccelFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
                profile.DampenerDeceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, dampenerFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
                profile.RetroBrakeAcceleration = Mathf.Clamp(baseDeceleration * Mathf.Max(0.01f, retroBrakeFromVesselMultiplier), minAccelFloor, maxInheritedAcceleration);
            }

            profile = profile.Sanitized();
            UpsertFlightProfile(entity, profile);
            ApplyFlagshipVariantFromSelection(entity);

            var runtimeState = ResolveFlightRuntimeState(entity, profile);
            runtimeState.VelocityWorld = float3.zero;
            runtimeState.AngularSpeedRadians = 0f;
            runtimeState.ForwardThrottle = 0f;
            runtimeState.StrafeThrottle = 0f;
            runtimeState.VerticalThrottle = 0f;
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

            ApplyAbilityModulesFromSelection(entity);
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

        private void ApplyAbilityModulesFromSelection(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            var isSkipShip = IsSkipShipPreset();
            var isTimeShip = IsTimeShipPreset();
            var primaryAbility = ShipAbilityKind.BoostDrive;
            if (isTimeShip)
            {
                primaryAbility = ShipAbilityKind.TimeCore;
            }
            else if (isSkipShip)
            {
                primaryAbility = ShipAbilityKind.SkipDrive;
            }

            if (primaryAbility == ShipAbilityKind.SkipDrive)
            {
                var config = new SkipDriveModuleConfig
                {
                    MinRange = Mathf.Max(0f, skipshipMinRange),
                    MaxRange = Mathf.Max(skipshipMaxRange, skipshipMinRange),
                    CooldownSeconds = Mathf.Max(0f, skipshipCooldownSeconds),
                    ChargeTimeSeconds = 0f,
                    OriginDamageRadius = 0f,
                    OriginDamage = 0f,
                    DestinationDamageRadius = 0f,
                    DestinationDamage = 0f,
                    AllowPhaseShift = 0,
                    PhaseDurationSeconds = 0f,
                    AllowCloak = 0,
                    CloakDurationSeconds = 0f
                };

                if (_entityManager.HasComponent<SkipDriveModuleConfig>(entity))
                {
                    _entityManager.SetComponentData(entity, config);
                }
                else
                {
                    _entityManager.AddComponentData(entity, config);
                }

                if (!_entityManager.HasComponent<SkipJumpState>(entity))
                {
                    _entityManager.AddComponentData(entity, new SkipJumpState());
                }
            }
            else
            {
                if (_entityManager.HasComponent<SkipDriveModuleConfig>(entity))
                {
                    _entityManager.RemoveComponent<SkipDriveModuleConfig>(entity);
                }
                if (_entityManager.HasComponent<SkipJumpState>(entity))
                {
                    _entityManager.RemoveComponent<SkipJumpState>(entity);
                }
            }

            if (primaryAbility == ShipAbilityKind.TimeCore)
            {
                var config = new TimeCoreModuleConfig
                {
                    StopDurationSeconds = Mathf.Max(0f, timeshipStopDurationSeconds),
                    SlowDurationSeconds = Mathf.Max(0f, timeshipSlowDurationSeconds),
                    SlowTimeScale = Mathf.Clamp(timeshipSlowTimeScale, 0.01f, 1f),
                    CooldownSeconds = Mathf.Max(0f, timeshipCooldownSeconds),
                    AllowSlowFallback = timeshipAllowSlowFallback ? (byte)1 : (byte)0,
                    GlobalStop = timeshipGlobalStop ? (byte)1 : (byte)0
                };

                if (_entityManager.HasComponent<TimeCoreModuleConfig>(entity))
                {
                    _entityManager.SetComponentData(entity, config);
                }
                else
                {
                    _entityManager.AddComponentData(entity, config);
                }
            }
            else
            {
                if (_entityManager.HasComponent<TimeCoreModuleConfig>(entity))
                {
                    _entityManager.RemoveComponent<TimeCoreModuleConfig>(entity);
                }
            }

            if (primaryAbility == ShipAbilityKind.BoostDrive)
            {
                var config = new BoostDriveModuleConfig
                {
                    BoostMultiplier = Mathf.Max(1f, boostMultiplier),
                    BoostDurationSeconds = 0f,
                    CooldownSeconds = 0f,
                    EnergyCost = 0f,
                    DisablesBaseBoost = 0
                };

                if (_entityManager.HasComponent<BoostDriveModuleConfig>(entity))
                {
                    _entityManager.SetComponentData(entity, config);
                }
                else
                {
                    _entityManager.AddComponentData(entity, config);
                }
            }
            else
            {
                if (_entityManager.HasComponent<BoostDriveModuleConfig>(entity))
                {
                    _entityManager.RemoveComponent<BoostDriveModuleConfig>(entity);
                }
            }

            EnsureAbilitySelection(entity, primaryAbility);
        }

        private ShipAbilityKind ResolveShiftAbility(Entity entity)
        {
            if (!IsValidTarget(entity))
                return ShipAbilityKind.None;

            if (_entityManager.HasComponent<ShipAbilityModule>(entity))
            {
                var selection = _entityManager.GetComponentData<ShipAbilityModule>(entity).Kind;
                if (IsAbilityAvailable(entity, selection))
                    return selection;
            }

            if (IsAbilityAvailable(entity, ShipAbilityKind.TimeCore))
                return ShipAbilityKind.TimeCore;
            if (IsAbilityAvailable(entity, ShipAbilityKind.SkipDrive))
                return ShipAbilityKind.SkipDrive;
            if (IsAbilityAvailable(entity, ShipAbilityKind.BoostDrive))
                return ShipAbilityKind.BoostDrive;

            return ShipAbilityKind.None;
        }

        private void TryCycleAbilitySelection()
        {
            if (!IsValidTarget(_flagship))
                return;

            var available = new ShipAbilityKind[3];
            var availableCount = 0;

            if (IsAbilityAvailable(_flagship, ShipAbilityKind.TimeCore))
            {
                available[availableCount++] = ShipAbilityKind.TimeCore;
            }

            if (IsAbilityAvailable(_flagship, ShipAbilityKind.SkipDrive))
            {
                available[availableCount++] = ShipAbilityKind.SkipDrive;
            }

            if (IsAbilityAvailable(_flagship, ShipAbilityKind.BoostDrive))
            {
                available[availableCount++] = ShipAbilityKind.BoostDrive;
            }

            if (availableCount == 0)
                return;

            var current = ShipAbilityKind.None;
            if (_entityManager.HasComponent<ShipAbilityModule>(_flagship))
            {
                current = _entityManager.GetComponentData<ShipAbilityModule>(_flagship).Kind;
            }

            var nextIndex = 0;
            for (var i = 0; i < availableCount; i++)
            {
                if (available[i] != current)
                    continue;

                nextIndex = (i + 1) % availableCount;
                break;
            }

            var next = available[nextIndex];
            if (_entityManager.HasComponent<ShipAbilityModule>(_flagship))
            {
                _entityManager.SetComponentData(_flagship, new ShipAbilityModule { Kind = next });
            }
            else
            {
                _entityManager.AddComponentData(_flagship, new ShipAbilityModule { Kind = next });
            }
        }

        private bool IsAbilityAvailable(Entity entity, ShipAbilityKind ability)
        {
            if (!IsValidTarget(entity))
                return false;

            switch (ability)
            {
                case ShipAbilityKind.TimeCore:
                    return _entityManager.HasComponent<TimeCoreModuleConfig>(entity);
                case ShipAbilityKind.SkipDrive:
                    return _entityManager.HasComponent<SkipDriveModuleConfig>(entity);
                case ShipAbilityKind.BoostDrive:
                    if (_entityManager.HasComponent<BoostDriveModuleConfig>(entity))
                        return true;
                    return !_entityManager.HasComponent<TimeCoreModuleConfig>(entity) &&
                           !_entityManager.HasComponent<SkipDriveModuleConfig>(entity);
                default:
                    return false;
            }
        }

        private void EnsureAbilitySelection(Entity entity, ShipAbilityKind primaryAbility)
        {
            if (!IsValidTarget(entity))
                return;

            var hasTime = _entityManager.HasComponent<TimeCoreModuleConfig>(entity);
            var hasSkip = _entityManager.HasComponent<SkipDriveModuleConfig>(entity);
            var hasBoost = _entityManager.HasComponent<BoostDriveModuleConfig>(entity);

            if (!hasTime && !hasSkip && !hasBoost)
            {
                if (_entityManager.HasComponent<ShipAbilityModule>(entity))
                {
                    _entityManager.RemoveComponent<ShipAbilityModule>(entity);
                }
                return;
            }

            if (_entityManager.HasComponent<ShipAbilityModule>(entity))
            {
                var current = _entityManager.GetComponentData<ShipAbilityModule>(entity).Kind;
                if (IsAbilityAvailable(entity, current))
                {
                    return;
                }
            }

            var desired = primaryAbility;
            if (!IsAbilityAvailable(entity, desired))
            {
                desired = hasTime ? ShipAbilityKind.TimeCore
                    : hasSkip ? ShipAbilityKind.SkipDrive
                    : ShipAbilityKind.BoostDrive;
            }

            if (_entityManager.HasComponent<ShipAbilityModule>(entity))
            {
                _entityManager.SetComponentData(entity, new ShipAbilityModule { Kind = desired });
            }
            else
            {
                _entityManager.AddComponentData(entity, new ShipAbilityModule { Kind = desired });
            }
        }

        private static bool IsSkipShipPreset()
        {
            var presetId = Space4XRunStartSelection.ShipPresetId;
            return !string.IsNullOrWhiteSpace(presetId) &&
                   presetId.IndexOf("skipship", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTimeShipPreset()
        {
            var presetId = Space4XRunStartSelection.ShipPresetId;
            return !string.IsNullOrWhiteSpace(presetId) &&
                   presetId.IndexOf("timeship", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryQueueTimeStop()
        {
            if (!IsValidTarget(_flagship))
                return;

            if (_entityManager.HasComponent<TimeStopRequest>(_flagship))
                return;

            if (!_entityManager.HasComponent<TimeCoreModuleConfig>(_flagship))
                return;

            var config = _entityManager.GetComponentData<TimeCoreModuleConfig>(_flagship);
            var duration = Mathf.Max(0f, config.StopDurationSeconds);
            var mode = TimeStopMode.Stop;
            var timeScale = 0f;

            if (duration <= 0f && config.AllowSlowFallback != 0)
            {
                duration = Mathf.Max(0.01f, config.SlowDurationSeconds);
                mode = TimeStopMode.Slow;
                timeScale = Mathf.Clamp(config.SlowTimeScale, 0.01f, 1f);
            }

            if (duration <= 0f)
                return;

            _entityManager.AddComponentData(_flagship, new TimeStopRequest
            {
                Source = _flagship,
                DurationSeconds = duration,
                TimeScale = timeScale,
                Mode = mode
            });
        }

        private void TryQueueSkipJump(float forwardInput, float strafeInput, bool translationOverride, float3 translationForward, float3 translationUp)
        {
            if (!IsValidTarget(_flagship))
                return;

            if (_entityManager.HasComponent<SkipJumpRequest>(_flagship))
                return;

            if (!_entityManager.HasComponent<SkipDriveModuleConfig>(_flagship))
                return;

            var config = _entityManager.GetComponentData<SkipDriveModuleConfig>(_flagship);
            var direction = ResolveSkipDirection(forwardInput, strafeInput, translationOverride, translationForward, translationUp);
            if (math.lengthsq(direction) < 0.0001f)
            {
                direction = ResolveShipForward(_flagship);
            }

            var distance = ResolveSkipDistance(config);
            var origin = _entityManager.GetComponentData<LocalTransform>(_flagship).Position;
            var destination = origin + (direction * distance);

            _entityManager.AddComponentData(_flagship, new SkipJumpRequest
            {
                Destination = destination,
                MinRange = config.MinRange,
                MaxRange = config.MaxRange,
                ClampToRange = 1
            });
        }

        private static float ResolveSkipDistance(in SkipDriveModuleConfig config)
        {
            var maxRange = math.max(0f, config.MaxRange);
            var minRange = math.max(0f, config.MinRange);
            var distance = maxRange > 0.01f ? maxRange : math.max(1f, minRange);
            if (distance < minRange)
            {
                distance = minRange;
            }

            return distance;
        }

        private float3 ResolveSkipDirection(float forwardInput, float strafeInput, bool translationOverride, float3 translationForward, float3 translationUp)
        {
            var hasInput = math.abs(forwardInput) > 0.001f || math.abs(strafeInput) > 0.001f;
            if (!hasInput)
            {
                return ResolveShipForward(_flagship);
            }

            float3 forward;
            float3 right;

            if (translationOverride)
            {
                forward = math.normalizesafe(translationForward, new float3(0f, 0f, 1f));
                right = math.cross(math.normalizesafe(translationUp, new float3(0f, 1f, 0f)), forward);
                right = math.normalizesafe(right, new float3(1f, 0f, 0f));
            }
            else
            {
                var transform = _entityManager.GetComponentData<LocalTransform>(_flagship);
                forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
                right = math.mul(transform.Rotation, new float3(1f, 0f, 0f));
                forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
                right = math.normalizesafe(right, new float3(1f, 0f, 0f));
            }

            var direction = (forward * forwardInput) + (right * strafeInput);
            return math.normalizesafe(direction, forward);
        }

        private float3 ResolveShipForward(Entity entity)
        {
            if (!_entityManager.Exists(entity) || !_entityManager.HasComponent<LocalTransform>(entity))
            {
                return new float3(0f, 0f, 1f);
            }

            var transform = _entityManager.GetComponentData<LocalTransform>(entity);
            return math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
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
                runtime.AngularSpeedRadians = math.max(0f, runtime.AngularSpeedRadians);
                runtime.ForwardThrottle = math.clamp(runtime.ForwardThrottle, -1f, 1f);
                runtime.StrafeThrottle = math.clamp(runtime.StrafeThrottle, -1f, 1f);
                runtime.VerticalThrottle = math.clamp(runtime.VerticalThrottle, -1f, 1f);
                return runtime;
            }

            var created = new ShipFlightRuntimeState
            {
                VelocityWorld = _flagshipVelocityWorld,
                InertialDampenersEnabled = profile.DefaultInertialDampenersEnabled != 0 ? (byte)1 : (byte)0,
                AngularSpeedRadians = 0f,
                ForwardThrottle = 0f,
                StrafeThrottle = 0f,
                VerticalThrottle = 0f
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

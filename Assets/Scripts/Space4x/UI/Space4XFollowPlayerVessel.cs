using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Presentation;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.InputSystem;
using UTime = UnityEngine.Time;
using UCamera = UnityEngine.Camera;
using Space4XCameraRigController = Space4X.Camera.Space4XCameraRigController;
using CameraRigApplier = PureDOTS.Runtime.Camera.CameraRigApplier;
using FlyCameraController = Space4X.Camera.FlyCameraController;
using Space4XCameraPlaceholder = Space4X.Camera.Space4XCameraPlaceholder;
using Space4XDebugLockCamera = Space4X.DebugTools.Space4XDebugLockCamera;

namespace Space4X.UI
{
    /// <summary>
    /// Gameplay camera follow that supports Cursor-Orient, Cruise-Look, RTS, and Divine Hand camera modes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XFollowPlayerVessel : MonoBehaviour
    {
        // Orbit pitch envelopes by control mode:
        // Mode 1 (CursorOrient): full sphere (~360 total pitch travel).
        // Mode 2 (CruiseLook): half sphere (~180 total pitch travel).
        private const float CursorModePitchMinDegrees = -179.5f;
        private const float CursorModePitchMaxDegrees = 179.5f;
        private const float CruiseModePitchMinDegrees = -89.95f;
        private const float CruiseModePitchMaxDegrees = 89.95f;

        [SerializeField] private float followDistance = 26f;
        [SerializeField] private float followHeight = 10f;
        [SerializeField] private float lookHeight = 2f;
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private float behindYawOffsetDegrees = 0f;

        [Header("Follow Lock")]
        [SerializeField] private bool lockCameraToTargetOutsideRts = true;
        [SerializeField] private bool interpolateControlledFlagshipPose = false;

        [Header("Cursor Mode")]
        [SerializeField] private bool cursorModeFollowsShipHeading = true;
        [SerializeField] private bool cursorModeIndependentCamera = true;
        [SerializeField] private float cursorHeadingFollowSharpness = 10f;
        [SerializeField] private float cursorHeadingOffsetDegrees = 0f;
        [SerializeField] private float cursorManualYawOffsetLimitDegrees = 75f;

        [Header("Mode 1 Alt Offset")]
        [SerializeField] private float cursorAltPositionUnitsPerPixel = 0.03f;
        [SerializeField] private float cursorAltPositionLateralLimit = 24f;
        [SerializeField] private float cursorAltPositionVerticalLimit = 24f;

        [Header("Mode Hotkeys")]
        [SerializeField] private Key cursorModeHotkey = Key.Digit1;
        [SerializeField] private Key cruiseModeHotkey = Key.Digit2;
        [SerializeField] private Key rtsModeHotkey = Key.Digit3;
        [SerializeField] private Key divineHandModeHotkey = Key.Digit4;

        [Header("Orbit Controls")]
        [SerializeField] private float orbitYawSensitivity = 0.2f;
        [SerializeField] private float orbitPitchSensitivity = 0.18f;
        [SerializeField] private bool useOrbitClickAnchorDeadZone = true;
        [SerializeField] private float orbitClickDeadZonePixels = 20f;
        [SerializeField] private float minPitchDegrees = -179f;
        [SerializeField] private float maxPitchDegrees = 179f;
        [SerializeField] private float cruiseMinPitchDegrees = -89.95f;
        [SerializeField] private float cruiseMaxPitchDegrees = 89.95f;
        [SerializeField] private bool cruiseModeIndependentCamera = true;
        [SerializeField] private bool enableCruiseMovementRecentre = false;
        [SerializeField] private float cruiseAltDoubleTapSeconds = 0.35f;
        [SerializeField] private float cruiseMovementRecentreSharpness = 8f;
        [SerializeField] private float minFollowDistance = 6f;
        [SerializeField] private float maxFollowDistance = 52f;
        [SerializeField] private float zoomDistancePerNotch = 1.8f;
        [SerializeField] private float scrollUnitsPerNotch = 120f;
        [SerializeField] private bool centerOrbitOnTarget = true;
        [SerializeField] private float maxFollowHeightToDistanceRatio = 0.42f;
        [SerializeField] private float lookHeightCompensationFromFollowHeight = 0.35f;

        [Header("Target Framing")]
        [SerializeField] private bool autoFrameByTargetSize = true;
        [SerializeField] private bool snapBehindOnTargetAcquire = true;
        [SerializeField] private float fallbackTargetRadius = 1.5f;
        [SerializeField] private float minTargetRadius = 0.25f;
        [SerializeField] private float maxTargetRadius = 64f;
        [SerializeField] private float defaultFollowDistancePerRadius = 5.2f;
        [SerializeField] private float defaultFollowHeightPerRadius = 2.1f;
        [SerializeField] private float defaultLookHeightPerRadius = 0.55f;
        [SerializeField] private float defaultZoomScalar = 1f;
        [SerializeField] private float minZoomScalar = 0.35f;
        [SerializeField] private float maxZoomScalar = 2.5f;

        [Header("Flagship Intro Framing")]
        [SerializeField] private bool useIntroFramingByRadius = true;
        [SerializeField] private float introFollowDistancePerRadius = 4.2f;
        [SerializeField] private float introFollowHeightPerRadius = 1.3f;
        [SerializeField] private float introLookHeightPerRadius = 0.35f;
        [SerializeField] private float introZoomScalar = 1f;
        [SerializeField] private float introFallbackFollowDistance = 12f;
        [SerializeField] private float introFallbackFollowHeight = 4.5f;
        [SerializeField] private float introFallbackLookHeight = 1.2f;
        [SerializeField] private float introFollowSharpness = 14f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _playerFlagshipQuery;
        private EntityQuery _miningVesselQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _miningRenderableQuery;
        private EntityQuery _carrierRenderableQuery;
        private EntityQuery _fallbackRenderableQuery;
        private EntityQuery _timeInterpolationQuery;
        private bool _queriesReady;
        private Entity _target;
        private Entity _lastFramedTarget;
        private bool _introFramingActive;
        private float _baseFramedDistance;
        private float _zoomScalar;

        private Space4XControlMode _currentMode = Space4XControlMode.CursorOrient;
        private float _cursorWorldYawDegrees;
        private bool _cursorWorldYawInitialized;
        private float _cursorHeadingYawDegrees;
        private bool _cursorHeadingYawInitialized;
        private float _cursorManualYawOffsetDegrees;
        private float _cursorAltLateralOffsetUnits;
        private float _cursorAltVerticalOffsetUnits;
        private float _orbitPitchDegrees;
        private float _cruiseYawDegrees;
        private float _cruiseYawOffsetDegrees;
        private bool _cruiseInitialized;
        private bool _cruiseOrbitAdjustedThisFrame;
        private bool _altPressedLastFrame;
        private float _lastAltTapAt;
        private UCamera _hostCamera;
        private Space4XCameraRigController _rtsRigController;
        private CameraRigApplier _rtsRigApplier;
        private Space4XPlayerFlagshipController _flagshipController;
        private bool _hasCachedTargetPose;
        private Vector3 _cachedTargetPosition;
        private Quaternion _cachedTargetRotation;
        private Entity _interpolatedPoseTarget;
        private uint _interpolatedPoseTick;
        private bool _hasInterpolatedPose;
        private Vector3 _interpolatedPreviousPosition;
        private Vector3 _interpolatedCurrentPosition;
        private Quaternion _interpolatedPreviousRotation;
        private Quaternion _interpolatedCurrentRotation;
        private bool _orbitInputAnchorActive;
        private bool _orbitInputDeadZoneUnlocked;
        private Vector2 _orbitInputAnchorPointer;
        private Vector2 _orbitInputLastPointer;
        private bool _controlledPoseInterpolationActive;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _loggedTargetAcquired;
#endif

        private void OnEnable()
        {
            _hostCamera = GetComponent<UCamera>();
            _flagshipController = GetComponent<Space4XPlayerFlagshipController>();
            _target = Entity.Null;
            _lastFramedTarget = Entity.Null;
            _queriesReady = false;
            _introFramingActive = false;
            ValidateSettings();
            followDistance = Mathf.Clamp(followDistance, minFollowDistance, maxFollowDistance);
            followHeight = Mathf.Clamp(followHeight, 0f, ResolveMaxFollowHeight(followDistance));
            lookHeight = Mathf.Clamp(lookHeight, -ResolveMaxFollowHeight(followDistance), ResolveMaxFollowHeight(followDistance));
            _baseFramedDistance = followDistance;
            _zoomScalar = Mathf.Clamp(defaultZoomScalar, minZoomScalar, maxZoomScalar);
            _orbitPitchDegrees = DerivePitchFromHeightAndDistance(followHeight, followDistance);
            _cursorWorldYawDegrees = 0f;
            _cursorWorldYawInitialized = false;
            _cursorHeadingYawDegrees = 0f;
            _cursorHeadingYawInitialized = false;
            _cursorManualYawOffsetDegrees = 0f;
            _cursorAltLateralOffsetUnits = 0f;
            _cursorAltVerticalOffsetUnits = 0f;
            _cruiseInitialized = false;
            _cruiseYawOffsetDegrees = 0f;
            _altPressedLastFrame = false;
            _lastAltTapAt = -1000f;
            _hasCachedTargetPose = false;
            _interpolatedPoseTarget = Entity.Null;
            _interpolatedPoseTick = 0u;
            _hasInterpolatedPose = false;
            _interpolatedPreviousPosition = Vector3.zero;
            _interpolatedCurrentPosition = Vector3.zero;
            _interpolatedPreviousRotation = Quaternion.identity;
            _interpolatedCurrentRotation = Quaternion.identity;
            _orbitInputAnchorActive = false;
            _orbitInputDeadZoneUnlocked = false;
            _orbitInputAnchorPointer = Vector2.zero;
            _orbitInputLastPointer = Vector2.zero;
            _controlledPoseInterpolationActive = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _loggedTargetAcquired = false;
#endif

            Space4XControlModeState.ModeChanged += OnControlModeChanged;
            OnControlModeChanged(Space4XControlModeState.CurrentMode);
            SuppressConflictingCameraDrivers();
            EnsureQueries();
        }

        private void OnDisable()
        {
            Space4XControlModeState.ModeChanged -= OnControlModeChanged;
            SetRtsCameraEnabled(false);
            _hasCachedTargetPose = false;
            _controlledPoseInterpolationActive = false;
            ResetOrbitInputAnchor();
        }

        private void LateUpdate()
        {
            HandleModeHotkeys();
            SuppressConflictingCameraDrivers();
            if (IsRtsRigMode(_currentMode))
            {
                ApplyRtsModeToggleVariant();
                return;
            }

            // Defensive guard: ensure RTS camera components cannot stay enabled after mode switches.
            if ((_rtsRigController != null && _rtsRigController.enabled) ||
                (_rtsRigApplier != null && _rtsRigApplier.enabled))
            {
                SetRtsCameraEnabled(false);
            }

            if (!TryGetTargetPose(out var targetPosition, out var targetRotation))
            {
                _hasCachedTargetPose = false;
                return;
            }

            _cachedTargetPosition = targetPosition;
            _cachedTargetRotation = targetRotation;
            _hasCachedTargetPose = true;

            UpdateCruiseAltDoubleTapReset(targetRotation);
            HandleCameraOrbitInput(targetPosition, targetRotation);
            ApplyCruiseMovementRecentre(targetRotation);
            ApplyFollow(targetPosition, targetRotation, snap: ShouldSnapFollowToTarget());
        }

        private void OnPreCull()
        {
            if (IsRtsRigMode(_currentMode) || !lockCameraToTargetOutsideRts)
                return;

            SuppressConflictingCameraDrivers();

            if (UCamera.current != null && _hostCamera != null && UCamera.current != _hostCamera)
                return;

            if (!_hasCachedTargetPose)
            {
                if (!TryGetTargetPose(out _cachedTargetPosition, out _cachedTargetRotation))
                    return;
            }

            ApplyFollow(_cachedTargetPosition, _cachedTargetRotation, snap: true);
        }

        public void SnapNow()
        {
            if (IsRtsRigMode(_currentMode))
                return;

            if (!TryGetTargetPose(out var targetPosition, out var targetRotation))
                return;

            _cachedTargetPosition = targetPosition;
            _cachedTargetRotation = targetRotation;
            _hasCachedTargetPose = true;
            ApplyFollow(targetPosition, targetRotation, snap: true);
        }

        public bool TryGetDebugTarget(out Entity target)
        {
            target = _target;
            if (!EnsureQueries())
                return false;

            return IsValidTarget(target);
        }

        public Space4XControlMode DebugCurrentMode => _currentMode;
        public bool DebugOrbitAnchorActive => _orbitInputAnchorActive;
        public bool DebugOrbitDeadZoneUnlocked => _orbitInputDeadZoneUnlocked;
        public float DebugOrbitDeadZonePixels => orbitClickDeadZonePixels;
        public bool DebugInterpolateControlledFlagshipPose => interpolateControlledFlagshipPose;
        public bool DebugControlledPoseInterpolationActive => _controlledPoseInterpolationActive;
        public bool CursorModeUsesIndependentCamera => IsCursorModeIndependentCamera();

        public void ConfigureForFlagshipIntro()
        {
            _introFramingActive = true;
            _target = Entity.Null;
            _lastFramedTarget = Entity.Null;
            _zoomScalar = Mathf.Clamp(introZoomScalar, minZoomScalar, maxZoomScalar);
            followSharpness = Mathf.Max(0.01f, introFollowSharpness);

            if (!autoFrameByTargetSize || !useIntroFramingByRadius)
            {
                followDistance = Mathf.Clamp(introFallbackFollowDistance, minFollowDistance, maxFollowDistance);
                followHeight = Mathf.Clamp(introFallbackFollowHeight, 0f, ResolveMaxFollowHeight(followDistance));
                lookHeight = Mathf.Clamp(introFallbackLookHeight, -ResolveMaxFollowHeight(followDistance), ResolveMaxFollowHeight(followDistance));
                _baseFramedDistance = followDistance;
            }

            _orbitPitchDegrees = DerivePitchFromHeightAndDistance(followHeight, followDistance);
            ResetOrbitState();
        }

        private bool ShouldSnapFollowToTarget()
        {
            return !IsRtsRigMode(_currentMode) && lockCameraToTargetOutsideRts;
        }

        private void ApplyFollow(Vector3 targetPosition, Quaternion targetRotation, bool snap)
        {
            if (_currentMode == Space4XControlMode.CursorOrient &&
                IsCursorModeShipHeadingFollowActive() &&
                TryBuildShipAlignedPose(targetPosition, targetRotation, out var shipAlignedPosition, out var shipAlignedRotation))
            {
                shipAlignedPosition = ApplyCursorPositionOffset(shipAlignedPosition, targetRotation);
                ApplyCameraPose(shipAlignedPosition, shipAlignedRotation, snap);
                return;
            }

            var baseYaw = NormalizeDegrees(ResolveStableYaw(targetRotation, _cursorHeadingYawInitialized ? _cursorHeadingYawDegrees : transform.eulerAngles.y) + behindYawOffsetDegrees);
            var yaw = baseYaw;
            if (_currentMode == Space4XControlMode.CursorOrient)
            {
                if (IsCursorModeShipHeadingFollowActive())
                {
                    yaw = ResolveCursorHeadingYaw(targetRotation, snap);
                }
                else
                {
                    EnsureCursorOrbitInitialized(targetPosition, targetRotation);
                    yaw = _cursorWorldYawDegrees;
                }
            }
            else if (_currentMode == Space4XControlMode.CruiseLook)
            {
                EnsureCruiseOrbitInitialized(targetPosition, targetRotation);
                yaw = ResolveCruiseYaw(targetRotation);
            }

            var clampedDistance = Mathf.Clamp(followDistance, minFollowDistance, maxFollowDistance);
            if (!Mathf.Approximately(clampedDistance, followDistance))
            {
                followDistance = clampedDistance;
            }
            var clampedFollowHeight = Mathf.Clamp(followHeight, 0f, ResolveMaxFollowHeight(followDistance));
            if (!Mathf.Approximately(clampedFollowHeight, followHeight))
            {
                followHeight = clampedFollowHeight;
            }
            var compensatedLookHeight = ResolveCompensatedLookHeight(clampedFollowHeight, followDistance);

            var orbitRotation = Quaternion.Euler(_orbitPitchDegrees, yaw, 0f);
            var desiredPosition = targetPosition + orbitRotation * (Vector3.back * followDistance);
            var lookPosition = targetPosition + new Vector3(0f, compensatedLookHeight, 0f);
            var lookDirection = lookPosition - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            var desiredUp = _currentMode == Space4XControlMode.CursorOrient
                ? orbitRotation * Vector3.up
                : Vector3.up;
            var desiredRotation = ResolveLookRotation(lookDirection, desiredUp);
            desiredPosition = ApplyCursorPositionOffset(desiredPosition, targetRotation);
            ApplyCameraPose(desiredPosition, desiredRotation, snap);
        }

        private bool TryBuildShipAlignedPose(
            Vector3 targetPosition,
            Quaternion targetRotation,
            out Vector3 desiredPosition,
            out Quaternion desiredRotation)
        {
            desiredPosition = default;
            desiredRotation = Quaternion.identity;

            var shipForward = targetRotation * Vector3.forward;
            if (shipForward.sqrMagnitude < 0.0001f)
                return false;

            shipForward.Normalize();
            var shipUp = targetRotation * Vector3.up;
            if (shipUp.sqrMagnitude < 0.0001f)
            {
                shipUp = Vector3.up;
            }
            else
            {
                shipUp.Normalize();
            }

            var shipRight = Vector3.Cross(shipUp, shipForward);
            if (shipRight.sqrMagnitude < 0.0001f)
            {
                shipRight = targetRotation * Vector3.right;
            }
            if (shipRight.sqrMagnitude < 0.0001f)
            {
                shipRight = Vector3.right;
            }
            shipRight.Normalize();

            var basePitch = DerivePitchFromHeightAndDistance(followHeight, followDistance);
            var extraPitch = _orbitPitchDegrees - basePitch;
            var yawOffset = behindYawOffsetDegrees + cursorHeadingOffsetDegrees + _cursorManualYawOffsetDegrees;
            var yawRotation = Quaternion.AngleAxis(yawOffset, shipUp);
            var yawedForward = yawRotation * shipForward;
            var yawedUp = yawRotation * shipUp;
            var yawedRight = yawRotation * shipRight;
            var pitchRotation = Quaternion.AngleAxis(extraPitch, yawedRight);
            var alignedForward = pitchRotation * yawedForward;
            var alignedUp = pitchRotation * yawedUp;

            var clampedFollowHeight = Mathf.Clamp(followHeight, 0f, ResolveMaxFollowHeight(followDistance));
            var compensatedLookHeight = ResolveCompensatedLookHeight(clampedFollowHeight, followDistance);
            desiredPosition = targetPosition - (alignedForward * followDistance) + (alignedUp * clampedFollowHeight);
            var lookPosition = targetPosition + (alignedUp * compensatedLookHeight);
            var lookDirection = lookPosition - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.0001f)
                return false;

            desiredRotation = Quaternion.LookRotation(lookDirection.normalized, alignedUp);
            return true;
        }

        private void ApplyCameraPose(Vector3 desiredPosition, Quaternion desiredRotation, bool snap)
        {
            if (snap)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                return;
            }

            var dt = Mathf.Max(UTime.unscaledDeltaTime, 0f);
            var t = 1f - Mathf.Exp(-followSharpness * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private void HandleCameraOrbitInput(Vector3 targetPosition, Quaternion targetRotation)
        {
            _cruiseOrbitAdjustedThisFrame = false;
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var unitsPerNotch = Mathf.Max(1f, Mathf.Abs(scrollUnitsPerNotch));
                var notches = scroll / unitsPerNotch;
                var nextDistance = Mathf.Clamp(followDistance - (notches * zoomDistancePerNotch), minFollowDistance, maxFollowDistance);
                followDistance = nextDistance;

                if (autoFrameByTargetSize && _baseFramedDistance > 0.001f)
                {
                    _zoomScalar = Mathf.Clamp(followDistance / _baseFramedDistance, minZoomScalar, maxZoomScalar);
                }
            }

            var keyboard = Keyboard.current;
            var altPressed = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            var fighterModeActive =
                _currentMode == Space4XControlMode.CursorOrient &&
                Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient);
            var cruiseRotate = _currentMode == Space4XControlMode.CruiseLook && mouse.rightButton.isPressed;
            var cursorRotate = _currentMode == Space4XControlMode.CursorOrient && mouse.rightButton.isPressed;
            var orbitEngaged = fighterModeActive || altPressed || cruiseRotate || cursorRotate;
            Vector2 delta;
            if (fighterModeActive)
            {
                // Fighter mode uses raw mouse delta every frame (no click-gate / deadzone anchor).
                ResetOrbitInputAnchor();
                delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude < 0.0001f)
                {
                    return;
                }
            }
            else if (!TryResolveOrbitInputDelta(mouse, orbitEngaged, out delta))
            {
                return;
            }

            if (_currentMode == Space4XControlMode.CursorOrient)
            {
                if (IsCursorModeShipHeadingFollowActive())
                {
                    var yawLimit = Mathf.Max(0f, cursorManualYawOffsetLimitDegrees);
                    _cursorManualYawOffsetDegrees = Mathf.Clamp(
                        NormalizeDegrees(_cursorManualYawOffsetDegrees + (delta.x * orbitYawSensitivity)),
                        -yawLimit,
                        yawLimit);
                    _cursorAltVerticalOffsetUnits = Mathf.Clamp(
                        _cursorAltVerticalOffsetUnits + (delta.y * cursorAltPositionUnitsPerPixel),
                        -Mathf.Abs(cursorAltPositionVerticalLimit),
                        Mathf.Abs(cursorAltPositionVerticalLimit));
                }
                else
                {
                    EnsureCursorOrbitInitialized(targetPosition, targetRotation);
                    _cursorWorldYawDegrees = NormalizeDegrees(_cursorWorldYawDegrees + (delta.x * orbitYawSensitivity));
                    _orbitPitchDegrees = Mathf.Clamp(
                        _orbitPitchDegrees - (delta.y * orbitPitchSensitivity),
                        ResolveModeMinPitchDegrees(),
                        ResolveModeMaxPitchDegrees());
                    _cursorManualYawOffsetDegrees = 0f;
                    _cursorAltLateralOffsetUnits = 0f;
                    _cursorAltVerticalOffsetUnits = 0f;
                }
                return;
            }

            EnsureCruiseOrbitInitialized(targetPosition, targetRotation);
            if (IsCruiseModeIndependentCamera())
            {
                _cruiseYawDegrees = NormalizeDegrees(_cruiseYawDegrees + (delta.x * orbitYawSensitivity));
            }
            else
            {
                _cruiseYawOffsetDegrees = NormalizeDegrees(_cruiseYawOffsetDegrees + (delta.x * orbitYawSensitivity));
                _cruiseYawDegrees = ResolveCruiseYaw(targetRotation);
            }
            _cruiseOrbitAdjustedThisFrame = true;

            _orbitPitchDegrees = Mathf.Clamp(
                _orbitPitchDegrees - (delta.y * orbitPitchSensitivity),
                ResolveModeMinPitchDegrees(),
                ResolveModeMaxPitchDegrees());
        }

        private bool TryResolveOrbitInputDelta(Mouse mouse, bool orbitEngaged, out Vector2 delta)
        {
            delta = Vector2.zero;
            if (!orbitEngaged)
            {
                ResetOrbitInputAnchor();
                return false;
            }

            if (!useOrbitClickAnchorDeadZone)
            {
                var rawDelta = mouse.delta.ReadValue();
                if (rawDelta.sqrMagnitude < 0.0001f)
                {
                    return false;
                }

                delta = rawDelta;
                return true;
            }

            var pointer = mouse.position.ReadValue();
            if (!_orbitInputAnchorActive)
            {
                _orbitInputAnchorPointer = pointer;
                _orbitInputLastPointer = pointer;
                _orbitInputAnchorActive = true;
                _orbitInputDeadZoneUnlocked = false;
                return false;
            }

            var deadZone = Mathf.Max(0f, orbitClickDeadZonePixels);
            if (!_orbitInputDeadZoneUnlocked)
            {
                var fromAnchor = pointer - _orbitInputAnchorPointer;
                if (fromAnchor.sqrMagnitude <= deadZone * deadZone)
                {
                    return false;
                }

                // Unlock from a neutral frame to prevent a one-frame directional throw when exiting deadzone.
                _orbitInputDeadZoneUnlocked = true;
                _orbitInputLastPointer = pointer;
                return false;
            }

            delta = pointer - _orbitInputLastPointer;
            _orbitInputLastPointer = pointer;
            return delta.sqrMagnitude >= 0.0001f;
        }

        private float ResolveCursorHeadingYaw(Quaternion targetRotation, bool snap)
        {
            var stableYaw = ResolveStableYaw(targetRotation, _cursorHeadingYawInitialized ? _cursorHeadingYawDegrees : transform.eulerAngles.y);
            var targetYaw = NormalizeDegrees(stableYaw + behindYawOffsetDegrees + cursorHeadingOffsetDegrees + _cursorManualYawOffsetDegrees);
            if (!_cursorHeadingYawInitialized || snap)
            {
                _cursorHeadingYawDegrees = targetYaw;
                _cursorHeadingYawInitialized = true;
                return _cursorHeadingYawDegrees;
            }

            var dt = Mathf.Max(UTime.unscaledDeltaTime, 0f);
            var gain = 1f - Mathf.Exp(-Mathf.Max(0.01f, cursorHeadingFollowSharpness) * dt);
            _cursorHeadingYawDegrees = Mathf.LerpAngle(_cursorHeadingYawDegrees, targetYaw, gain);
            return _cursorHeadingYawDegrees;
        }

        private void EnsureCursorOrbitInitialized(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (_cursorWorldYawInitialized)
                return;

            var toCamera = transform.position - targetPosition;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                var stableYaw = ResolveStableYaw(targetRotation, transform.eulerAngles.y);
                var fallbackRotation = Quaternion.Euler(_orbitPitchDegrees, stableYaw + behindYawOffsetDegrees, 0f);
                toCamera = fallbackRotation * (Vector3.back * followDistance);
            }

            var distance = Mathf.Max(0.01f, toCamera.magnitude);
            followDistance = Mathf.Clamp(distance, minFollowDistance, maxFollowDistance);
            var normalized = toCamera / distance;
            _orbitPitchDegrees = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg,
                ResolveModeMinPitchDegrees(),
                ResolveModeMaxPitchDegrees());
            _cursorWorldYawDegrees = NormalizeDegrees(Mathf.Atan2(-normalized.x, -normalized.z) * Mathf.Rad2Deg);
            _cursorWorldYawInitialized = true;
        }

        private void EnsureCruiseOrbitInitialized(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (_cruiseInitialized)
                return;

            var toCamera = transform.position - targetPosition;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                var fallbackYaw = _cursorHeadingYawInitialized
                    ? _cursorHeadingYawDegrees
                    : (_cursorWorldYawInitialized
                    ? _cursorWorldYawDegrees
                    : ResolveStableYaw(targetRotation, transform.eulerAngles.y) + behindYawOffsetDegrees);
                var fallbackRotation = Quaternion.Euler(_orbitPitchDegrees, fallbackYaw, 0f);
                toCamera = fallbackRotation * (Vector3.back * followDistance);
            }

            var distance = Mathf.Max(0.01f, toCamera.magnitude);
            followDistance = Mathf.Clamp(distance, minFollowDistance, maxFollowDistance);
            var normalized = toCamera / distance;
            _orbitPitchDegrees = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg,
                ResolveModeMinPitchDegrees(),
                ResolveModeMaxPitchDegrees());
            var worldYaw = NormalizeDegrees(Mathf.Atan2(-normalized.x, -normalized.z) * Mathf.Rad2Deg);
            if (IsCruiseModeIndependentCamera())
            {
                _cruiseYawDegrees = worldYaw;
                _cruiseYawOffsetDegrees = 0f;
            }
            else
            {
                var shipYaw = ResolveCruiseBaseYaw(targetRotation, worldYaw);
                _cruiseYawOffsetDegrees = NormalizeDegrees(worldYaw - shipYaw);
                _cruiseYawDegrees = NormalizeDegrees(shipYaw + _cruiseYawOffsetDegrees);
            }
            _cruiseInitialized = true;
        }

        private bool TryGetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!EnsureQueries())
                return false;

            var hasControlledTarget = false;
            var controlledFlagship = Entity.Null;
            if (_flagshipController == null)
            {
                _flagshipController = GetComponent<Space4XPlayerFlagshipController>();
            }

            if (_flagshipController != null && _flagshipController.TryGetControlledFlagship(out controlledFlagship))
            {
                _target = controlledFlagship;
                hasControlledTarget = true;
            }

            var preferredFlagship = hasControlledTarget ? Entity.Null : PickNearestToCamera(_playerFlagshipQuery);
            if (!hasControlledTarget && preferredFlagship != Entity.Null && preferredFlagship != _target)
            {
                _target = preferredFlagship;
            }

            if (!IsValidTarget(_target))
            {
                _target = AcquireTarget();
            }

            if (!IsValidTarget(_target))
                return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedTargetAcquired)
            {
                var hasCarrier = _entityManager.HasComponent<Carrier>(_target);
                var hasMining = _entityManager.HasComponent<MiningVessel>(_target);
                var hasMaterialMesh = _entityManager.HasComponent<MaterialMeshInfo>(_target);
                UnityEngine.Debug.Log($"[Space4XFollowPlayerVessel] Target={_target} HasCarrier={hasCarrier} HasMiningVessel={hasMining} HasMaterialMeshInfo={hasMaterialMesh} Preset='{Space4XRunStartSelection.ShipPresetId}'");
                _loggedTargetAcquired = true;
            }
#endif

            var controlledPoseFromHybrid = hasControlledTarget &&
                                           controlledFlagship == _target &&
                                           _entityManager.HasComponent<LocalTransform>(_target);
            var controlledPoseFromRendered =
                controlledPoseFromHybrid && _entityManager.HasComponent<LocalToWorld>(_target);

            if (controlledPoseFromRendered)
            {
                var localToWorld = _entityManager.GetComponentData<LocalToWorld>(_target);
                position = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
                var forward = new Vector3(localToWorld.Value.c2.x, localToWorld.Value.c2.y, localToWorld.Value.c2.z);
                var up = new Vector3(localToWorld.Value.c1.x, localToWorld.Value.c1.y, localToWorld.Value.c1.z);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    if (up.sqrMagnitude < 0.0001f)
                    {
                        up = Vector3.up;
                    }

                    rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
                }
                else
                {
                    var localTransform = _entityManager.GetComponentData<LocalTransform>(_target);
                    rotation = new Quaternion(
                        localTransform.Rotation.value.x,
                        localTransform.Rotation.value.y,
                        localTransform.Rotation.value.z,
                        localTransform.Rotation.value.w);
                }
            }
            else if (controlledPoseFromHybrid)
            {
                var localTransform = _entityManager.GetComponentData<LocalTransform>(_target);
                position = new Vector3(localTransform.Position.x, localTransform.Position.y, localTransform.Position.z);
                rotation = new Quaternion(
                    localTransform.Rotation.value.x,
                    localTransform.Rotation.value.y,
                    localTransform.Rotation.value.z,
                    localTransform.Rotation.value.w);
            }
            else if (_entityManager.HasComponent<LocalToWorld>(_target))
            {
                var localToWorld = _entityManager.GetComponentData<LocalToWorld>(_target);
                position = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
                var forward = new Vector3(localToWorld.Value.c2.x, localToWorld.Value.c2.y, localToWorld.Value.c2.z);
                var up = new Vector3(localToWorld.Value.c1.x, localToWorld.Value.c1.y, localToWorld.Value.c1.z);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    if (up.sqrMagnitude < 0.0001f)
                    {
                        up = Vector3.up;
                    }

                    rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
                }
                else if (_entityManager.HasComponent<LocalTransform>(_target))
                {
                    var localTransform = _entityManager.GetComponentData<LocalTransform>(_target);
                    rotation = new Quaternion(
                        localTransform.Rotation.value.x,
                        localTransform.Rotation.value.y,
                        localTransform.Rotation.value.z,
                        localTransform.Rotation.value.w);
                }
            }
            else if (_entityManager.HasComponent<LocalTransform>(_target))
            {
                var localTransform = _entityManager.GetComponentData<LocalTransform>(_target);
                position = new Vector3(localTransform.Position.x, localTransform.Position.y, localTransform.Position.z);
                rotation = new Quaternion(
                    localTransform.Rotation.value.x,
                    localTransform.Rotation.value.y,
                    localTransform.Rotation.value.z,
                    localTransform.Rotation.value.w);
            }

            var speedDrivenInterpolation = false;
            if (controlledPoseFromHybrid &&
                !interpolateControlledFlagshipPose &&
                !_timeInterpolationQuery.IsEmptyIgnoreFilter)
            {
                // At higher simulation speed, enable render interpolation to reduce fixed-step jumpiness.
                var timeState = _timeInterpolationQuery.GetSingleton<TimeState>();
                speedDrivenInterpolation = timeState.CurrentSpeedMultiplier > 1.05f;
            }

            var fixedStepDrivenControlledPose = controlledPoseFromHybrid &&
                                                _entityManager.HasComponent<MovementSuppressed>(_target) &&
                                                _entityManager.IsComponentEnabled<MovementSuppressed>(_target);
            var renderedPoseAlreadyInterpolated = controlledPoseFromRendered &&
                                                  _entityManager.HasComponent<SimPoseSnapshot>(_target);

            var shouldInterpolateTargetPose =
                !controlledPoseFromHybrid ||
                interpolateControlledFlagshipPose ||
                speedDrivenInterpolation ||
                fixedStepDrivenControlledPose;
            if (!interpolateControlledFlagshipPose && renderedPoseAlreadyInterpolated)
            {
                // LocalToWorld already comes from snapshot interpolation; avoid double-smoothing camera follow.
                shouldInterpolateTargetPose = false;
            }
            _controlledPoseInterpolationActive = controlledPoseFromHybrid && shouldInterpolateTargetPose;
            if (shouldInterpolateTargetPose)
            {
                ApplyInterpolatedTargetPose(_target, ref position, ref rotation);
            }
            else
            {
                // Controlled flagship already updates in fixed simulation; avoid extra render interpolation to reduce jitter.
                _hasInterpolatedPose = false;
                _interpolatedPoseTarget = Entity.Null;
                _controlledPoseInterpolationActive = false;
            }

            if (_target != _lastFramedTarget)
            {
                _lastFramedTarget = _target;
                ApplyFramingForTarget(_target, resetZoom: true);

                if (snapBehindOnTargetAcquire)
                {
                    SnapOrbitBehindTarget(rotation);
                    ApplyFollow(position, rotation, snap: true);
                }
            }

            return true;
        }

        private Entity AcquireTarget()
        {
            var playerFlagship = PickNearestToCamera(_playerFlagshipQuery);
            if (playerFlagship != Entity.Null)
                return playerFlagship;

            var preferCarrier = PreferCarrierTargetForRun();
            if (preferCarrier)
            {
                var carrierRenderable = PickNearestToCamera(_carrierRenderableQuery);
                if (carrierRenderable != Entity.Null)
                    return carrierRenderable;

                var miningRenderable = PickNearestToCamera(_miningRenderableQuery);
                if (miningRenderable != Entity.Null)
                    return miningRenderable;

                var carrierTarget = PickNearestToCamera(_carrierQuery);
                if (carrierTarget != Entity.Null)
                    return carrierTarget;

                var miningTarget = PickNearestToCamera(_miningVesselQuery);
                if (miningTarget != Entity.Null)
                    return miningTarget;
            }
            else
            {
                var miningRenderable = PickNearestToCamera(_miningRenderableQuery);
                if (miningRenderable != Entity.Null)
                    return miningRenderable;

                var carrierRenderable = PickNearestToCamera(_carrierRenderableQuery);
                if (carrierRenderable != Entity.Null)
                    return carrierRenderable;

                var miningTarget = PickNearestToCamera(_miningVesselQuery);
                if (miningTarget != Entity.Null)
                    return miningTarget;

                var carrierTarget = PickNearestToCamera(_carrierQuery);
                if (carrierTarget != Entity.Null)
                    return carrierTarget;
            }

            return PickNearestToCamera(_fallbackRenderableQuery);
        }

        private static bool PreferCarrierTargetForRun()
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
                Vector3 worldPos;
                if (_entityManager.HasComponent<LocalToWorld>(entity))
                {
                    var ltw = _entityManager.GetComponentData<LocalToWorld>(entity);
                    worldPos = new Vector3(ltw.Position.x, ltw.Position.y, ltw.Position.z);
                }
                else if (_entityManager.HasComponent<LocalTransform>(entity))
                {
                    var local = _entityManager.GetComponentData<LocalTransform>(entity);
                    worldPos = new Vector3(local.Position.x, local.Position.y, local.Position.z);
                }
                else
                {
                    continue;
                }

                var distanceSq = (worldPos - cameraPosition).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestEntity = entity;
                }
            }

            return bestEntity;
        }

        private bool IsValidTarget(Entity entity)
        {
            return entity != Entity.Null
                   && _entityManager.Exists(entity)
                   && (_entityManager.HasComponent<LocalToWorld>(entity) || _entityManager.HasComponent<LocalTransform>(entity));
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
                    ComponentType.ReadOnly<PlayerFlagshipTag>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _miningVesselQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MiningVessel>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _carrierQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Carrier>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _carrierRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _miningRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _fallbackRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _timeInterpolationQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TimeState>(),
                    ComponentType.ReadOnly<FixedStepInterpolationState>()
                }
            });
            _queriesReady = true;
            return true;
        }

        private void HandleModeHotkeys()
        {
            var keyboard = Keyboard.current;
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
            else if (divineHandModeHotkey != Key.None && keyboard[divineHandModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetModeOrToggleVariant(Space4XControlMode.DivineHand);
            }
        }

        private void OnControlModeChanged(Space4XControlMode mode)
        {
            _currentMode = mode;
            ResetOrbitInputAnchor();
            _altPressedLastFrame = false;
            if (IsRtsRigMode(_currentMode))
            {
                SetRtsCameraEnabled(true);
                ApplyRtsModeToggleVariant();
            }
            else
            {
                SetRtsCameraEnabled(false);
                _orbitPitchDegrees = Mathf.Clamp(_orbitPitchDegrees, ResolveModeMinPitchDegrees(), ResolveModeMaxPitchDegrees());
                if (_currentMode == Space4XControlMode.CursorOrient)
                {
                    _cursorWorldYawInitialized = false;
                    _cursorHeadingYawInitialized = false;
                    _cursorManualYawOffsetDegrees = 0f;
                }
                if (_currentMode == Space4XControlMode.CruiseLook)
                {
                    _cruiseInitialized = false;
                }

                SnapNow();
            }
        }

        private void SetRtsCameraEnabled(bool enabled)
        {
            var camera = ResolveHostCamera();
            if (camera == null)
                return;

            _rtsRigController = camera.GetComponent<Space4XCameraRigController>();
            _rtsRigApplier = camera.GetComponent<CameraRigApplier>();

            if (_rtsRigController != null)
            {
                _rtsRigController.enabled = enabled;
            }

            if (_rtsRigApplier != null)
            {
                _rtsRigApplier.enabled = enabled;
            }

            if (enabled)
            {
                ApplyRtsModeToggleVariant();
            }
        }

        private void SuppressConflictingCameraDrivers()
        {
            var camera = ResolveHostCamera();
            if (camera == null)
                return;

            DisableCameraDriver(camera.GetComponent<global::FocusFirstRenderable>());
            DisableCameraDriver(camera.GetComponent<FlyCameraController>());
            DisableCameraDriver(camera.GetComponent<Space4XCameraPlaceholder>());
            DisableCameraDriver(camera.GetComponent<Space4XDebugLockCamera>());
        }

        private UCamera ResolveHostCamera()
        {
            if (_hostCamera == null)
            {
                _hostCamera = GetComponent<UCamera>();
            }

            return _hostCamera != null
                ? _hostCamera
                : (UCamera.main ?? UnityEngine.Object.FindAnyObjectByType<UCamera>());
        }

        private static void DisableCameraDriver(Behaviour behavior)
        {
            if (behavior != null && behavior.enabled)
            {
                behavior.enabled = false;
            }
        }

        private float DerivePitchFromHeightAndDistance(float height, float distance)
        {
            var safeDistance = Mathf.Max(0.1f, distance);
            var pitch = Mathf.Atan2(Mathf.Max(0f, height), safeDistance) * Mathf.Rad2Deg;
            return Mathf.Clamp(pitch, ResolveModeMinPitchDegrees(), ResolveModeMaxPitchDegrees());
        }

        private float ResolveModeMinPitchDegrees()
        {
            return _currentMode switch
            {
                Space4XControlMode.CursorOrient => CursorModePitchMinDegrees,
                Space4XControlMode.CruiseLook => CruiseModePitchMinDegrees,
                _ => minPitchDegrees
            };
        }

        private float ResolveModeMaxPitchDegrees()
        {
            return _currentMode switch
            {
                Space4XControlMode.CursorOrient => CursorModePitchMaxDegrees,
                Space4XControlMode.CruiseLook => CruiseModePitchMaxDegrees,
                _ => maxPitchDegrees
            };
        }

        private static Quaternion ResolveLookRotation(Vector3 forward, Vector3 preferredUp)
        {
            var forwardSafe = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            var upSafe = preferredUp.sqrMagnitude > 0.0001f ? preferredUp.normalized : Vector3.up;
            var alignment = Mathf.Abs(Vector3.Dot(forwardSafe, upSafe));
            if (alignment > 0.999f)
            {
                // Avoid LookRotation degeneracy when forward and up become nearly parallel.
                upSafe = Mathf.Abs(Vector3.Dot(forwardSafe, Vector3.up)) < 0.999f ? Vector3.up : Vector3.right;
            }

            return Quaternion.LookRotation(forwardSafe, upSafe);
        }

        private void ApplyFramingForTarget(Entity target, bool resetZoom)
        {
            if (!autoFrameByTargetSize || !_entityManager.Exists(target))
                return;

            var radius = ResolveTargetRadius(target);
            var distancePerRadius = _introFramingActive && useIntroFramingByRadius
                ? introFollowDistancePerRadius
                : defaultFollowDistancePerRadius;
            var heightPerRadius = _introFramingActive && useIntroFramingByRadius
                ? introFollowHeightPerRadius
                : defaultFollowHeightPerRadius;
            var lookPerRadius = _introFramingActive && useIntroFramingByRadius
                ? introLookHeightPerRadius
                : defaultLookHeightPerRadius;

            _baseFramedDistance = Mathf.Clamp(radius * Mathf.Max(0.01f, distancePerRadius), minFollowDistance, maxFollowDistance);

            if (resetZoom)
            {
                var targetZoom = _introFramingActive ? introZoomScalar : defaultZoomScalar;
                _zoomScalar = Mathf.Clamp(targetZoom, minZoomScalar, maxZoomScalar);
            }

            followDistance = Mathf.Clamp(_baseFramedDistance * _zoomScalar, minFollowDistance, maxFollowDistance);
            var maxHeight = ResolveMaxFollowHeight(followDistance);
            followHeight = Mathf.Clamp(radius * Mathf.Max(0f, heightPerRadius), 0f, maxHeight);
            lookHeight = Mathf.Clamp(radius * Mathf.Max(0f, lookPerRadius), -maxHeight, maxHeight);
            _orbitPitchDegrees = DerivePitchFromHeightAndDistance(followHeight, followDistance);
        }

        private float ResolveTargetRadius(Entity target)
        {
            var radius = Mathf.Max(0.01f, fallbackTargetRadius);

            if (_entityManager.HasComponent<VesselPhysicalProperties>(target))
            {
                var physical = _entityManager.GetComponentData<VesselPhysicalProperties>(target);
                radius = Mathf.Max(radius, Mathf.Max(0.01f, physical.Radius));
            }

            var scale = 1f;
            if (_entityManager.HasComponent<LocalTransform>(target))
            {
                var local = _entityManager.GetComponentData<LocalTransform>(target);
                scale *= Mathf.Max(0.01f, local.Scale);
            }

            if (_entityManager.HasComponent<PresentationScale>(target))
            {
                var presentationScale = _entityManager.GetComponentData<PresentationScale>(target);
                scale *= Mathf.Max(0.01f, presentationScale.Value);
            }

            return Mathf.Clamp(radius * scale, minTargetRadius, maxTargetRadius);
        }

        private void SnapOrbitBehindTarget(Quaternion targetRotation)
        {
            var yaw = NormalizeDegrees(ResolveStableYaw(targetRotation, transform.eulerAngles.y) + behindYawOffsetDegrees);
            _cursorManualYawOffsetDegrees = 0f;
            _cursorWorldYawDegrees = yaw;
            _cursorWorldYawInitialized = true;
            _cursorHeadingYawDegrees = NormalizeDegrees(yaw + cursorHeadingOffsetDegrees);
            _cursorHeadingYawInitialized = true;
            _cruiseYawOffsetDegrees = 0f;
            _cruiseYawDegrees = yaw;
            _cruiseInitialized = true;
            _orbitPitchDegrees = DerivePitchFromHeightAndDistance(followHeight, followDistance);
        }

        private void ResetOrbitState()
        {
            _cursorWorldYawDegrees = 0f;
            _cursorWorldYawInitialized = false;
            _cursorHeadingYawDegrees = 0f;
            _cursorHeadingYawInitialized = false;
            _cursorManualYawOffsetDegrees = 0f;
            _cursorAltLateralOffsetUnits = 0f;
            _cursorAltVerticalOffsetUnits = 0f;
            _cruiseYawOffsetDegrees = 0f;
            _cruiseInitialized = false;
            ResetOrbitInputAnchor();
        }

        private void ResetOrbitInputAnchor()
        {
            _orbitInputAnchorActive = false;
            _orbitInputDeadZoneUnlocked = false;
            _orbitInputAnchorPointer = Vector2.zero;
            _orbitInputLastPointer = Vector2.zero;
        }

        private void ValidateSettings()
        {
            followSharpness = Mathf.Max(0.01f, followSharpness);
            cursorHeadingFollowSharpness = Mathf.Max(0.01f, cursorHeadingFollowSharpness);
            cursorManualYawOffsetLimitDegrees = Mathf.Clamp(cursorManualYawOffsetLimitDegrees, 0f, 180f);
            cursorAltPositionUnitsPerPixel = Mathf.Clamp(cursorAltPositionUnitsPerPixel, 0.001f, 1f);
            cursorAltPositionLateralLimit = Mathf.Max(0f, cursorAltPositionLateralLimit);
            cursorAltPositionVerticalLimit = Mathf.Max(0f, cursorAltPositionVerticalLimit);
            cruiseMovementRecentreSharpness = Mathf.Clamp(cruiseMovementRecentreSharpness, 0.01f, 40f);
            orbitClickDeadZonePixels = Mathf.Max(0f, orbitClickDeadZonePixels);
            minPitchDegrees = Mathf.Clamp(minPitchDegrees, -179.5f, 179.5f);
            maxPitchDegrees = Mathf.Clamp(maxPitchDegrees, minPitchDegrees, 179.5f);
            cruiseMinPitchDegrees = Mathf.Clamp(cruiseMinPitchDegrees, -89.95f, 89.95f);
            cruiseMaxPitchDegrees = Mathf.Clamp(cruiseMaxPitchDegrees, cruiseMinPitchDegrees, 89.95f);
            cruiseAltDoubleTapSeconds = Mathf.Clamp(cruiseAltDoubleTapSeconds, 0.1f, 1f);
            minFollowDistance = Mathf.Max(0.25f, minFollowDistance);
            maxFollowDistance = Mathf.Max(minFollowDistance, maxFollowDistance);
            minTargetRadius = Mathf.Max(0.01f, minTargetRadius);
            maxTargetRadius = Mathf.Max(minTargetRadius, maxTargetRadius);
            fallbackTargetRadius = Mathf.Clamp(fallbackTargetRadius, minTargetRadius, maxTargetRadius);
            minZoomScalar = Mathf.Max(0.05f, minZoomScalar);
            maxZoomScalar = Mathf.Max(minZoomScalar, maxZoomScalar);
            defaultZoomScalar = Mathf.Clamp(defaultZoomScalar, minZoomScalar, maxZoomScalar);
            introZoomScalar = Mathf.Clamp(introZoomScalar, minZoomScalar, maxZoomScalar);
            defaultFollowDistancePerRadius = Mathf.Max(0.01f, defaultFollowDistancePerRadius);
            defaultFollowHeightPerRadius = Mathf.Max(0f, defaultFollowHeightPerRadius);
            defaultLookHeightPerRadius = Mathf.Max(0f, defaultLookHeightPerRadius);
            introFollowDistancePerRadius = Mathf.Max(0.01f, introFollowDistancePerRadius);
            introFollowHeightPerRadius = Mathf.Max(0f, introFollowHeightPerRadius);
            introLookHeightPerRadius = Mathf.Max(0f, introLookHeightPerRadius);
            introFallbackFollowDistance = Mathf.Clamp(introFallbackFollowDistance, minFollowDistance, maxFollowDistance);
            introFallbackFollowHeight = Mathf.Max(0f, introFallbackFollowHeight);
            introFallbackLookHeight = Mathf.Max(0f, introFallbackLookHeight);
            introFollowSharpness = Mathf.Max(0.01f, introFollowSharpness);
            maxFollowHeightToDistanceRatio = Mathf.Clamp(maxFollowHeightToDistanceRatio, 0.05f, 2f);
            lookHeightCompensationFromFollowHeight = Mathf.Clamp01(lookHeightCompensationFromFollowHeight);
        }

        private float ResolveMaxFollowHeight(float distance)
        {
            return Mathf.Max(0.01f, distance * Mathf.Max(0.05f, maxFollowHeightToDistanceRatio));
        }

        private float ResolveCompensatedLookHeight(float currentFollowHeight, float distance)
        {
            if (centerOrbitOnTarget)
            {
                return 0f;
            }

            var compensated = lookHeight - (currentFollowHeight * lookHeightCompensationFromFollowHeight);
            var maxHeight = ResolveMaxFollowHeight(distance);
            return Mathf.Clamp(compensated, -maxHeight, maxHeight);
        }

        private Vector3 ApplyCursorPositionOffset(Vector3 desiredPosition, Quaternion targetRotation)
        {
            if (_currentMode != Space4XControlMode.CursorOrient)
            {
                return desiredPosition;
            }

            if (Mathf.Abs(_cursorAltLateralOffsetUnits) < 0.0001f &&
                Mathf.Abs(_cursorAltVerticalOffsetUnits) < 0.0001f)
            {
                return desiredPosition;
            }

            var shipForward = targetRotation * Vector3.forward;
            if (shipForward.sqrMagnitude < 0.0001f)
            {
                shipForward = Vector3.forward;
            }
            else
            {
                shipForward.Normalize();
            }

            var shipUp = targetRotation * Vector3.up;
            if (shipUp.sqrMagnitude < 0.0001f)
            {
                shipUp = Vector3.up;
            }
            else
            {
                shipUp.Normalize();
            }

            var shipRight = Vector3.Cross(shipUp, shipForward);
            if (shipRight.sqrMagnitude < 0.0001f)
            {
                shipRight = targetRotation * Vector3.right;
            }
            if (shipRight.sqrMagnitude < 0.0001f)
            {
                shipRight = Vector3.right;
            }
            shipRight.Normalize();

            return desiredPosition
                   + (shipRight * _cursorAltLateralOffsetUnits)
                   + (shipUp * _cursorAltVerticalOffsetUnits);
        }

        private void UpdateCruiseAltDoubleTapReset(Quaternion targetRotation)
        {
            var keyboard = Keyboard.current;
            var altPressed = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (_currentMode == Space4XControlMode.CruiseLook && altPressed && !_altPressedLastFrame)
            {
                var now = UTime.unscaledTime;
                if (now - _lastAltTapAt <= cruiseAltDoubleTapSeconds)
                {
                    SnapOrbitBehindTarget(targetRotation);
                    _lastAltTapAt = -1000f;
                }
                else
                {
                    _lastAltTapAt = now;
                }
            }

            _altPressedLastFrame = altPressed;
        }

        private void ApplyCruiseMovementRecentre(Quaternion targetRotation)
        {
            if (_currentMode != Space4XControlMode.CruiseLook)
            {
                return;
            }

            if (!enableCruiseMovementRecentre)
            {
                return;
            }

            // Mode 2 variant is heading-hold; only auto-recenter in default cruise behavior.
            if (Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CruiseLook))
            {
                return;
            }

            if (_cruiseOrbitAdjustedThisFrame || !IsCruiseMovementInputPressed())
            {
                return;
            }

            var dt = Mathf.Max(UTime.unscaledDeltaTime, 0f);
            var gain = 1f - Mathf.Exp(-cruiseMovementRecentreSharpness * dt);
            _cruiseYawOffsetDegrees = Mathf.LerpAngle(_cruiseYawOffsetDegrees, 0f, gain);
            _cruiseYawDegrees = ResolveCruiseYaw(targetRotation);
        }

        private static bool IsCruiseMovementInputPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.wKey.isPressed ||
                   keyboard.aKey.isPressed ||
                   keyboard.sKey.isPressed ||
                   keyboard.dKey.isPressed ||
                   keyboard.spaceKey.isPressed ||
                   keyboard.leftCtrlKey.isPressed ||
                   keyboard.cKey.isPressed;
        }

        private void ApplyRtsModeToggleVariant()
        {
            if (!IsRtsRigMode(_currentMode))
            {
                return;
            }

            if (_rtsRigController == null)
            {
                var camera = ResolveHostCamera();
                if (camera != null)
                {
                    _rtsRigController = camera.GetComponent<Space4XCameraRigController>();
                }
            }

            if (_rtsRigController == null || !_rtsRigController.enabled)
            {
                return;
            }

            var variantMode = _currentMode == Space4XControlMode.DivineHand
                ? Space4XControlMode.DivineHand
                : Space4XControlMode.Rts;
            var variantEnabled = Space4XControlModeState.IsVariantEnabled(variantMode);
            // Modes 3/4 default to free-form god camera travel.
            // Variant toggle enables planar lock.
            _rtsRigController.SetYAxisLocked(variantEnabled);
        }

        private static bool IsRtsRigMode(Space4XControlMode mode)
        {
            return mode == Space4XControlMode.Rts || mode == Space4XControlMode.DivineHand;
        }

        private float ResolveCruiseBaseYaw(Quaternion targetRotation, float fallbackYaw)
        {
            return NormalizeDegrees(ResolveStableYaw(targetRotation, fallbackYaw) + behindYawOffsetDegrees);
        }

        private float ResolveCruiseYaw(Quaternion targetRotation)
        {
            if (IsCruiseModeIndependentCamera())
            {
                _cruiseYawDegrees = NormalizeDegrees(_cruiseYawDegrees);
                return _cruiseYawDegrees;
            }

            var baseYaw = ResolveCruiseBaseYaw(targetRotation, _cruiseYawDegrees);
            _cruiseYawDegrees = NormalizeDegrees(baseYaw + _cruiseYawOffsetDegrees);
            return _cruiseYawDegrees;
        }

        private bool IsCursorModeIndependentCamera()
        {
            if (_currentMode == Space4XControlMode.CursorOrient &&
                Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient))
            {
                // Fighter mode owns camera heading directly.
                return true;
            }

            return cursorModeIndependentCamera;
        }

        private bool IsCursorModeShipHeadingFollowActive()
        {
            return cursorModeFollowsShipHeading && !IsCursorModeIndependentCamera();
        }

        private bool IsCruiseModeIndependentCamera()
        {
            if (_currentMode == Space4XControlMode.CruiseLook &&
                !Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CruiseLook))
            {
                // Default mode 2 should let ship align to current camera facing, not pull camera to ship heading.
                return true;
            }

            return cruiseModeIndependentCamera;
        }

        private void ApplyInterpolatedTargetPose(Entity target, ref Vector3 position, ref Quaternion rotation)
        {
            if (_timeInterpolationQuery.IsEmptyIgnoreFilter)
            {
                _hasInterpolatedPose = false;
                return;
            }

            var timeState = _timeInterpolationQuery.GetSingleton<TimeState>();
            var interpolation = _timeInterpolationQuery.GetSingleton<FixedStepInterpolationState>();
            var alpha = Mathf.Clamp01(interpolation.Alpha);

            if (!_hasInterpolatedPose || _interpolatedPoseTarget != target)
            {
                _interpolatedPoseTarget = target;
                _interpolatedPoseTick = timeState.Tick;
                _interpolatedPreviousPosition = position;
                _interpolatedCurrentPosition = position;
                _interpolatedPreviousRotation = rotation;
                _interpolatedCurrentRotation = rotation;
                _hasInterpolatedPose = true;
                return;
            }

            if (timeState.Tick != _interpolatedPoseTick)
            {
                _interpolatedPoseTick = timeState.Tick;
                _interpolatedPreviousPosition = _interpolatedCurrentPosition;
                _interpolatedPreviousRotation = _interpolatedCurrentRotation;
                _interpolatedCurrentPosition = position;
                _interpolatedCurrentRotation = rotation;
            }

            position = Vector3.Lerp(_interpolatedPreviousPosition, _interpolatedCurrentPosition, alpha);
            rotation = Quaternion.Slerp(_interpolatedPreviousRotation, _interpolatedCurrentRotation, alpha);
        }

        private static float NormalizeDegrees(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        private static float ResolveStableYaw(Quaternion rotation, float fallbackYawDegrees)
        {
            var planarForward = rotation * Vector3.forward;
            planarForward.y = 0f;
            if (planarForward.sqrMagnitude < 0.0001f)
                return NormalizeDegrees(fallbackYawDegrees);

            planarForward.Normalize();
            return NormalizeDegrees(Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg);
        }
    }
}

using System;
using System.Collections.Generic;
using PureDOTS.Input;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.InputKernel;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Spatial;
using Space4X.Input;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
        [SerializeField] private bool pureMovementKernelMode = true;

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
        [SerializeField] private float angularSpeedFromVesselMultiplier = 1f;
        [SerializeField] private float angularAccelerationFromVesselMultiplier = 3f;
        [SerializeField] private float angularDampingFromVesselMultiplier = 3.6f;
        [SerializeField] private float minInheritedSpeed = 0.5f;
        [SerializeField] private float maxInheritedSpeed = 220f;
        [SerializeField] private float minInheritedAcceleration = 0.1f;
        [SerializeField] private float maxInheritedAcceleration = 280f;
        [SerializeField] private float minInheritedAngularSpeedDegrees = 8f;
        [SerializeField] private float maxInheritedAngularSpeedDegrees = 180f;
        [SerializeField] private float minInheritedAngularAccelerationDegrees = 20f;
        [SerializeField] private float maxInheritedAngularAccelerationDegrees = 720f;

        [Header("Mode Hotkeys")]
        [SerializeField] private Key cursorModeHotkey = Key.Digit1;
        [SerializeField] private Key cruiseModeHotkey = Key.Digit2;
        [SerializeField] private Key rtsModeHotkey = Key.Digit3;
        [SerializeField] private Key divineModeHotkey = Key.Digit4;

        [Header("Click Targeting")]
        [SerializeField] private bool enableMode2ClickTargeting = true;
        [SerializeField] private bool enableMode1DefaultClickTargeting = true;
        [SerializeField] private float mode2TargetRaycastDistance = 4000f;
        [SerializeField] private float mode2TargetFallbackRadius = 24f;
        [SerializeField] private LayerMask mode2TargetLayerMask = ~0;
        [SerializeField] private bool enableScreenSpaceTargetProxy = true;
        [SerializeField] [Min(4f)] private float targetProxyPixelRadius = 22f;
        [SerializeField] [Range(0f, 0.05f)] private float targetProxyDepthBias = 0.0015f;
        [SerializeField] private bool enableFighterAutoTargeting = true;
        [SerializeField] [Range(1f, 30f)] private float fighterAutoAcquireConeDegrees = 6f;
        [SerializeField] [Range(2f, 45f)] private float fighterAutoBreakConeDegrees = 10f;
        [SerializeField] [Min(0f)] private float fighterAutoMinLockSeconds = 0.3f;
        [SerializeField] [Min(0f)] private float fighterAutoRetargetCooldownSeconds = 0.2f;
        [SerializeField] [Min(10f)] private float fighterAutoMaxDistance = 2800f;
        [SerializeField] [Range(0f, 20f)] private float fighterAutoHostileCenterBonusDegrees = 2.5f;
        [SerializeField] [Range(0f, 20f)] private float fighterAutoFriendlyCenterPenaltyDegrees = 2f;
        [SerializeField] [Min(0f)] private float fighterAutoDistanceScoreScale = 0.0018f;
        [SerializeField] [Range(5f, 120f)] private float fighterManualSnapConeDegrees = 75f;
        [SerializeField] [Min(0.05f)] private float fighterManualClearDoubleTapWindowSeconds = 0.35f;
        [SerializeField] private bool fighterManualClearWithCtrlMmb = true;

        [Header("Combat Input")]
        [SerializeField] private Key toggleManualAimKey = Key.B;
        [SerializeField] private bool manualAimDefaultEnabled = true;
        [SerializeField] private bool manualAimFireWithLeftMouse = true;
        [SerializeField] private bool autoEnableManualAimOnFire = true;
        [SerializeField] private bool autoAcquireTargetOnFire = true;
        [SerializeField] [Range(2f, 75f)] private float autoAcquireFireConeDegrees = 18f;
        [SerializeField] [Min(10f)] private float autoAcquireFireDistance = 3200f;
        [SerializeField] private bool emitWeaponInputDiagnostics = true;

        [Header("Multi-Target")]
        [SerializeField] private bool enableMultiTargetSelection = true;
        [SerializeField] [Min(1)] private int multiTargetMaxLocks = 12;
        [SerializeField] [Min(10f)] private float multiTargetMaxDistance = 4000f;
        [SerializeField] [Min(0f)] private float multiTargetDistanceScoreScale = 0.0012f;
        [SerializeField] [Range(0f, 20f)] private float multiTargetHostileBonus = 1.25f;
        [SerializeField] [Range(0f, 20f)] private float multiTargetFriendlyPenalty = 1f;
        [SerializeField] private bool multiTargetIncludeAsteroids = true;

        [Header("Attitude")]
        [SerializeField] private float rollSpeedDegrees = 75f;
        [SerializeField] private float cursorTurnSharpness = 12f;
        [SerializeField] private float maxAngularSpeedDegrees = 24f;
        [SerializeField] private float angularAccelerationDegrees = 90f;
        [SerializeField] private float angularDampingDegrees = 110f;
        [SerializeField] [Range(0f, 8f)] private float angularDeadbandDegrees = 0.6f;
        [SerializeField] [Range(1f, 179f)] private float maxCursorLeadDegrees = 150f;
        [SerializeField] [Range(0.05f, 1f)] private float turnAuthorityAtMaxSpeed = 0.45f;
        [SerializeField] [Range(0f, 0.75f)] private float angularOvershootRatio = 0.18f;
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
        private EntityQuery _stationAnyQuery;
        private EntityQuery _colonyAnyQuery;
        private EntityQuery _carrierRenderableQuery;
        private EntityQuery _miningRenderableQuery;
        private EntityQuery _stationRenderableQuery;
        private EntityQuery _colonyRenderableQuery;
        private EntityQuery _fallbackRenderableQuery;
        private EntityQuery _asteroidAnyQuery;
        private EntityQuery _physicsWorldQuery;
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
        private bool _shipAbilityModuleAddSuppressed;
        private Entity _playerFlightInputOverflowEntity;
        private bool _playerFlightInputCapacityWarned;
        private Entity _kernelLocomotionIntentOverflowEntity;
        private bool _kernelLocomotionIntentCapacityWarned;
        private Entity _flightRuntimeStateOverflowEntity;
        private bool _flightRuntimeStateCapacityWarned;
        private Entity _flightProfileOverflowEntity;
        private bool _flightProfileCapacityWarned;
        private Entity _boostDriveConfigOverflowEntity;
        private bool _boostDriveConfigCapacityWarned;
        private Entity _timeCoreConfigOverflowEntity;
        private bool _timeCoreConfigCapacityWarned;
        private Entity _skipDriveConfigOverflowEntity;
        private bool _skipDriveConfigCapacityWarned;
        private Entity _skipJumpStateOverflowEntity;
        private bool _skipJumpStateCapacityWarned;
        private Entity _targetSelectionOverflowEntity;
        private bool _targetSelectionCapacityWarned;
        private Entity _playerWeaponControlOverflowEntity;
        private bool _playerWeaponControlCapacityWarned;
        private float _fighterLastRetargetTime;
        private float _fighterLastLockTime;
        private float _fighterLastMiddleTapTime;
        private Entity _targetLockBufferOverflowEntity;
        private bool _targetLockBufferCapacityWarned;
        private readonly List<MultiTargetCandidate> _multiTargetCandidates = new List<MultiTargetCandidate>(32);
        private readonly List<Space4XPlayerTargetLockEntry> _multiTargetEntriesScratch = new List<Space4XPlayerTargetLockEntry>(32);
        private float _nextWeaponInputDiagnosticTime;

        private struct MultiTargetCandidate
        {
            public Entity Entity;
            public float3 Point;
            public float Score;
        }
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
            _shipAbilityModuleAddSuppressed = false;
            _playerFlightInputOverflowEntity = Entity.Null;
            _playerFlightInputCapacityWarned = false;
            _kernelLocomotionIntentOverflowEntity = Entity.Null;
            _kernelLocomotionIntentCapacityWarned = false;
            _flightRuntimeStateOverflowEntity = Entity.Null;
            _flightRuntimeStateCapacityWarned = false;
            _flightProfileOverflowEntity = Entity.Null;
            _flightProfileCapacityWarned = false;
            _boostDriveConfigOverflowEntity = Entity.Null;
            _boostDriveConfigCapacityWarned = false;
            _timeCoreConfigOverflowEntity = Entity.Null;
            _timeCoreConfigCapacityWarned = false;
            _skipDriveConfigOverflowEntity = Entity.Null;
            _skipDriveConfigCapacityWarned = false;
            _skipJumpStateOverflowEntity = Entity.Null;
            _skipJumpStateCapacityWarned = false;
            _targetSelectionOverflowEntity = Entity.Null;
            _targetSelectionCapacityWarned = false;
            _playerWeaponControlOverflowEntity = Entity.Null;
            _playerWeaponControlCapacityWarned = false;
            _fighterLastRetargetTime = float.NegativeInfinity;
            _fighterLastLockTime = float.NegativeInfinity;
            _fighterLastMiddleTapTime = float.NegativeInfinity;
            _targetLockBufferOverflowEntity = Entity.Null;
            _targetLockBufferCapacityWarned = false;
            _multiTargetCandidates.Clear();
            _multiTargetEntriesScratch.Clear();
            _nextWeaponInputDiagnosticTime = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _loggedClaim = false;
#endif
            Space4XRunStartSelection.ApplyInitialControlModeOverride();
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
                _entityManager.HasComponent<InputKernelLocomotionIntent>(_flagship))
            {
                _entityManager.SetComponentData(_flagship, InputKernelLocomotionIntent.Disabled);
            }

            if (_queriesReady &&
                _world != null &&
                _world.IsCreated &&
                IsValidTarget(_flagship) &&
                _entityManager.HasComponent<Space4XPlayerWeaponControl>(_flagship))
            {
                var weaponControl = _entityManager.GetComponentData<Space4XPlayerWeaponControl>(_flagship);
                weaponControl.TriggerHeld = 0;
                _entityManager.SetComponentData(_flagship, weaponControl);
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
            _shipAbilityModuleAddSuppressed = false;
            _playerFlightInputOverflowEntity = Entity.Null;
            _playerFlightInputCapacityWarned = false;
            _kernelLocomotionIntentOverflowEntity = Entity.Null;
            _kernelLocomotionIntentCapacityWarned = false;
            _boostDriveConfigOverflowEntity = Entity.Null;
            _boostDriveConfigCapacityWarned = false;
            _timeCoreConfigOverflowEntity = Entity.Null;
            _timeCoreConfigCapacityWarned = false;
            _skipDriveConfigOverflowEntity = Entity.Null;
            _skipDriveConfigCapacityWarned = false;
            _skipJumpStateOverflowEntity = Entity.Null;
            _skipJumpStateCapacityWarned = false;
            _fighterLastRetargetTime = float.NegativeInfinity;
            _fighterLastLockTime = float.NegativeInfinity;
            _fighterLastMiddleTapTime = float.NegativeInfinity;
            _targetLockBufferOverflowEntity = Entity.Null;
            _targetLockBufferCapacityWarned = false;
            _multiTargetCandidates.Clear();
            _multiTargetEntriesScratch.Clear();
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

            var controlMode = Space4XControlModeState.CurrentMode;
            HandleWeaponInput(keyboard, controlMode);
            if (controlMode == Space4XControlMode.Rts)
            {
                PrepareFlagshipForRtsOrders();
                SuppressFlagshipMovement();
                MaintainHighlight(_flagship);
                return;
            }

            if (controlMode == Space4XControlMode.DivineHand)
            {
                PrepareFlagshipForManualFlight();
                SuppressFlagshipMovement();
                MaintainHighlight(_flagship);
                return;
            }

            HandleClickTargetingInput();
            var multiTargetApplied = HandleMultiTargetSelectionInput();
            if (!multiTargetApplied)
            {
                HandleFighterAutoTargetingInput(keyboard);
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

            _flagship = PickNearestToCameraControllable(_playerFlagshipQuery);
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
            var preferredAnchor = Space4XRunStartSelection.PreferredFlagshipAnchor;
            if (preferredAnchor == Space4XRunStartAnchor.Auto)
            {
                preferredAnchor = ResolveAutoAnchorPreference();
            }

            var candidate = PickCandidateByAnchor(preferredAnchor, preferCarrier);
            if (candidate == Entity.Null)
            {
                candidate = PickCandidateByAnchor(Space4XRunStartAnchor.Ship, preferCarrier);
            }

            if (candidate == Entity.Null)
            {
                candidate = PickCandidateByAnchor(Space4XRunStartAnchor.Station, preferCarrier);
            }

            if (candidate == Entity.Null)
            {
                candidate = PickCandidateByAnchor(Space4XRunStartAnchor.Colony, preferCarrier);
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

            _flagshipVelocityWorld = float3.zero;
            var initialProfile = ResolveFlightProfile(candidate);
            SetFlightRuntimeState(candidate, CreateDefaultFlightRuntimeState(initialProfile, float3.zero));
            EnsureClaimedFlagshipWeapons(candidate);
            UpsertTargetSelection(candidate, Space4XPlayerTargetSelection.None);
            UpsertPlayerWeaponControl(candidate, new Space4XPlayerWeaponControl
            {
                ManualAimMode = manualAimDefaultEnabled ? (byte)1 : (byte)0,
                TriggerHeld = 0
            });
            ClearMultiTargetLocks(candidate);

            DetachFromAmbientOrbit(candidate);
            ApplyFlightTuningFromEntity(candidate);
            ApplyFlagshipVariantFromSelection(candidate);

            MaintainHighlight(candidate);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedClaim)
            {
                var hasCarrier = _entityManager.HasComponent<Carrier>(candidate);
                var hasMiningVessel = _entityManager.HasComponent<MiningVessel>(candidate);
                var hasStation = _entityManager.HasComponent<StationId>(candidate);
                var hasColony = _entityManager.HasComponent<Space4XColony>(candidate);
                var hasMaterialMesh = _entityManager.HasComponent<MaterialMeshInfo>(candidate);
                var weaponMounts = _entityManager.HasBuffer<WeaponMount>(candidate) ? _entityManager.GetBuffer<WeaponMount>(candidate).Length : 0;
                var manualAimState = manualAimDefaultEnabled ? "ON" : "OFF";
                UnityEngine.Debug.Log($"[Space4XPlayerFlagshipController] Claimed={candidate} HasCarrier={hasCarrier} HasMiningVessel={hasMiningVessel} HasStation={hasStation} HasColony={hasColony} HasMaterialMeshInfo={hasMaterialMesh} WeaponMounts={weaponMounts} ManualAimDefault={manualAimState} Preset='{Space4XRunStartSelection.ShipPresetId}' AnchorPref={Space4XRunStartSelection.PreferredFlagshipAnchor}");
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

        private void HandleWeaponInput(Keyboard keyboard, Space4XControlMode controlMode)
        {
            if (!IsValidTarget(_flagship))
            {
                return;
            }

            var weaponControl = ResolvePlayerWeaponControl(_flagship);
            if (keyboard != null &&
                toggleManualAimKey != Key.None &&
                keyboard[toggleManualAimKey].wasPressedThisFrame)
            {
                weaponControl.ManualAimMode = (byte)(weaponControl.ManualAimMode == 0 ? 1 : 0);
                weaponControl.TriggerHeld = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log($"[Space4XPlayerFlagshipController] ManualAim={(weaponControl.ManualAimMode != 0 ? "ON" : "OFF")}.");
#endif
            }

            var manualFlightMode = controlMode == Space4XControlMode.CursorOrient ||
                                   controlMode == Space4XControlMode.CruiseLook;
            byte triggerHeld = 0;
            if (manualFlightMode && manualAimFireWithLeftMouse)
            {
                var mouse = Mouse.current;
                var leftPressed = mouse != null && mouse.leftButton.isPressed;
                if (leftPressed)
                {
                    if (IsPointerOverUi())
                    {
                        LogWeaponInputDiagnostic("Fire blocked: pointer over UI.");
                    }
                    else if (Space4XTargetSelectionRectangleOverlay.IsPointerCaptureActive ||
                             Space4XTargetSelectionRectangleOverlay.IsSelectionDragActive ||
                             Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive)
                    {
                        LogWeaponInputDiagnostic("Fire blocked: selection drag/capture active.");
                    }
                    else
                    {
                        if (weaponControl.ManualAimMode == 0 && autoEnableManualAimOnFire)
                        {
                            weaponControl.ManualAimMode = 1;
                            LogWeaponInputDiagnostic("Manual aim auto-enabled from fire input.");
                        }

                        if (weaponControl.ManualAimMode != 0)
                        {
                            triggerHeld = 1;
                            if (!EnsureManualFireTarget(controlMode))
                            {
                                LogWeaponInputDiagnostic("Trigger held but no valid target lock/engagement target.");
                            }
                        }
                        else
                        {
                            LogWeaponInputDiagnostic("Fire blocked: manual aim is OFF (toggle with B).");
                        }
                    }
                }
            }

            weaponControl.TriggerHeld = triggerHeld;
            UpsertPlayerWeaponControl(_flagship, weaponControl);
        }

        private bool EnsureManualFireTarget(Space4XControlMode controlMode)
        {
            if (!IsValidTarget(_flagship))
            {
                return false;
            }

            if (TryGetCurrentTargetSelection(out _))
            {
                return true;
            }

            if (_entityManager.HasComponent<Space4XEngagement>(_flagship))
            {
                var engagement = _entityManager.GetComponentData<Space4XEngagement>(_flagship);
                if (IsValidTarget(engagement.PrimaryTarget))
                {
                    var point = _entityManager.HasComponent<LocalTransform>(engagement.PrimaryTarget)
                        ? _entityManager.GetComponentData<LocalTransform>(engagement.PrimaryTarget).Position
                        : float3.zero;
                    var selectedInMode2 = controlMode == Space4XControlMode.CruiseLook ? (byte)1 : (byte)0;
                    ApplyClickTargetSelection(engagement.PrimaryTarget, point, selectedInMode2);
                    return true;
                }
            }

            if (!autoAcquireTargetOnFire)
            {
                return false;
            }

            var camera = ResolveDrivingCamera();
            if (camera == null)
            {
                return false;
            }

            var cone = math.clamp(autoAcquireFireConeDegrees, 2f, 120f);
            var maxDistance = math.max(10f, autoAcquireFireDistance);
            if (!TryFindBestFighterCenterTarget(camera, cone, maxDistance, out var target, out var pointOnTarget))
            {
                return false;
            }

            var mode2 = controlMode == Space4XControlMode.CruiseLook ? (byte)1 : (byte)0;
            ApplyClickTargetSelection(target, pointOnTarget, mode2);
            LogWeaponInputDiagnostic($"Auto-locked target {target.Index} for fire.");
            return true;
        }

        private void EnsureClaimedFlagshipWeapons(Entity entity)
        {
            if (!_entityManager.Exists(entity))
            {
                return;
            }

            if (!_entityManager.HasBuffer<WeaponMount>(entity))
            {
                _entityManager.AddBuffer<WeaponMount>(entity);
            }

            if (!_entityManager.HasBuffer<WeaponMount>(entity))
            {
                return;
            }

            var mounts = _entityManager.GetBuffer<WeaponMount>(entity);
            if (mounts.Length == 0)
            {
                mounts.Add(CreateDefaultWeaponMount(Space4XWeapon.Missile(WeaponSize.Small)));
                mounts.Add(CreateDefaultWeaponMount(Space4XWeapon.Laser(WeaponSize.Small)));
                return;
            }

            var hasMissile = false;
            for (var i = 0; i < mounts.Length; i++)
            {
                if (mounts[i].Weapon.Type == WeaponType.Missile)
                {
                    hasMissile = true;
                    break;
                }
            }

            if (!hasMissile)
            {
                mounts.Add(CreateDefaultWeaponMount(Space4XWeapon.Missile(WeaponSize.Small)));
            }
        }

        private static WeaponMount CreateDefaultWeaponMount(in Space4XWeapon weapon)
        {
            return new WeaponMount
            {
                Weapon = weapon,
                CurrentTarget = Entity.Null,
                FireArcCenterOffsetDeg = (half)0f,
                IsEnabled = 1,
                ShotsFired = 0,
                ShotsHit = 0,
                SourceModule = Entity.Null,
                CoolingRating = (half)1f,
                Heat01 = 0f,
                HeatCapacity = 100f,
                HeatDissipation = 4f,
                HeatPerShot = 2f
            };
        }

        private void LogWeaponInputDiagnostic(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!emitWeaponInputDiagnostics)
            {
                return;
            }

            var now = UTime.unscaledTime;
            if (now < _nextWeaponInputDiagnosticTime)
            {
                return;
            }

            _nextWeaponInputDiagnosticTime = now + 0.35f;
            UnityEngine.Debug.Log($"[Space4XWeaponInput] {message}");
#endif
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
            input.PureKernelMode = pureMovementKernelMode ? (byte)1 : (byte)0;

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

            var leftHeld = cursorSteerWithLeftMouse &&
                           mouse.leftButton.isPressed &&
                           !Space4XRtsSelectionRectangleOverlay.IsSelectionDragActive;
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
            input.PureKernelMode = pureMovementKernelMode ? (byte)1 : (byte)0;
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
            else if (divineModeHotkey != Key.None && keyboard[divineModeHotkey].wasPressedThisFrame)
            {
                Space4XControlModeState.SetModeOrToggleVariant(Space4XControlMode.DivineHand);
            }
        }

        private bool ShouldHandleModeHotkeys()
        {
            if (_followPlayerVessel == null)
            {
                _followPlayerVessel = GetComponent<Space4XFollowPlayerVessel>();
            }

            // Follow camera owns mode hotkeys when present to avoid duplicate mode processing.
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
            _stationAnyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StationId>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });
            _colonyAnyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Space4XColony>(),
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
            _stationRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StationId>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _colonyRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Space4XColony>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _fallbackRenderableQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<VesselMovement>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });
            _asteroidAnyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Asteroid>(),
                    ComponentType.ReadOnly<LocalTransform>()
                }
            });
            _physicsWorldQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());

            _queriesReady = true;
            return true;
        }

        private void HandleClickTargetingInput()
        {
            if (!IsValidTarget(_flagship) || !TryGetTargetingModeFlag(out var selectedInMode2))
            {
                return;
            }

            if (Space4XTargetSelectionRectangleOverlay.IsPointerCaptureActive ||
                Space4XTargetSelectionRectangleOverlay.IsSelectionDragActive)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                return;
            }

            var camera = ResolveDrivingCamera();
            if (camera == null)
            {
                return;
            }

            var pointer = mouse.position.ReadValue();
            var ray = camera.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));
            if (TryResolveTargetFromScreenProxy(
                    camera,
                    pointer,
                    math.max(10f, mode2TargetRaycastDistance),
                    out var proxyTarget,
                    out var proxyPoint) &&
                proxyTarget != Entity.Null &&
                proxyTarget != _flagship &&
                IsValidTarget(proxyTarget))
            {
                ApplyClickTargetSelection(proxyTarget, proxyPoint, selectedInMode2);
                return;
            }

            if (TryRaycastForEntity(ray, math.max(10f, mode2TargetRaycastDistance), out var target, out var hitPoint) &&
                target != Entity.Null &&
                target != _flagship &&
                IsValidTarget(target))
            {
                ApplyClickTargetSelection(target, hitPoint, selectedInMode2);
                return;
            }

            if (TryResolveNearestTargetFromPoint(hitPoint, math.max(1f, mode2TargetFallbackRadius), out var nearest) &&
                nearest != Entity.Null &&
                nearest != _flagship &&
                IsValidTarget(nearest))
            {
                ApplyClickTargetSelection(nearest, hitPoint, selectedInMode2);
                return;
            }

            ClearClickTargetSelection();
        }

        private bool HandleMultiTargetSelectionInput()
        {
            if (!enableMultiTargetSelection || !IsValidTarget(_flagship))
            {
                return false;
            }

            if (!Space4XTargetSelectionRectangleOverlay.TryConsumePendingSelection(out var screenRect, out var append))
            {
                return false;
            }

            if (!IsMultiTargetSelectionModeActive())
            {
                return false;
            }

            var camera = ResolveDrivingCamera();
            if (camera == null)
            {
                return false;
            }

            _multiTargetCandidates.Clear();
            var maxDistance = math.max(10f, multiTargetMaxDistance);
            CollectMultiTargetCandidatesFromQuery(_fallbackRenderableQuery, camera, in screenRect, maxDistance);
            if (multiTargetIncludeAsteroids)
            {
                CollectMultiTargetCandidatesFromQuery(_asteroidAnyQuery, camera, in screenRect, maxDistance);
            }

            if (_multiTargetCandidates.Count == 0)
            {
                if (!append)
                {
                    ClearClickTargetSelection();
                }

                return true;
            }

            _multiTargetCandidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            ApplyMultiTargetCandidates(_multiTargetCandidates, append);
            _fighterLastRetargetTime = UTime.unscaledTime;
            return true;
        }

        private static bool IsMultiTargetSelectionModeActive()
        {
            var mode = Space4XControlModeState.CurrentMode;
            return mode == Space4XControlMode.CursorOrient || mode == Space4XControlMode.CruiseLook;
        }

        private void CollectMultiTargetCandidatesFromQuery(EntityQuery query, UCamera camera, in Rect screenRect, float maxDistance)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var cameraPosition = (float3)camera.transform.position;
            var center = screenRect.center;
            var hasSelfSide = _entityManager.HasComponent<ScenarioSide>(_flagship);
            var selfSide = hasSelfSide ? _entityManager.GetComponentData<ScenarioSide>(_flagship).Side : (byte)0;
            var distanceScale = math.max(0f, multiTargetDistanceScoreScale);

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == Entity.Null || candidate == _flagship || !_entityManager.Exists(candidate))
                {
                    continue;
                }

                if (FindMultiTargetCandidateIndex(_multiTargetCandidates, candidate) >= 0)
                {
                    continue;
                }

                if (_entityManager.HasComponent<HullIntegrity>(candidate) &&
                    _entityManager.GetComponentData<HullIntegrity>(candidate).Current <= 0f)
                {
                    continue;
                }

                var point = transforms[i].Position;
                var toCandidate = point - cameraPosition;
                var distance = math.length(toCandidate);
                if (distance <= 0.01f || distance > maxDistance)
                {
                    continue;
                }

                var projected = camera.WorldToScreenPoint(new Vector3(point.x, point.y, point.z));
                if (projected.z <= 0f)
                {
                    continue;
                }

                var projected2 = new Vector2(projected.x, projected.y);
                if (!screenRect.Contains(projected2))
                {
                    continue;
                }

                var dx = projected2.x - center.x;
                var dy = projected2.y - center.y;
                var score = math.sqrt(dx * dx + dy * dy) + (distance * distanceScale);
                score += ResolveMultiTargetRelationBias(candidate, hasSelfSide, selfSide);
                _multiTargetCandidates.Add(new MultiTargetCandidate
                {
                    Entity = candidate,
                    Point = point,
                    Score = score
                });
            }
        }

        private float ResolveMultiTargetRelationBias(Entity candidate, bool hasSelfSide, byte selfSide)
        {
            if (!hasSelfSide || !_entityManager.HasComponent<ScenarioSide>(candidate))
            {
                return 0f;
            }

            var candidateSide = _entityManager.GetComponentData<ScenarioSide>(candidate).Side;
            if (candidateSide == selfSide)
            {
                return math.max(0f, multiTargetFriendlyPenalty);
            }

            return -math.max(0f, multiTargetHostileBonus);
        }

        private void ApplyMultiTargetCandidates(IReadOnlyList<MultiTargetCandidate> candidates, bool append)
        {
            var maxLocks = math.max(1, multiTargetMaxLocks);
            _multiTargetEntriesScratch.Clear();
            if (append)
            {
                CollectExistingMultiTargetEntries(_multiTargetEntriesScratch, maxLocks);
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (_multiTargetEntriesScratch.Count >= maxLocks)
                {
                    break;
                }

                var candidate = candidates[i];
                if (FindMultiTargetEntryIndex(_multiTargetEntriesScratch, candidate.Entity) >= 0)
                {
                    continue;
                }

                _multiTargetEntriesScratch.Add(new Space4XPlayerTargetLockEntry
                {
                    TargetEntity = candidate.Entity,
                    LastKnownPoint = candidate.Point,
                    Score = candidate.Score,
                    PurposeMask = (byte)(Space4XPlayerTargetPurpose.Combat | Space4XPlayerTargetPurpose.Inspect),
                    IsPrimary = 0
                });
            }

            if (_multiTargetEntriesScratch.Count == 0)
            {
                if (!append)
                {
                    ClearClickTargetSelection();
                }

                return;
            }

            var selectedInMode2 = Space4XControlModeState.CurrentMode == Space4XControlMode.CruiseLook ? (byte)1 : (byte)0;
            var preferredPrimary = Entity.Null;
            if (append && TryGetCurrentTargetSelection(out var existingSelection))
            {
                preferredPrimary = existingSelection.TargetEntity;
            }

            var primaryIndex = 0;
            if (preferredPrimary != Entity.Null)
            {
                var preferredIndex = FindMultiTargetEntryIndex(_multiTargetEntriesScratch, preferredPrimary);
                if (preferredIndex >= 0)
                {
                    primaryIndex = preferredIndex;
                }
            }

            for (var i = 0; i < _multiTargetEntriesScratch.Count; i++)
            {
                var entry = _multiTargetEntriesScratch[i];
                entry.IsPrimary = (byte)(i == primaryIndex ? 1 : 0);
                _multiTargetEntriesScratch[i] = entry;
            }

            if (!TryEnsureTargetLockBuffer(_flagship, out var buffer))
            {
                return;
            }

            buffer.Clear();
            for (var i = 0; i < _multiTargetEntriesScratch.Count; i++)
            {
                buffer.Add(_multiTargetEntriesScratch[i]);
            }

            var primary = _multiTargetEntriesScratch[primaryIndex];
            ApplyClickTargetSelection(primary.TargetEntity, primary.LastKnownPoint, selectedInMode2, syncMultiTargetLock: false);
        }

        private void CollectExistingMultiTargetEntries(List<Space4XPlayerTargetLockEntry> entries, int maxLocks)
        {
            if (!_entityManager.HasBuffer<Space4XPlayerTargetLockEntry>(_flagship))
            {
                return;
            }

            var buffer = _entityManager.GetBuffer<Space4XPlayerTargetLockEntry>(_flagship);
            for (var i = 0; i < buffer.Length; i++)
            {
                if (entries.Count >= maxLocks)
                {
                    return;
                }

                var entry = buffer[i];
                if (!IsValidTarget(entry.TargetEntity) ||
                    FindMultiTargetEntryIndex(entries, entry.TargetEntity) >= 0)
                {
                    continue;
                }

                entry.IsPrimary = 0;
                entries.Add(entry);
            }
        }

        private static int FindMultiTargetEntryIndex(List<Space4XPlayerTargetLockEntry> entries, Entity entity)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].TargetEntity == entity)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindMultiTargetCandidateIndex(List<MultiTargetCandidate> entries, Entity entity)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Entity == entity)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleFighterAutoTargetingInput(Keyboard keyboard)
        {
            if (!IsValidTarget(_flagship) || !IsFighterModeActive())
            {
                return;
            }

            if (Space4XTargetSelectionRectangleOverlay.IsPointerCaptureActive ||
                Space4XTargetSelectionRectangleOverlay.IsSelectionDragActive)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var now = UTime.unscaledTime;
            var middleTapped = mouse.middleButton.wasPressedThisFrame;
            if (middleTapped && IsPointerOverUi())
            {
                return;
            }

            var clearByCtrlChord = middleTapped &&
                                   fighterManualClearWithCtrlMmb &&
                                   keyboard != null &&
                                   (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

            var clearByDoubleTap = false;
            if (middleTapped)
            {
                var clearWindow = math.max(0.05f, fighterManualClearDoubleTapWindowSeconds);
                clearByDoubleTap = now - _fighterLastMiddleTapTime <= clearWindow;
                _fighterLastMiddleTapTime = now;
            }

            if (clearByCtrlChord || clearByDoubleTap)
            {
                ClearClickTargetSelection();
                _fighterLastLockTime = float.NegativeInfinity;
                _fighterLastRetargetTime = now;
                return;
            }

            var camera = ResolveDrivingCamera();
            if (camera == null)
            {
                return;
            }

            var maxDistance = math.max(10f, fighterAutoMaxDistance);
            if (middleTapped)
            {
                if (TryFindBestFighterCenterTarget(
                        camera,
                        math.max(5f, fighterManualSnapConeDegrees),
                        maxDistance,
                        out var snapTarget,
                        out var snapPoint))
                {
                    ApplyClickTargetSelection(snapTarget, snapPoint, selectedInMode2: 0);
                    _fighterLastLockTime = now;
                }

                _fighterLastRetargetTime = now;
                return;
            }

            if (!enableFighterAutoTargeting)
            {
                return;
            }

            var retargetCooldown = math.max(0f, fighterAutoRetargetCooldownSeconds);
            if (now - _fighterLastRetargetTime < retargetCooldown)
            {
                return;
            }

            if (TryGetCurrentTargetSelection(out var selection))
            {
                var breakCone = math.max(math.max(2f, fighterAutoAcquireConeDegrees), fighterAutoBreakConeDegrees);
                if (TryResolveCameraTargetAngleAndDistance(camera, selection.TargetEntity, out var angle, out var distance) &&
                    angle <= breakCone &&
                    distance <= maxDistance)
                {
                    return;
                }

                var minLock = math.max(0f, fighterAutoMinLockSeconds);
                if (now - _fighterLastLockTime < minLock)
                {
                    return;
                }
            }

            if (TryFindBestFighterCenterTarget(
                    camera,
                    math.max(1f, fighterAutoAcquireConeDegrees),
                    maxDistance,
                    out var autoTarget,
                    out var autoPoint))
            {
                ApplyClickTargetSelection(autoTarget, autoPoint, selectedInMode2: 0);
                _fighterLastLockTime = now;
            }

            _fighterLastRetargetTime = now;
        }

        private static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private static bool IsFighterModeActive()
        {
            return Space4XControlModeState.CurrentMode == Space4XControlMode.CursorOrient &&
                   Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient);
        }

        private bool TryGetCurrentTargetSelection(out Space4XPlayerTargetSelection selection)
        {
            selection = Space4XPlayerTargetSelection.None;
            if (!IsValidTarget(_flagship) || !_entityManager.HasComponent<Space4XPlayerTargetSelection>(_flagship))
            {
                return false;
            }

            selection = _entityManager.GetComponentData<Space4XPlayerTargetSelection>(_flagship);
            return selection.HasSelection != 0 && IsValidTarget(selection.TargetEntity);
        }

        private bool TryResolveCameraTargetAngleAndDistance(UCamera camera, Entity target, out float angleDeg, out float distance)
        {
            angleDeg = 180f;
            distance = float.MaxValue;
            if (camera == null || !IsValidTarget(target))
            {
                return false;
            }

            var cameraForward = math.normalizesafe((float3)camera.transform.forward, new float3(0f, 0f, 1f));
            var toTarget = _entityManager.GetComponentData<LocalTransform>(target).Position - (float3)camera.transform.position;
            distance = math.length(toTarget);
            if (distance <= 0.01f)
            {
                return false;
            }

            var dot = math.dot(cameraForward, toTarget / distance);
            if (dot <= 0f)
            {
                return false;
            }

            angleDeg = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
            return true;
        }

        private bool TryFindBestFighterCenterTarget(
            UCamera camera,
            float coneDegrees,
            float maxDistance,
            out Entity target,
            out float3 worldPoint)
        {
            target = Entity.Null;
            worldPoint = float3.zero;
            if (camera == null || _fallbackRenderableQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var clampedCone = math.clamp(coneDegrees, 1f, 120f);
            var maxDistanceSafe = math.max(10f, maxDistance);
            var distanceScale = math.max(0f, fighterAutoDistanceScoreScale);
            var hasSelfSide = _entityManager.HasComponent<ScenarioSide>(_flagship);
            var selfSide = hasSelfSide ? _entityManager.GetComponentData<ScenarioSide>(_flagship).Side : (byte)0;
            var cameraPosition = (float3)camera.transform.position;
            var cameraForward = math.normalizesafe((float3)camera.transform.forward, new float3(0f, 0f, 1f));
            var bestScore = float.MaxValue;

            using var entities = _fallbackRenderableQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _fallbackRenderableQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == Entity.Null || candidate == _flagship || !_entityManager.Exists(candidate))
                {
                    continue;
                }

                if (_entityManager.HasComponent<HullIntegrity>(candidate) &&
                    _entityManager.GetComponentData<HullIntegrity>(candidate).Current <= 0f)
                {
                    continue;
                }

                var candidatePosition = transforms[i].Position;
                var toCandidate = candidatePosition - cameraPosition;
                var distance = math.length(toCandidate);
                if (distance <= 0.01f || distance > maxDistanceSafe)
                {
                    continue;
                }

                var direction = toCandidate / distance;
                var dot = math.dot(cameraForward, direction);
                if (dot <= 0f)
                {
                    continue;
                }

                var angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                if (angle > clampedCone)
                {
                    continue;
                }

                var score = angle + (distance * distanceScale);
                score += ResolveFighterRelationScoreBias(candidate, hasSelfSide, selfSide);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = candidate;
                worldPoint = candidatePosition;
            }

            return target != Entity.Null;
        }

        private float ResolveFighterRelationScoreBias(Entity candidate, bool hasSelfSide, byte selfSide)
        {
            if (!hasSelfSide || !_entityManager.HasComponent<ScenarioSide>(candidate))
            {
                return 0f;
            }

            var candidateSide = _entityManager.GetComponentData<ScenarioSide>(candidate).Side;
            if (candidateSide == selfSide)
            {
                return math.max(0f, fighterAutoFriendlyCenterPenaltyDegrees);
            }

            return -math.max(0f, fighterAutoHostileCenterBonusDegrees);
        }

        private bool TryGetTargetingModeFlag(out byte selectedInMode2)
        {
            selectedInMode2 = 0;

            var mode = Space4XControlModeState.CurrentMode;
            if (mode == Space4XControlMode.CruiseLook)
            {
                if (!enableMode2ClickTargeting)
                {
                    return false;
                }

                selectedInMode2 = 1;
                return true;
            }

            if (mode == Space4XControlMode.CursorOrient &&
                !Space4XControlModeState.IsVariantEnabled(Space4XControlMode.CursorOrient))
            {
                return enableMode1DefaultClickTargeting;
            }

            return false;
        }

        private void ApplyClickTargetSelection(Entity target, float3 hitPoint, byte selectedInMode2, bool syncMultiTargetLock = true)
        {
            if (!IsValidTarget(_flagship) || !IsValidTarget(target))
            {
                return;
            }

            UpsertTargetSelection(_flagship, new Space4XPlayerTargetSelection
            {
                TargetEntity = target,
                TargetPoint = hitPoint,
                HasSelection = 1,
                SelectedInMode2 = selectedInMode2
            });

            if (_entityManager.HasComponent<TargetPriority>(_flagship))
            {
                var priority = _entityManager.GetComponentData<TargetPriority>(_flagship);
                priority.CurrentTarget = target;
                priority.EngagementDuration = 0f;
                priority.ForceReevaluate = 0;
                _entityManager.SetComponentData(_flagship, priority);
            }

            if (syncMultiTargetLock)
            {
                UpsertPrimaryTargetLock(target, hitPoint);
            }
        }

        private void ClearClickTargetSelection()
        {
            if (!IsValidTarget(_flagship))
            {
                return;
            }

            UpsertTargetSelection(_flagship, Space4XPlayerTargetSelection.None);

            if (_entityManager.HasComponent<TargetPriority>(_flagship))
            {
                var priority = _entityManager.GetComponentData<TargetPriority>(_flagship);
                priority.CurrentTarget = Entity.Null;
                priority.CurrentScore = 0f;
                priority.EngagementDuration = 0f;
                priority.ForceReevaluate = 1;
                _entityManager.SetComponentData(_flagship, priority);
            }

            ClearMultiTargetLocks(_flagship);
        }

        private void UpsertPrimaryTargetLock(Entity target, float3 hitPoint)
        {
            if (!enableMultiTargetSelection || !IsValidTarget(_flagship) || !IsValidTarget(target))
            {
                return;
            }

            if (!TryEnsureTargetLockBuffer(_flagship, out var buffer))
            {
                return;
            }

            var maxLocks = math.max(1, multiTargetMaxLocks);
            _multiTargetEntriesScratch.Clear();
            for (var i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (!IsValidTarget(entry.TargetEntity) ||
                    FindMultiTargetEntryIndex(_multiTargetEntriesScratch, entry.TargetEntity) >= 0)
                {
                    continue;
                }

                entry.IsPrimary = 0;
                _multiTargetEntriesScratch.Add(entry);
                if (_multiTargetEntriesScratch.Count >= maxLocks)
                {
                    break;
                }
            }

            var primaryIndex = FindMultiTargetEntryIndex(_multiTargetEntriesScratch, target);
            if (primaryIndex < 0)
            {
                if (_multiTargetEntriesScratch.Count >= maxLocks)
                {
                    _multiTargetEntriesScratch.RemoveAt(_multiTargetEntriesScratch.Count - 1);
                }

                _multiTargetEntriesScratch.Insert(0, new Space4XPlayerTargetLockEntry
                {
                    TargetEntity = target,
                    LastKnownPoint = hitPoint,
                    Score = 0f,
                    PurposeMask = (byte)(Space4XPlayerTargetPurpose.Combat | Space4XPlayerTargetPurpose.Inspect),
                    IsPrimary = 1
                });
                primaryIndex = 0;
            }
            else
            {
                var primary = _multiTargetEntriesScratch[primaryIndex];
                primary.LastKnownPoint = hitPoint;
                primary.IsPrimary = 1;
                primary.Score = 0f;
                _multiTargetEntriesScratch[primaryIndex] = primary;
            }

            for (var i = 0; i < _multiTargetEntriesScratch.Count; i++)
            {
                var entry = _multiTargetEntriesScratch[i];
                entry.IsPrimary = (byte)(i == primaryIndex ? 1 : 0);
                _multiTargetEntriesScratch[i] = entry;
            }

            buffer.Clear();
            for (var i = 0; i < _multiTargetEntriesScratch.Count; i++)
            {
                buffer.Add(_multiTargetEntriesScratch[i]);
            }
        }

        private void ClearMultiTargetLocks(Entity entity)
        {
            if (!IsValidTarget(entity) || !_entityManager.HasBuffer<Space4XPlayerTargetLockEntry>(entity))
            {
                return;
            }

            var buffer = _entityManager.GetBuffer<Space4XPlayerTargetLockEntry>(entity);
            buffer.Clear();
        }

        private bool TryEnsureTargetLockBuffer(Entity entity, out DynamicBuffer<Space4XPlayerTargetLockEntry> buffer)
        {
            buffer = default;
            if (!IsValidTarget(entity))
            {
                return false;
            }

            if (_entityManager.HasBuffer<Space4XPlayerTargetLockEntry>(entity))
            {
                buffer = _entityManager.GetBuffer<Space4XPlayerTargetLockEntry>(entity);
                return true;
            }

            if (_targetLockBufferOverflowEntity == entity)
            {
                return false;
            }

            try
            {
                buffer = _entityManager.AddBuffer<Space4XPlayerTargetLockEntry>(entity);
                _targetLockBufferOverflowEntity = Entity.Null;
                return true;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _targetLockBufferOverflowEntity = entity;
                if (!_targetLockBufferCapacityWarned)
                {
                    _targetLockBufferCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Space4XPlayerTargetLockEntry add skipped: entity archetype is at chunk capacity.");
                }

                return false;
            }
        }

        private bool TryResolveTargetFromScreenProxy(
            UCamera camera,
            Vector2 pointer,
            float maxDistance,
            out Entity target,
            out float3 worldPoint)
        {
            target = Entity.Null;
            worldPoint = float3.zero;
            if (!enableScreenSpaceTargetProxy || camera == null)
            {
                return false;
            }

            var radius = Mathf.Max(4f, targetProxyPixelRadius);
            var radiusSq = radius * radius;
            var bestScore = float.MaxValue;
            var maxDepth = math.max(10f, maxDistance);
            TryResolveTargetFromScreenProxyQuery(_fallbackRenderableQuery, camera, pointer, radiusSq, maxDepth, ref bestScore, ref target, ref worldPoint);
            TryResolveTargetFromScreenProxyQuery(_asteroidAnyQuery, camera, pointer, radiusSq, maxDepth, ref bestScore, ref target, ref worldPoint);
            return target != Entity.Null;
        }

        private void TryResolveTargetFromScreenProxyQuery(
            EntityQuery query,
            UCamera camera,
            Vector2 pointer,
            float radiusSq,
            float maxDepth,
            ref float bestScore,
            ref Entity target,
            ref float3 worldPoint)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == Entity.Null || candidate == _flagship || !_entityManager.Exists(candidate))
                {
                    continue;
                }

                var world = transforms[i].Position;
                var projected = camera.WorldToScreenPoint(new Vector3(world.x, world.y, world.z));
                if (projected.z <= 0f || projected.z > maxDepth)
                {
                    continue;
                }

                var dx = projected.x - pointer.x;
                var dy = projected.y - pointer.y;
                var pixelDistanceSq = dx * dx + dy * dy;
                if (pixelDistanceSq > radiusSq)
                {
                    continue;
                }

                var score = pixelDistanceSq + (projected.z * Mathf.Max(0f, targetProxyDepthBias));
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = candidate;
                worldPoint = world;
            }
        }

        private bool TryRaycastForEntity(UnityEngine.Ray ray, float maxDistance, out Entity entity, out float3 worldPoint)
        {
            entity = Entity.Null;
            worldPoint = ResolveRayProjectionOnFlagshipPlane(ray, maxDistance);

            if (_physicsWorldQuery.IsEmptyIgnoreFilter == false)
            {
                var physicsWorld = _physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();
                var input = new RaycastInput
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * maxDistance,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(input, out var hit))
                {
                    entity = hit.Entity;
                    worldPoint = hit.Position;
                    return true;
                }
            }

            if (UnityEngine.Physics.Raycast(
                    ray,
                    out UnityEngine.RaycastHit hit3d,
                    maxDistance,
                    mode2TargetLayerMask.value,
                    QueryTriggerInteraction.Ignore))
            {
                worldPoint = new float3(hit3d.point.x, hit3d.point.y, hit3d.point.z);
                if (hit3d.collider != null)
                {
                    var bridge = hit3d.collider.GetComponent<IEntityBridge>();
                    if (bridge != null && bridge.TryGetEntity(out var bridged))
                    {
                        entity = bridged;
                    }
                }

                return entity != Entity.Null;
            }

            return false;
        }

        private float3 ResolveRayProjectionOnFlagshipPlane(UnityEngine.Ray ray, float maxDistance)
        {
            var fallbackY = 0f;
            if (IsValidTarget(_flagship))
            {
                fallbackY = _entityManager.GetComponentData<LocalTransform>(_flagship).Position.y;
            }

            var plane = new UnityEngine.Plane(Vector3.up, new Vector3(0f, fallbackY, 0f));
            if (plane.Raycast(ray, out var enter))
            {
                var distance = Mathf.Clamp(enter, 0f, maxDistance);
                var point = ray.GetPoint(distance);
                return new float3(point.x, point.y, point.z);
            }

            var clamped = ray.GetPoint(Mathf.Clamp(maxDistance * 0.25f, 1f, maxDistance));
            return new float3(clamped.x, clamped.y, clamped.z);
        }

        private bool TryResolveNearestTargetFromPoint(float3 point, float radius, out Entity nearest)
        {
            nearest = Entity.Null;
            var radiusSq = radius * radius;
            var bestDistanceSq = radiusSq;

            if (TryResolveNearestFromQuery(_fallbackRenderableQuery, point, ref bestDistanceSq, ref nearest))
            {
                return true;
            }

            if (TryResolveNearestFromQuery(_asteroidAnyQuery, point, ref bestDistanceSq, ref nearest))
            {
                return true;
            }

            return nearest != Entity.Null;
        }

        private bool TryResolveNearestFromQuery(EntityQuery query, float3 point, ref float bestDistanceSq, ref Entity nearest)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var found = false;
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == Entity.Null || candidate == _flagship || !_entityManager.Exists(candidate))
                {
                    continue;
                }

                var distanceSq = math.lengthsq(transforms[i].Position - point);
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                nearest = candidate;
                found = true;
            }

            return found;
        }

        private void UpsertTargetSelection(Entity entity, in Space4XPlayerTargetSelection selection)
        {
            if (!IsValidTarget(entity))
            {
                return;
            }

            if (_entityManager.HasComponent<Space4XPlayerTargetSelection>(entity))
            {
                _entityManager.SetComponentData(entity, selection);
                return;
            }

            if (_targetSelectionOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, selection);
                _targetSelectionOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _targetSelectionOverflowEntity = entity;
                if (_targetSelectionCapacityWarned)
                {
                    return;
                }

                _targetSelectionCapacityWarned = true;
                UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Space4XPlayerTargetSelection add skipped: entity archetype is at chunk capacity.");
            }
        }

        private Space4XPlayerWeaponControl ResolvePlayerWeaponControl(Entity entity)
        {
            if (_entityManager.HasComponent<Space4XPlayerWeaponControl>(entity))
            {
                return _entityManager.GetComponentData<Space4XPlayerWeaponControl>(entity);
            }

            return new Space4XPlayerWeaponControl
            {
                ManualAimMode = manualAimDefaultEnabled ? (byte)1 : (byte)0,
                TriggerHeld = 0
            };
        }

        private void UpsertPlayerWeaponControl(Entity entity, in Space4XPlayerWeaponControl control)
        {
            if (!IsValidTarget(entity))
            {
                return;
            }

            if (_entityManager.HasComponent<Space4XPlayerWeaponControl>(entity))
            {
                _entityManager.SetComponentData(entity, control);
                return;
            }

            if (_playerWeaponControlOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, control);
                _playerWeaponControlOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _playerWeaponControlOverflowEntity = entity;
                if (_playerWeaponControlCapacityWarned)
                {
                    return;
                }

                _playerWeaponControlCapacityWarned = true;
                UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Space4XPlayerWeaponControl add skipped: entity archetype is at chunk capacity.");
            }
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

        private Entity PickNearestToCameraControllable(EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
                return Entity.Null;

            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return Entity.Null;

            var cameraPosition = transform.position;
            var consumed = new bool[entities.Length];
            for (var attempt = 0; attempt < entities.Length; attempt++)
            {
                var bestIndex = -1;
                var bestDistanceSq = float.MaxValue;
                for (var i = 0; i < entities.Length; i++)
                {
                    if (consumed[i])
                    {
                        continue;
                    }

                    var entity = entities[i];
                    if (!_entityManager.HasComponent<LocalToWorld>(entity))
                    {
                        continue;
                    }

                    var ltw = _entityManager.GetComponentData<LocalToWorld>(entity);
                    var worldPos = new Vector3(ltw.Position.x, ltw.Position.y, ltw.Position.z);
                    var distanceSq = (worldPos - cameraPosition).sqrMagnitude;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                consumed[bestIndex] = true;
                var candidate = entities[bestIndex];
                if (CanAcceptPlayerFlightInput(candidate))
                {
                    return candidate;
                }
            }

            return Entity.Null;
        }

        private bool CanAcceptPlayerFlightInput(Entity entity)
        {
            if (!IsValidTarget(entity))
            {
                return false;
            }

            if (!CanAcceptFlightProfile(entity))
            {
                return false;
            }

            if (!CanAcceptFlightRuntimeState(entity))
            {
                return false;
            }

            if (_entityManager.HasComponent<PlayerFlagshipFlightInput>(entity))
            {
                return true;
            }

            if (_playerFlightInputOverflowEntity == entity)
            {
                return false;
            }

            try
            {
                _entityManager.AddComponentData(entity, PlayerFlagshipFlightInput.Disabled);
                _playerFlightInputOverflowEntity = Entity.Null;
                return true;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _playerFlightInputOverflowEntity = entity;
                if (!_playerFlightInputCapacityWarned)
                {
                    _playerFlightInputCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Skipping oversized flagship candidate: cannot attach PlayerFlagshipFlightInput.");
                }

                return false;
            }
        }

        private bool CanAcceptFlightProfile(Entity entity)
        {
            if (_entityManager.HasComponent<ShipFlightProfile>(entity))
            {
                return true;
            }

            if (_flightProfileOverflowEntity == entity)
            {
                return false;
            }

            var fallback = Space4XRunStartSelection.FlightProfile;
            if (!fallback.IsConfigured)
            {
                fallback = BuildProfileFromCurrentFields();
            }

            fallback = fallback.Sanitized();
            try
            {
                _entityManager.AddComponentData(entity, fallback);
                _flightProfileOverflowEntity = Entity.Null;
                return true;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _flightProfileOverflowEntity = entity;
                if (!_flightProfileCapacityWarned)
                {
                    _flightProfileCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Skipping oversized flagship candidate: cannot attach ShipFlightProfile.");
                }

                return false;
            }
        }

        private bool CanAcceptFlightRuntimeState(Entity entity)
        {
            if (_entityManager.HasComponent<ShipFlightRuntimeState>(entity))
            {
                return true;
            }

            if (_flightRuntimeStateOverflowEntity == entity)
            {
                return false;
            }

            ShipFlightProfile profile;
            if (_entityManager.HasComponent<ShipFlightProfile>(entity))
            {
                profile = _entityManager.GetComponentData<ShipFlightProfile>(entity).Sanitized();
            }
            else
            {
                profile = BuildProfileFromCurrentFields().Sanitized();
            }

            var runtime = CreateDefaultFlightRuntimeState(profile, float3.zero);
            try
            {
                _entityManager.AddComponentData(entity, runtime);
                _flightRuntimeStateOverflowEntity = Entity.Null;
                return true;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _flightRuntimeStateOverflowEntity = entity;
                if (!_flightRuntimeStateCapacityWarned)
                {
                    _flightRuntimeStateCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] Skipping oversized flagship candidate: cannot attach ShipFlightRuntimeState.");
                }

                return false;
            }
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

        private Space4XRunStartAnchor ResolveAutoAnchorPreference()
        {
            var presetId = Space4XRunStartSelection.ShipPresetId;
            if (!string.IsNullOrWhiteSpace(presetId))
            {
                if (presetId.Contains("station", StringComparison.OrdinalIgnoreCase))
                {
                    return Space4XRunStartAnchor.Station;
                }

                if (presetId.Contains("colony", StringComparison.OrdinalIgnoreCase))
                {
                    return Space4XRunStartAnchor.Colony;
                }
            }

            return Space4XRunStartAnchor.Ship;
        }

        private Entity PickCandidateByAnchor(Space4XRunStartAnchor anchor, bool preferCarrier)
        {
            return anchor switch
            {
                Space4XRunStartAnchor.Ship => PickShipCandidate(preferCarrier),
                Space4XRunStartAnchor.Station => PickStationCandidate(),
                Space4XRunStartAnchor.Colony => PickColonyCandidate(),
                _ => PickShipCandidate(preferCarrier)
            };
        }

        private Entity PickShipCandidate(bool preferCarrier)
        {
            var candidate = preferCarrier
                ? PickNearestToCameraControllable(_carrierRenderableQuery)
                : PickNearestToCameraControllable(_miningRenderableQuery);

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCameraControllable(_carrierAnyQuery)
                    : PickNearestToCameraControllable(_miningAnyQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCameraControllable(_miningRenderableQuery)
                    : PickNearestToCameraControllable(_carrierRenderableQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = preferCarrier
                    ? PickNearestToCameraControllable(_miningAnyQuery)
                    : PickNearestToCameraControllable(_carrierAnyQuery);
            }

            if (candidate == Entity.Null)
            {
                candidate = PickNearestToCameraControllable(_fallbackRenderableQuery);
            }

            return candidate;
        }

        private Entity PickStationCandidate()
        {
            var candidate = PickNearestToCameraControllable(_stationRenderableQuery);
            if (candidate == Entity.Null)
            {
                candidate = PickNearestToCameraControllable(_stationAnyQuery);
            }

            return candidate;
        }

        private Entity PickColonyCandidate()
        {
            var candidate = PickNearestToCameraControllable(_colonyRenderableQuery);
            if (candidate == Entity.Null)
            {
                candidate = PickNearestToCameraControllable(_colonyAnyQuery);
            }

            return candidate;
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

                var baseTurnSpeedRadians = movement.TurnSpeed > 0f ? movement.TurnSpeed : 2f;
                var baseTurnSpeedDegrees = baseTurnSpeedRadians * Mathf.Rad2Deg;
                var derivedMaxAngularSpeed = Mathf.Clamp(
                    baseTurnSpeedDegrees * Mathf.Max(0.01f, angularSpeedFromVesselMultiplier),
                    minInheritedAngularSpeedDegrees,
                    maxInheritedAngularSpeedDegrees);
                var derivedAngularAcceleration = Mathf.Clamp(
                    derivedMaxAngularSpeed * Mathf.Max(0.01f, angularAccelerationFromVesselMultiplier),
                    minInheritedAngularAccelerationDegrees,
                    maxInheritedAngularAccelerationDegrees);
                var derivedAngularDamping = Mathf.Clamp(
                    derivedMaxAngularSpeed * Mathf.Max(0.01f, angularDampingFromVesselMultiplier),
                    minInheritedAngularAccelerationDegrees,
                    maxInheritedAngularAccelerationDegrees);
                profile.MaxAngularSpeedDegrees = derivedMaxAngularSpeed;
                profile.AngularAccelerationDegrees = derivedAngularAcceleration;
                profile.AngularDampingDegrees = derivedAngularDamping;

                var derivedTurnAuthority = profile.TurnAuthorityAtMaxSpeed;
                if (_entityManager.HasComponent<ModuleCapabilityOutput>(entity))
                {
                    var capability = _entityManager.GetComponentData<ModuleCapabilityOutput>(entity);
                    if (capability.TurnAuthority > 0f)
                    {
                        derivedTurnAuthority = Mathf.Clamp((float)capability.TurnAuthority, 0.05f, 1f);
                    }
                }

                if (_entityManager.HasComponent<EnginePerformanceOutput>(entity))
                {
                    var engineOutput = _entityManager.GetComponentData<EnginePerformanceOutput>(entity);
                    if (engineOutput.TurnAuthority > 0f)
                    {
                        derivedTurnAuthority = Mathf.Clamp((float)engineOutput.TurnAuthority, 0.05f, 1f);
                    }
                }

                profile.TurnAuthorityAtMaxSpeed = derivedTurnAuthority;
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
            maxAngularSpeedDegrees = profile.MaxAngularSpeedDegrees;
            angularAccelerationDegrees = profile.AngularAccelerationDegrees;
            angularDampingDegrees = profile.AngularDampingDegrees;
            angularDeadbandDegrees = profile.AngularDeadbandDegrees;
            maxCursorLeadDegrees = profile.MaxCursorLeadDegrees;
            turnAuthorityAtMaxSpeed = profile.TurnAuthorityAtMaxSpeed;
            angularOvershootRatio = profile.AngularOvershootRatio;
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

                UpsertSkipDriveModuleConfig(entity, config);

                EnsureSkipJumpState(entity);
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

                UpsertTimeCoreModuleConfig(entity, config);
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

                UpsertBoostDriveModuleConfig(entity, config);
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
                TrySetOrAddShipAbilitySelection(_flagship, next);
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
                TrySetOrAddShipAbilitySelection(entity, desired);
            }
        }

        private bool TrySetOrAddShipAbilitySelection(Entity entity, ShipAbilityKind ability)
        {
            if (!IsValidTarget(entity))
                return false;

            if (_entityManager.HasComponent<ShipAbilityModule>(entity))
            {
                _entityManager.SetComponentData(entity, new ShipAbilityModule { Kind = ability });
                return true;
            }

            if (_shipAbilityModuleAddSuppressed)
            {
                return false;
            }

            try
            {
                _entityManager.AddComponentData(entity, new ShipAbilityModule { Kind = ability });
                return true;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _shipAbilityModuleAddSuppressed = true;
                UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] ShipAbilityModule add skipped: entity archetype is at chunk capacity. Ability selection falls back to available module configs.");
                return false;
            }
        }

        private static bool IsArchetypeCapacityException(InvalidOperationException ex)
        {
            return ex != null &&
                   ex.Message != null &&
                   ex.Message.IndexOf("Entity archetype component data is too large", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpsertBoostDriveModuleConfig(Entity entity, in BoostDriveModuleConfig config)
        {
            if (_entityManager.HasComponent<BoostDriveModuleConfig>(entity))
            {
                _entityManager.SetComponentData(entity, config);
                return;
            }

            if (_boostDriveConfigOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, config);
                _boostDriveConfigOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _boostDriveConfigOverflowEntity = entity;
                if (!_boostDriveConfigCapacityWarned)
                {
                    _boostDriveConfigCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] BoostDriveModuleConfig add skipped: entity archetype is at chunk capacity.");
                }
            }
        }

        private void UpsertTimeCoreModuleConfig(Entity entity, in TimeCoreModuleConfig config)
        {
            if (_entityManager.HasComponent<TimeCoreModuleConfig>(entity))
            {
                _entityManager.SetComponentData(entity, config);
                return;
            }

            if (_timeCoreConfigOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, config);
                _timeCoreConfigOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _timeCoreConfigOverflowEntity = entity;
                if (!_timeCoreConfigCapacityWarned)
                {
                    _timeCoreConfigCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] TimeCoreModuleConfig add skipped: entity archetype is at chunk capacity.");
                }
            }
        }

        private void UpsertSkipDriveModuleConfig(Entity entity, in SkipDriveModuleConfig config)
        {
            if (_entityManager.HasComponent<SkipDriveModuleConfig>(entity))
            {
                _entityManager.SetComponentData(entity, config);
                return;
            }

            if (_skipDriveConfigOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, config);
                _skipDriveConfigOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _skipDriveConfigOverflowEntity = entity;
                if (!_skipDriveConfigCapacityWarned)
                {
                    _skipDriveConfigCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] SkipDriveModuleConfig add skipped: entity archetype is at chunk capacity.");
                }
            }
        }

        private void EnsureSkipJumpState(Entity entity)
        {
            if (_entityManager.HasComponent<SkipJumpState>(entity))
            {
                return;
            }

            if (_skipJumpStateOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, new SkipJumpState());
                _skipJumpStateOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _skipJumpStateOverflowEntity = entity;
                if (!_skipJumpStateCapacityWarned)
                {
                    _skipJumpStateCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] SkipJumpState add skipped: entity archetype is at chunk capacity.");
                }
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
            if (_flightProfileOverflowEntity == entity)
            {
                return fallback;
            }

            UpsertFlightProfile(entity, fallback);
            return fallback;
        }

        private static ShipFlightRuntimeState CreateDefaultFlightRuntimeState(in ShipFlightProfile profile, in float3 velocityWorld)
        {
            return new ShipFlightRuntimeState
            {
                VelocityWorld = velocityWorld,
                InertialDampenersEnabled = profile.DefaultInertialDampenersEnabled != 0 ? (byte)1 : (byte)0,
                AngularSpeedRadians = 0f,
                ForwardThrottle = 0f,
                StrafeThrottle = 0f,
                VerticalThrottle = 0f
            };
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

            var created = CreateDefaultFlightRuntimeState(profile, _flagshipVelocityWorld);
            if (_flightRuntimeStateOverflowEntity == entity)
            {
                return created;
            }

            try
            {
                _entityManager.AddComponentData(entity, created);
                _flightRuntimeStateOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _flightRuntimeStateOverflowEntity = entity;
                if (!_flightRuntimeStateCapacityWarned)
                {
                    _flightRuntimeStateCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] ShipFlightRuntimeState add skipped: entity archetype is at chunk capacity. Manual flight runtime falls back to transient state.");
                }
            }

            return created;
        }

        private void SetFlightRuntimeState(Entity entity, in ShipFlightRuntimeState runtimeState)
        {
            if (_entityManager.HasComponent<ShipFlightRuntimeState>(entity))
            {
                _entityManager.SetComponentData(entity, runtimeState);
                return;
            }

            if (_flightRuntimeStateOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, runtimeState);
                _flightRuntimeStateOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _flightRuntimeStateOverflowEntity = entity;
                if (!_flightRuntimeStateCapacityWarned)
                {
                    _flightRuntimeStateCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] ShipFlightRuntimeState add skipped during update: entity archetype is at chunk capacity.");
                }
            }
        }

        private PlayerFlagshipFlightInput ResolveFlightInputIntent(Entity entity)
        {
            if (_entityManager.HasComponent<InputKernelLocomotionIntent>(entity))
            {
                var kernelIntent = _entityManager.GetComponentData<InputKernelLocomotionIntent>(entity);
                return PlayerFlagshipFlightInput.FromKernelLocomotionIntent(kernelIntent);
            }

            if (_entityManager.HasComponent<PlayerFlagshipFlightInput>(entity))
            {
                var input = _entityManager.GetComponentData<PlayerFlagshipFlightInput>(entity);
                UpsertKernelLocomotionIntent(entity, input.ToKernelLocomotionIntent(0u));
                return input;
            }

            var created = PlayerFlagshipFlightInput.Disabled;
            if (_playerFlightInputOverflowEntity == entity)
            {
                return created;
            }

            try
            {
                _entityManager.AddComponentData(entity, created);
                _playerFlightInputOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _playerFlightInputOverflowEntity = entity;
                if (!_playerFlightInputCapacityWarned)
                {
                    _playerFlightInputCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] PlayerFlagshipFlightInput add skipped: entity archetype is at chunk capacity. Manual input is suppressed for this claim target.");
                }
            }

            UpsertKernelLocomotionIntent(entity, created.ToKernelLocomotionIntent(0u));
            return created;
        }

        private void SetFlightInputIntent(Entity entity, in PlayerFlagshipFlightInput input)
        {
            UpsertKernelLocomotionIntent(entity, input.ToKernelLocomotionIntent(0u));

            if (_entityManager.HasComponent<PlayerFlagshipFlightInput>(entity))
            {
                _entityManager.SetComponentData(entity, input);
                return;
            }

            if (_playerFlightInputOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, input);
                _playerFlightInputOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _playerFlightInputOverflowEntity = entity;
                if (!_playerFlightInputCapacityWarned)
                {
                    _playerFlightInputCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] PlayerFlagshipFlightInput add skipped during update: entity archetype is at chunk capacity.");
                }
            }
        }

        private void UpsertKernelLocomotionIntent(Entity entity, in InputKernelLocomotionIntent intent)
        {
            if (_entityManager.HasComponent<InputKernelLocomotionIntent>(entity))
            {
                _entityManager.SetComponentData(entity, intent);
                return;
            }

            if (_kernelLocomotionIntentOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, intent);
                _kernelLocomotionIntentOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _kernelLocomotionIntentOverflowEntity = entity;
                if (!_kernelLocomotionIntentCapacityWarned)
                {
                    _kernelLocomotionIntentCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] InputKernelLocomotionIntent add skipped: entity archetype is at chunk capacity.");
                }
            }
        }

        private void UpsertFlightProfile(Entity entity, in ShipFlightProfile profile)
        {
            if (_entityManager.HasComponent<ShipFlightProfile>(entity))
            {
                _entityManager.SetComponentData(entity, profile);
                return;
            }

            if (_flightProfileOverflowEntity == entity)
            {
                return;
            }

            try
            {
                _entityManager.AddComponentData(entity, profile);
                _flightProfileOverflowEntity = Entity.Null;
            }
            catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
            {
                _flightProfileOverflowEntity = entity;
                if (!_flightProfileCapacityWarned)
                {
                    _flightProfileCapacityWarned = true;
                    UnityEngine.Debug.LogWarning("[Space4XPlayerFlagshipController] ShipFlightProfile add skipped: entity archetype is at chunk capacity.");
                }
            }
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
                MaxAngularSpeedDegrees = maxAngularSpeedDegrees,
                AngularAccelerationDegrees = angularAccelerationDegrees,
                AngularDampingDegrees = angularDampingDegrees,
                AngularDeadbandDegrees = angularDeadbandDegrees,
                MaxCursorLeadDegrees = maxCursorLeadDegrees,
                TurnAuthorityAtMaxSpeed = turnAuthorityAtMaxSpeed,
                AngularOvershootRatio = angularOvershootRatio,
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

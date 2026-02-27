using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Input;
using Space4X.Registry;
using Space4X.Runtime.Interaction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using HandStateData = PureDOTS.Runtime.Hand.HandState;
using Unity.Physics;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Hand.HandAffordanceSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Hand.HandCommandEmitterSystem))]
    public partial struct Space4XHandCommandStateSystem : ISystem
    {
        private const float DefaultHoldDistance = 10f;
        private const float ScrollAdjustSpeed = 5f;
        private const float ThrowSpeed = 20f;
        private const float MaxChargeSeconds = 1.2f;

        private ComponentLookup<HandPickable> _pickableLookup;
        private ComponentLookup<Space4XHandPickable> _spacePickableLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
        private ComponentLookup<Space4XCelestialManipulable> _celestialLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SelectionOwner> _selectionOwnerLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<FactionResources> _factionResourcesLookup;
        private EntityQuery _playerFlagshipQuery;
        private EntityQuery _ownedInfluenceQuery;
        private NativeParallelHashSet<Entity> _loggedOutOfInfluenceEntities;
        private uint _lastInputSampleId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<HandAffordances>();
            state.RequireForUpdate<HandStateData>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();
            _pickableLookup = state.GetComponentLookup<HandPickable>(true);
            _spacePickableLookup = state.GetComponentLookup<Space4XHandPickable>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _celestialLookup = state.GetComponentLookup<Space4XCelestialManipulable>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _selectionOwnerLookup = state.GetComponentLookup<SelectionOwner>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _factionResourcesLookup = state.GetComponentLookup<FactionResources>(true);
            _playerFlagshipQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerFlagshipTag, LocalTransform>()
                .Build();
            _ownedInfluenceQuery = SystemAPI.QueryBuilder()
                .WithAll<SelectionOwner, LocalTransform>()
                .Build();
            _loggedOutOfInfluenceEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_loggedOutOfInfluenceEntities.IsCreated)
            {
                _loggedOutOfInfluenceEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (modeState.IsDivineHandEnabled == 0)
            {
                return;
            }

            var input = SystemAPI.GetSingleton<HandInputFrame>();
            var affordances = SystemAPI.GetSingleton<HandAffordances>();
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var policy = new HandPickupPolicy
            {
                AutoPickDynamicPhysics = 0,
                EnableWorldGrab = 0,
                DebugWorldGrabAny = 0,
                WorldGrabRequiresTag = 1
            };
            if (SystemAPI.TryGetSingleton(out HandPickupPolicy policyValue))
            {
                policy = policyValue;
            }
            var divinePolicy = Space4XDivineHandPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out Space4XDivineHandPolicy configuredPolicy))
            {
                divinePolicy = Space4XDivineHandInfluenceUtility.NormalizePolicy(configuredPolicy);
            }
            uint currentTick = timeState.Tick;
            float deltaTime = timeState.DeltaTime > 0f ? timeState.DeltaTime : 1f / 60f;
            bool isNewSample = input.SampleId != _lastInputSampleId;
            bool rmbPressed = isNewSample && input.RmbPressed;
            bool rmbReleased = isNewSample && input.RmbReleased;
            float scrollDelta = isNewSample ? input.ScrollDelta : 0f;

            _pickableLookup.Update(ref state);
            _spacePickableLookup.Update(ref state);
            _massLookup.Update(ref state);
            _celestialLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _selectionOwnerLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _factionResourcesLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer) in SystemAPI.Query<RefRW<HandStateData>, DynamicBuffer<HandCommand>>())
            {
                var handState = handStateRef.ValueRW;
                if (handState.HoldDistance <= 0f)
                {
                    handState.HoldDistance = DefaultHoldDistance;
                }

                if (math.abs(scrollDelta) > 0.001f)
                {
                    handState.HoldDistance = math.clamp(
                        handState.HoldDistance + scrollDelta * ScrollAdjustSpeed,
                        2f,
                        200f);
                }

                var holdDistance = handState.HoldDistance;
                if (handState.HeldEntity != Entity.Null && _pickableLookup.HasComponent(handState.HeldEntity))
                {
                    var pickable = _pickableLookup[handState.HeldEntity];
                    if (pickable.MaxHoldDistance > 0f)
                    {
                        holdDistance = math.min(holdDistance, pickable.MaxHoldDistance);
                    }
                }

                var holdTarget = input.RayOrigin + input.RayDirection * holdDistance;
                var releaseVelocity = float3.zero;
                if (handState.HeldEntity != Entity.Null && deltaTime > 1e-5f)
                {
                    releaseVelocity = (holdTarget - handState.HoldPoint) / deltaTime;
                }

                var worldGrabActive = policy.EnableWorldGrab != 0 && input.CtrlHeld && input.ShiftHeld;
                var debugWorldGrabAny = worldGrabActive && policy.DebugWorldGrabAny != 0;
                var celestialPick = divinePolicy.AllowCelestialDirectPick != 0 &&
                    affordances.TargetEntity != Entity.Null &&
                    _celestialLookup.HasComponent(affordances.TargetEntity);

                if (handState.HeldEntity == Entity.Null)
                {
                    if (rmbPressed && ((affordances.Flags & HandAffordanceFlags.CanPickUp) != 0 || celestialPick))
                    {
                        if (!CanPickTarget(ref state, affordances.TargetEntity, celestialPick, debugWorldGrabAny, in divinePolicy))
                        {
                            handStateRef.ValueRW = handState;
                            continue;
                        }

                        commandBuffer.Add(new HandCommand
                        {
                            Tick = currentTick,
                            Type = HandCommandType.Pick,
                            TargetEntity = affordances.TargetEntity,
                            TargetPosition = holdTarget,
                            Direction = input.RayDirection,
                            Speed = 0f,
                            ChargeLevel = 0f,
                            ResourceTypeIndex = 0,
                            Amount = 0f
                        });
                    }
                }
                else
                {
                    if (input.RmbHeld)
                    {
                        if (input.CtrlHeld)
                        {
                            handState.ChargeTimer = math.min(handState.ChargeTimer + deltaTime, MaxChargeSeconds);
                        }
                        else
                        {
                            handState.ChargeTimer = 0f;
                        }

                        commandBuffer.Add(new HandCommand
                        {
                            Tick = currentTick,
                            Type = HandCommandType.Hold,
                            TargetEntity = handState.HeldEntity,
                            TargetPosition = holdTarget,
                            Direction = input.RayDirection,
                            Speed = 0f,
                            ChargeLevel = 0f,
                            ResourceTypeIndex = 0,
                            Amount = 0f
                        });

                        handState.HoldPoint = holdTarget;
                    }

                    if (rmbReleased)
                    {
                        var chargeLevel = math.clamp(handState.ChargeTimer / MaxChargeSeconds, 0f, 1f);

                        var commandType = input.ShiftHeld ? HandCommandType.QueueThrow :
                            input.CtrlHeld ? HandCommandType.SlingshotThrow :
                            HandCommandType.Throw;

                        var direction = math.normalizesafe(input.RayDirection, new float3(0f, 1f, 0f));
                        float speed;
                        if (commandType == HandCommandType.SlingshotThrow)
                        {
                            var throwMult = GetSpeedMultiplier(handState.HeldEntity, HandCommandType.Throw);
                            var slingMult = GetSpeedMultiplier(handState.HeldEntity, HandCommandType.SlingshotThrow);
                            speed = math.lerp(ThrowSpeed * throwMult, ThrowSpeed * slingMult, chargeLevel);
                        }
                        else
                        {
                            var releaseSpeed = math.length(releaseVelocity);
                            if (releaseSpeed > 1e-4f)
                            {
                                direction = releaseVelocity / releaseSpeed;
                            }
                            speed = releaseSpeed;
                        }

                        commandBuffer.Add(new HandCommand
                        {
                            Tick = currentTick,
                            Type = commandType,
                            TargetEntity = handState.HeldEntity,
                            TargetPosition = holdTarget,
                            Direction = direction,
                            Speed = speed,
                            ChargeLevel = chargeLevel,
                            ResourceTypeIndex = 0,
                            Amount = 0f
                        });

                        handState.ChargeTimer = 0f;
                        handState.HoldPoint = holdTarget;
                    }
                }

                handStateRef.ValueRW = handState;
            }

            if (isNewSample)
            {
                _lastInputSampleId = input.SampleId;
            }
        }

        private bool CanPickTarget(
            ref SystemState state,
            Entity target,
            bool allowCelestialPick,
            bool debugWorldGrabAny,
            in Space4XDivineHandPolicy divinePolicy)
        {
            if (target == Entity.Null)
            {
                return false;
            }

            if (_celestialLookup.HasComponent(target) && !allowCelestialPick)
            {
                return false;
            }

            if (!debugWorldGrabAny && _spacePickableLookup.HasComponent(target))
            {
                var config = _spacePickableLookup[target];
                if (config.MaxMass > 0f)
                {
                    var mass = GetMass(target);
                    if (mass > config.MaxMass)
                    {
                        return false;
                    }
                }
            }

            if (!Space4XDivineHandInfluenceUtility.CanManipulateTarget(
                    ref state,
                    target,
                    in divinePolicy,
                    in _transformLookup,
                    in _selectionOwnerLookup,
                    in _affiliationLookup,
                    in _factionResourcesLookup,
                    in _playerFlagshipQuery,
                    in _ownedInfluenceQuery,
                    out var ownedByPlayer,
                    out _,
                    out _))
            {
                LogOutOfInfluenceOnce(target, ownedByPlayer);
                return false;
            }

            return true;
        }

        private float GetSpeedMultiplier(Entity target, HandCommandType commandType)
        {
            if (!_spacePickableLookup.HasComponent(target))
            {
                return 1f;
            }

            var config = _spacePickableLookup[target];
            return commandType == HandCommandType.SlingshotThrow
                ? math.max(0.1f, config.SlingshotSpeedMultiplier)
                : math.max(0.1f, config.ThrowSpeedMultiplier);
        }

        private float GetMass(Entity target)
        {
            if (_massLookup.HasComponent(target))
            {
                var mass = _massLookup[target];
                if (mass.InverseMass > 0f)
                {
                    return 1f / mass.InverseMass;
                }
            }

            if (_pickableLookup.HasComponent(target))
            {
                return math.max(_pickableLookup[target].Mass, 0f);
            }

            return 0f;
        }

        private void LogOutOfInfluenceOnce(Entity target, bool ownedByPlayer)
        {
            if (!_loggedOutOfInfluenceEntities.IsCreated || !_loggedOutOfInfluenceEntities.Add(target))
            {
                return;
            }

            var scope = ownedByPlayer ? "owned target constrained by policy" : "outside player influence";
            UnityEngine.Debug.Log($"[Space4XHandCommandStateSystem] Divine Hand blocked pickup for {target.Index}:{target.Version} ({scope}).");
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using HandStateData = PureDOTS.Runtime.Hand.HandState;
using Unity.Physics;

namespace Space4X.Systems.Interaction
{
    [BurstCompile]
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
        private uint _lastInputSampleId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<HandAffordances>();
            state.RequireForUpdate<HandStateData>();
            _pickableLookup = state.GetComponentLookup<HandPickable>(true);
            _spacePickableLookup = state.GetComponentLookup<Space4XHandPickable>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _celestialLookup = state.GetComponentLookup<Space4XCelestialManipulable>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
                var celestialPick = debugWorldGrabAny &&
                    affordances.TargetEntity != Entity.Null &&
                    _celestialLookup.HasComponent(affordances.TargetEntity);

                if (handState.HeldEntity == Entity.Null)
                {
                    if (rmbPressed && ((affordances.Flags & HandAffordanceFlags.CanPickUp) != 0 || celestialPick))
                    {
                        if (!CanPickTarget(affordances.TargetEntity, celestialPick, debugWorldGrabAny))
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

        private bool CanPickTarget(Entity target, bool celestialModifierHeld, bool debugWorldGrabAny)
        {
            if (target == Entity.Null)
            {
                return false;
            }

            if (_celestialLookup.HasComponent(target) && !celestialModifierHeld)
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
    }
}

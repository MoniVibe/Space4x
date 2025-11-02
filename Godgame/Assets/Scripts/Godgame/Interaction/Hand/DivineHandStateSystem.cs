using Godgame.Interaction;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Godgame.Interaction.HandSystems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RightClickRouterSystem))]
    public partial struct DivineHandStateSystem : ISystem
    {
        private EntityQuery _handQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputState>();
            _handQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<Hand>(),
                    ComponentType.ReadWrite<RightClickResolved>()
                }
            });
            state.RequireForUpdate(_handQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            var elapsedSeconds = (float)state.WorldUnmanaged.Time.ElapsedTime;
            var input = SystemAPI.GetSingleton<InputState>();
            var handEntity = _handQuery.GetSingletonEntity();
            var handRW = SystemAPI.GetComponentRW<Hand>(handEntity);
            ref var hand = ref handRW.ValueRW;
            var resolved = SystemAPI.GetComponentRO<RightClickResolved>(handEntity).ValueRO;

            var previousState = hand.State;
            var nextState = previousState;

            bool secondaryHeld = input.SecondaryHeld;
            bool cooldownReady = elapsedSeconds >= hand.CooldownUntilSeconds;
            bool secondaryActive = secondaryHeld && cooldownReady;
            float minCharge = math.max(0f, hand.MinChargeSeconds);
            float maxCharge = math.max(minCharge, hand.MaxChargeSeconds);

            switch (previousState)
            {
                case HandState.Empty:
                    if (input.PrimaryHeld && (hand.Grabbed != Entity.Null || hand.HasHeldType))
                    {
                        nextState = HandState.Holding;
                    }
                    else if (hand.HasHeldType && hand.HeldAmount > 0)
                    {
                        nextState = HandState.Holding;
                    }
                    break;

                case HandState.Holding:
                    if (!input.PrimaryHeld && hand.HeldAmount <= 0 && hand.Grabbed == Entity.Null)
                    {
                        nextState = HandState.Empty;
                        break;
                    }

                    if (secondaryActive && resolved.HasHandler)
                    {
                        switch (resolved.Handler)
                        {
                            case HandRightClickHandler.StorehouseDump when hand.HasHeldType && hand.HeldAmount > 0:
                                nextState = HandState.Dumping;
                                break;
                            case HandRightClickHandler.PileSiphon when hand.HeldAmount < hand.HeldCapacity:
                                nextState = HandState.Holding;
                                break;
                            case HandRightClickHandler.Drag when hand.Grabbed != Entity.Null:
                                nextState = HandState.Dragging;
                                break;
                            case HandRightClickHandler.SlingshotAim:
                                nextState = HandState.SlingshotAim;
                                break;
                            case HandRightClickHandler.GroundDrip when hand.HasHeldType && hand.HeldAmount > 0:
                                nextState = HandState.Dumping;
                                break;
                        }
                    }
                    break;

                case HandState.Dumping:
                    {
                        bool validHandler = resolved.HasHandler &&
                                            (resolved.Handler == HandRightClickHandler.StorehouseDump ||
                                             resolved.Handler == HandRightClickHandler.GroundDrip);
                        bool keepDumping = secondaryActive && validHandler && hand.HeldAmount > 0;

                        if (!keepDumping)
                        {
                            if (validHandler && (hand.HeldAmount <= 0 || !secondaryHeld))
                            {
                                hand.CooldownUntilSeconds = elapsedSeconds + hand.CooldownDurationSeconds;
                            }

                            nextState = hand.HeldAmount > 0 || hand.Grabbed != Entity.Null
                                ? HandState.Holding
                                : HandState.Empty;
                        }
                    }
                    break;

                case HandState.Dragging:
                    if (!secondaryActive || !resolved.HasHandler || resolved.Handler != HandRightClickHandler.Drag)
                    {
                        nextState = hand.Grabbed != Entity.Null ? HandState.Holding : HandState.Empty;
                    }
                    break;

                case HandState.SlingshotAim:
                    {
                        bool validHandler = resolved.HasHandler && resolved.Handler == HandRightClickHandler.SlingshotAim;
                        if (!secondaryHeld || !validHandler)
                        {
                            bool releaseAttempt = !secondaryHeld && validHandler;
                            bool meetsCharge = hand.ChargeSeconds >= (minCharge <= 0f ? 0f : minCharge - 0.0001f);

                            if (releaseAttempt && meetsCharge)
                            {
                                hand.CooldownUntilSeconds = elapsedSeconds + hand.CooldownDurationSeconds;
                            }

                            hand.ChargeSeconds = 0f;
                            nextState = hand.HasHeldType || hand.Grabbed != Entity.Null ? HandState.Holding : HandState.Empty;
                        }
                        else
                        {
                            hand.ChargeSeconds = math.clamp(hand.ChargeSeconds + deltaTime, 0f, maxCharge);
                        }
                    }
                    break;
            }

            if (nextState == HandState.SlingshotAim && previousState != HandState.SlingshotAim)
            {
                hand.ChargeSeconds = 0f;
            }

            if (nextState != previousState)
            {
                if (SystemAPI.HasBuffer<HandStateChangedEvent>(handEntity))
                {
                    var buffer = SystemAPI.GetBuffer<HandStateChangedEvent>(handEntity);
                    buffer.Add(new HandStateChangedEvent
                    {
                        From = previousState,
                        To = nextState
                    });
                }

                hand.State = nextState;
            }
        }
    }
}

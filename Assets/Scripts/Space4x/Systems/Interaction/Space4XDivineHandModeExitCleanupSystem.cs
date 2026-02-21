using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using Space4X.Runtime.Interaction;
using Unity.Collections;
using Unity.Entities;
using PDHandState = PureDOTS.Runtime.Hand.HandState;
using PDHandStateType = PureDOTS.Runtime.Hand.HandStateType;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Performs one-shot cleanup when leaving Divine Hand mode.
    /// Prevents sticky held entities, queued throws, and stale hand commands across mode switches.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHandCommandStateSystem))]
    [UpdateBefore(typeof(Space4XPickupSystem))]
    [UpdateBefore(typeof(Space4XThrowSystem))]
    [UpdateBefore(typeof(Space4XThrowQueueSystem))]
    [UpdateBefore(typeof(Space4XHeldFollowSystem))]
    [UpdateBefore(typeof(Space4XCelestialHandCommandSystem))]
    public partial struct Space4XDivineHandModeExitCleanupSystem : ISystem
    {
        private byte _wasDivineHandActive;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XControlModeRuntimeState>();
            state.RequireForUpdate<PDHandState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            bool divineActive = modeState.IsDivineHandEnabled != 0;
            if (divineActive)
            {
                _wasDivineHandActive = 1;
                return;
            }

            if (_wasDivineHandActive == 0)
            {
                return;
            }

            _wasDivineHandActive = 0;

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (handStateRef, commandBuffer, handEntity) in
                     SystemAPI.Query<RefRW<PDHandState>, DynamicBuffer<HandCommand>>().WithEntityAccess())
            {
                var handState = handStateRef.ValueRO;
                commandBuffer.Clear();

                if (em.HasBuffer<ThrowQueue>(handEntity))
                {
                    em.GetBuffer<ThrowQueue>(handEntity).Clear();
                }

                ForceReleaseHeldEntity(ref ecb, em, handState.HeldEntity);

                handState.HeldEntity = Entity.Null;
                handState.CurrentState = PDHandStateType.Idle;
                handState.PreviousState = PDHandStateType.Idle;
                handState.ChargeTimer = 0f;
                handState.CooldownTimer = 0f;
                handState.StateTimer = 0;
                handStateRef.ValueRW = handState;
            }

            foreach (var pickupStateRef in SystemAPI.Query<RefRW<PickupState>>().WithAll<Space4XGodHandTag>())
            {
                var pickupState = pickupStateRef.ValueRO;
                ForceReleaseHeldEntity(ref ecb, em, pickupState.TargetEntity);

                pickupState.State = PickupStateType.Empty;
                pickupState.TargetEntity = Entity.Null;
                pickupState.CursorMovementAccumulator = 0f;
                pickupState.HoldTime = 0f;
                pickupState.HoldDistance = 0f;
                pickupState.AccumulatedVelocity = Unity.Mathematics.float3.zero;
                pickupState.IsMoving = false;
                pickupStateRef.ValueRW = pickupState;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void ForceReleaseHeldEntity(ref EntityCommandBuffer ecb, EntityManager entityManager, Entity heldEntity)
        {
            if (heldEntity == Entity.Null || !entityManager.Exists(heldEntity))
            {
                return;
            }

            if (entityManager.HasComponent<HandHeldTag>(heldEntity))
            {
                ecb.RemoveComponent<HandHeldTag>(heldEntity);
            }

            if (entityManager.HasComponent<HeldByPlayer>(heldEntity))
            {
                ecb.SetComponentEnabled<HeldByPlayer>(heldEntity, false);
            }

            if (entityManager.HasComponent<MovementSuppressed>(heldEntity))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(heldEntity, false);
            }

            if (entityManager.HasComponent<Space4XCelestialHoldState>(heldEntity))
            {
                ecb.RemoveComponent<Space4XCelestialHoldState>(heldEntity);
            }
        }
    }
}

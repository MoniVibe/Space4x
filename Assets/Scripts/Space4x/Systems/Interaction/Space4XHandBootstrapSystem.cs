using PureDOTS.Runtime.Hand;
using Unity.Entities;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XHandBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HandState>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(HandState));
                state.EntityManager.SetComponentData(entity, new HandState
                {
                    CurrentState = HandStateType.Idle,
                    PreviousState = HandStateType.Idle,
                    HeldEntity = Entity.Null,
                    HoldPoint = Unity.Mathematics.float3.zero,
                    HoldDistance = 10f,
                    ChargeTimer = 0f,
                    CooldownTimer = 0f,
                    StateTimer = 0
                });
                state.EntityManager.AddBuffer<HandCommand>(entity);
                state.EntityManager.AddBuffer<PureDOTS.Runtime.Interaction.ThrowQueue>(entity);
            }

            if (!SystemAPI.TryGetSingleton<HandPickupPolicy>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(HandPickupPolicy));
                state.EntityManager.SetComponentData(entity, new HandPickupPolicy
                {
                    AutoPickDynamicPhysics = 1,
                    EnableWorldGrab = 1,
                    DebugWorldGrabAny = 1,
                    WorldGrabRequiresTag = 1
                });
            }
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}

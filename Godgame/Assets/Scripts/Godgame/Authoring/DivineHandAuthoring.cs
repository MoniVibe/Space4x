using Godgame.Interaction;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Godgame.Authoring
{
    public class DivineHandAuthoring : MonoBehaviour
    {
        [Header("Capacity & Rates")]
        public int heldCapacity = 1000;
        public float maxCarryMass = 200f;
        public float dumpRatePerSecond = 150f;
        public float siphonRange = 8f;

        [Header("Throw & Aim")]
        public float grabLiftHeight = 3f;
        public float throwScalar = 25f;
        public float cooldownAfterThrowSeconds = 0.15f;
        public float minChargeSeconds = 0.2f;
        public float maxChargeSeconds = 1.5f;

        private class Baker : Baker<DivineHandAuthoring>
        {
            public override void Bake(DivineHandAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Hand
                {
                    State = HandState.Empty,
                    WorldPos = float3.zero,
                    PrevWorldPos = float3.zero,
                    AimDir = new float3(0f, -1f, 0f),
                    Hovered = Entity.Null,
                    Grabbed = Entity.Null,
                    HeldType = ResourceType.None,
                    HasHeldType = false,
                    HeldAmount = 0,
                    HeldCapacity = math.max(0, authoring.heldCapacity),
                    MaxCarryMass = math.max(0f, authoring.maxCarryMass),
                    GrabLiftHeight = authoring.grabLiftHeight,
                    ThrowScalar = authoring.throwScalar,
                    CooldownDurationSeconds = math.max(0f, authoring.cooldownAfterThrowSeconds),
                    CooldownUntilSeconds = 0f,
                    MinChargeSeconds = math.max(0f, authoring.minChargeSeconds),
                    MaxChargeSeconds = math.max(authoring.minChargeSeconds, authoring.maxChargeSeconds),
                    ChargeSeconds = 0f,
                    DumpRatePerSecond = math.max(1f, authoring.dumpRatePerSecond),
                    SiphonRange = math.max(0f, authoring.siphonRange)
                });

                AddComponent(entity, new RightClickResolved
                {
                    HasHandler = false,
                    Handler = HandRightClickHandler.None,
                    Target = Entity.Null,
                    HitPosition = float3.zero,
                    HitNormal = new float3(0f, 1f, 0f)
                });

                AddComponent(entity, new InputState());
                AddComponent(entity, new HandHistory());

                AddBuffer<RightClickContextElement>(entity);
                AddBuffer<HandStateChangedEvent>(entity);
                AddBuffer<HandCarryingChangedEvent>(entity);
                AddBuffer<HandEventDump>(entity);
                AddBuffer<HandEventSiphon>(entity);
                AddBuffer<HandEventThrow>(entity);
            }
        }
    }
}

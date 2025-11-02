using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Godgame.Interaction
{
    public enum HandState : byte
    {
        Empty,
        Holding,
        Dragging,
        SlingshotAim,
        Dumping
    }

    public enum HandRightClickHandler : byte
    {
        None = 0,
        UiBlocker = 1,
        ModalTool = 2,
        StorehouseDump = 3,
        PileSiphon = 4,
        Drag = 5,
        GroundDrip = 6,
        SlingshotAim = 7
    }

    public static class HandRightClickPriority
    {
        public const int Ui = 0;
        public const int ModalTool = 10;
        public const int StorehouseDump = 20;
        public const int PileSiphon = 30;
        public const int Drag = 40;
        public const int GroundDrip = 50;
        public const int SlingshotAim = 60;
    }

    public enum ResourceType : byte
    {
        None = 0,
        Wood = 1,
        Ore = 2,
        Food = 3,
        Worship = 4
    }

    public struct InputState : IComponentData
    {
        public float2 PointerPos;
        public float2 PointerDelta;
        public float Scroll;
        public bool PrimaryHeld;
        public bool SecondaryHeld;
        public bool MiddleHeld;
        public bool ThrowModifier;
        // Camera inputs
        public float2 Move; // WASD movement (from Move action)
        public float Vertical; // Q/E up/down movement (from CameraVertical action)
        public bool CameraToggleMode; // Toggle camera mode button press
    }

    public struct HandHistory : IComponentData
    {
        public float3 V0;
        public float3 V1;
        public float3 V2;
        public float3 V3;
    }

    public struct Hand : IComponentData
    {
        public HandState State;
        public float3 WorldPos;
        public float3 PrevWorldPos;
        public float3 AimDir;
        public Entity Hovered;
        public Entity Grabbed;
        public ResourceType HeldType;
        public bool HasHeldType;
        public int HeldAmount;
        public int HeldCapacity;
        public float MaxCarryMass;
        public float GrabLiftHeight;
        public float ThrowScalar;
        public float CooldownDurationSeconds;
        public float CooldownUntilSeconds;
        public float MinChargeSeconds;
        public float MaxChargeSeconds;
        public float ChargeSeconds;
        public float DumpRatePerSecond;
        public float SiphonRange;
    }

    [InternalBufferCapacity(4)]
    public struct RightClickContextElement : IBufferElementData
    {
        public HandRightClickHandler Handler;
        public int Priority;
        public Entity Target;
        public float3 HitPosition;
        public float3 HitNormal;
    }

    public struct RightClickResolved : IComponentData
    {
        public bool HasHandler;
        public HandRightClickHandler Handler;
        public Entity Target;
        public float3 HitPosition;
        public float3 HitNormal;

        public static RightClickResolved None => new RightClickResolved
        {
            HasHandler = false,
            Handler = HandRightClickHandler.None,
            Target = Entity.Null,
            HitPosition = float3.zero,
            HitNormal = new float3(0f, 1f, 0f)
        };
    }

    [InternalBufferCapacity(1)]
    public struct HandStateChangedEvent : IBufferElementData
    {
        public HandState From;
        public HandState To;
    }

    [InternalBufferCapacity(1)]
    public struct HandCarryingChangedEvent : IBufferElementData
    {
        public bool HasResource;
        public ResourceType Type;
        public int Amount;
        public int Capacity;
    }

    [InternalBufferCapacity(1)]
    public struct HandEventDump : IBufferElementData
    {
        public ResourceType Type;
        public int Amount;
        public Entity Storehouse;
    }

    [InternalBufferCapacity(1)]
    public struct HandEventSiphon : IBufferElementData
    {
        public ResourceType Type;
        public int Amount;
        public Entity Source;
    }

    [InternalBufferCapacity(1)]
    public struct HandEventThrow : IBufferElementData
    {
        public Entity Target;
        public float3 Impulse;
        public float ChargeSeconds;
    }
}

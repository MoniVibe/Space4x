using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Marker for the global Divine Hand singleton entity.
    /// </summary>
    public struct HandSingletonTag : IComponentData { }

    public enum HandInteractableType : byte
    {
        None = 0,
        ResourceChunk = 1,
        ResourceToken = 2,
        Villager = 3,
        Structure = 4,
        MiracleToken = 5
    }

    public enum HandHeldType : byte
    {
        None = 0,
        ResourceChunk = 1,
        ResourceToken = 2,
        Villager = 3,
        MiracleToken = 4
    }

    /// <summary>
    /// Current state of the Divine Hand in screen and world terms.
    /// </summary>
    public struct HandState : IComponentData
    {
        public float2 ScreenPosition;
        public float3 WorldPosition;
        public float3 AimDirection;
        public Entity HoveredEntity;
        public Entity HeldEntity;
        public uint GrabStartTick;
        public float SlingshotCharge;
        public byte HoveredType;
        public byte HeldType;
        public byte PrimaryPressed;
        public byte SecondaryPressed;
        public byte PrimaryJustPressed;
        public byte PrimaryJustReleased;
        public byte SecondaryJustPressed;
        public byte SecondaryJustReleased;
        public byte IsDragging;
    }

    /// <summary>
    /// Command buffer for the Divine Hand; hybrid bridges write, DOTS systems consume.
    /// </summary>
    public struct HandCommand : IBufferElementData
    {
        public enum CommandType : byte
        {
            None = 0,
            SetScreenPosition = 1,
            PrimaryDown = 2,
            PrimaryUp = 3,
            SecondaryDown = 4,
            SecondaryUp = 5,
            SetWorldPosition = 6
        }

        public CommandType Type;
        public float2 Float2Param;
        public float3 Float3Param;
        public float FloatParam;
        public Entity EntityParam;
    }

    /// <summary>
    /// Marks an entity as interactable by the hand with a simple radius.
    /// </summary>
    public struct HandInteractable : IComponentData
    {
        public HandInteractableType Type;
        public float Radius;
    }

    /// <summary>
    /// Tagged on entities currently held by the hand.
    /// </summary>
    public struct HandHeldTag : IComponentData { }

    public struct HandHeld : IComponentData
    {
        public HandHeldType Type;
        public float3 Offset;
    }

    public struct MiracleEffect : IComponentData
    {
        public float Lifetime;
        public float Elapsed;
        public float Radius;
    }
}



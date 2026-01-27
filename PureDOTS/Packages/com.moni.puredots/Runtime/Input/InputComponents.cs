using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Input
{
    /// <summary>
    /// Button identifiers for input edge events.
    /// </summary>
    public enum InputButton : byte
    {
        None = 0,
        Primary = 1,      // Left mouse / primary action
        Secondary = 2,    // Right mouse / secondary action
        Middle = 3,        // Middle mouse / tertiary action
        ScrollUp = 4,
        ScrollDown = 5,
        Modifier = 6      // Shift/Ctrl/Alt modifiers
    }

    /// <summary>
    /// Edge event kind (Down = press, Up = release).
    /// </summary>
    public enum InputEdgeKind : byte
    {
        Down = 0,
        Up = 1
    }

    /// <summary>
    /// Edge event buffer element capturing single-frame button transitions.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HandInputEdge : IBufferElementData
    {
        public InputButton Button;
        public InputEdgeKind Kind;
        public uint Tick;
        public float2 PointerPosition; // Screen-space at event time
    }

    /// <summary>
    /// Edge event buffer element for camera input (orbit, pan, zoom transitions).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CameraInputEdge : IBufferElementData
    {
        public InputButton Button;
        public InputEdgeKind Kind;
        public uint Tick;
        public float2 PointerPosition; // Screen-space at event time
    }

    /// <summary>
    /// Continuous hand input state sampled at each DOTS tick.
    /// Contains polled values (axes, held buttons) without edge information.
    /// </summary>
    public struct DivineHandInput : IComponentData
    {
        public uint SampleTick;
        public byte PlayerId;               // Player identifier (0 = default/single-player)
        public float2 PointerPosition;      // Screen-space (0-1 normalized or pixels)
        public float2 PointerDelta;         // Screen-space delta since last tick
        public float3 CursorWorldPosition; // World-space cursor position
        public float3 AimDirection;         // World-space aim direction
        public byte PrimaryHeld;           // Continuous held state (0/1)
        public byte SecondaryHeld;         // Continuous held state (0/1)
        public float ThrowCharge;          // Accumulated charge while holding
        public byte PointerOverUI;          // Flag indicating pointer is over UI
        public byte AppHasFocus;            // Flag indicating app window has focus

        // Extended controls
        public byte QueueModifierHeld;      // Shift modifier for queueing throws
        public byte ReleaseSingleTriggered; // Q pressed this frame
        public byte ReleaseAllTriggered;    // E pressed this frame
        public byte ToggleThrowModeTriggered; // T pressed this frame
        public byte ThrowModeIsSlingshot;   // 1 = slingshot, 0 = velocity
    }

    /// <summary>
    /// Continuous camera input state sampled at each DOTS tick.
    /// Contains polled values (mouse deltas, scroll) without edge information.
    /// </summary>
    public struct CameraInputState : IComponentData
    {
        public uint SampleTick;
        public byte PlayerId;               // Player identifier (0 = default/single-player)
        public float2 OrbitDelta;    // Mouse delta for orbit (yaw/pitch)
        public float2 PanDelta;      // Mouse delta for pan (world-space)
        public float ZoomDelta;      // Scroll wheel delta (positive = zoom in)
        public float2 PointerPosition; // Screen-space pointer position
        public byte PointerOverUI;     // Flag indicating pointer is over UI
        public byte AppHasFocus;       // Flag indicating app window has focus

        // Extended movement controls
        public float2 MoveInput;       // WASD planar movement
        public float VerticalMove;     // Z/X vertical movement
        public byte YAxisUnlocked;     // 1 = unlocked, 0 = locked
        public byte ToggleYAxisTriggered; // 1 when toggle pressed this frame
    }

    /// <summary>
    /// Time control inputs gathered from UI or keyboard shortcuts.
    /// Consumed by time/rewind systems to enqueue commands.
    /// </summary>
    public struct TimeControlInputState : IComponentData
    {
        public uint SampleTick;
        public byte RewindHeld;
        public byte RewindPressedThisFrame;
        public byte RewindSpeedLevel;       // 0 = none, 1-4 speed tiers
        public byte EnterGhostPreview;      // 1 when rew key released
        public byte StepDownTriggered;      // '[' pressed
        public byte StepUpTriggered;        // ']' pressed
        public byte PauseToggleTriggered;   // Space pressed
    }

    /// <summary>
    /// Player identifier for multi-hand/multi-camera support.
    /// </summary>
    public struct PlayerId : IComponentData
    {
        public byte Value;

        public static PlayerId Default => new PlayerId { Value = 0 };
    }

    /// <summary>
    /// Tag component indicating an entity is controlled by a specific player.
    /// </summary>
    public struct ControlledBy : IComponentData
    {
        public byte PlayerId;
    }

    /// <summary>
    /// Per-tick input snapshot used for rewind playback and deterministic re-simulation.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct InputSnapshotRecord : IBufferElementData
    {
        public uint Tick;
        public DivineHandInput HandInput;
        public CameraInputState CameraInput;
        public int HandEdgeStart;
        public int HandEdgeCount;
        public int CameraEdgeStart;
        public int CameraEdgeCount;
    }

    /// <summary>
    /// Singleton state for the input history ring buffer.
    /// </summary>
    public struct InputHistoryState : IComponentData
    {
        public uint HorizonTicks;
        public uint LastRecordedTick;
    }
}



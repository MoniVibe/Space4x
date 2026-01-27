using Unity.Entities;

namespace PureDOTS.Input
{
    /// <summary>
    /// Selection click behavior mode.
    /// </summary>
    public enum SelectionClickMode : byte
    {
        Replace = 0,  // Clear existing selection, select clicked entity
        Toggle = 1    // Toggle clicked entity in/out of selection
    }

    /// <summary>
    /// Selection box behavior mode.
    /// </summary>
    public enum SelectionBoxMode : byte
    {
        Replace = 0,        // Replace selection with entities in box
        AdditiveToggle = 1  // XOR add/remove entities in box
    }

    /// <summary>
    /// Time control command kinds.
    /// </summary>
    public enum TimeControlCommandKind : byte
    {
        TogglePause = 0,
        SetScale = 1,
        EnterRewind = 2,
        ExitRewind = 3,
        StepTicks = 4
    }

    /// <summary>
    /// God-hand throw mode command kinds.
    /// </summary>
    public enum GodHandCommandKind : byte
    {
        ToggleThrowMode = 0,
        QueueOrLaunchHeld = 1,
        LaunchNextQueued = 2,
        LaunchAllQueued = 3
    }

    /// <summary>
    /// Save/load command kinds.
    /// </summary>
    public enum SaveLoadCommandKind : byte
    {
        QuickSave = 0,
        QuickLoad = 1
    }
}























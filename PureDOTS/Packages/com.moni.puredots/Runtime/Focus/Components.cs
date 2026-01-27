using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Focus
{
    /// <summary>
    /// Focus state - mental bandwidth for multitasking, multi-targeting, command.
    /// Universal stat consumed by all systems (piloting, mind-control, command).
    /// </summary>
    public struct FocusState : IComponentData
    {
        public float Current;          // 0..Max
        public float Max;
        public float RegenRate;        // Per tick regeneration
        public float SoftThreshold;    // Below this: fatigue penalties start
        public float HardThreshold;    // Below this: risk of break/coma
        public float Load;             // Current "tasks" weight (calculated from FocusTask buffer)
    }

    /// <summary>
    /// Active focus task consuming mental bandwidth.
    /// Examples: PilotCraft, Gunner, SquadronLead, MindLinkControl.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct FocusTask : IBufferElementData
    {
        public int TaskId;         // Task type identifier
        public float Cost;         // Contribution to FocusState.Load
    }

    /// <summary>
    /// Mental break state - result of hard focus depletion.
    /// </summary>
    public enum MentalBreakState : byte
    {
        Stable,      // Normal operation
        Frazzled,    // Erratic but functional
        Panicked,    // Flee, surrender, ignore orders
        Catatonic,   // Coma / unresponsive
        Berserk,     // Reckless aggression
    }

    /// <summary>
    /// Current mental state - tracks mental breaks and recovery.
    /// </summary>
    public struct MentalState : IComponentData
    {
        public MentalBreakState State;
        public uint LastStateChangeTick;
    }
}


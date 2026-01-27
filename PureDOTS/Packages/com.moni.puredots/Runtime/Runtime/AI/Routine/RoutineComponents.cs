using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI.Routine
{
    /// <summary>
    /// Phases of the day for routine scheduling.
    /// </summary>
    public enum DayPhase : byte
    {
        Dawn = 0,        // 5:00 - 7:00
        Morning = 1,     // 7:00 - 11:00
        Noon = 2,        // 11:00 - 13:00
        Afternoon = 3,   // 13:00 - 17:00
        Dusk = 4,        // 17:00 - 19:00
        Evening = 5,     // 19:00 - 22:00
        Night = 6,       // 22:00 - 2:00
        Midnight = 7     // 2:00 - 5:00
    }

    /// <summary>
    /// Activities that can be scheduled in routines.
    /// </summary>
    public enum RoutineActivity : byte
    {
        None = 0,
        Sleep = 1,
        Wake = 2,
        Work = 3,
        Eat = 4,
        Leisure = 5,
        Socialize = 6,
        Worship = 7,
        Train = 8,
        Patrol = 9,
        Rest = 10,
        Study = 11,
        Craft = 12,
        Trade = 13,
        Guard = 14,
        Hunt = 15,
        Gather = 16,
        Maintain = 17,   // Maintenance/repair
        Travel = 18,
        Meeting = 19
    }

    /// <summary>
    /// Current routine state for an entity.
    /// </summary>
    public struct EntityRoutine : IComponentData
    {
        public DayPhase CurrentPhase;
        public RoutineActivity CurrentActivity;
        public RoutineActivity ScheduledActivity;  // What should be doing this phase
        public float PhaseStartTime;               // When current phase started
        public float ActivityStartTime;            // When current activity started
        public bool IsInterrupted;                 // Doing something else
        public byte InterruptPriority;             // Priority of interrupt
        public uint LastPhaseChangeTick;
    }

    /// <summary>
    /// Per-phase schedule entry.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RoutineSchedule : IBufferElementData
    {
        public DayPhase Phase;
        public RoutineActivity Activity;
        public byte Priority;              // Higher = harder to interrupt
        public float Duration;             // Optional duration override
        public bool IsOptional;            // Can be skipped if busy
    }

    /// <summary>
    /// Configuration for the routine system.
    /// </summary>
    public struct RoutineConfig : IComponentData
    {
        public float DawnHour;             // When dawn starts (default 5.0)
        public float MorningHour;          // When morning starts (default 7.0)
        public float NoonHour;             // When noon starts (default 11.0)
        public float AfternoonHour;        // When afternoon starts (default 13.0)
        public float DuskHour;             // When dusk starts (default 17.0)
        public float EveningHour;          // When evening starts (default 19.0)
        public float NightHour;            // When night starts (default 22.0)
        public float MidnightHour;         // When midnight starts (default 2.0)
        public float DayLengthSeconds;     // Seconds per game day
    }

    /// <summary>
    /// Request to interrupt an entity's routine.
    /// </summary>
    public struct RoutineInterruptRequest : IComponentData
    {
        public Entity TargetEntity;
        public RoutineActivity NewActivity;
        public byte Priority;
        public float Duration;             // How long the interrupt lasts
    }

    /// <summary>
    /// Event emitted when routine phase changes.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct PhaseChangeEvent : IBufferElementData
    {
        public DayPhase OldPhase;
        public DayPhase NewPhase;
        public RoutineActivity NewActivity;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when routine is interrupted.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct RoutineInterruptEvent : IBufferElementData
    {
        public RoutineActivity InterruptedActivity;
        public RoutineActivity NewActivity;
        public byte Priority;
        public uint Tick;
    }

    /// <summary>
    /// Global day/night cycle state.
    /// </summary>
    public struct DayCycleState : IComponentData
    {
        public float CurrentHour;          // 0-24
        public DayPhase CurrentPhase;
        public uint DayNumber;
        public float TimeScale;            // Speed multiplier
        public bool IsPaused;
    }
}


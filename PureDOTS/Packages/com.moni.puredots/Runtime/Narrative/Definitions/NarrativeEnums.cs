namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// How a narrative event is delivered to the player.
    /// </summary>
    public enum NarrativeEventDeliveryKind : byte
    {
        LogOnly = 0,        // text/audio log
        PopupChoice = 1,    // choice UI
        Ambient = 2,        // chatter, background
        WorldChangeOnly = 3  // silent structural change; games visualize
    }

    /// <summary>
    /// Current phase of a situation's lifecycle.
    /// </summary>
    public enum SituationPhase : byte
    {
        Inactive = 0,
        Intro = 1,
        Running = 2,
        Resolving = 3,
        Finished = 4,
        Failed = 5,
        Aborted = 6
    }

    /// <summary>
    /// How a situation step progresses.
    /// </summary>
    public enum SituationStepKind : byte
    {
        AutoAdvance = 0,        // progresses when conditions met
        WaitForChoice = 1,      // waits for player input
        TimedTick = 2,          // repeats while timer runs
        Checkpoint = 3          // marks milestone (reward, penalty)
    }
}


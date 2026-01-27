using System;

namespace PureDOTS.Runtime.Motivation
{
    /// <summary>
    /// Layer/type of motivation goal.
    /// Defines the temporal scope and nature of the goal.
    /// </summary>
    public enum MotivationLayer : byte
    {
        /// <summary>Short-lived, small or re-rollable goals (Sims-style wants).</summary>
        Dream = 0,
        /// <summary>Identity arcs: "who I want to become".</summary>
        Aspiration = 1,
        /// <summary>Medium-term concrete goals: "things I want to accomplish".</summary>
        Desire = 2,
        /// <summary>Long-term, often aggregate-scale end states (become a multicultural theocracy, ascend, form a dynasty, etc.).</summary>
        Ambition = 3,
        /// <summary>Soft/personal wants ("better house", "children with X", "die well").</summary>
        Wish = 4
    }

    /// <summary>
    /// Current status of a motivation slot.
    /// </summary>
    public enum MotivationStatus : byte
    {
        /// <summary>Slot empty, can be filled.</summary>
        Inactive = 0,
        /// <summary>Visible to AI/player but not yet in progress.</summary>
        Available = 1,
        /// <summary>Currently being pursued.</summary>
        InProgress = 2,
        /// <summary>Completed successfully.</summary>
        Satisfied = 3,
        /// <summary>Failed/invalid.</summary>
        Failed = 4,
        /// <summary>Explicitly dropped / rerolled.</summary>
        Abandoned = 5
    }

    /// <summary>
    /// Lock flags for motivation slots.
    /// Determines what can prevent a goal from being rerolled or abandoned.
    /// </summary>
    [Flags]
    public enum MotivationLockFlags : byte
    {
        /// <summary>No locks applied.</summary>
        None = 0,
        /// <summary>Locked by player/god (Sims-style "pin").</summary>
        LockedByPlayer = 1 << 0,
        /// <summary>Locked by narrative or quest system.</summary>
        LockedByStory = 1 << 1,
        /// <summary>Bound to village/guild/fleet/empire aggregate goals.</summary>
        LockedByAggregate = 1 << 2
    }

    /// <summary>
    /// Scope of a motivation goal.
    /// Determines whether it applies to individuals, aggregates, or both.
    /// </summary>
    public enum MotivationScope : byte
    {
        /// <summary>Individual-only goal (personal dreams, wishes).</summary>
        Individual = 0,
        /// <summary>Aggregate-only goal (village/fleet/empire ambitions).</summary>
        Aggregate = 1,
        /// <summary>Can apply to either individuals or aggregates.</summary>
        Either = 2
    }

    /// <summary>
    /// High-level category of what the goal is about.
    /// Games interpret these into concrete behavior.
    /// </summary>
    public enum MotivationTag : ushort
    {
        /// <summary>No specific tag (generic or custom).</summary>
        None = 0,
        /// <summary>"Raise [Stat] to X".</summary>
        IncreaseStat = 1,
        /// <summary>"Reach [relation threshold] with [target]".</summary>
        ImproveRelationship = 2,
        /// <summary>"Earn/save X wealth".</summary>
        GainWealth = 3,
        /// <summary>"Reach X fame/glory/renown".</summary>
        GainFame = 4,
        /// <summary>"Win fight / battle".</summary>
        WinCombat = 5,
        /// <summary>"Found new village/colony/outpost".</summary>
        FoundSettlement = 6,
        /// <summary>"Raise dynasty / have N descendants / crew line".</summary>
        GrowLineage = 7,
        /// <summary>"Run successful business / guild / carrier group".</summary>
        RunEnterprise = 8,
        /// <summary>"Spread faith, culture, ethics".</summary>
        SpreadIdeology = 9,
        /// <summary>"Discover/colonize new region/planet".</summary>
        ExploreOrColonize = 10,
        /// <summary>"Become hero/saint/icon".</summary>
        BecomeLegendary = 11,
        /// <summary>"Die in battle / sacrifice / meaningful end".</summary>
        DieWell = 12
    }
}

























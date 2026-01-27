using Unity.Collections;

namespace PureDOTS.Runtime.AI.WorldFacts
{
    /// <summary>
    /// Typed enum for world fact keys.
    /// Prevents ad-hoc string-based fact queries.
    /// Must be locked and stable before building action libraries.
    /// </summary>
    public enum WorldFactKey : ushort
    {
        // Perception facts (0-99)
        HasTarget = 0,
        IsThreatened = 1,
        SeesResource = 2,
        SeesEnemy = 3,
        SeesAlly = 4,
        SeesStorehouse = 5,
        SeesBuildSite = 6,
        SeesClimbable = 7,
        SeesCover = 8,

        // Need facts (100-199)
        IsHungry = 100,
        IsTired = 101,
        IsInjured = 102,
        NeedsResource = 103,
        NeedsRest = 104,
        NeedsFood = 105,

        // State facts (200-299)
        IsInFormation = 200,
        IsFollowing = 201,
        IsPatrolling = 202,
        IsWorking = 203,
        IsCombat = 204,
        IsFleeing = 205,

        // Knowledge facts (300-399)
        KnowsStorehouseWithCapacity = 300,
        KnowsResourceLocation = 301,
        KnowsEnemyLocation = 302,
        KnowsSafePath = 303,
        KnowsLeaderLocation = 304,

        // Aggregate facts (400-499)
        IsInAggregate = 400,
        HasLeader = 401,
        HasOrders = 402,
        AggregateNeedsResources = 403,

        // Resource facts (500-599)
        HasResource = 500,
        HasCapacity = 501,
        ResourceAvailable = 502,
        DeliveryPending = 503,

        // Custom range for game-specific facts (1000-65535)
        CustomStart = 1000
    }
}




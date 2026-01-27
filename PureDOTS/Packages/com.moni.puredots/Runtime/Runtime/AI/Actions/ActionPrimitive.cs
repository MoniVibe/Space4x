namespace PureDOTS.Runtime.AI.Actions
{
    /// <summary>
    /// Primitive action types that can be executed by agents.
    /// These are the building blocks for higher-level behaviors.
    /// </summary>
    public enum ActionPrimitive : byte
    {
        None = 0,
        MoveTo = 1,         // Move to target position
        TraverseEdge = 2,   // Traverse a navigation edge (climb, jump, etc.)
        Interact = 3,       // Interact with target entity
        Recover = 4,        // Recover/rest (heal, restore energy)
        
        // Domain actions (built on primitives)
        Gather = 10,        // Gather resources from source
        Deliver = 11,       // Deliver resources to target
        Rest = 12,          // Rest to restore needs
        Patrol = 13,        // Patrol between waypoints
        Follow = 14,        // Follow target entity
        Attack = 15,        // Attack target entity
        Flee = 16,          // Flee from threat
        
        // Custom actions (game-specific)
        Custom = 255
    }
}




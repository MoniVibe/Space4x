namespace PureDOTS.Runtime.AI.WorldFacts
{
    /// <summary>
    /// Provenance of a world fact - where it came from.
    /// Allows planners to assess freshness and reliability.
    /// </summary>
    public enum WorldFactProvenance : byte
    {
        Perception = 0,    // Direct perception (most fresh, most reliable)
        Memory = 1,        // From memory/history (may be stale)
        Registry = 2,      // From registry/catalog (authoritative but may not reflect current state)
        Inference = 3,     // Inferred from other facts (less reliable)
        Directive = 4      // From orders/directives (authoritative for behavior)
    }
}




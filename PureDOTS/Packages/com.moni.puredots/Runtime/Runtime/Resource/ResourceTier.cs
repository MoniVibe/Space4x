namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Relative processing tier for a resource entry.
    /// </summary>
    public enum ResourceTier : byte
    {
        Unknown = 0,
        Raw = 1,
        Refined = 2,
        Composite = 3,
        Byproduct = 4
    }
}


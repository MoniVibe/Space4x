namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Role definition within a situation archetype.
    /// Defines what entities can fill this role and whether it's required.
    /// </summary>
    public struct SituationRoleDef
    {
        public int RoleId;          // e.g. Hostage=1, Captor=2, Authority=3, Rebel=4, Loyalist=5
        public NarrativeTagMask CompatibleEntityTags; // "Band", "Village", "Fleet", "Army", etc.
        public byte Required;       // 0 optional, 1 required
    }
}


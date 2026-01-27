namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Power network domains representing different scales of infrastructure.
    /// </summary>
    public enum PowerDomain : byte
    {
        GroundLocal,   // villages, planetside colonies
        ShipLocal,     // individual ships, stations, megaships
        Orbital,       // orbital rings, starbases, beaming platforms
        SystemWide     // dyson spheres, system grids
    }
}


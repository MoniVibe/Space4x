namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Types of miracles available in the system. Shared between Godgame and Space4x.
    /// </summary>
    public enum MiracleType : byte
    {
        None = 0,
        Rain = 1,
        Fireball = 2,
        Heal = 3,
        Shield = 4,
        Healing = Heal,
        Blessing = 5,
        Fertility = 6,
        Sunlight = 7,
        Fire = 8,
        Lightning = 9,
        // Region miracles used by Godgame
        BlessRegion = 10,
        CurseRegion = 11,
        RestoreBiome = 12
    }

    /// <summary>
    /// Lifecycle state of a miracle instance.
    /// </summary>
    public enum MiracleLifecycleState : byte
    {
        Idle = 0,
        Charging = 1,
        Ready = 2,
        Active = 3,
        CoolingDown = 4
    }

    /// <summary>
    /// How a miracle is cast (token throw, sustained channel, instant effect).
    /// </summary>
    public enum MiracleCastingMode : byte
    {
        Token = 0,
        Sustained = 1,
        Instant = 2
    }
}





























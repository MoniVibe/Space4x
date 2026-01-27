using Unity.Entities;

namespace PureDOTS.Runtime.Platform
{
    /// <summary>
    /// Reactor failure mode when destroyed or critically damaged.
    /// </summary>
    public enum ReactorFailureMode : byte
    {
        SafeShutdown = 0,
        Explosive = 1,
        Venting = 2
    }

    /// <summary>
    /// Reactor definition (stored in blob or component data).
    /// </summary>
    public struct ReactorDef
    {
        public int ReactorDefId;
        public float MaxOutput;
        public ReactorFailureMode FailureMode;
        public float MeltdownDamage;
        public float MeltdownRadius;
        public float SafeShutdownChance;
    }

    /// <summary>
    /// Reactor runtime state per module or aggregated per segment.
    /// </summary>
    public struct ReactorState : IComponentData
    {
        public int ReactorDefId;
        public float CurrentOutput;
        public float Heat;
        public byte Online;
    }
}


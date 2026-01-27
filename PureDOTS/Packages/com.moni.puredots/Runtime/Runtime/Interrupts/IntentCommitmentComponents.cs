using Unity.Entities;

namespace PureDOTS.Runtime.Interrupts
{
    /// <summary>
    /// Tunable commitment guardrails for intent changes to prevent thrashing.
    /// </summary>
    public struct IntentCommitmentConfig : IComponentData
    {
        public uint CommitmentTicks;
        public uint ReplanCooldownTicks;
        public InterruptPriority OverridePriority;

        public static IntentCommitmentConfig Default => new IntentCommitmentConfig
        {
            CommitmentTicks = 30,
            ReplanCooldownTicks = 15,
            OverridePriority = InterruptPriority.High
        };
    }

    /// <summary>
    /// Runtime commitment state updated when intents change.
    /// </summary>
    public struct IntentCommitmentState : IComponentData
    {
        public uint LockUntilTick;
        public uint CooldownUntilTick;
        public uint LastIntentTick;
    }
}

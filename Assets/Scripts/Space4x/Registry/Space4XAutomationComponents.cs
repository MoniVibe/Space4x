using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Flags for enabled automation policies.
    /// </summary>
    [System.Flags]
    public enum AutomationFlags : ushort
    {
        None = 0,

        /// <summary>
        /// Automatically seek repairs when damaged.
        /// </summary>
        AutoRepair = 1 << 0,

        /// <summary>
        /// Automatically return to carrier when resources low.
        /// </summary>
        AutoReturn = 1 << 1,

        /// <summary>
        /// Automatically escalate stance when threats detected.
        /// </summary>
        StanceEscalation = 1 << 2,

        /// <summary>
        /// Automatically balance cargo between vessels.
        /// </summary>
        ResourceBalance = 1 << 3,

        /// <summary>
        /// Automatically engage hostile targets.
        /// </summary>
        AutoEngage = 1 << 4,

        /// <summary>
        /// Automatically evade when heavily damaged.
        /// </summary>
        AutoEvade = 1 << 5,

        /// <summary>
        /// Automatically request resupply when low.
        /// </summary>
        AutoResupply = 1 << 6,

        /// <summary>
        /// Automatically launch craft when carrier threatened.
        /// </summary>
        AutoLaunch = 1 << 7,

        /// <summary>
        /// Automatically dock craft when operations complete.
        /// </summary>
        AutoDock = 1 << 8,

        /// <summary>
        /// Automatically assign idle vessels to tasks.
        /// </summary>
        AutoAssign = 1 << 9,

        /// <summary>
        /// Allow captain to deviate based on outlook (chaotic behavior).
        /// </summary>
        AllowDeviation = 1 << 10,

        /// <summary>
        /// All standard automations enabled.
        /// </summary>
        Standard = AutoRepair | AutoReturn | AutoEvade | AutoDock,

        /// <summary>
        /// Combat-focused automation set.
        /// </summary>
        Combat = AutoRepair | AutoEngage | AutoEvade | StanceEscalation | AutoLaunch,

        /// <summary>
        /// Logistics-focused automation set.
        /// </summary>
        Logistics = AutoReturn | ResourceBalance | AutoResupply | AutoDock | AutoAssign
    }

    /// <summary>
    /// Automation policy configuration for a vessel/carrier.
    /// </summary>
    public struct AutomationPolicy : IComponentData
    {
        /// <summary>
        /// Enabled automation flags.
        /// </summary>
        public AutomationFlags Flags;

        /// <summary>
        /// Priority level for automated tasks (lower = higher priority).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// How aggressively to pursue automated actions [0, 1].
        /// </summary>
        public half Aggressiveness;

        /// <summary>
        /// Tick when policy was last changed.
        /// </summary>
        public uint LastChangeTick;

        public bool HasFlag(AutomationFlags flag) => (Flags & flag) != 0;

        public static AutomationPolicy Default => new AutomationPolicy
        {
            Flags = AutomationFlags.Standard,
            Priority = 5,
            Aggressiveness = (half)0.5f,
            LastChangeTick = 0
        };

        public static AutomationPolicy Combat => new AutomationPolicy
        {
            Flags = AutomationFlags.Combat,
            Priority = 3,
            Aggressiveness = (half)0.8f,
            LastChangeTick = 0
        };

        public static AutomationPolicy Logistics => new AutomationPolicy
        {
            Flags = AutomationFlags.Logistics,
            Priority = 5,
            Aggressiveness = (half)0.4f,
            LastChangeTick = 0
        };

        public static AutomationPolicy Passive => new AutomationPolicy
        {
            Flags = AutomationFlags.None,
            Priority = 10,
            Aggressiveness = (half)0f,
            LastChangeTick = 0
        };
    }

    /// <summary>
    /// Thresholds that trigger automated actions.
    /// </summary>
    public struct AutomationThresholds : IComponentData
    {
        /// <summary>
        /// Hull % below which AutoRepair triggers.
        /// </summary>
        public half RepairThreshold;

        /// <summary>
        /// Resource % below which AutoReturn triggers.
        /// </summary>
        public half ReturnThreshold;

        /// <summary>
        /// Hull % below which AutoEvade triggers.
        /// </summary>
        public half EvadeThreshold;

        /// <summary>
        /// Threat level above which StanceEscalation triggers.
        /// </summary>
        public half EscalationThreshold;

        /// <summary>
        /// Threat level above which AutoEngage triggers.
        /// </summary>
        public half EngageThreshold;

        /// <summary>
        /// Resource imbalance % that triggers ResourceBalance.
        /// </summary>
        public half BalanceThreshold;

        /// <summary>
        /// Resource % below which AutoResupply triggers.
        /// </summary>
        public half ResupplyThreshold;

        /// <summary>
        /// Threat level above which AutoLaunch triggers.
        /// </summary>
        public half LaunchThreshold;

        public static AutomationThresholds Default => new AutomationThresholds
        {
            RepairThreshold = (half)0.5f,    // Repair at 50% hull
            ReturnThreshold = (half)0.2f,    // Return at 20% resources
            EvadeThreshold = (half)0.25f,    // Evade at 25% hull
            EscalationThreshold = (half)0.6f, // Escalate stance at 60% threat
            EngageThreshold = (half)0.4f,    // Engage at 40% threat
            BalanceThreshold = (half)0.3f,   // Balance when 30% imbalance
            ResupplyThreshold = (half)0.3f,  // Resupply at 30%
            LaunchThreshold = (half)0.7f     // Launch all at 70% threat
        };

        public static AutomationThresholds Aggressive => new AutomationThresholds
        {
            RepairThreshold = (half)0.3f,
            ReturnThreshold = (half)0.1f,
            EvadeThreshold = (half)0.15f,
            EscalationThreshold = (half)0.4f,
            EngageThreshold = (half)0.2f,
            BalanceThreshold = (half)0.4f,
            ResupplyThreshold = (half)0.2f,
            LaunchThreshold = (half)0.5f
        };

        public static AutomationThresholds Cautious => new AutomationThresholds
        {
            RepairThreshold = (half)0.7f,
            ReturnThreshold = (half)0.4f,
            EvadeThreshold = (half)0.4f,
            EscalationThreshold = (half)0.8f,
            EngageThreshold = (half)0.6f,
            BalanceThreshold = (half)0.2f,
            ResupplyThreshold = (half)0.5f,
            LaunchThreshold = (half)0.9f
        };
    }

    /// <summary>
    /// Current automation state tracking.
    /// </summary>
    public struct AutomationState : IComponentData
    {
        /// <summary>
        /// Currently active automated behavior.
        /// </summary>
        public AutomationFlags ActiveBehavior;

        /// <summary>
        /// Tick when current behavior started.
        /// </summary>
        public uint BehaviorStartTick;

        /// <summary>
        /// Entity target for current behavior (if applicable).
        /// </summary>
        public Entity BehaviorTarget;

        /// <summary>
        /// Whether automation is temporarily suspended.
        /// </summary>
        public byte Suspended;

        /// <summary>
        /// Tick when suspension ends (0 = indefinite).
        /// </summary>
        public uint SuspendUntilTick;

        public bool IsSuspended => Suspended == 1;

        public static AutomationState Default => new AutomationState
        {
            ActiveBehavior = AutomationFlags.None,
            BehaviorStartTick = 0,
            BehaviorTarget = Entity.Null,
            Suspended = 0,
            SuspendUntilTick = 0
        };
    }

    /// <summary>
    /// Automation event log entry.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AutomationEventLog : IBufferElementData
    {
        /// <summary>
        /// Which automation triggered.
        /// </summary>
        public AutomationFlags Behavior;

        /// <summary>
        /// Tick when triggered.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Whether action was successful.
        /// </summary>
        public byte Success;

        /// <summary>
        /// Target entity if applicable.
        /// </summary>
        public Entity Target;
    }

    /// <summary>
    /// Utility functions for automation.
    /// </summary>
    public static class AutomationUtility
    {
        /// <summary>
        /// Checks if an automation should trigger based on current state.
        /// </summary>
        public static bool ShouldTrigger(
            AutomationFlags flag,
            in AutomationPolicy policy,
            in AutomationThresholds thresholds,
            in AutomationState state,
            float currentValue,
            uint currentTick)
        {
            // Check if automation is enabled
            if (!policy.HasFlag(flag))
            {
                return false;
            }

            // Check if suspended
            if (state.IsSuspended && (state.SuspendUntilTick == 0 || currentTick < state.SuspendUntilTick))
            {
                return false;
            }

            // Check if already doing this behavior
            if ((state.ActiveBehavior & flag) != 0)
            {
                return false;
            }

            // Check threshold
            float threshold = GetThreshold(flag, thresholds);
            return flag switch
            {
                // These trigger when value is BELOW threshold
                AutomationFlags.AutoRepair or
                AutomationFlags.AutoReturn or
                AutomationFlags.AutoEvade or
                AutomationFlags.AutoResupply => currentValue <= threshold,

                // These trigger when value is ABOVE threshold
                AutomationFlags.StanceEscalation or
                AutomationFlags.AutoEngage or
                AutomationFlags.AutoLaunch => currentValue >= threshold,

                // Balance triggers on absolute difference
                AutomationFlags.ResourceBalance => math.abs(currentValue - 0.5f) >= threshold,

                _ => false
            };
        }

        private static float GetThreshold(AutomationFlags flag, in AutomationThresholds thresholds)
        {
            return flag switch
            {
                AutomationFlags.AutoRepair => (float)thresholds.RepairThreshold,
                AutomationFlags.AutoReturn => (float)thresholds.ReturnThreshold,
                AutomationFlags.AutoEvade => (float)thresholds.EvadeThreshold,
                AutomationFlags.StanceEscalation => (float)thresholds.EscalationThreshold,
                AutomationFlags.AutoEngage => (float)thresholds.EngageThreshold,
                AutomationFlags.ResourceBalance => (float)thresholds.BalanceThreshold,
                AutomationFlags.AutoResupply => (float)thresholds.ResupplyThreshold,
                AutomationFlags.AutoLaunch => (float)thresholds.LaunchThreshold,
                _ => 0.5f
            };
        }
    }
}


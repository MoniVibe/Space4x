using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.AI.Threshold
{
    /// <summary>
    /// State relative to a threshold.
    /// </summary>
    public enum ThresholdState : byte
    {
        Safe = 0,           // Well above threshold
        Approaching = 1,    // Getting close to threshold
        Crossed = 2,        // Just crossed threshold
        Critical = 3,       // Far below threshold
        Recovering = 4      // Rising back toward safe
    }

    /// <summary>
    /// Direction of threshold comparison.
    /// </summary>
    public enum ThresholdDirection : byte
    {
        Below = 0,          // Trigger when value falls below threshold
        Above = 1           // Trigger when value rises above threshold
    }

    /// <summary>
    /// Action to take when threshold is crossed.
    /// </summary>
    public enum ThresholdActionType : byte
    {
        None = 0,
        Flee = 1,           // Retreat from danger
        Recall = 2,         // Return to base/carrier
        Resupply = 3,       // Seek supplies
        Rest = 4,           // Stop activity and recover
        Alert = 5,          // Notify player/AI
        Emergency = 6,      // Trigger emergency protocol
        Migrate = 7,        // Leave current location
        Request = 8         // Request assistance
    }

    /// <summary>
    /// Definition of a single threshold.
    /// </summary>
    public struct ThresholdDefinition
    {
        public float TriggerValue;         // Value at which to trigger
        public float RecoveryValue;        // Value to recover (hysteresis)
        public ThresholdDirection Direction;
        public ThresholdActionType Action;
        public float UrgencyMultiplier;    // How urgent when triggered
    }

    /// <summary>
    /// Current state for a monitored resource.
    /// </summary>
    public struct ResourceThresholdState : IComponentData
    {
        public float CurrentValue;
        public float MaxValue;
        public ThresholdState State;
        public ThresholdActionType ActiveAction;
        public float CurrentUrgency;       // 0-1, how urgent is the situation
        public uint StateChangedTick;
        public byte ActionInProgress;      // Is the triggered action being executed
    }

    /// <summary>
    /// Configuration for threshold monitoring.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ThresholdConfig : IBufferElementData
    {
        public FixedString32Bytes ResourceId;  // Which resource this monitors
        public ThresholdDefinition Warning;    // First level warning
        public ThresholdDefinition Critical;   // Serious threshold
        public ThresholdDefinition Emergency;  // Emergency threshold
        public byte IsEnabled;
    }

    /// <summary>
    /// Event emitted when threshold is crossed.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ThresholdEvent : IBufferElementData
    {
        public FixedString32Bytes ResourceId;
        public ThresholdState OldState;
        public ThresholdState NewState;
        public ThresholdActionType TriggeredAction;
        public float ValueAtTrigger;
        public uint Tick;
    }

    /// <summary>
    /// Generic threshold set for common resources.
    /// </summary>
    public struct CommonThresholds : IComponentData
    {
        // Health/Hull
        public float HealthWarning;         // e.g., 0.5 (50%)
        public float HealthCritical;        // e.g., 0.25 (25%)
        public float HealthEmergency;       // e.g., 0.1 (10%)
        
        // Energy/Fuel
        public float EnergyWarning;
        public float EnergyCritical;
        public float EnergyEmergency;
        
        // Ammo/Supplies
        public float SupplyWarning;
        public float SupplyCritical;
        public float SupplyEmergency;
        
        // Recovery values (for hysteresis)
        public float RecoveryMargin;        // How much above threshold to recover
    }
}


using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.AI.Threshold
{
    /// <summary>
    /// Static helpers for threshold-based behavior triggers.
    /// </summary>
    [BurstCompile]
    public static class ThresholdHelpers
    {
        /// <summary>
        /// Default common thresholds.
        /// </summary>
        public static CommonThresholds DefaultThresholds => new CommonThresholds
        {
            HealthWarning = 0.5f,
            HealthCritical = 0.25f,
            HealthEmergency = 0.1f,
            EnergyWarning = 0.3f,
            EnergyCritical = 0.15f,
            EnergyEmergency = 0.05f,
            SupplyWarning = 0.3f,
            SupplyCritical = 0.15f,
            SupplyEmergency = 0.05f,
            RecoveryMargin = 0.1f
        };

        /// <summary>
        /// Checks a value against a threshold definition.
        /// </summary>
        public static ThresholdState CheckThreshold(
            float currentValue,
            float maxValue,
            in ThresholdDefinition threshold,
            ThresholdState previousState)
        {
            float ratio = maxValue > 0 ? currentValue / maxValue : 0;
            
            bool isBelowTrigger = threshold.Direction == ThresholdDirection.Below
                ? ratio < threshold.TriggerValue
                : ratio > threshold.TriggerValue;
                
            bool isAboveRecovery = threshold.Direction == ThresholdDirection.Below
                ? ratio > threshold.RecoveryValue
                : ratio < threshold.RecoveryValue;

            // Hysteresis logic
            if (previousState == ThresholdState.Safe || previousState == ThresholdState.Approaching)
            {
                if (isBelowTrigger)
                    return ThresholdState.Crossed;
                if (IsApproaching(ratio, threshold))
                    return ThresholdState.Approaching;
                return ThresholdState.Safe;
            }
            else // Was triggered
            {
                if (isAboveRecovery)
                    return ThresholdState.Safe;
                if (IsCritical(ratio, threshold))
                    return ThresholdState.Critical;
                return ThresholdState.Recovering;
            }
        }

        /// <summary>
        /// Checks if value is approaching threshold.
        /// </summary>
        private static bool IsApproaching(float ratio, in ThresholdDefinition threshold)
        {
            float margin = 0.1f; // 10% warning before threshold
            if (threshold.Direction == ThresholdDirection.Below)
                return ratio < threshold.TriggerValue + margin && ratio >= threshold.TriggerValue;
            else
                return ratio > threshold.TriggerValue - margin && ratio <= threshold.TriggerValue;
        }

        /// <summary>
        /// Checks if value is critically past threshold.
        /// </summary>
        private static bool IsCritical(float ratio, in ThresholdDefinition threshold)
        {
            float criticalMargin = 0.5f; // 50% past threshold is critical
            if (threshold.Direction == ThresholdDirection.Below)
                return ratio < threshold.TriggerValue * criticalMargin;
            else
                return ratio > threshold.TriggerValue * (2f - criticalMargin);
        }

        /// <summary>
        /// Calculates urgency based on how far past threshold.
        /// </summary>
        public static float GetUrgency(float currentValue, float maxValue, in ThresholdDefinition threshold)
        {
            float ratio = maxValue > 0 ? currentValue / maxValue : 0;
            
            if (threshold.Direction == ThresholdDirection.Below)
            {
                if (ratio >= threshold.TriggerValue)
                    return 0; // Not triggered
                
                // Linear urgency from trigger to zero
                return math.saturate(1f - ratio / threshold.TriggerValue) * threshold.UrgencyMultiplier;
            }
            else
            {
                if (ratio <= threshold.TriggerValue)
                    return 0; // Not triggered
                    
                // Linear urgency from trigger to max
                float excess = ratio - threshold.TriggerValue;
                float maxExcess = 1f - threshold.TriggerValue;
                return math.saturate(excess / maxExcess) * threshold.UrgencyMultiplier;
            }
        }

        /// <summary>
        /// Evaluates multiple thresholds and returns the most urgent.
        /// </summary>
        public static ThresholdState EvaluateThresholds(
            float currentValue,
            float maxValue,
            in ThresholdDefinition warning,
            in ThresholdDefinition critical,
            in ThresholdDefinition emergency,
            ThresholdState previousState,
            out ThresholdActionType action,
            out float urgency)
        {
            // Check emergency first (most severe)
            var emergencyState = CheckThreshold(currentValue, maxValue, emergency, previousState);
            if (emergencyState == ThresholdState.Crossed || emergencyState == ThresholdState.Critical)
            {
                action = emergency.Action;
                urgency = GetUrgency(currentValue, maxValue, emergency);
                return emergencyState;
            }

            // Check critical
            var criticalState = CheckThreshold(currentValue, maxValue, critical, previousState);
            if (criticalState == ThresholdState.Crossed || criticalState == ThresholdState.Critical)
            {
                action = critical.Action;
                urgency = GetUrgency(currentValue, maxValue, critical);
                return criticalState;
            }

            // Check warning
            var warningState = CheckThreshold(currentValue, maxValue, warning, previousState);
            if (warningState == ThresholdState.Crossed || warningState == ThresholdState.Approaching)
            {
                action = warning.Action;
                urgency = GetUrgency(currentValue, maxValue, warning);
                return warningState;
            }

            action = ThresholdActionType.None;
            urgency = 0;
            return ThresholdState.Safe;
        }

        /// <summary>
        /// Checks if action should trigger based on state.
        /// </summary>
        public static bool ShouldTriggerAction(ThresholdState state)
        {
            return state == ThresholdState.Crossed ||
                   state == ThresholdState.Critical;
        }

        /// <summary>
        /// Checks if entity has recovered from threshold state.
        /// </summary>
        public static bool HasRecovered(ThresholdState state)
        {
            return state == ThresholdState.Safe;
        }

        /// <summary>
        /// Creates threshold definition for recall behavior.
        /// </summary>
        public static ThresholdDefinition CreateRecallThreshold(float triggerPercent, float recoveryPercent)
        {
            return new ThresholdDefinition
            {
                TriggerValue = triggerPercent,
                RecoveryValue = recoveryPercent,
                Direction = ThresholdDirection.Below,
                Action = ThresholdActionType.Recall,
                UrgencyMultiplier = 1f
            };
        }

        /// <summary>
        /// Creates threshold definition for flee behavior.
        /// </summary>
        public static ThresholdDefinition CreateFleeThreshold(float triggerPercent, float recoveryPercent)
        {
            return new ThresholdDefinition
            {
                TriggerValue = triggerPercent,
                RecoveryValue = recoveryPercent,
                Direction = ThresholdDirection.Below,
                Action = ThresholdActionType.Flee,
                UrgencyMultiplier = 1.5f
            };
        }

        /// <summary>
        /// Creates threshold definition for resupply behavior.
        /// </summary>
        public static ThresholdDefinition CreateResupplyThreshold(float triggerPercent, float recoveryPercent)
        {
            return new ThresholdDefinition
            {
                TriggerValue = triggerPercent,
                RecoveryValue = recoveryPercent,
                Direction = ThresholdDirection.Below,
                Action = ThresholdActionType.Resupply,
                UrgencyMultiplier = 0.8f
            };
        }

        /// <summary>
        /// Updates resource threshold state.
        /// </summary>
        public static ResourceThresholdState UpdateThresholdState(
            float currentValue,
            float maxValue,
            in ThresholdConfig config,
            ThresholdState previousState,
            uint currentTick)
        {
            var newState = EvaluateThresholds(
                currentValue,
                maxValue,
                config.Warning,
                config.Critical,
                config.Emergency,
                previousState,
                out var action,
                out var urgency);

            return new ResourceThresholdState
            {
                CurrentValue = currentValue,
                MaxValue = maxValue,
                State = newState,
                ActiveAction = action,
                CurrentUrgency = urgency,
                StateChangedTick = newState != previousState ? currentTick : 0,
                ActionInProgress = (byte)(ShouldTriggerAction(newState) ? 1 : 0)
            };
        }
    }
}


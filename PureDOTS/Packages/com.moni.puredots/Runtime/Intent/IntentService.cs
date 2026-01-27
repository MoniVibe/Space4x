using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Intent
{
    /// <summary>
    /// Static utility service for managing entity intents.
    /// Provides helper methods for setting, clearing, and validating intents.
    /// </summary>
    [BurstCompile]
    public static class IntentService
    {
        /// <summary>
        /// Sets an intent with a target entity.
        /// </summary>
        public static void SetIntent(
            ref EntityIntent intent,
            IntentMode mode,
            in Entity targetEntity,
            InterruptPriority priority,
            InterruptType triggeringInterrupt,
            uint currentTick)
        {
            intent.Mode = mode;
            intent.TargetEntity = targetEntity;
            intent.TargetPosition = float3.zero;
            intent.Priority = priority;
            intent.TriggeringInterrupt = triggeringInterrupt;
            intent.IntentSetTick = currentTick;
            intent.IsValid = 1;
        }

        /// <summary>
        /// Sets an intent with a target position.
        /// </summary>
        public static void SetIntent(
            ref EntityIntent intent,
            IntentMode mode,
            in float3 targetPosition,
            InterruptPriority priority,
            InterruptType triggeringInterrupt,
            uint currentTick)
        {
            intent.Mode = mode;
            intent.TargetEntity = Entity.Null;
            intent.TargetPosition = targetPosition;
            intent.Priority = priority;
            intent.TriggeringInterrupt = triggeringInterrupt;
            intent.IntentSetTick = currentTick;
            intent.IsValid = 1;
        }

        /// <summary>
        /// Sets an intent with both target entity and position.
        /// </summary>
        public static void SetIntent(
            ref EntityIntent intent,
            IntentMode mode,
            in Entity targetEntity,
            in float3 targetPosition,
            InterruptPriority priority,
            InterruptType triggeringInterrupt,
            uint currentTick)
        {
            intent.Mode = mode;
            intent.TargetEntity = targetEntity;
            intent.TargetPosition = targetPosition;
            intent.Priority = priority;
            intent.TriggeringInterrupt = triggeringInterrupt;
            intent.IntentSetTick = currentTick;
            intent.IsValid = 1;
        }

        /// <summary>
        /// Clears the intent, setting it to Idle.
        /// </summary>
        public static void ClearIntent(ref EntityIntent intent)
        {
            intent.Mode = IntentMode.Idle;
            intent.TargetEntity = Entity.Null;
            intent.TargetPosition = float3.zero;
            intent.Priority = InterruptPriority.Low;
            intent.TriggeringInterrupt = InterruptType.None;
            intent.IntentSetTick = 0;
            intent.IsValid = 0;
        }

        /// <summary>
        /// Validates that an intent is still valid.
        /// Checks if target entity exists (if specified) and if intent hasn't expired.
        /// </summary>
        [BurstCompile]
        public static bool ValidateIntent(
            in EntityIntent intent,
            in EntityStorageInfoLookup storageInfoLookup,
            uint currentTick,
            uint maxIntentAgeTicks = 600) // Default: 10 seconds at 60fps
        {
            if (intent.IsValid == 0)
            {
                return false;
            }

            // Check if intent has expired
            if (intent.IntentSetTick > 0)
            {
                uint age = currentTick - intent.IntentSetTick;
                if (age > maxIntentAgeTicks)
                {
                    return false;
                }
            }

            // Ensure target entity still exists when specified
            if (intent.TargetEntity != Entity.Null && !storageInfoLookup.Exists(intent.TargetEntity))
            {
                return false;
            }

            // Ensure required targets are still populated
            if (!HasValidTarget(intent) && RequiresTarget(intent.Mode))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the current intent mode.
        /// </summary>
        public static IntentMode GetCurrentIntent(in EntityIntent intent)
        {
            return intent.Mode;
        }

        /// <summary>
        /// Checks if an intent has a valid target (entity or position).
        /// </summary>
        public static bool HasValidTarget(in EntityIntent intent)
        {
            return intent.TargetEntity != Entity.Null || math.any(intent.TargetPosition != float3.zero);
        }

        /// <summary>
        /// Checks if an intent can be overridden by a new interrupt.
        /// Higher priority interrupts can override lower priority intents.
        /// </summary>
        public static bool CanOverride(in EntityIntent currentIntent, InterruptPriority newPriority)
        {
            return (byte)newPriority > (byte)currentIntent.Priority;
        }

        [BurstCompile]
        private static bool RequiresTarget(IntentMode mode)
        {
            return mode switch
            {
                IntentMode.Idle => false,
                IntentMode.Patrol => false,
                IntentMode.Flee => false,
                _ => true
            };
        }
    }
}


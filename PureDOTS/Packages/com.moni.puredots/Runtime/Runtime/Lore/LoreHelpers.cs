using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Lore
{
    /// <summary>
    /// Static helpers for lore and discovery management.
    /// </summary>
    [BurstCompile]
    public static class LoreHelpers
    {
        /// <summary>
        /// Checks if position triggers zone lore.
        /// </summary>
        public static bool CheckZoneTrigger(
            float3 position,
            float3 previousPosition,
            in LoreTriggerZone zone)
        {
            bool wasInside = math.length(previousPosition - zone.Center) <= zone.Radius;
            bool isInside = math.length(position - zone.Center) <= zone.Radius;
            
            if (zone.TriggerOnEnter != 0 && !wasInside && isInside)
                return true;
            
            if (zone.TriggerOnExit != 0 && wasInside && !isInside)
                return true;
            
            if (zone.TriggerOnStay != 0 && isInside)
                return true;
            
            return false;
        }

        /// <summary>
        /// Selects best lore entry from available options.
        /// </summary>
        public static int SelectLoreEntry(
            in DynamicBuffer<ZoneLoreEntry> entries,
            in LoreContext context,
            uint currentTick,
            uint minCooldown,
            uint seed)
        {
            float totalWeight = 0;
            int validCount = 0;
            
            // Calculate valid entries and weights
            for (int i = 0; i < entries.Length; i++)
            {
                if (!IsEntryValid(entries[i], currentTick, minCooldown))
                    continue;
                
                totalWeight += entries[i].Weight;
                validCount++;
            }
            
            if (validCount == 0) return -1;
            
            // Weighted random selection
            var rng = new Random(seed);
            float roll = rng.NextFloat(0, totalWeight);
            float accumulated = 0;
            
            for (int i = 0; i < entries.Length; i++)
            {
                if (!IsEntryValid(entries[i], currentTick, minCooldown))
                    continue;
                
                accumulated += entries[i].Weight;
                if (roll <= accumulated)
                    return i;
            }
            
            return entries.Length > 0 ? 0 : -1;
        }

        private static bool IsEntryValid(
            in ZoneLoreEntry entry,
            uint currentTick,
            uint minCooldown)
        {
            // Check cooldown
            if (currentTick - entry.LastTriggeredTick < minCooldown)
                return false;
            
            return true;
        }

        /// <summary>
        /// Adds quote to delivery queue.
        /// </summary>
        public static bool QueueQuote(
            ref DynamicBuffer<PendingQuote> queue,
            in LoreDeliverySettings settings,
            FixedString128Bytes text,
            FixedString32Bytes speaker,
            float priority,
            uint currentTick,
            uint expirationDuration)
        {
            // Check queue capacity
            if (queue.Length >= settings.MaxQueueSize)
            {
                // Try to replace lowest priority
                int lowestIdx = -1;
                float lowestPriority = priority;
                for (int i = 0; i < queue.Length; i++)
                {
                    if (queue[i].Priority < lowestPriority)
                    {
                        lowestPriority = queue[i].Priority;
                        lowestIdx = i;
                    }
                }
                
                if (lowestIdx >= 0)
                    queue.RemoveAt(lowestIdx);
                else
                    return false; // Queue full with higher priority
            }
            
            // Check for duplicates
            if (settings.AllowDuplicates == 0)
            {
                for (int i = 0; i < queue.Length; i++)
                {
                    if (queue[i].Text.Equals(text))
                        return false;
                }
            }
            
            queue.Add(new PendingQuote
            {
                Text = text,
                SpeakerRole = speaker,
                Priority = priority,
                QueuedTick = currentTick,
                ExpiresAt = currentTick + expirationDuration
            });
            
            return true;
        }

        /// <summary>
        /// Gets next quote to deliver.
        /// </summary>
        public static bool TryDeliverQuote(
            ref DynamicBuffer<PendingQuote> queue,
            ref LoreDeliverySettings settings,
            uint currentTick,
            out PendingQuote quote)
        {
            quote = default;
            
            // Check delivery cooldown
            if (currentTick - settings.LastQuoteDeliveredTick < settings.MinQuoteInterval)
                return false;
            
            if (queue.Length == 0)
                return false;
            
            // Remove expired quotes
            for (int i = queue.Length - 1; i >= 0; i--)
            {
                if (queue[i].ExpiresAt < currentTick)
                    queue.RemoveAt(i);
            }
            
            if (queue.Length == 0)
                return false;
            
            // Find highest priority
            int bestIdx = 0;
            float bestPriority = queue[0].Priority;
            for (int i = 1; i < queue.Length; i++)
            {
                if (queue[i].Priority > bestPriority)
                {
                    bestPriority = queue[i].Priority;
                    bestIdx = i;
                }
            }
            
            quote = queue[bestIdx];
            queue.RemoveAt(bestIdx);
            settings.LastQuoteDeliveredTick = currentTick;
            
            return true;
        }

        /// <summary>
        /// Records a discovery.
        /// </summary>
        public static void RecordDiscovery(
            ref DynamicBuffer<DiscoveryLogEntry> log,
            FixedString64Bytes discoveryId,
            FixedString32Bytes category,
            float significance,
            uint currentTick)
        {
            // Check if already discovered
            for (int i = 0; i < log.Length; i++)
            {
                if (log[i].DiscoveryId.Equals(discoveryId))
                    return; // Already discovered
            }
            
            log.Add(new DiscoveryLogEntry
            {
                DiscoveryId = discoveryId,
                Category = category,
                DiscoveredTick = currentTick,
                Significance = significance,
                WasShared = 0
            });
        }

        /// <summary>
        /// Checks if discovery is new.
        /// </summary>
        public static bool IsNewDiscovery(
            in DynamicBuffer<DiscoveryLogEntry> log,
            FixedString64Bytes discoveryId)
        {
            for (int i = 0; i < log.Length; i++)
            {
                if (log[i].DiscoveryId.Equals(discoveryId))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets discovery count by category.
        /// </summary>
        public static int GetDiscoveryCount(
            in DynamicBuffer<DiscoveryLogEntry> log,
            FixedString32Bytes category)
        {
            int count = 0;
            for (int i = 0; i < log.Length; i++)
            {
                if (log[i].Category.Equals(category))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Filters lore by context.
        /// </summary>
        public static bool MatchesContext(
            in LoreEntry entry,
            in LoreContext context)
        {
            // Skip combat lore when not in combat
            if (entry.Category.Equals(new FixedString32Bytes("combat")) && 
                context.InCombat == 0)
                return false;
            
            // Skip happy quotes when mood is low
            if (entry.Category.Equals(new FixedString32Bytes("celebration")) && 
                context.MoodLevel < 5)
                return false;
            
            return true;
        }
    }
}


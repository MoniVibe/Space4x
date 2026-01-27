using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Registry.Aggregates
{
    /// <summary>
    /// Static helpers for aggregate registry operations.
    /// </summary>
    [BurstCompile]
    public static class AggregateHelpers
    {
        // Event type constants built using Append (Burst-compatible, avoids BC1016 and BC1091)
        private static FixedString32Bytes GetHarvestEventType()
        {
            var result = default(FixedString32Bytes);
            result.Append('h'); result.Append('a'); result.Append('r'); result.Append('v');
            result.Append('e'); result.Append('s'); result.Append('t');
            return result;
        }

        private static FixedString32Bytes GetBirthEventType()
        {
            var result = default(FixedString32Bytes);
            result.Append('b'); result.Append('i'); result.Append('r'); result.Append('t'); result.Append('h');
            return result;
        }

        private static FixedString32Bytes GetDeathEventType()
        {
            var result = default(FixedString32Bytes);
            result.Append('d'); result.Append('e'); result.Append('a'); result.Append('t'); result.Append('h');
            return result;
        }

        /// <summary>
        /// Default compression configuration.
        /// </summary>
        public static CompressionConfig DefaultCompressionConfig => new CompressionConfig
        {
            DistanceThreshold = 500f,
            InactivityThreshold = 3000, // ~50 seconds at 60 ticks/sec
            UpdateIntervalCompressed = 60, // Once per second
            MaxCompressionLevel = 2,
            AllowAutoCompression = true
        };

        /// <summary>
        /// Calculates aggregate statistics from member entities.
        /// </summary>
        public static void CalculateAggregateStats(
            ref AggregateRegistryEntry aggregate,
            in DynamicBuffer<AggregateMember> members,
            uint currentTick)
        {
            aggregate.EntityCount = members.Length;
            aggregate.ActiveCount = 0;
            aggregate.IdleCount = 0;

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].IsActive)
                    aggregate.ActiveCount++;
                else
                    aggregate.IdleCount++;
            }

            aggregate.LastUpdatedTick = currentTick;
        }

        /// <summary>
        /// Determines if a group should be compressed.
        /// </summary>
        public static bool ShouldCompress(
            in AggregateRegistryEntry aggregate,
            in CompressionConfig config,
            float distanceFromPlayer,
            uint currentTick)
        {
            if (!config.AllowAutoCompression)
                return false;

            if (aggregate.IsCompressed)
                return false;

            // Distance-based compression
            if (distanceFromPlayer > config.DistanceThreshold)
                return true;

            // Inactivity-based compression
            uint inactiveTicks = currentTick - aggregate.LastUpdatedTick;
            if (inactiveTicks > config.InactivityThreshold)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if a group should be decompressed.
        /// </summary>
        public static bool ShouldDecompress(
            in AggregateRegistryEntry aggregate,
            in CompressionConfig config,
            float distanceFromPlayer)
        {
            if (!aggregate.IsCompressed)
                return false;

            // Player is close enough to require full simulation
            return distanceFromPlayer < config.DistanceThreshold * 0.8f; // Hysteresis
        }

        /// <summary>
        /// Calculates compression level based on distance.
        /// </summary>
        public static byte CalculateCompressionLevel(
            float distanceFromPlayer,
            in CompressionConfig config)
        {
            if (distanceFromPlayer < config.DistanceThreshold)
                return 0; // Full detail

            float normalizedDistance = (distanceFromPlayer - config.DistanceThreshold) / config.DistanceThreshold;
            return (byte)math.min(config.MaxCompressionLevel, (int)(normalizedDistance + 1));
        }

        /// <summary>
        /// Calculates update interval based on compression level.
        /// </summary>
        public static uint GetUpdateInterval(byte compressionLevel, in CompressionConfig config)
        {
            // Higher compression = less frequent updates
            return config.UpdateIntervalCompressed * (uint)(1 << compressionLevel);
        }

        /// <summary>
        /// Adds a compressed event to the log.
        /// </summary>
        public static void AddCompressedEvent(
            ref DynamicBuffer<CompressedEvent> events,
            FixedString32Bytes eventType,
            uint tick,
            float magnitude)
        {
            // Try to merge with existing event of same type
            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.EventType.Equals(eventType) && tick - evt.EndTick < 60) // Within 1 second
                {
                    evt.EndTick = tick;
                    evt.Count++;
                    evt.Magnitude += magnitude;
                    events[i] = evt;
                    return;
                }
            }

            // Add new event
            if (events.Length >= events.Capacity)
            {
                // Remove oldest
                events.RemoveAt(0);
            }

            events.Add(new CompressedEvent
            {
                EventType = eventType,
                StartTick = tick,
                EndTick = tick,
                Count = 1,
                Magnitude = magnitude
            });
        }

        /// <summary>
        /// Simulates resource production for compressed groups.
        /// </summary>
        public static void SimulateProduction(
            ref AggregateRegistryEntry aggregate,
            in AggregateProduction production,
            float deltaTime)
        {
            aggregate.TotalFood += (production.FoodPerTick - production.FoodConsumptionPerTick) * deltaTime;
            aggregate.TotalGold += (production.GoldPerTick - production.GoldConsumptionPerTick) * deltaTime;
            aggregate.TotalWood += production.WoodPerTick * deltaTime;
            aggregate.TotalStone += production.StonePerTick * deltaTime;
            aggregate.TotalMetal += production.MetalPerTick * deltaTime;

            // Clamp to non-negative
            aggregate.TotalFood = math.max(0, aggregate.TotalFood);
            aggregate.TotalGold = math.max(0, aggregate.TotalGold);
        }

        /// <summary>
        /// Generates pseudo-history entries for compressed time period.
        /// </summary>
        public static void GeneratePseudoHistory(
            ref DynamicBuffer<PseudoHistoryEntry> history,
            in AggregateRegistryEntry aggregate,
            in AggregateProduction production,
            uint startTick,
            uint endTick,
            uint seed)
        {
            uint tickRange = endTick - startTick;
            
            // Cache event types (built using Append - Burst-compatible)
            var harvestEventType = GetHarvestEventType();
            var birthEventType = GetBirthEventType();
            var deathEventType = GetDeathEventType();
            
            // Generate production events
            if (production.FoodPerTick > 0)
            {
                float totalFood = production.FoodPerTick * tickRange;
                int harvestEvents = (int)(totalFood / 10f); // One event per 10 food
                
                for (int i = 0; i < harvestEvents && history.Length < history.Capacity; i++)
                {
                    uint eventTick = startTick + (DeterministicRandom(seed + (uint)i) % tickRange);
                    history.Add(new PseudoHistoryEntry
                    {
                        Tick = eventTick,
                        EventType = harvestEventType,
                        Value = 10f
                    });
                }
            }

            // Generate population events (births/deaths)
            if (aggregate.EntityCount > 10)
            {
                float birthRate = 0.001f; // Per entity per tick
                float deathRate = 0.0005f;
                
                int births = (int)(aggregate.EntityCount * birthRate * tickRange);
                int deaths = (int)(aggregate.EntityCount * deathRate * tickRange);

                for (int i = 0; i < births && history.Length < history.Capacity; i++)
                {
                    uint eventTick = startTick + (DeterministicRandom(seed + 1000 + (uint)i) % tickRange);
                    history.Add(new PseudoHistoryEntry
                    {
                        Tick = eventTick,
                        EventType = birthEventType,
                        Value = 1f
                    });
                }

                for (int i = 0; i < deaths && history.Length < history.Capacity; i++)
                {
                    uint eventTick = startTick + (DeterministicRandom(seed + 2000 + (uint)i) % tickRange);
                    history.Add(new PseudoHistoryEntry
                    {
                        Tick = eventTick,
                        EventType = deathEventType,
                        Value = 1f
                    });
                }
            }
        }

        /// <summary>
        /// Merges member entity data into aggregate.
        /// </summary>
        public static void MergeEntityData(
            ref AggregateRegistryEntry aggregate,
            float health,
            float happiness,
            float morale,
            float skillLevel,
            bool isCombatCapable,
            bool isWorker,
            bool isLeader)
        {
            // Running average calculation
            int count = aggregate.EntityCount + 1;
            aggregate.AverageHealth = ((aggregate.AverageHealth * aggregate.EntityCount) + health) / count;
            aggregate.AverageHappiness = ((aggregate.AverageHappiness * aggregate.EntityCount) + happiness) / count;
            aggregate.AverageMorale = ((aggregate.AverageMorale * aggregate.EntityCount) + morale) / count;
            aggregate.AverageSkillLevel = ((aggregate.AverageSkillLevel * aggregate.EntityCount) + skillLevel) / count;

            if (isCombatCapable) aggregate.CombatCapable++;
            if (isWorker) aggregate.WorkerCount++;
            if (isLeader) aggregate.LeaderCount++;
            
            aggregate.EntityCount = count;
        }

        /// <summary>
        /// Updates resource entry in buffer.
        /// </summary>
        public static void UpdateResourceEntry(
            ref DynamicBuffer<AggregateResourceEntry> resources,
            ushort resourceTypeId,
            float amount,
            float productionRate,
            float consumptionRate)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceTypeId == resourceTypeId)
                {
                    var entry = resources[i];
                    entry.TotalAmount = amount;
                    entry.ProductionRate = productionRate;
                    entry.ConsumptionRate = consumptionRate;
                    entry.NetChange = productionRate - consumptionRate;
                    resources[i] = entry;
                    return;
                }
            }

            // Add new entry
            resources.Add(new AggregateResourceEntry
            {
                ResourceTypeId = resourceTypeId,
                TotalAmount = amount,
                ProductionRate = productionRate,
                ConsumptionRate = consumptionRate,
                NetChange = productionRate - consumptionRate
            });
        }

        /// <summary>
        /// Simple deterministic random.
        /// </summary>
        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }
    }
}


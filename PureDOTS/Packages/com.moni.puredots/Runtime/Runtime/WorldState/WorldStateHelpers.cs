using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.WorldState
{
    /// <summary>
    /// Static helpers for world state management.
    /// </summary>
    [BurstCompile]
    public static class WorldStateHelpers
    {
        /// <summary>
        /// Default world transformation configuration.
        /// </summary>
        public static WorldTransformConfig DefaultConfig => new WorldTransformConfig
        {
            BiomeTransformDuration = 3600,   // 1 minute
            WaveExpansionRate = 1f,
            CounterSpawnCheckInterval = 36000, // 10 minutes
            AllowRebirthCycles = 1
        };

        /// <summary>
        /// Default apocalypse trigger configuration.
        /// </summary>
        public static ApocalypseTriggerConfig DefaultTriggerConfig => new ApocalypseTriggerConfig
        {
            TerritoryThreshold = 0.8f,
            UnchallengeDuration = 36000,     // 10 minutes
            RequireAllCivilizationsDestroyed = 0
        };

        /// <summary>
        /// Checks if apocalypse conditions are met.
        /// </summary>
        public static bool CheckApocalypseTrigger(
            in ApocalypseTrigger trigger,
            in ApocalypseTriggerConfig config)
        {
            if (trigger.TerritoryControl < config.TerritoryThreshold)
                return false;

            if (trigger.UnchallengeTicks < config.UnchallengeDuration)
                return false;

            if (config.RequireAllCivilizationsDestroyed != 0 && trigger.CivilizationsDestroyed == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Gets counter faction for apocalypse type.
        /// </summary>
        public static FixedString32Bytes GetCounterFaction(WorldStateType apocalypseType)
        {
            return apocalypseType switch
            {
                WorldStateType.DemonHellscape => new FixedString32Bytes("Angels"),
                WorldStateType.UndeadWasteland => new FixedString32Bytes("Paladins"),
                WorldStateType.FrozenEternity => new FixedString32Bytes("FireElementals"),
                WorldStateType.VerdantOvergrowth => new FixedString32Bytes("Industrialists"),
                WorldStateType.CelestialDominion => new FixedString32Bytes("Demons"),
                WorldStateType.VoidCorruption => new FixedString32Bytes("Guardians"),
                _ => new FixedString32Bytes("Heroes")
            };
        }

        /// <summary>
        /// Calculates wave radius at tick.
        /// </summary>
        public static float CalculateWaveRadius(
            uint startTick,
            uint currentTick,
            float expansionRate,
            float maxRadius)
        {
            uint elapsed = currentTick - startTick;
            float radius = elapsed * expansionRate;
            return math.min(radius, maxRadius);
        }

        /// <summary>
        /// Checks if position is within transformation wave.
        /// </summary>
        public static bool IsInTransformationWave(float3 position, in TransformationWave wave)
        {
            float dist = math.distance(position, wave.EpicenterPosition);
            return dist <= wave.CurrentRadius;
        }

        /// <summary>
        /// Updates biome transformation progress.
        /// </summary>
        public static float UpdateBiomeProgress(
            float currentProgress,
            uint startTick,
            uint currentTick,
            uint duration)
        {
            if (duration == 0) return 1f;
            
            uint elapsed = currentTick - startTick;
            return math.saturate((float)elapsed / duration);
        }

        /// <summary>
        /// Rolls for counter-apocalypse spawn.
        /// </summary>
        public static bool RollCounterSpawn(
            in CounterApocalypseSpawn spawn,
            uint currentTick,
            uint apocalypseStartTick,
            uint seed)
        {
            if (spawn.HasSpawned != 0)
                return false;

            if (currentTick - apocalypseStartTick < spawn.MinTicksBeforeSpawn)
                return false;

            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < spawn.SpawnProbability;
        }

        /// <summary>
        /// Creates world state transition.
        /// </summary>
        public static WorldState CreateTransition(
            WorldStateType from,
            WorldStateType to,
            Entity cause,
            uint tick)
        {
            return new WorldState
            {
                CurrentState = to,
                PreviousState = from,
                StateChangedTick = tick,
                TransitionProgress = 0f,
                DominantFaction = cause
            };
        }

        /// <summary>
        /// Updates transition progress.
        /// </summary>
        public static float UpdateTransitionProgress(
            float currentProgress,
            float rate)
        {
            return math.saturate(currentProgress + rate);
        }

        /// <summary>
        /// Gets spawn table for world state.
        /// </summary>
        public static FixedString64Bytes GetSpawnTableId(WorldStateType state)
        {
            return state switch
            {
                WorldStateType.DemonHellscape => new FixedString64Bytes("spawn_demon_hellscape"),
                WorldStateType.UndeadWasteland => new FixedString64Bytes("spawn_undead_wasteland"),
                WorldStateType.FrozenEternity => new FixedString64Bytes("spawn_frozen_eternity"),
                WorldStateType.VerdantOvergrowth => new FixedString64Bytes("spawn_verdant_overgrowth"),
                WorldStateType.CelestialDominion => new FixedString64Bytes("spawn_celestial_dominion"),
                WorldStateType.VoidCorruption => new FixedString64Bytes("spawn_void_corruption"),
                _ => new FixedString64Bytes("spawn_normal")
            };
        }

        /// <summary>
        /// Checks if world can return to normal.
        /// </summary>
        public static bool CanRebirth(in WorldState state, in WorldTransformConfig config)
        {
            if (config.AllowRebirthCycles == 0)
                return false;

            // Can rebirth if dominant faction is defeated
            return state.DominantFaction == Entity.Null;
        }

        /// <summary>
        /// Records world state event.
        /// </summary>
        public static void RecordEvent(
            ref DynamicBuffer<WorldStateEvent> events,
            WorldStateType from,
            WorldStateType to,
            uint tick,
            Entity cause,
            FixedString64Bytes description)
        {
            if (events.Length >= events.Capacity)
            {
                events.RemoveAt(0);
            }

            events.Add(new WorldStateEvent
            {
                FromState = from,
                ToState = to,
                EventTick = tick,
                CausingEntity = cause,
                EventDescription = description
            });
        }

        /// <summary>
        /// Gets biome target type for world state.
        /// </summary>
        public static byte GetTargetBiomeType(WorldStateType state, byte originalBiome)
        {
            // Simplified - real implementation would have full biome conversion table
            return state switch
            {
                WorldStateType.DemonHellscape => 100,     // Scorched
                WorldStateType.UndeadWasteland => 101,    // Blighted
                WorldStateType.FrozenEternity => 102,     // Frozen
                WorldStateType.VerdantOvergrowth => 103,  // Overgrown
                WorldStateType.CelestialDominion => 104,  // Sanctified
                WorldStateType.VoidCorruption => 105,     // Corrupted
                _ => originalBiome
            };
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


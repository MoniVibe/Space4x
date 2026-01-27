using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Discovery
{
    /// <summary>
    /// Static helpers for discovery and fog of war calculations.
    /// </summary>
    [BurstCompile]
    public static class DiscoveryHelpers
    {
        /// <summary>
        /// Checks if a position is discovered by an observer.
        /// </summary>
        public static bool IsDiscovered(
            float3 position,
            float3 observerPosition,
            in FogOfWarState fogState)
        {
            if (fogState.IsBlind != 0)
                return false;

            float distance = math.length(position - observerPosition);
            return distance <= fogState.VisionRange;
        }

        /// <summary>
        /// Reveals area around a position in visibility map.
        /// </summary>
        public static void RevealArea(
            float3 centerPosition,
            float radius,
            in VisibilityMap map,
            ref DynamicBuffer<VisibilityCell> cells,
            uint currentTick)
        {
            if (cells.Length == 0)
                return;

            float3 localPos = centerPosition - map.WorldMin;
            int centerX = (int)(localPos.x / map.CellSize);
            int centerZ = (int)(localPos.z / map.CellSize);
            int cellRadius = (int)math.ceil(radius / map.CellSize);

            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    int cellX = centerX + x;
                    int cellZ = centerZ + z;

                    if (cellX < 0 || cellX >= map.Resolution.x ||
                        cellZ < 0 || cellZ >= map.Resolution.y)
                        continue;

                    float dist = math.sqrt(x * x + z * z) * map.CellSize;
                    if (dist > radius)
                        continue;

                    int index = cellZ * map.Resolution.x + cellX;
                    if (index < 0 || index >= cells.Length)
                        continue;

                    var cell = cells[index];
                    cell.State = VisibilityState.Visible;
                    cell.LastSeenTick = currentTick;
                    cell.ObserverCount++;
                    cells[index] = cell;
                }
            }
        }

        /// <summary>
        /// Updates visibility decay (visible -> explored).
        /// </summary>
        public static void UpdateVisibilityDecay(
            ref DynamicBuffer<VisibilityCell> cells,
            float decayRate,
            uint currentTick)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                if (cell.State == VisibilityState.Visible && cell.ObserverCount == 0)
                {
                    // Decay to explored
                    float ticksSinceLastSeen = currentTick - cell.LastSeenTick;
                    if (ticksSinceLastSeen > decayRate)
                    {
                        cell.State = VisibilityState.Explored;
                        cells[i] = cell;
                    }
                }

                // Reset observer count for next frame
                cell.ObserverCount = 0;
                cells[i] = cell;
            }
        }

        /// <summary>
        /// Samples visibility at a position.
        /// </summary>
        public static VisibilityState SampleVisibility(
            float3 position,
            in VisibilityMap map,
            in DynamicBuffer<VisibilityCell> cells)
        {
            if (cells.Length == 0)
                return VisibilityState.Unknown;

            float3 local = position - map.WorldMin;
            int x = (int)(local.x / map.CellSize);
            int z = (int)(local.z / map.CellSize);

            x = math.clamp(x, 0, map.Resolution.x - 1);
            z = math.clamp(z, 0, map.Resolution.y - 1);

            int index = z * map.Resolution.x + x;
            if (index < 0 || index >= cells.Length)
                return VisibilityState.Unknown;

            return cells[index].State;
        }

        /// <summary>
        /// Checks if can unlock a technology.
        /// </summary>
        public static bool CanUnlockTech(
            in DynamicBuffer<TechPrerequisite> prerequisites,
            in FixedString64Bytes techId,
            in KnowledgePool knowledge)
        {
            float totalCost = 0f;
            bool hasPrereqs = true;

            for (int i = 0; i < prerequisites.Length; i++)
            {
                var prereq = prerequisites[i];
                if (!prereq.TechId.Equals(techId))
                    continue;

                totalCost = prereq.ResearchCost;

                if (prereq.IsMet == 0)
                {
                    hasPrereqs = false;
                    break;
                }

                if ((byte)knowledge.CurrentTier < (byte)prereq.RequiredTier)
                {
                    hasPrereqs = false;
                    break;
                }
            }

            return hasPrereqs && knowledge.ResearchPoints >= totalCost;
        }

        /// <summary>
        /// Accumulates research points.
        /// </summary>
        public static void AccumulateResearch(
            ref KnowledgePool knowledge,
            float deltaTime,
            uint currentTick)
        {
            float points = knowledge.ResearchRate * deltaTime;
            knowledge.ResearchPoints = math.min(
                knowledge.MaxStorage,
                knowledge.ResearchPoints + points);
            knowledge.LastAccumulationTick = currentTick;
        }

        /// <summary>
        /// Processes research progress on a technology.
        /// </summary>
        public static void ProcessResearch(
            ref TechnologyState tech,
            ref KnowledgePool knowledge,
            float researchRate,
            float deltaTime)
        {
            if (tech.IsResearching == 0 || tech.IsUnlocked != 0)
                return;

            float pointsToUse = math.min(knowledge.ResearchPoints, researchRate * deltaTime);
            knowledge.ResearchPoints -= pointsToUse;

            float progressDelta = pointsToUse / math.max(1f, tech.TotalCost);
            tech.ResearchProgress = math.min(1f, tech.ResearchProgress + progressDelta);
        }

        /// <summary>
        /// Completes technology research.
        /// </summary>
        public static bool TryCompleteTech(
            ref TechnologyState tech,
            uint currentTick)
        {
            if (tech.ResearchProgress < 1f)
                return false;

            tech.IsUnlocked = 1;
            tech.IsResearching = 0;
            tech.UnlockedTick = currentTick;
            return true;
        }

        /// <summary>
        /// Calculates discovery chance for hidden entity.
        /// </summary>
        public static float CalculateDiscoveryChance(
            in Discoverable discoverable,
            in ExplorerStats explorer,
            float distance)
        {
            if (discoverable.IsHidden != 0 && explorer.StealthDetection < discoverable.Difficulty)
                return 0f;

            float distanceFactor = 1f - (distance / math.max(1f, discoverable.DiscoveryRadius));
            distanceFactor = math.saturate(distanceFactor);

            float difficultyFactor = 1f - (discoverable.Difficulty * 0.01f);
            difficultyFactor = math.saturate(difficultyFactor);

            float explorerBonus = 1f + explorer.DiscoveryChanceBonus;

            return distanceFactor * difficultyFactor * explorerBonus;
        }

        /// <summary>
        /// Updates intel decay.
        /// </summary>
        public static void UpdateIntelDecay(
            ref IntelState intel,
            ref DynamicBuffer<IntelEntry> entries,
            uint currentTick)
        {
            float ticksSinceUpdate = currentTick - intel.LastUpdateTick;
            float decay = ticksSinceUpdate * intel.IntelDecay;

            intel.IntelLevel = math.max(0, intel.IntelLevel - decay);
            intel.IsAccurate = (byte)(decay < 100f ? 1 : 0);

            // Decay individual entries
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                float entryAge = currentTick - entry.ObservedTick;
                entry.Confidence = math.saturate(1f - entryAge * intel.IntelDecay * 0.001f);
                entries[i] = entry;
            }
        }

        /// <summary>
        /// Gets research cost for a tier.
        /// </summary>
        public static float GetTierResearchCost(
            ResearchTier tier,
            float baseCost,
            float tierMultiplier)
        {
            int tierLevel = (int)tier;
            return baseCost * math.pow(tierMultiplier, tierLevel);
        }

        /// <summary>
        /// Adds a discovery to memory.
        /// </summary>
        public static void AddToMemory(
            ref DynamicBuffer<ExplorationMemory> memory,
            float3 position,
            in FixedString32Bytes locationId,
            DiscoveryType type,
            float significance,
            uint currentTick)
        {
            // Check if already in memory
            for (int i = 0; i < memory.Length; i++)
            {
                if (memory[i].LocationId.Equals(locationId))
                {
                    // Update existing
                    var existing = memory[i];
                    existing.WasVisited = 1;
                    existing.Significance = math.max(existing.Significance, significance);
                    memory[i] = existing;
                    return;
                }
            }

            // Add new
            memory.Add(new ExplorationMemory
            {
                Position = position,
                LocationId = locationId,
                Type = type,
                DiscoveredTick = currentTick,
                Significance = significance,
                IsBookmarked = 0,
                WasVisited = 1
            });
        }

        /// <summary>
        /// Creates default fog of war state.
        /// </summary>
        public static FogOfWarState CreateDefaultFogState()
        {
            return new FogOfWarState
            {
                VisionRange = 50f,
                MemoryDecay = 0.001f,
                HasNightVision = 0,
                CanSeeStealth = 0,
                IsBlind = 0
            };
        }

        /// <summary>
        /// Creates default tech tree config.
        /// </summary>
        public static TechTreeConfig CreateDefaultTechConfig()
        {
            return new TechTreeConfig
            {
                BaseResearchRate = 1f,
                TierCostMultiplier = 2f,
                ParallelResearchPenalty = 0.5f,
                MaxParallelResearch = 3,
                RequirePrerequisites = 1
            };
        }

        /// <summary>
        /// Gets tier name.
        /// </summary>
        public static FixedString32Bytes GetTierName(ResearchTier tier)
        {
            return tier switch
            {
                ResearchTier.Primitive => "Primitive",
                ResearchTier.Basic => "Basic",
                ResearchTier.Intermediate => "Intermediate",
                ResearchTier.Advanced => "Advanced",
                ResearchTier.Experimental => "Experimental",
                ResearchTier.Transcendent => "Transcendent",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Calculates explorer vision bonus.
        /// </summary>
        public static float CalculateVisionRange(
            in FogOfWarState fogState,
            in ExplorerStats explorer,
            float timeOfDay)
        {
            float baseRange = fogState.VisionRange + explorer.BonusVisionRange;

            // Night penalty
            bool isNight = timeOfDay < 6f || timeOfDay > 20f;
            if (isNight && fogState.HasNightVision == 0)
            {
                baseRange *= 0.5f;
            }

            return baseRange;
        }
    }
}


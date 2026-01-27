using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Service for managing production cooperation and collaborative crafting.
    /// </summary>
    [BurstCompile]
    public static class ProductionCooperationService
    {
        /// <summary>
        /// Form a production team from entities.
        /// </summary>
        [BurstCompile]
        public static void FormProductionTeam(
            ref NativeList<Entity> entities,
            ProductionRole leaderRole,
            out ProductionTeam team)
        {
            team = new ProductionTeam
            {
                Leader = entities.Length > 0 ? entities[0] : Entity.Null,
                MemberCount = (byte)entities.Length,
                Cohesion = 0.5f, // Initial cohesion
                Status = ProductionTeamStatus.Forming
            };
        }

        /// <summary>
        /// Start collaborative crafting.
        /// </summary>
        [BurstCompile]
        public static void StartCollaborativeCrafting(
            in Entity teamEntity,
            in FixedString64Bytes itemName,
            uint currentTick,
            out CollaborativeCrafting crafting)
        {
            crafting = new CollaborativeCrafting
            {
                TeamEntity = teamEntity,
                ItemName = itemName,
                Phase = CraftingPhase.Planning,
                Progress = 0f,
                Quality = 0f,
                StartedTick = currentTick
            };
        }

        /// <summary>
        /// Update crafting progress.
        /// </summary>
        [BurstCompile]
        public static void UpdateCraftProgress(
            ref CollaborativeCrafting crafting,
            CraftingPhase phase,
            float progress,
            float cohesion)
        {
            crafting.Phase = phase;
            crafting.Progress = math.clamp(progress, 0f, 1f);
            
            // Quality increases with cohesion
            if (crafting.Progress >= 1f)
            {
                crafting.Quality = CalculateQuality(crafting.Quality, cohesion);
            }
        }

        /// <summary>
        /// Calculate final quality from cohesion.
        /// </summary>
        [BurstCompile]
        public static float CalculateQuality(float baseQuality, float cohesion)
        {
            // High cohesion → quality approaches max (1.0)
            // Formula: quality = baseQuality + (cohesion * (1.0 - baseQuality))
            return math.clamp(baseQuality + (cohesion * (1f - baseQuality)), 0f, 1f);
        }
    }
}


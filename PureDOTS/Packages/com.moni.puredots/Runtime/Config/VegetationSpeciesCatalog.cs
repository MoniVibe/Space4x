using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Config
{
    /// <summary>
    /// ScriptableObject defining vegetation species with data-driven growth parameters.
    /// Converts to blob asset for fast runtime lookup.
    /// </summary>
    [CreateAssetMenu(fileName = "VegetationSpeciesCatalog", menuName = "PureDOTS/Vegetation Species Catalog")]
    public class VegetationSpeciesCatalog : ScriptableObject
    {
        [Header("Global Settings")]
        public float globalHealthRecoveryScale = 1f;
        public float globalDamageScale = 1f;
        public float defaultSpawnDensity = 1f;

        [Header("Species Definitions")]
        public List<VegetationSpeciesDefinition> species = new List<VegetationSpeciesDefinition>();

        /// <summary>
        /// Gets the species definition by species type index.
        /// </summary>
        public VegetationSpeciesDefinition GetSpecies(byte speciesType)
        {
            if (speciesType < species.Count)
            {
                return species[speciesType];
            }
            return null;
        }
    }

    /// <summary>
    /// Definition for a single vegetation species.
    /// </summary>
    [Serializable]
    public class VegetationSpeciesDefinition
    {
        [Header("Identity")]
        public string speciesId = "Tree";
        public Sprite icon;

        [Header("Stage Durations (seconds)")]
        public float seedlingDuration = 30f;
        public float growingDuration = 120f;
        public float matureDuration = 60f;
        public float floweringDuration = 30f;
        public float fruitingDuration = 180f;
        public float dyingDuration = 60f;
        public float respawnDelay = 300f;

        [Header("Growth Settings")]
        public float baseGrowthRate = 0.5f;
        public float[] stageMultipliers = new float[] { 1f, 1f, 1f, 1f, 1f, 1f }; // Per stage
        public float[] seasonalMultipliers = new float[] { 1f, 1.2f, 0.8f, 0.6f }; // Spring, Summer, Autumn, Winter

        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float baselineRegen = 1f;
        public float damagePerDeficit = 10f;
        public float droughtToleranceSeconds = 60f;
        public float frostToleranceSeconds = 30f;

        [Header("Harvest Settings")]
        public float maxYieldPerCycle = 10f;
        public float harvestCooldown = 60f;
        public string resourceTypeId = "Wood";
        public float partialHarvestPenalty = 0.5f;

        [Header("Environment Thresholds")]
        public float desiredMinWater = 30f;
        public float desiredMaxWater = 100f;
        public float desiredMinLight = 50f;
        public float desiredMaxLight = 100f;
        public float desiredMinSoilQuality = 40f;
        public float desiredMaxSoilQuality = 100f;
        public float pollutionTolerance = 0.5f;
        public float windTolerance = 0.5f;

        [Header("Reproduction Settings")]
        public float reproductionCooldown = 300f;
        public int seedsPerEvent = 3;
        public float spreadRadius = 5f;
        public int offspringCapPerParent = 5;
        public float maturityRequirement = 0.8f; // Must be mature to reproduce
        public int gridCellPadding = 1;

        [Header("Random Seeds")]
        public uint growthSeed = 12345;
        public uint reproductionSeed = 67890;
        public uint lootSeed = 54321;
    }
}





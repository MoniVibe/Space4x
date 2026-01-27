#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject asset for defining villager archetypes.
    /// Each archetype defines base stats, needs decay rates, job preferences, and alignment lean.
    /// </summary>
    [CreateAssetMenu(fileName = "VillagerArchetypeCatalog", menuName = "PureDOTS/Villager Archetype Catalog")]
    public class VillagerArchetypeCatalog : ScriptableObject
    {
        [Serializable]
        public class ArchetypeDefinition
        {
            [Header("Identity")]
            public string archetypeName = "Default";
            
            [Header("Base Stats (0-100)")]
            [Range(0, 100)] public int basePhysique = 50;
            [Range(0, 100)] public int baseFinesse = 50;
            [Range(0, 100)] public int baseWillpower = 50;
            
            [Header("Needs Decay Rates (per tick, normalized)")]
            [Range(0f, 1f)] public float hungerDecayRate = 0.01f;
            [Range(0f, 1f)] public float energyDecayRate = 0.02f;
            [Range(0f, 1f)] public float moraleDecayRate = 0.005f;
            
            [Header("Job Preference Weights (0-100)")]
            [Range(0, 100)] public int gatherJobWeight = 50;
            [Range(0, 100)] public int buildJobWeight = 50;
            [Range(0, 100)] public int craftJobWeight = 50;
            [Range(0, 100)] public int combatJobWeight = 30;
            [Range(0, 100)] public int tradeJobWeight = 40;
            
            [Header("Alignment Lean (-100 to +100)")]
            [Range(-100, 100)] public int moralAxisLean = 0;
            [Range(-100, 100)] public int orderAxisLean = 0;
            [Range(-100, 100)] public int purityAxisLean = 0;
            
            [Header("Loyalty")]
            [Range(0, 100)] public int baseLoyalty = 50;
        }
        
        [Header("Archetype Definitions")]
        public List<ArchetypeDefinition> archetypes = new List<ArchetypeDefinition>();
        
        private void OnValidate()
        {
            // Ensure archetype names are unique
            var nameSet = new HashSet<string>();
            foreach (var archetype in archetypes)
            {
                if (string.IsNullOrEmpty(archetype.archetypeName))
                {
                    archetype.archetypeName = "Unnamed";
                }
                
                if (nameSet.Contains(archetype.archetypeName))
                {
                    Debug.LogWarning($"Duplicate archetype name: {archetype.archetypeName}");
                }
                nameSet.Add(archetype.archetypeName);
            }
        }
    }
}
#endif


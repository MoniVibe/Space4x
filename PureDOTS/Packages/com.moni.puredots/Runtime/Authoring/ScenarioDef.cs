using UnityEngine;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "ScenarioDef", menuName = "PureDOTS/Scenario Definition", order = 10)]
    public sealed class ScenarioDef : ScriptableObject
    {
        [Header("Game Mode Toggles")]
        [Tooltip("Enable Godgame slice (villages, villagers, terrain).")]
        public bool EnableGodgame = true;
        
        [Tooltip("Enable Space4X slice (carriers, asteroids, miners).")]
        public bool EnableSpace4x = true;

        [Tooltip("Enable economy/logistics systems.")]
        public bool EnableEconomy = false;

        [Header("Seeds")]
        [Tooltip("Deterministic seed for Godgame world generation.")]
        public uint GodgameSeed = 12345u;
        
        [Tooltip("Deterministic seed for Space4X world generation.")]
        public uint Space4xSeed = 67890u;

        [Header("Entity Counts")]
        [Min(0)]
        [Tooltip("Number of villages to spawn in Godgame.")]
        public int VillageCount = 1;
        
        [Min(1)]
        [Tooltip("Villagers per village.")]
        public int VillagersPerVillage = 3;
        
        [Min(0)]
        [Tooltip("Number of carriers to spawn in Space4X.")]
        public int CarrierCount = 1;
        
        [Min(0)]
        [Tooltip("Number of asteroids to spawn in Space4X.")]
        public int AsteroidCount = 2;
        
        [Min(0)]
        [Tooltip("Starting band count for Godgame.")]
        public int StartingBandCount = 0;

        [Header("Difficulty & Density")]
        [Range(0f, 1f)]
        [Tooltip("Difficulty multiplier (affects resource scarcity, enemy strength, etc).")]
        public float Difficulty = 0.5f;
        
        [Range(0f, 1f)]
        [Tooltip("Density multiplier (affects entity spacing, resource node density, etc).")]
        public float Density = 0.5f;
    }
}

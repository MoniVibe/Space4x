using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Serializable stat data for scenario entity seeding.
    /// </summary>
    [Serializable]
    public class ScenarioEntityStatData
    {
        public string entityId = string.Empty;
        public string archetype = string.Empty;
        
        // IndividualStats fields (Space4X)
        public float command = 0f;
        public float tactics = 0f;
        public float logistics = 0f;
        public float diplomacy = 0f;
        public float engineering = 0f;
        public float resolve = 0f;
        
        // PhysiqueFinesseWill fields (Space4X)
        public float physique = 0f;
        public float finesse = 0f;
        public float will = 0f;
        public float physiqueInclination = 0f;
        public float finesseInclination = 0f;
        public float willInclination = 0f;
        public float generalXP = 0f;
        
        // Expertise entries (Space4X)
        public ScenarioExpertiseEntry[] expertise = Array.Empty<ScenarioExpertiseEntry>();
        
        // Service traits (Space4X)
        public string[] traits = Array.Empty<string>();
        
        // VillagerNeeds fields (Godgame)
        public byte food = 100;
        public byte rest = 100;
        public byte sleep = 100;
        public byte generalHealth = 100;
        public float health = 100f;
        public float maxHealth = 100f;
        public float energy = 100f;
        
        // VillagerMood fields (Godgame)
        public float mood = 50f;
        
        // VillagerCombatStats fields (Godgame)
        public float attackDamage = 0f;
        public float attackSpeed = 0f;
    }

    [Serializable]
    public class ScenarioExpertiseEntry
    {
        public string type = string.Empty;
        public byte tier = 0;
    }

    /// <summary>
    /// Extended scenario definition with stat seeding support.
    /// </summary>
    [Serializable]
    public class ScenarioDefinitionDataWithStats : ScenarioDefinitionData
    {
        public ScenarioEntityStatData[] entities = Array.Empty<ScenarioEntityStatData>();
    }

    /// <summary>
    /// Component attached to entities that need stat seeding.
    /// Contains the entity ID to match against scenario stat data.
    /// </summary>
    public struct ScenarioStatSeedRequest : IComponentData
    {
        public FixedString64Bytes EntityId;
        public FixedString64Bytes Archetype;
    }

    /// <summary>
    /// Utilities for converting JSON stat data to ECS components.
    /// </summary>
    public static class ScenarioStatSeedingUtilities
    {
        /// <summary>
        /// Apply stat seeding data to an entity based on scenario JSON.
        /// </summary>
        public static void ApplyStatSeeding(
            EntityManager entityManager,
            Entity entity,
            ScenarioEntityStatData statData)
        {
            if (statData == null)
            {
                return;
            }

            // Apply IndividualStats (Space4X)
            if (HasAnyIndividualStat(statData))
            {
                entityManager.AddComponent<IndividualStats>(entity);
                var stats = new IndividualStats
                {
                    Command = (half)math.clamp(statData.command, 0f, 100f),
                    Tactics = (half)math.clamp(statData.tactics, 0f, 100f),
                    Logistics = (half)math.clamp(statData.logistics, 0f, 100f),
                    Diplomacy = (half)math.clamp(statData.diplomacy, 0f, 100f),
                    Engineering = (half)math.clamp(statData.engineering, 0f, 100f),
                    Resolve = (half)math.clamp(statData.resolve, 0f, 100f)
                };
                entityManager.SetComponentData(entity, stats);
            }

            // Apply PhysiqueFinesseWill (Space4X)
            if (HasAnyPhysiqueFinesseWill(statData))
            {
                entityManager.AddComponent<PhysiqueFinesseWill>(entity);
                var pfw = new PhysiqueFinesseWill
                {
                    Physique = (half)math.clamp(statData.physique, 1f, 10f),
                    Finesse = (half)math.clamp(statData.finesse, 1f, 10f),
                    Will = (half)math.clamp(statData.will, 1f, 10f),
                    PhysiqueInclination = (half)math.clamp(statData.physiqueInclination, 1f, 10f),
                    FinesseInclination = (half)math.clamp(statData.finesseInclination, 1f, 10f),
                    WillInclination = (half)math.clamp(statData.willInclination, 1f, 10f),
                    GeneralXP = math.max(0f, statData.generalXP)
                };
                entityManager.SetComponentData(entity, pfw);
            }

            // Apply ExpertiseEntry buffer (Space4X)
            if (statData.expertise != null && statData.expertise.Length > 0)
            {
                var expertiseBuffer = entityManager.AddBuffer<ExpertiseEntry>(entity);
                foreach (var entry in statData.expertise)
                {
                    if (!string.IsNullOrEmpty(entry.type))
                    {
                        expertiseBuffer.Add(new ExpertiseEntry
                        {
                            Type = new FixedString32Bytes(entry.type),
                            Tier = entry.tier
                        });
                    }
                }
            }

            // Apply ServiceTrait buffer (Space4X)
            if (statData.traits != null && statData.traits.Length > 0)
            {
                var traitBuffer = entityManager.AddBuffer<ServiceTrait>(entity);
                foreach (var traitId in statData.traits)
                {
                    if (!string.IsNullOrEmpty(traitId))
                    {
                        traitBuffer.Add(new ServiceTrait
                        {
                            Id = new FixedString32Bytes(traitId)
                        });
                    }
                }
            }

            // Apply VillagerNeeds (Godgame)
            if (HasAnyVillagerNeed(statData))
            {
                entityManager.AddComponent<VillagerNeeds>(entity);
                var needs = new VillagerNeeds
                {
                    Food = (byte)math.clamp(statData.food, 0, 100),
                    Rest = (byte)math.clamp(statData.rest, 0, 100),
                    Sleep = (byte)math.clamp(statData.sleep, 0, 100),
                    GeneralHealth = (byte)math.clamp(statData.generalHealth, 0, 100),
                    Health = math.max(0f, statData.health),
                    MaxHealth = math.max(1f, statData.maxHealth),
                    Energy = math.clamp(statData.energy, 0f, 100f)
                };
                entityManager.SetComponentData(entity, needs);
            }

            // Apply VillagerMood (Godgame)
            if (statData.mood > 0f || statData.mood < 100f)
            {
                entityManager.AddComponent<VillagerMood>(entity);
                var mood = new VillagerMood
                {
                    Mood = math.clamp(statData.mood, 0f, 100f)
                };
                entityManager.SetComponentData(entity, mood);
            }

            // Apply VillagerCombatStats (Godgame)
            if (statData.attackDamage > 0f || statData.attackSpeed > 0f)
            {
                entityManager.AddComponent<VillagerCombatStats>(entity);
                var combatStats = new VillagerCombatStats
                {
                    AttackDamage = math.max(0f, statData.attackDamage),
                    AttackSpeed = math.max(0f, statData.attackSpeed),
                    CurrentTarget = Entity.Null
                };
                entityManager.SetComponentData(entity, combatStats);
            }
        }

        private static bool HasAnyIndividualStat(ScenarioEntityStatData data)
        {
            return data.command > 0f || data.tactics > 0f || data.logistics > 0f ||
                   data.diplomacy > 0f || data.engineering > 0f || data.resolve > 0f;
        }

        private static bool HasAnyPhysiqueFinesseWill(ScenarioEntityStatData data)
        {
            return data.physique > 0f || data.finesse > 0f || data.will > 0f ||
                   data.physiqueInclination > 0f || data.finesseInclination > 0f ||
                   data.willInclination > 0f || data.generalXP > 0f;
        }

        private static bool HasAnyVillagerNeed(ScenarioEntityStatData data)
        {
            return data.food < 100 || data.rest < 100 || data.sleep < 100 ||
                   data.generalHealth < 100 || data.health < data.maxHealth ||
                   data.energy < 100f;
        }
    }
}


using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Stats
{
    /// <summary>
    /// Tests for stat seeding from scenario JSON.
    /// </summary>
    public class StatSeedingTests
    {
        [Test]
        public void ScenarioStatSeedingUtilities_ApplyIndividualStats()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                command = 75f,
                tactics = 60f,
                logistics = 50f,
                diplomacy = 80f,
                engineering = 45f,
                resolve = 70f
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasComponent<IndividualStats>(entity));
            var stats = entityManager.GetComponentData<IndividualStats>(entity);
            Assert.AreEqual((half)75f, stats.Command);
            Assert.AreEqual((half)60f, stats.Tactics);
            Assert.AreEqual((half)50f, stats.Logistics);
            Assert.AreEqual((half)80f, stats.Diplomacy);
            Assert.AreEqual((half)45f, stats.Engineering);
            Assert.AreEqual((half)70f, stats.Resolve);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyPhysiqueFinesseWill()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                physique = 6.5f,
                finesse = 7.0f,
                will = 5.5f,
                physiqueInclination = 7f,
                finesseInclination = 8f,
                willInclination = 5f,
                generalXP = 150f
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasComponent<PhysiqueFinesseWill>(entity));
            var pfw = entityManager.GetComponentData<PhysiqueFinesseWill>(entity);
            Assert.AreEqual((half)6.5f, pfw.Physique);
            Assert.AreEqual((half)7.0f, pfw.Finesse);
            Assert.AreEqual((half)5.5f, pfw.Will);
            Assert.AreEqual((half)7f, pfw.PhysiqueInclination);
            Assert.AreEqual((half)8f, pfw.FinesseInclination);
            Assert.AreEqual((half)5f, pfw.WillInclination);
            Assert.AreEqual(150f, pfw.GeneralXP);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyVillagerNeeds()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                food = 100,
                rest = 85,
                sleep = 90,
                generalHealth = 95,
                health = 95f,
                maxHealth = 100f,
                energy = 85f
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasComponent<VillagerNeeds>(entity));
            var needs = entityManager.GetComponentData<VillagerNeeds>(entity);
            Assert.AreEqual(100, needs.Food);
            Assert.AreEqual(85, needs.Rest);
            Assert.AreEqual(90, needs.Sleep);
            Assert.AreEqual(95, needs.GeneralHealth);
            Assert.AreEqual(95f, needs.Health);
            Assert.AreEqual(100f, needs.MaxHealth);
            Assert.AreEqual(85f, needs.Energy);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyVillagerMood()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                mood = 75f
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasComponent<VillagerMood>(entity));
            var mood = entityManager.GetComponentData<VillagerMood>(entity);
            Assert.AreEqual(75f, mood.Mood);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyVillagerCombatStats()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                attackDamage = 10f,
                attackSpeed = 1.5f
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasComponent<VillagerCombatStats>(entity));
            var combatStats = entityManager.GetComponentData<VillagerCombatStats>(entity);
            Assert.AreEqual(10f, combatStats.AttackDamage);
            Assert.AreEqual(1.5f, combatStats.AttackSpeed);
            Assert.AreEqual(Entity.Null, combatStats.CurrentTarget);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyExpertiseBuffer()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                expertise = new[]
                {
                    new ScenarioExpertiseEntry { type = "CarrierCommand", tier = 5 },
                    new ScenarioExpertiseEntry { type = "Logistics", tier = 3 }
                }
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasBuffer<ExpertiseEntry>(entity));
            var expertiseBuffer = entityManager.GetBuffer<ExpertiseEntry>(entity);
            Assert.AreEqual(2, expertiseBuffer.Length);
            Assert.AreEqual(new FixedString32Bytes("CarrierCommand"), expertiseBuffer[0].Type);
            Assert.AreEqual(5, expertiseBuffer[0].Tier);
            Assert.AreEqual(new FixedString32Bytes("Logistics"), expertiseBuffer[1].Type);
            Assert.AreEqual(3, expertiseBuffer[1].Tier);

            world.Dispose();
        }

        [Test]
        public void ScenarioStatSeedingUtilities_ApplyServiceTraitBuffer()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var statData = new ScenarioEntityStatData
            {
                traits = new[] { "ReactorWhisperer", "TacticalSavant" }
            };

            ScenarioStatSeedingUtilities.ApplyStatSeeding(entityManager, entity, statData);

            Assert.IsTrue(entityManager.HasBuffer<ServiceTrait>(entity));
            var traitBuffer = entityManager.GetBuffer<ServiceTrait>(entity);
            Assert.AreEqual(2, traitBuffer.Length);
            Assert.AreEqual(new FixedString32Bytes("ReactorWhisperer"), traitBuffer[0].Id);
            Assert.AreEqual(new FixedString32Bytes("TacticalSavant"), traitBuffer[1].Id);

            world.Dispose();
        }
    }
}


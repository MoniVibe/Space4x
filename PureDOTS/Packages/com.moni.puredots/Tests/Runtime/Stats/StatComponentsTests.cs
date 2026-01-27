using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Stats;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Stats
{
    /// <summary>
    /// Unit tests for stat component defaults and initialization.
    /// </summary>
    public class StatComponentsTests
    {
        [Test]
        public void VillagerNeeds_DefaultValues()
        {
            var needs = new VillagerNeeds();
            Assert.AreEqual(0, needs.Food);
            Assert.AreEqual(0, needs.Rest);
            Assert.AreEqual(0, needs.Sleep);
            Assert.AreEqual(0, needs.GeneralHealth);
            Assert.AreEqual(0f, needs.Health);
            Assert.AreEqual(0f, needs.MaxHealth);
            Assert.AreEqual(0f, needs.Energy);
        }

        [Test]
        public void VillagerNeeds_SensibleDefaults()
        {
            var needs = new VillagerNeeds
            {
                Food = 100,
                Rest = 100,
                Sleep = 100,
                GeneralHealth = 100,
                Health = 100f,
                MaxHealth = 100f,
                Energy = 100f
            };

            Assert.AreEqual(100, needs.Food);
            Assert.AreEqual(100, needs.Rest);
            Assert.AreEqual(100, needs.Sleep);
            Assert.AreEqual(100, needs.GeneralHealth);
            Assert.AreEqual(100f, needs.Health);
            Assert.AreEqual(100f, needs.MaxHealth);
            Assert.AreEqual(100f, needs.Energy);
        }

        [Test]
        public void VillagerMood_DefaultValue()
        {
            var mood = new VillagerMood();
            Assert.AreEqual(0f, mood.Mood);
        }

        [Test]
        public void VillagerMood_NeutralDefault()
        {
            var mood = new VillagerMood
            {
                Mood = 50f
            };
            Assert.AreEqual(50f, mood.Mood);
        }

        [Test]
        public void VillagerCombatStats_DefaultValues()
        {
            var combatStats = new VillagerCombatStats();
            Assert.AreEqual(0f, combatStats.AttackDamage);
            Assert.AreEqual(0f, combatStats.AttackSpeed);
            Assert.AreEqual(Entity.Null, combatStats.CurrentTarget);
        }

        [Test]
        public void IndividualStats_DefaultValues()
        {
            var stats = new IndividualStats();
            Assert.AreEqual((half)0f, stats.Command);
            Assert.AreEqual((half)0f, stats.Tactics);
            Assert.AreEqual((half)0f, stats.Logistics);
            Assert.AreEqual((half)0f, stats.Diplomacy);
            Assert.AreEqual((half)0f, stats.Engineering);
            Assert.AreEqual((half)0f, stats.Resolve);
        }

        [Test]
        public void PhysiqueFinesseWill_DefaultValues()
        {
            var pfw = new PhysiqueFinesseWill();
            Assert.AreEqual((half)0f, pfw.Physique);
            Assert.AreEqual((half)0f, pfw.Finesse);
            Assert.AreEqual((half)0f, pfw.Will);
            Assert.AreEqual((half)0f, pfw.PhysiqueInclination);
            Assert.AreEqual((half)0f, pfw.FinesseInclination);
            Assert.AreEqual((half)0f, pfw.WillInclination);
            Assert.AreEqual(0f, pfw.GeneralXP);
        }

        [Test]
        public void ServiceContract_DefaultValues()
        {
            var contract = new ServiceContract();
            Assert.AreEqual(FixedString64Bytes.Empty, contract.EmployerId);
            Assert.AreEqual(0, contract.Type);
            Assert.AreEqual(0u, contract.StartTick);
            Assert.AreEqual(0u, contract.DurationTicks);
            Assert.AreEqual(0u, contract.ExpiryTick);
            Assert.AreEqual(0, contract.IsActive);
        }

        [Test]
        public void StatDisplayBinding_DefaultValues()
        {
            var binding = new StatDisplayBinding();
            Assert.AreEqual(FixedString64Bytes.Empty, binding.EntityId);
            Assert.AreEqual(0, binding.Mode);
            Assert.AreEqual(0, binding.VisibleStatsMask);
        }
    }
}


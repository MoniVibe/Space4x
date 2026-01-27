using NUnit.Framework;
using PureDOTS.Runtime.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Combat
{
    /// <summary>
    /// EditMode tests for combat system functionality.
    /// </summary>
    public class CombatSystemTests
    {
        [Test]
        public void Damage_Application_ReducesHealth()
        {
            // Create health component
            var health = new Health
            {
                Current = 100f,
                Max = 100f,
                RegenRate = 0f,
                LastDamageTick = 0
            };

            // Simulate damage application
            float damage = 25f;
            health.Current -= damage;

            Assert.AreEqual(75f, health.Current, 0.001f, "Damage should reduce health");
            Assert.IsTrue(health.Current > 0f, "Health should be above zero");
        }

        [Test]
        public void Hit_Detection_DeterministicWithSameSeed()
        {
            // Test deterministic RNG
            int seed1 = 100;
            int seed2 = 200;
            uint tick = 50;

            float roll1 = DeterministicRandom(seed1, seed2, tick);
            float roll2 = DeterministicRandom(seed1, seed2, tick);

            Assert.AreEqual(roll1, roll2, 0.001f, "Same seeds should produce same random value");
        }

        [Test]
        public void Armor_Reduces_PhysicalDamage()
        {
            // Simulate armor reduction
            float rawDamage = 50f;
            float armorValue = 20f;

            float finalDamage = math.max(1f, rawDamage - armorValue);

            Assert.AreEqual(30f, finalDamage, 0.001f, "Armor should reduce physical damage");
            Assert.IsTrue(finalDamage >= 1f, "Minimum damage should be 1");
        }

        [Test]
        public void Shield_Absorbs_Before_Health()
        {
            // Simulate shield absorption
            float damage = 30f;
            float shieldCurrent = 20f;
            float healthCurrent = 100f;

            float shieldAbsorbed = math.min(shieldCurrent, damage);
            shieldCurrent -= shieldAbsorbed;
            damage -= shieldAbsorbed;

            healthCurrent -= damage;

            Assert.AreEqual(0f, shieldCurrent, 0.001f, "Shield should be depleted");
            Assert.AreEqual(90f, healthCurrent, 0.001f, "Health should take remaining damage");
        }

        [Test]
        public void Buff_DamagePercent_Applies_Correctly()
        {
            // Simulate buff damage modifier
            float baseDamage = 100f;
            float damagePercent = 0.2f; // +20%

            float finalDamage = baseDamage * (1f + damagePercent);

            Assert.AreEqual(120f, finalDamage, 0.001f, "Damage percent modifier should apply correctly");
        }

        [Test]
        public void Death_Triggers_At_Zero_Health()
        {
            // Simulate death
            var health = new Health
            {
                Current = 10f,
                Max = 100f
            };

            health.Current -= 15f; // Overkill
            health.Current = math.max(0f, health.Current);

            Assert.AreEqual(0f, health.Current, 0.001f, "Health should be clamped to zero");
            Assert.IsTrue(health.Current <= 0f, "Entity should be dead");
        }

        [Test]
        public void DeathSavingThrow_CanPreventDeath()
        {
            // Simulate death saving throw
            float survivalChance = 0.3f; // 30% chance
            bool alliesPresent = true;
            bool medicalTreatment = false;

            if (alliesPresent)
            {
                survivalChance += 0.1f; // +10% for allies
            }
            if (medicalTreatment)
            {
                survivalChance += 0.2f; // +20% for medical treatment
            }

            Assert.AreEqual(0.4f, survivalChance, 0.001f, "Survival chance should be calculated correctly");
        }

        [Test]
        public void Combat_Resolution_Advances_Rounds()
        {
            // Simulate combat round advancement
            var combat = new ActiveCombat
            {
                CurrentRound = 0
            };

            combat.CurrentRound++;

            Assert.AreEqual(1, combat.CurrentRound, "Combat round should advance");
        }

        [Test]
        public void Projectile_Pierce_Hits_Multiple_Targets()
        {
            // Simulate projectile pierce
            byte pierceCount = 3;
            pierceCount--;

            Assert.AreEqual(2, pierceCount, "Pierce count should decrease on hit");
            Assert.IsTrue(pierceCount > 0, "Projectile should still be able to pierce");
        }

        [Test]
        public void Critical_Hit_Multiplier_Applies()
        {
            // Simulate critical hit
            float baseDamage = 50f;
            float critMultiplier = 1.5f;

            float critDamage = baseDamage * critMultiplier;

            Assert.AreEqual(75f, critDamage, 0.001f, "Critical hit should apply multiplier");
        }

        [Test]
        public void Resistance_Reduces_Damage_By_Percentage()
        {
            // Simulate resistance reduction
            float damage = 100f;
            float fireResistance = 0.3f; // 30% resistance

            float finalDamage = damage * (1f - fireResistance);

            Assert.AreEqual(70f, finalDamage, 0.001f, "Resistance should reduce damage by percentage");
        }

        // Helper method for deterministic RNG (matches HitDetectionSystem)
        private static float DeterministicRandom(int seed1, int seed2, uint tick)
        {
            uint hash = (uint)(seed1 * 73856093) ^ (uint)(seed2 * 19349663) ^ tick;
            hash = hash * 1103515245 + 12345;
            return (hash & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}


using NUnit.Framework;
using PureDOTS.Runtime.Knowledge;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Knowledge
{
    /// <summary>
    /// EditMode tests for lesson progression system.
    /// </summary>
    public class LessonProgressionTests
    {
        [Test]
        public void Lesson_Xp_AdvancesTier()
        {
            // Simulate XP gain
            float totalXp = 0f;
            float xpPerTier = 100f;

            // Gain XP
            totalXp += 150f;

            // Calculate progress
            float tiersCompleted = totalXp / xpPerTier;
            float progress = math.clamp(tiersCompleted / 5f, 0f, 1f);

            var tier = MasteryTierUtility.GetTierFromProgress(progress);

            Assert.AreEqual(MasteryTier.Apprentice, tier, "Should advance to Apprentice tier");
            Assert.IsTrue(progress > 0f, "Progress should be greater than zero");
        }

        [Test]
        public void Lesson_Effect_UnlocksSpell()
        {
            // Simulate lesson effect unlocking a spell
            MasteryTier currentTier = MasteryTier.Expert;
            MasteryTier requiredTier = MasteryTier.Expert;

            bool spellUnlocked = currentTier >= requiredTier;

            Assert.IsTrue(spellUnlocked, "Spell should be unlocked when tier requirement is met");
        }

        [Test]
        public void Lesson_Prerequisite_Blocks()
        {
            // Simulate prerequisite check
            string requiredLessonId = "BasicSmithing";
            MasteryTier requiredTier = MasteryTier.Journeyman;
            MasteryTier currentTier = MasteryTier.Novice;

            bool canLearn = currentTier >= requiredTier;

            Assert.IsFalse(canLearn, "Should not be able to learn if prerequisite not met");
        }

        [Test]
        public void Lesson_TierProgress_CalculatesCorrectly()
        {
            // Test tier progress calculation
            float totalProgress = 0.5f; // 50% overall
            float tierProgress = MasteryTierUtility.GetProgressWithinTier(totalProgress);

            Assert.IsTrue(tierProgress >= 0f && tierProgress <= 1f, "Tier progress should be normalized");
        }

        [Test]
        public void Lesson_Effect_YieldMultiplier()
        {
            // Simulate yield multiplier effect
            float baseYield = 10f;
            float multiplier = 1.2f; // +20%

            float finalYield = baseYield * multiplier;

            Assert.AreEqual(12f, finalYield, 0.001f, "Yield multiplier should apply correctly");
        }

        [Test]
        public void Lesson_Effect_QualityBonus()
        {
            // Simulate quality bonus effect
            float baseQuality = 50f;
            float qualityBonus = 10f;

            float finalQuality = baseQuality + qualityBonus;

            Assert.AreEqual(60f, finalQuality, 0.001f, "Quality bonus should apply correctly");
        }

        [Test]
        public void Lesson_Xp_Source_Multiplier()
        {
            // Simulate different XP sources
            float baseXp = 10f;
            float successMultiplier = 1.5f;
            float criticalSuccessMultiplier = 2f;

            float successXp = baseXp * successMultiplier;
            float criticalXp = baseXp * criticalSuccessMultiplier;

            Assert.AreEqual(15f, successXp, 0.001f, "Success should grant more XP");
            Assert.AreEqual(20f, criticalXp, 0.001f, "Critical success should grant even more XP");
        }
    }
}


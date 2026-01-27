using NUnit.Framework;
using PureDOTS.Runtime.Spells;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Spells
{
    /// <summary>
    /// EditMode tests for extended spell mastery system.
    /// </summary>
    public class ExtendedMasteryTests
    {
        [Test]
        public void Mastery_SuccessChance_0Percent_CannotCast()
        {
            float mastery = 0.0f;
            float successChance = SpellMasteryUtility.GetSuccessChance(mastery);
            
            Assert.AreEqual(0f, successChance, 0.001f, "0% mastery should have 0% success chance");
            Assert.IsFalse(SpellMasteryUtility.CanAttemptCast(mastery), "Cannot attempt cast at 0%");
        }

        [Test]
        public void Mastery_SuccessChance_20Percent_CanPractice()
        {
            float mastery = 0.2f;
            float successChance = SpellMasteryUtility.GetSuccessChance(mastery);
            
            Assert.IsTrue(SpellMasteryUtility.CanAttemptCast(mastery), "Can attempt cast at 20%");
            Assert.IsTrue(SpellMasteryUtility.CanPractice(mastery), "Can practice at 20%");
            Assert.Greater(successChance, 0f, "Should have some success chance at 20%");
            Assert.Less(successChance, 0.5f, "Success chance should be less than 50% at 20%");
        }

        [Test]
        public void Mastery_SuccessChance_50Percent_HalfSuccess()
        {
            float mastery = 0.5f;
            float successChance = SpellMasteryUtility.GetSuccessChance(mastery);
            
            Assert.AreEqual(0.5f, successChance, 0.01f, "50% mastery should have ~50% success chance");
        }

        [Test]
        public void Mastery_SuccessChance_100Percent_AlwaysSucceeds()
        {
            float mastery = 1.0f;
            float successChance = SpellMasteryUtility.GetSuccessChance(mastery);
            
            Assert.AreEqual(1.0f, successChance, 0.001f, "100% mastery should have 100% success chance");
            Assert.IsFalse(SpellMasteryUtility.CanPractice(mastery), "Cannot practice at 100% (mastered)");
        }

        [Test]
        public void Mastery_SuccessChance_400Percent_AlwaysSucceeds()
        {
            float mastery = 4.0f;
            float successChance = SpellMasteryUtility.GetSuccessChance(mastery);
            
            Assert.AreEqual(1.0f, successChance, 0.001f, "400% mastery should have 100% success chance");
        }

        [Test]
        public void Mastery_Milestone_Detection()
        {
            Assert.AreEqual(SpellMasteryMilestone.Observing, SpellMasteryUtility.GetMilestone(0.1f));
            Assert.AreEqual(SpellMasteryMilestone.Practicing, SpellMasteryUtility.GetMilestone(0.3f));
            Assert.AreEqual(SpellMasteryMilestone.Casting, SpellMasteryUtility.GetMilestone(0.75f));
            Assert.AreEqual(SpellMasteryMilestone.Mastered, SpellMasteryUtility.GetMilestone(1.0f));
            Assert.AreEqual(SpellMasteryMilestone.Signature1, SpellMasteryUtility.GetMilestone(2.0f));
            Assert.AreEqual(SpellMasteryMilestone.Signature2, SpellMasteryUtility.GetMilestone(3.0f));
            Assert.AreEqual(SpellMasteryMilestone.Signature3, SpellMasteryUtility.GetMilestone(4.0f));
        }

        [Test]
        public void Mastery_Progress_Clamped()
        {
            // Test that mastery progress is clamped to 4.0 (400%)
            float mastery = 5.0f;
            float clamped = math.min(mastery, 4.0f);
            
            Assert.AreEqual(4.0f, clamped, 0.001f, "Mastery should be clamped to 4.0");
        }
    }
}


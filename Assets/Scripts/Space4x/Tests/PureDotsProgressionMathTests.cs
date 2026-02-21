#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Progression;

namespace Space4X.Tests
{
    public sealed class PureDotsProgressionMathTests
    {
        [Test]
        public void ResolveSkill01FromPractice_ScalesByWisdomAndAptitude()
        {
            var novice = ProgressionMath.ResolveSkill01FromPractice(
                practiceSeconds: 300f,
                secondsToMastery: 1200f,
                wisdom01: 0f,
                aptitude01: 0f,
                wisdomMultiplierMin: 0.8f,
                wisdomMultiplierMax: 1.2f,
                aptitudeMultiplierMin: 0.8f,
                aptitudeMultiplierMax: 1.2f);
            var veteran = ProgressionMath.ResolveSkill01FromPractice(
                practiceSeconds: 300f,
                secondsToMastery: 1200f,
                wisdom01: 1f,
                aptitude01: 1f,
                wisdomMultiplierMin: 0.8f,
                wisdomMultiplierMax: 1.2f,
                aptitudeMultiplierMin: 0.8f,
                aptitudeMultiplierMax: 1.2f);

            Assert.Greater(veteran, novice);
            Assert.LessOrEqual(veteran, 1f);
        }

        [Test]
        public void ResolveLinearMilestoneCount_CountsReachedThresholds()
        {
            var count = ProgressionMath.ResolveLinearMilestoneCount(
                totalValue: 35f,
                baseThreshold: 10f,
                perMilestoneStep: 10f,
                maxMilestones: 8);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void AccumulatePositive_IgnoresNegativeDelta()
        {
            var value = 7f;
            ProgressionMath.AccumulatePositive(ref value, -2f);
            ProgressionMath.AccumulatePositive(ref value, 3f);
            Assert.AreEqual(10f, value, 1e-4f);
        }
    }
}
#endif

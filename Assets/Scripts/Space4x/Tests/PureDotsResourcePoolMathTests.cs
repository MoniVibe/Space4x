#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Math;
using PureDOTS.Runtime.Resources;

namespace Space4X.Tests
{
    public sealed class PureDotsResourcePoolMathTests
    {
        [Test]
        public void ResolveModifiedMax_AppliesAdditiveAndMultiplicativeModifiers()
        {
            var max = ResourcePoolMath.ResolveModifiedMax(100f, -20f, 0.75f);
            Assert.AreEqual(60f, max, 1e-4f);
        }

        [Test]
        public void Regen_RespectsCapAndDeltaTime()
        {
            var current = ResourcePoolMath.Regen(10f, 20f, 4f, 1.5f);
            Assert.AreEqual(16f, current, 1e-4f);

            var capped = ResourcePoolMath.Regen(19f, 20f, 4f, 1f);
            Assert.AreEqual(20f, capped, 1e-4f);
        }

        [Test]
        public void TrySpend_FailsWhenInsufficient()
        {
            var current = 5f;
            var spent = ResourcePoolMath.TrySpend(ref current, 8f);
            Assert.IsFalse(spent);
            Assert.AreEqual(5f, current, 1e-4f);
        }

        [Test]
        public void AccumulateModifier_UsesMultiplicativeComposition()
        {
            var accumulator = ResourcePoolModifier.Identity;
            ResourcePoolMath.AccumulateModifier(ref accumulator, new ResourcePoolModifier
            {
                AdditiveMax = 10f,
                MultiplicativeMax = 1.2f,
                AdditiveRegenPerSecond = 1f,
                MultiplicativeRegen = 0.9f
            });
            ResourcePoolMath.AccumulateModifier(ref accumulator, new ResourcePoolModifier
            {
                AdditiveMax = -5f,
                MultiplicativeMax = 0.5f,
                AdditiveRegenPerSecond = 2f,
                MultiplicativeRegen = 1.1f
            });

            Assert.AreEqual(5f, accumulator.AdditiveMax, 1e-4f);
            Assert.AreEqual(0.6f, accumulator.MultiplicativeMax, 1e-4f);
            Assert.AreEqual(3f, accumulator.AdditiveRegenPerSecond, 1e-4f);
            Assert.AreEqual(0.99f, accumulator.MultiplicativeRegen, 1e-4f);
        }
    }
}
#endif

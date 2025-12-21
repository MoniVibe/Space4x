#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Mathematics;

namespace Space4X.Tests.TestHarness
{
    public static class HalfUtil
    {
        public static half H(float value) => (half)value;
        public static float F(half value) => (float)value;

        public static void AreEqual(half actual, float expected, float epsilon = 1e-3f)
        {
            Assert.LessOrEqual(math.abs(F(actual) - expected), epsilon);
        }
    }
}
#endif

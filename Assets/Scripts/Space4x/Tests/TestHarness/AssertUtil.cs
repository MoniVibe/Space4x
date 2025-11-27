using NUnit.Framework;
using Unity.Mathematics;

namespace Space4X.Tests.TestHarness
{
    public static class AssertUtil
    {
        public static void Approximately(float actual, float expected, float epsilon = 1e-3f)
        {
            Assert.LessOrEqual(math.abs(actual - expected), epsilon);
        }

        public static void Conservation(float produced, float stored, float losses = 0f, float epsilon = 1e-3f)
        {
            Assert.LessOrEqual(math.abs(produced - (stored + losses)), epsilon);
        }
    }
}

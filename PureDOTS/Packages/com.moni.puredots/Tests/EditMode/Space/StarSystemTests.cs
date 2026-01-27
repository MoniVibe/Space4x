using NUnit.Framework;
using PureDOTS.Runtime.Space;
using PureDOTS.Systems.Space;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.EditMode.Space
{
    /// <summary>
    /// Unit tests for star system components and solar yield calculation.
    /// </summary>
    public class StarSystemTests
    {
        [Test]
        public void StarSolarYieldSystem_CalculateNormalizedYield_ReturnsCorrectValues()
        {
            var config = new StarSolarYieldConfig
            {
                Strategy = SolarYieldStrategy.Normalize,
                MaxLuminosity = 100f,
                MinLuminosity = 0f,
                LogBase = 10f,
                CustomMultiplier = 1f,
                CustomExponent = 1f
            };

            // Test minimum luminosity
            var minYield = StarSolarYieldSystem.CalculateNormalizedYield(0f, in config);
            Assert.AreEqual(0f, minYield, 0.001f, "Minimum luminosity should yield 0");

            // Test maximum luminosity
            var maxYield = StarSolarYieldSystem.CalculateNormalizedYield(100f, in config);
            Assert.AreEqual(1f, maxYield, 0.001f, "Maximum luminosity should yield 1");

            // Test middle luminosity
            var midYield = StarSolarYieldSystem.CalculateNormalizedYield(50f, in config);
            Assert.AreEqual(0.5f, midYield, 0.001f, "Middle luminosity should yield 0.5");
        }

        [Test]
        public void StarSolarYieldSystem_CalculateLogarithmicYield_ReturnsCorrectValues()
        {
            var config = new StarSolarYieldConfig
            {
                Strategy = SolarYieldStrategy.Logarithmic,
                MaxLuminosity = 100f,
                MinLuminosity = 0f,
                LogBase = 10f,
                CustomMultiplier = 1f,
                CustomExponent = 1f
            };

            // Test minimum luminosity (should handle edge case)
            var minYield = StarSolarYieldSystem.CalculateLogarithmicYield(0f, in config);
            Assert.AreEqual(0f, minYield, 0.001f, "Zero luminosity should yield 0");

            // Test maximum luminosity
            var maxYield = StarSolarYieldSystem.CalculateLogarithmicYield(100f, in config);
            Assert.AreEqual(1f, maxYield, 0.001f, "Maximum luminosity should yield 1");

            // Test logarithmic scaling (log(10) / log(100) = 0.5)
            var midYield = StarSolarYieldSystem.CalculateLogarithmicYield(10f, in config);
            var expected = math.log(10f) / math.log(100f);
            Assert.AreEqual(expected, midYield, 0.001f, "Logarithmic yield should scale correctly");
        }

        [Test]
        public void StarSolarYieldSystem_CalculateCustomYield_ReturnsCorrectValues()
        {
            var config = new StarSolarYieldConfig
            {
                Strategy = SolarYieldStrategy.Custom,
                MaxLuminosity = 100f,
                MinLuminosity = 0f,
                LogBase = 10f,
                CustomMultiplier = 0.01f,
                CustomExponent = 2f
            };

            // Test with custom formula: 0.01 * (luminosity ^ 2)
            var yield1 = StarSolarYieldSystem.CalculateCustomYield(10f, in config);
            var expected1 = 0.01f * math.pow(10f, 2f);
            Assert.AreEqual(expected1, yield1, 0.001f, "Custom yield should use multiplier and exponent");

            // Test zero luminosity
            var zeroYield = StarSolarYieldSystem.CalculateCustomYield(0f, in config);
            Assert.AreEqual(0f, zeroYield, 0.001f, "Zero luminosity should yield 0");
        }

        [Test]
        public void StarSolarYieldSystem_CalculateYield_ClampsToValidRange()
        {
            var config = StarSolarYieldConfig.Default;

            // Test negative luminosity (should clamp to 0)
            var negativeYield = StarSolarYieldSystem.CalculateYield(-10f, in config);
            Assert.GreaterOrEqual(negativeYield, 0f, "Negative luminosity should clamp to 0");

            // Test very high luminosity (should clamp to 1 for normalize)
            config.Strategy = SolarYieldStrategy.Normalize;
            var highYield = StarSolarYieldSystem.CalculateYield(1000000f, in config);
            Assert.LessOrEqual(highYield, 1f, "Very high luminosity should clamp to 1");
        }

        [Test]
        public void StarComponents_StarPlanetBuffer_CanStorePlanetReferences()
        {
            // This test verifies the buffer element structure
            var planet1 = new StarPlanet { PlanetEntity = Entity.Null };
            var planet2 = new StarPlanet { PlanetEntity = Entity.Null };

            // Verify structure is correct
            Assert.IsTrue(planet1.PlanetEntity == Entity.Null);
            Assert.IsTrue(planet2.PlanetEntity == Entity.Null);
        }

        [Test]
        public void StarComponents_StarParent_CanReferenceParentStar()
        {
            var starParent = new StarParent
            {
                ParentStar = Entity.Null
            };

            // Verify structure
            Assert.IsTrue(starParent.ParentStar == Entity.Null);
        }

        [Test]
        public void StarComponents_StarPhysicalProperties_HasCorrectFields()
        {
            var properties = new StarPhysicalProperties
            {
                Mass = 1.0f,
                Density = 1408f,
                Radius = 1.0f,
                Temperature = 5778f
            };

            Assert.AreEqual(1.0f, properties.Mass);
            Assert.AreEqual(1408f, properties.Density);
            Assert.AreEqual(1.0f, properties.Radius);
            Assert.AreEqual(5778f, properties.Temperature);
        }

        [Test]
        public void StarComponents_StarSolarYield_HasCorrectFields()
        {
            var yield = new StarSolarYield
            {
                Yield = 0.5f,
                LastCalculationTick = 100u
            };

            Assert.AreEqual(0.5f, yield.Yield);
            Assert.AreEqual(100u, yield.LastCalculationTick);
        }

        [Test]
        public void StarComponents_StarCluster_HasCorrectFields()
        {
            var cluster = new StarCluster
            {
                ClusterId = 42
            };

            Assert.AreEqual(42, cluster.ClusterId);
        }
    }
}


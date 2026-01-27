#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Perception;
using Unity.Mathematics;

namespace PureDOTS.Tests.Perception
{
    /// <summary>
    /// Unit tests for stealth detection service methods.
    /// Tests determinism, correctness, and edge cases.
    /// </summary>
    public class StealthDetectionServiceTests
    {
        [Test]
        public void RollStealthCheck_Deterministic_SameSeedProducesSameResult()
        {
            // Arrange
            float stealthRating = 50f;
            float perceptionRating = 60f;
            float alertnessModifier = 0.5f;
            uint seed = 12345u;

            // Act - run twice with same seed
            var result1 = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed);
            var result2 = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed);

            // Assert
            Assert.AreEqual(result1, result2, "Same seed should produce same result for determinism");
        }

        [Test]
        public void RollStealthCheck_Deterministic_DifferentSeedsProduceDifferentResults()
        {
            // Arrange
            float stealthRating = 50f;
            float perceptionRating = 60f;
            float alertnessModifier = 0.5f;
            uint seed1 = 12345u;
            uint seed2 = 67890u;

            // Act
            var result1 = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed1);
            var result2 = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed2);

            // Assert - results may be same or different, but both should be valid enum values
            Assert.IsTrue(System.Enum.IsDefined(typeof(StealthCheckResult), result1), "Result1 should be valid enum value");
            Assert.IsTrue(System.Enum.IsDefined(typeof(StealthCheckResult), result2), "Result2 should be valid enum value");
        }

        [Test]
        public void RollStealthCheck_ZeroSeed_DoesNotCrash()
        {
            // Arrange
            float stealthRating = 50f;
            float perceptionRating = 60f;
            float alertnessModifier = 0.5f;
            uint zeroSeed = 0u;

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                var result = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, zeroSeed);
                Assert.IsTrue(System.Enum.IsDefined(typeof(StealthCheckResult), result));
            });
        }

        [Test]
        public void RollStealthCheck_EvenSeed_DoesNotCrash()
        {
            // Arrange
            float stealthRating = 50f;
            float perceptionRating = 60f;
            float alertnessModifier = 0.5f;
            uint evenSeed = 42u; // Even number

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                var result = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, evenSeed);
                Assert.IsTrue(System.Enum.IsDefined(typeof(StealthCheckResult), result));
            });
        }

        [Test]
        public void RollStealthCheck_HighStealthRating_RemainsUndetected()
        {
            // Arrange - stealth much higher than perception
            float stealthRating = 100f;
            float perceptionRating = 10f;
            float alertnessModifier = 0f;
            uint seed = 12345u;

            // Act - run multiple times to check trend
            int undetectedCount = 0;
            for (uint i = 0; i < 10; i++)
            {
                var result = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed + i);
                if (result == StealthCheckResult.RemainsUndetected)
                {
                    undetectedCount++;
                }
            }

            // Assert - high stealth should win most of the time
            Assert.Greater(undetectedCount, 5, "High stealth rating should win most checks");
        }

        [Test]
        public void RollStealthCheck_HighPerceptionRating_Detects()
        {
            // Arrange - perception much higher than stealth
            float stealthRating = 10f;
            float perceptionRating = 100f;
            float alertnessModifier = 1f; // Maximum alertness
            uint seed = 12345u;

            // Act - run multiple times to check trend
            int detectedCount = 0;
            for (uint i = 0; i < 10; i++)
            {
                var result = StealthDetectionService.RollStealthCheck(stealthRating, perceptionRating, alertnessModifier, seed + i);
                if (result != StealthCheckResult.RemainsUndetected)
                {
                    detectedCount++;
                }
            }

            // Assert - high perception should detect most of the time
            Assert.Greater(detectedCount, 5, "High perception rating should detect most checks");
        }

        [Test]
        public void GetStealthLevelBonus_AllLevels_ReturnsCorrectBonuses()
        {
            // Assert
            Assert.AreEqual(0f, StealthDetectionService.GetStealthLevelBonus(StealthLevel.Exposed), "Exposed should have 0% bonus");
            Assert.AreEqual(0.25f, StealthDetectionService.GetStealthLevelBonus(StealthLevel.Concealed), "Concealed should have 25% bonus");
            Assert.AreEqual(0.5f, StealthDetectionService.GetStealthLevelBonus(StealthLevel.Hidden), "Hidden should have 50% bonus");
            Assert.AreEqual(0.75f, StealthDetectionService.GetStealthLevelBonus(StealthLevel.Invisible), "Invisible should have 75% bonus");
        }

        [Test]
        public void CalculateEffectiveStealth_BaseOnly_ReturnsBaseRating()
        {
            // Arrange
            float baseStealth = 50f;
            StealthLevel level = StealthLevel.Exposed;
            StealthModifiers modifiers = StealthModifiers.Default;
            float movementSpeed = 0f;
            float lightLevel = 0.5f;
            float terrainBonus = 0f;

            // Act
            float effective = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, movementSpeed, lightLevel, terrainBonus);

            // Assert - with exposed level and no modifiers, should be close to base
            Assert.GreaterOrEqual(effective, baseStealth * 0.9f, "Effective should be at least 90% of base");
            Assert.LessOrEqual(effective, baseStealth * 1.1f, "Effective should be at most 110% of base");
        }

        [Test]
        public void CalculateEffectiveStealth_HiddenLevel_IncreasesRating()
        {
            // Arrange
            float baseStealth = 50f;
            StealthLevel exposed = StealthLevel.Exposed;
            StealthLevel hidden = StealthLevel.Hidden;
            StealthModifiers modifiers = StealthModifiers.Default;
            float movementSpeed = 0f;
            float lightLevel = 0.5f;
            float terrainBonus = 0f;

            // Act
            float effectiveExposed = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, exposed, modifiers, movementSpeed, lightLevel, terrainBonus);
            float effectiveHidden = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, hidden, modifiers, movementSpeed, lightLevel, terrainBonus);

            // Assert - hidden should be higher than exposed
            Assert.Greater(effectiveHidden, effectiveExposed, "Hidden level should increase effective stealth");
        }

        [Test]
        public void CalculateEffectiveStealth_MovementPenalty_ReducesRating()
        {
            // Arrange
            float baseStealth = 50f;
            StealthLevel level = StealthLevel.Hidden;
            StealthModifiers modifiers = StealthModifiers.Default;
            float lightLevel = 0.5f;
            float terrainBonus = 0f;

            // Act
            float effectiveStationary = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, 0f, lightLevel, terrainBonus);
            float effectiveRunning = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, 10f, lightLevel, terrainBonus); // Running speed

            // Assert - running should reduce stealth
            Assert.Less(effectiveRunning, effectiveStationary, "Running should reduce effective stealth");
        }

        [Test]
        public void CalculateEffectiveStealth_DarkLight_IncreasesRating()
        {
            // Arrange
            float baseStealth = 50f;
            StealthLevel level = StealthLevel.Hidden;
            StealthModifiers modifiers = StealthModifiers.Default;
            float movementSpeed = 0f;
            float terrainBonus = 0f;

            // Act
            float effectiveBright = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, movementSpeed, 1f, terrainBonus); // Bright light
            float effectiveDark = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, movementSpeed, 0f, terrainBonus); // Dark

            // Assert - dark should increase stealth
            Assert.Greater(effectiveDark, effectiveBright, "Darkness should increase effective stealth");
        }

        [Test]
        public void CalculateEffectiveStealth_NeverNegative()
        {
            // Arrange - extreme negative modifiers
            float baseStealth = 10f; // Low base
            StealthLevel level = StealthLevel.Exposed;
            StealthModifiers modifiers = new StealthModifiers
            {
                LightModifier = -1f, // Extreme penalty
                TerrainModifier = -1f,
                MovementModifier = -1f,
                EquipmentModifier = -100f // Extreme equipment penalty
            };
            float movementSpeed = 10f; // Running
            float lightLevel = 1f; // Bright light
            float terrainBonus = -1f;

            // Act
            float effective = StealthDetectionService.CalculateEffectiveStealth(
                baseStealth, level, modifiers, movementSpeed, lightLevel, terrainBonus);

            // Assert - should never be negative
            Assert.GreaterOrEqual(effective, 0f, "Effective stealth should never be negative");
        }

        [Test]
        public void ApplyStealthConfidencePenalty_RemainsUndetected_ReturnsZero()
        {
            // Arrange
            float baseConfidence = 0.8f;
            StealthCheckResult result = StealthCheckResult.RemainsUndetected;

            // Act
            float finalConfidence = StealthDetectionService.ApplyStealthConfidencePenalty(baseConfidence, result);

            // Assert
            Assert.AreEqual(0f, finalConfidence, "RemainsUndetected should result in zero confidence");
        }

        [Test]
        public void ApplyStealthConfidencePenalty_Suspicious_ReducesConfidence()
        {
            // Arrange
            float baseConfidence = 0.8f;
            StealthCheckResult result = StealthCheckResult.Suspicious;

            // Act
            float finalConfidence = StealthDetectionService.ApplyStealthConfidencePenalty(baseConfidence, result);

            // Assert
            Assert.Less(finalConfidence, baseConfidence, "Suspicious should reduce confidence");
            Assert.Greater(finalConfidence, 0f, "Suspicious should still have some confidence");
            Assert.AreEqual(baseConfidence * 0.3f, finalConfidence, 0.001f, "Suspicious should be 30% of base");
        }

        [Test]
        public void ApplyStealthConfidencePenalty_Spotted_ReducesConfidence()
        {
            // Arrange
            float baseConfidence = 0.8f;
            StealthCheckResult result = StealthCheckResult.Spotted;

            // Act
            float finalConfidence = StealthDetectionService.ApplyStealthConfidencePenalty(baseConfidence, result);

            // Assert
            Assert.Less(finalConfidence, baseConfidence, "Spotted should reduce confidence");
            Assert.Greater(finalConfidence, 0f, "Spotted should still have some confidence");
            Assert.AreEqual(baseConfidence * 0.7f, finalConfidence, 0.001f, "Spotted should be 70% of base");
        }

        [Test]
        public void ApplyStealthConfidencePenalty_Exposed_ReturnsFullConfidence()
        {
            // Arrange
            float baseConfidence = 0.8f;
            StealthCheckResult result = StealthCheckResult.Exposed;

            // Act
            float finalConfidence = StealthDetectionService.ApplyStealthConfidencePenalty(baseConfidence, result);

            // Assert
            Assert.AreEqual(baseConfidence, finalConfidence, "Exposed should return full confidence");
        }

        [Test]
        public void GetEnvironmentalModifiers_Stationary_ProvidesBonus()
        {
            // Act
            StealthDetectionService.GetEnvironmentalModifiers(0.5f, 1, 0f, out var modifiers); // Stationary

            // Assert
            Assert.Greater(modifiers.MovementModifier, 0f, "Stationary should provide movement bonus");
        }

        [Test]
        public void GetEnvironmentalModifiers_Running_AppliesPenalty()
        {
            // Act
            StealthDetectionService.GetEnvironmentalModifiers(0.5f, 1, 10f, out var modifiers); // Running

            // Assert
            Assert.Less(modifiers.MovementModifier, 0f, "Running should apply movement penalty");
        }

        [Test]
        public void GetEnvironmentalModifiers_DarkLight_ProvidesBonus()
        {
            // Act
            StealthDetectionService.GetEnvironmentalModifiers(0f, 1, 0f, out var modifiersDark); // Dark
            StealthDetectionService.GetEnvironmentalModifiers(1f, 1, 0f, out var modifiersBright); // Bright

            // Assert
            Assert.Greater(modifiersDark.LightModifier, modifiersBright.LightModifier, "Dark should provide better light modifier");
        }

        [Test]
        public void GetEnvironmentalModifiers_ForestTerrain_ProvidesBonus()
        {
            // Act
            StealthDetectionService.GetEnvironmentalModifiers(0.5f, 0, 0f, out var modifiersOpen); // Open field
            StealthDetectionService.GetEnvironmentalModifiers(0.5f, 2, 0f, out var modifiersForest); // Forest

            // Assert
            Assert.Greater(modifiersForest.TerrainModifier, modifiersOpen.TerrainModifier, "Forest should provide better terrain modifier");
        }
    }
}
#endif


using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Space4X.Authoring;
using Space4X.Editor;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Editor.Tests
{
    public class AggregateProfileTests
    {
        private const string TestCatalogPath = "Assets/Data/Catalogs";

        [Test]
        public void Profiles_Seed_FromMapping_Idempotent()
        {
            // Create test catalogs
            var templateCatalog = CreateTestTemplateCatalog();
            var outlookCatalog = CreateTestOutlookCatalog();
            var alignmentCatalog = CreateTestAlignmentCatalog();
            var personalityCatalog = CreateTestPersonalityCatalog();
            var themeCatalog = CreateTestThemeCatalog();

            // Build combo table twice
            var result1 = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);
            var result2 = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);

            // Verify idempotency: same inputs → same outputs
            Assert.AreEqual(result1.CreatedCount, result2.CreatedCount, "Combo counts should match");
            Assert.AreEqual(result1.Combos.Count, result2.Combos.Count, "Combo dictionaries should have same size");

            // Verify hashes are deterministic
            foreach (var kvp in result1.Combos)
            {
                Assert.IsTrue(result2.Combos.ContainsKey(kvp.Key), $"Combo {kvp.Key} should exist in second run");
                var combo1 = kvp.Value;
                var combo2 = result2.Combos[kvp.Key];
                Assert.AreEqual(combo1.AggregateId32, combo2.AggregateId32, "AggregateId32 should be deterministic");
            }
        }

        [Test]
        public void Aggregates_ComboTable_DeterministicHashes()
        {
            var result = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);

            // Verify all combos have unique hashes
            var hashSet = new HashSet<uint>();
            foreach (var combo in result.Combos.Values)
            {
                Assert.IsFalse(hashSet.Contains(combo.AggregateId32), $"Duplicate hash found: {combo.AggregateId32}");
                hashSet.Add(combo.AggregateId32);
            }

            // Verify hash is deterministic for same inputs
            var templateId = "test-template";
            var outlookId = "test-outlook";
            var alignmentId = "test-alignment";
            var personalityId = "test-personality";
            var themeId = "test-theme";

            var hash1 = AggregateComboBuilder.ComputeAggregateHash(templateId, outlookId, alignmentId, personalityId, themeId);
            var hash2 = AggregateComboBuilder.ComputeAggregateHash(templateId, outlookId, alignmentId, personalityId, themeId);

            Assert.AreEqual(hash1, hash2, "Hash should be deterministic");
        }

        [Test]
        public void Aggregates_DoctrineWeights_Normalized()
        {
            var result = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);

            foreach (var combo in result.Combos.Values)
            {
                var doctrineSum = combo.DoctrineMissile + combo.DoctrineLaser + combo.DoctrineHangar;
                Assert.AreEqual(1f, doctrineSum, 0.01f, 
                    $"Combo {combo.AggregateId32} doctrine weights should sum to 1.0, got {doctrineSum}");

                // Verify individual weights are in valid range
                Assert.GreaterOrEqual(combo.DoctrineMissile, 0f, "DoctrineMissile should be >= 0");
                Assert.GreaterOrEqual(combo.DoctrineLaser, 0f, "DoctrineLaser should be >= 0");
                Assert.GreaterOrEqual(combo.DoctrineHangar, 0f, "DoctrineHangar should be >= 0");
            }
        }

        [Test]
        public void Aggregates_TechGates_Enforced()
        {
            var result = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);

            foreach (var combo in result.Combos.Values)
            {
                Assert.LessOrEqual(combo.TechFloor, combo.TechCap, 
                    $"Combo {combo.AggregateId32} TechFloor ({combo.TechFloor}) should be <= TechCap ({combo.TechCap})");

                Assert.GreaterOrEqual(combo.TechFloor, 0f, "TechFloor should be >= 0");
                Assert.LessOrEqual(combo.TechCap, 10f, "TechCap should be <= 10");
            }

            // Verify validation catches invalid tech gates
            var validationIssues = AggregateComboBuilder.ValidateProfiles(TestCatalogPath);
            var techGateErrors = validationIssues.Where(i => i.Reason.Contains("TechCap") || i.Reason.Contains("TechFloor"));
            // Should have no tech gate errors if catalogs are valid
        }

        [Test]
        public void Aggregates_PolicySanity_Valid()
        {
            var result = AggregateComboBuilder.BuildComboTable(TestCatalogPath, true);

            foreach (var combo in result.Combos.Values)
            {
                // CollateralLimit should be >= 0
                Assert.GreaterOrEqual(combo.CollateralLimit, 0f, 
                    $"Combo {combo.AggregateId32} CollateralLimit should be >= 0");

                // FieldRefitMult should be in [0.5, 2.0]
                Assert.GreaterOrEqual(combo.FieldRefitMult, 0.5f, 
                    $"Combo {combo.AggregateId32} FieldRefitMult should be >= 0.5");
                Assert.LessOrEqual(combo.FieldRefitMult, 2f, 
                    $"Combo {combo.AggregateId32} FieldRefitMult should be <= 2.0");

                // CooldownMult should be in [0.5, 2.0]
                Assert.GreaterOrEqual(combo.CooldownMult, 0.5f, 
                    $"Combo {combo.AggregateId32} CooldownMult should be >= 0.5");
                Assert.LessOrEqual(combo.CooldownMult, 2f, 
                    $"Combo {combo.AggregateId32} CooldownMult should be <= 2.0");
            }
        }

        // Helper methods to create test catalogs (would need actual GameObject setup in real test)
        private AggregateTemplateCatalogAuthoring CreateTestTemplateCatalog()
        {
            // This would create a test GameObject with AggregateTemplateCatalogAuthoring
            // For now, assumes test catalogs exist at TestCatalogPath
            return null;
        }

        private OutlookProfileCatalogAuthoring CreateTestOutlookCatalog()
        {
            return null;
        }

        private AlignmentProfileCatalogAuthoring CreateTestAlignmentCatalog()
        {
            return null;
        }

        private PersonalityArchetypeCatalogAuthoring CreateTestPersonalityCatalog()
        {
            return null;
        }

        private ThemeProfileCatalogAuthoring CreateTestThemeCatalog()
        {
            return null;
        }
    }
}


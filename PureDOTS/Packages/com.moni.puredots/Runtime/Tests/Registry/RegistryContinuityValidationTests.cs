using NUnit.Framework;
using PureDOTS.Runtime.Registry;
using Unity.Collections;

namespace PureDOTS.Tests.Registry
{
    public class RegistryContinuityValidationTests
    {
        [Test]
        public void Validate_PassesForUniqueIdsAndConsistentContinuity()
        {
            var definitions = new[]
            {
                CreateDefinition("registry.alpha", 1, RegistryResidency.Runtime, RegistryCategory.Gameplay),
                CreateDefinition("registry.beta", 1, RegistryResidency.Runtime, RegistryCategory.Gameplay),
                CreateDefinition("registry.presentation", 2, RegistryResidency.HybridBridge, RegistryCategory.Presentation)
            };

            using var array = new NativeArray<RegistryDefinition>(definitions, Allocator.Temp);
            var summary = RegistryContinuityValidator.Validate(array);

            Assert.IsTrue(summary.IsValid, "Expected validation to pass for unique ids and aligned continuity meta.");
        }

        [Test]
        public void Validate_FailsOnDuplicateIds()
        {
            var definitions = new[]
            {
                CreateDefinition("duplicate.id", 1, RegistryResidency.Runtime, RegistryCategory.Gameplay),
                CreateDefinition("duplicate.id", 1, RegistryResidency.Runtime, RegistryCategory.Gameplay)
            };

            using var array = new NativeArray<RegistryDefinition>(definitions, Allocator.Temp);
            var summary = RegistryContinuityValidator.Validate(array);

            Assert.IsFalse(summary.IsValid, "Duplicate ids should fail validation.");
            Assert.AreEqual(1, summary.DuplicateIdCount);
        }

        [Test]
        public void Validate_FlagsVersionAndResidencyMismatches()
        {
            var definitions = new[]
            {
                CreateDefinition("registry.one", 1, RegistryResidency.Runtime, RegistryCategory.Gameplay),
                CreateDefinition("registry.two", 2, RegistryResidency.HybridBridge, RegistryCategory.Gameplay),
                CreateDefinition("registry.three", 2, RegistryResidency.Runtime, RegistryCategory.Presentation)
            };

            using var array = new NativeArray<RegistryDefinition>(definitions, Allocator.Temp);
            var summary = RegistryContinuityValidator.Validate(array);

            Assert.IsFalse(summary.IsValid);
            Assert.AreEqual(1, summary.VersionMismatchCount, "Gameplay category version mismatch should be flagged once.");
            Assert.AreEqual(1, summary.ResidencyMismatchCount, "Gameplay category residency mismatch should be flagged once.");
        }

        private static RegistryDefinition CreateDefinition(string id, uint version, RegistryResidency residency, RegistryCategory category)
        {
            return new RegistryDefinition
            {
                Id = RegistryId.FromString(id),
                DisplayName = new FixedString64Bytes(id),
                ArchetypeId = 0,
                TelemetryKey = RegistryTelemetryKey.FromString(id, RegistryId.FromString(id)),
                Continuity = new RegistryContinuityMeta
                {
                    SchemaVersion = version,
                    Residency = residency,
                    Category = category
                }
            };
        }
    }
}

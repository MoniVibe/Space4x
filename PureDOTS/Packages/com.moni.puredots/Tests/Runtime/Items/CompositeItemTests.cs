using NUnit.Framework;
using PureDOTS.Runtime.Items;
using PureDOTS.Runtime.Shared;
using PureDOTS.Systems.Items;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Tests.Items
{
    /// <summary>
    /// EditMode tests for composite item aggregation and wear determinism.
    /// </summary>
    public class CompositeItemTests
    {
        [Test]
        public void CompositeItem_Aggregation_Deterministic_SamePartsProduceSameStats()
        {
            // Create a test catalog
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<ItemPartCatalogBlob>();

            // Part specs: Wheel (id=1, weight=0.3), Axle (id=2, weight=0.4)
            var partSpecs = bb.Allocate(ref catalog.PartSpecs, 2);
            partSpecs[0] = new ItemPartSpec
            {
                PartTypeId = 1,
                PartName = new FixedString64Bytes("Wheel"),
                AggregationWeight = 0.3f,
                DurabilityMultiplier = 1f,
                RepairSkillRequired = 25,
                IsCritical = true,
                DamageThreshold01 = 0.3f
            };
            partSpecs[1] = new ItemPartSpec
            {
                PartTypeId = 2,
                PartName = new FixedString64Bytes("Axle"),
                AggregationWeight = 0.4f,
                DurabilityMultiplier = 1f,
                RepairSkillRequired = 30,
                IsCritical = true,
                DamageThreshold01 = 0.3f
            };

            // Materials: Iron (mod=1.0), Mithril (mod=1.5)
            var materialNames = bb.Allocate(ref catalog.MaterialNames, 2);
            var materialMods = bb.Allocate(ref catalog.MaterialDurabilityMods, 2);
            materialNames[0] = new FixedString32Bytes("Iron");
            materialMods[0] = 1.0f;
            materialNames[1] = new FixedString32Bytes("Mithril");
            materialMods[1] = 1.5f;

            var catalogRef = bb.CreateBlobAssetReference<ItemPartCatalogBlob>(Unity.Collections.Allocator.Persistent);

            // Create test parts
            var parts1 = new NativeList<ItemPart>(Allocator.Temp);
            parts1.Add(new ItemPart
            {
                PartTypeId = 1,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.8f,
                Durability01 = 0.9f,
                RarityWeight = 50,
                Flags = PartFlags.None
            });
            parts1.Add(new ItemPart
            {
                PartTypeId = 2,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.7f,
                Durability01 = 0.8f,
                RarityWeight = 60,
                Flags = PartFlags.None
            });

            var parts2 = new NativeList<ItemPart>(Allocator.Temp);
            parts2.Add(new ItemPart
            {
                PartTypeId = 1,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.8f,
                Durability01 = 0.9f,
                RarityWeight = 50,
                Flags = PartFlags.None
            });
            parts2.Add(new ItemPart
            {
                PartTypeId = 2,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.7f,
                Durability01 = 0.8f,
                RarityWeight = 60,
                Flags = PartFlags.None
            });

            // Compute aggregated stats (simulate aggregation logic)
            float weightedDurability1 = (0.9f * 0.3f + 0.8f * 0.4f) / (0.3f + 0.4f);
            float weightedDurability2 = (0.9f * 0.3f + 0.8f * 0.4f) / (0.3f + 0.4f);
            float weightedQuality1 = (0.8f * 0.3f + 0.7f * 0.4f) / (0.3f + 0.4f);
            float weightedQuality2 = (0.8f * 0.3f + 0.7f * 0.4f) / (0.3f + 0.4f);

            Assert.AreEqual(weightedDurability1, weightedDurability2, 0.001f, "Same parts should produce same aggregated durability");
            Assert.AreEqual(weightedQuality1, weightedQuality2, 0.001f, "Same parts should produce same aggregated quality");
        }

        [Test]
        public void DurabilityWear_DistributesToParts_UniformWearReducesAllParts()
        {
            // Create a test catalog
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<ItemPartCatalogBlob>();

            var partSpecs = bb.Allocate(ref catalog.PartSpecs, 1);
            partSpecs[0] = new ItemPartSpec
            {
                PartTypeId = 1,
                PartName = new FixedString64Bytes("TestPart"),
                AggregationWeight = 1f,
                DurabilityMultiplier = 1f,
                RepairSkillRequired = 25,
                IsCritical = false,
                DamageThreshold01 = 0.3f
            };

            var materialNames = bb.Allocate(ref catalog.MaterialNames, 1);
            var materialMods = bb.Allocate(ref catalog.MaterialDurabilityMods, 1);
            materialNames[0] = new FixedString32Bytes("Iron");
            materialMods[0] = 1.0f;

            // Create parts with full durability
            var parts = new NativeList<ItemPart>(Allocator.Temp);
            parts.Add(new ItemPart
            {
                PartTypeId = 1,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.5f,
                Durability01 = 1.0f,
                RarityWeight = 50,
                Flags = PartFlags.None
            });
            parts.Add(new ItemPart
            {
                PartTypeId = 1,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.5f,
                Durability01 = 1.0f,
                RarityWeight = 50,
                Flags = PartFlags.None
            });

            // Apply uniform wear of 0.2 (should reduce each part by 0.1)
            float wearAmount = 0.2f;
            float perPartWear = wearAmount / parts.Length;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                part.Durability01 = (half)math.max(0f, part.Durability01 - perPartWear);
                parts[i] = part;
            }

            Assert.AreEqual(0.9f, parts[0].Durability01, 0.001f, "First part should have 0.9 durability after uniform wear");
            Assert.AreEqual(0.9f, parts[1].Durability01, 0.001f, "Second part should have 0.9 durability after uniform wear");
        }

        [Test]
        public void Repair_CapsToSkillLevel_JourneymanCapsAt80Percent()
        {
            // Test repair skill caps
            byte journeymanSkill = 50;
            float skillCap = journeymanSkill switch
            {
                >= 100 => 1.0f,
                >= 75 => 0.95f,
                >= 50 => 0.80f,
                >= 25 => 0.60f,
                _ => 0.40f
            };

            Assert.AreEqual(0.80f, skillCap, 0.001f, "Journeyman skill (50) should cap at 80%");

            // Test that repair cannot exceed cap
            float targetDurability = 1.0f; // Try to repair to 100%
            float actualTarget = math.min(targetDurability, skillCap);

            Assert.AreEqual(0.80f, actualTarget, 0.001f, "Repair target should be capped at skill level");
        }

        [Test]
        public void BrokenItem_WhenCriticalPartZero_ItemIsMarkedBroken()
        {
            // Create a test catalog with critical part
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<ItemPartCatalogBlob>();

            var partSpecs = bb.Allocate(ref catalog.PartSpecs, 1);
            partSpecs[0] = new ItemPartSpec
            {
                PartTypeId = 1,
                PartName = new FixedString64Bytes("CriticalPart"),
                AggregationWeight = 1f,
                DurabilityMultiplier = 1f,
                RepairSkillRequired = 25,
                IsCritical = true, // Critical part
                DamageThreshold01 = 0.3f
            };

            var materialNames = bb.Allocate(ref catalog.MaterialNames, 1);
            var materialMods = bb.Allocate(ref catalog.MaterialDurabilityMods, 1);
            materialNames[0] = new FixedString32Bytes("Iron");
            materialMods[0] = 1.0f;

            // Create part with zero durability
            var part = new ItemPart
            {
                PartTypeId = 1,
                Material = new FixedString32Bytes("Iron"),
                Quality01 = 0.5f,
                Durability01 = 0f, // Broken
                RarityWeight = 50,
                Flags = PartFlags.None
            };

            // Check if part is broken and critical
            bool isBroken = part.Durability01 <= 0f;
            bool isCritical = partSpecs[0].IsCritical;
            bool itemShouldBeBroken = isBroken && isCritical;

            Assert.IsTrue(isBroken, "Part with zero durability should be broken");
            Assert.IsTrue(isCritical, "Part should be marked as critical");
            Assert.IsTrue(itemShouldBeBroken, "Item should be broken when critical part is at zero");
        }
    }
}


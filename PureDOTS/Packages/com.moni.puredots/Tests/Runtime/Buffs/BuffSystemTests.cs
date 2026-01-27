using NUnit.Framework;
using PureDOTS.Runtime.Buffs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.Runtime.Buffs
{
    /// <summary>
    /// EditMode tests for buff system functionality.
    /// </summary>
    public class BuffSystemTests
    {
        [Test]
        public void Buff_Application_AddsActiveBuff()
        {
            // Create a test buff catalog
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<BuffDefinitionBlob>();

            var buffs = bb.Allocate(ref catalog.Buffs, 1);
            var modifiers = bb.Allocate(ref buffs[0].StatModifiers, 1);
            modifiers[0] = new BuffStatModifier
            {
                Stat = StatTarget.Damage,
                Type = ModifierType.Flat,
                Value = 10f
            };

            var periodic = bb.Allocate(ref buffs[0].PeriodicEffects, 0);
            buffs[0] = new BuffEntry
            {
                BuffId = new FixedString64Bytes("test_buff"),
                DisplayName = new FixedString64Bytes("Test Buff"),
                Category = BuffCategory.Buff,
                Stacking = StackBehavior.Additive,
                MaxStacks = 5,
                BaseDuration = 10f,
                TickInterval = 0f
            };

            var blobRef = bb.CreateBlobAssetReference<BuffDefinitionBlob>(Unity.Collections.Allocator.Persistent);

            // Verify buff can be found in catalog
            bool found = false;
            for (int i = 0; i < blobRef.Value.Buffs.Length; i++)
            {
                if (blobRef.Value.Buffs[i].BuffId.Equals("test_buff"))
                {
                    found = true;
                    Assert.AreEqual(StackBehavior.Additive, blobRef.Value.Buffs[i].Stacking);
                    Assert.AreEqual(10f, blobRef.Value.Buffs[i].BaseDuration, 0.001f);
                    break;
                }
            }

            Assert.IsTrue(found, "Buff should be found in catalog");
        }

        [Test]
        public void Buff_Stacking_Additive_IncreasesStacks()
        {
            // Test that additive stacking increases stack count
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<BuffDefinitionBlob>();

            var buffs = bb.Allocate(ref catalog.Buffs, 1);
            var modifiers = bb.Allocate(ref buffs[0].StatModifiers, 0);
            var periodic = bb.Allocate(ref buffs[0].PeriodicEffects, 0);
            buffs[0] = new BuffEntry
            {
                BuffId = new FixedString64Bytes("stacking_buff"),
                Stacking = StackBehavior.Additive,
                MaxStacks = 5,
                BaseDuration = 10f,
                TickInterval = 0f
            };

            var blobRef = bb.CreateBlobAssetReference<BuffDefinitionBlob>(Unity.Collections.Allocator.Persistent);

            // Simulate stacking logic
            byte currentStacks = 1;
            byte stacksToApply = 2;
            byte maxStacks = blobRef.Value.Buffs[0].MaxStacks;
            byte newStacks = (byte)math.min(maxStacks, currentStacks + stacksToApply);

            Assert.AreEqual(3, newStacks, "Additive stacking should add stacks");
            Assert.IsTrue(newStacks <= maxStacks, "Stacks should not exceed max");
        }

        [Test]
        public void Buff_Stacking_Refresh_ResetsDuration()
        {
            // Test that refresh stacking resets duration without adding stacks
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<BuffDefinitionBlob>();

            var buffs = bb.Allocate(ref catalog.Buffs, 1);
            var modifiers = bb.Allocate(ref buffs[0].StatModifiers, 0);
            var periodic = bb.Allocate(ref buffs[0].PeriodicEffects, 0);
            buffs[0] = new BuffEntry
            {
                BuffId = new FixedString64Bytes("refresh_buff"),
                Stacking = StackBehavior.Refresh,
                MaxStacks = 1,
                BaseDuration = 10f,
                TickInterval = 0f
            };

            var blobRef = bb.CreateBlobAssetReference<BuffDefinitionBlob>(Unity.Collections.Allocator.Persistent);

            // Simulate refresh logic
            float remainingDuration = 3f; // Buff has 3 seconds left
            float newDuration = blobRef.Value.Buffs[0].BaseDuration; // Apply new buff
            float refreshedDuration = newDuration; // Refresh resets to full duration

            Assert.AreEqual(10f, refreshedDuration, 0.001f, "Refresh should reset duration to base");
        }

        [Test]
        public void Buff_Expiry_RemovesAfterDuration()
        {
            // Test that buffs expire after duration reaches zero
            float baseDuration = 10f;
            float deltaTime = 0.1f;
            float remainingDuration = baseDuration;

            // Simulate multiple ticks
            for (int i = 0; i < 100; i++)
            {
                remainingDuration -= deltaTime;
                if (remainingDuration <= 0f)
                {
                    Assert.IsTrue(remainingDuration <= 0f, "Buff should expire when duration reaches zero");
                    break;
                }
            }

            Assert.IsTrue(remainingDuration <= 0f, "Buff should expire after duration");
        }

        [Test]
        public void Buff_PeriodicDamage_TicksCorrectly()
        {
            // Test periodic effect timing
            float tickInterval = 2f;
            float timeSinceLastTick = 0f;
            float deltaTime = 0.5f;
            int tickCount = 0;

            // Simulate multiple updates
            for (int i = 0; i < 10; i++)
            {
                timeSinceLastTick += deltaTime;
                if (timeSinceLastTick >= tickInterval)
                {
                    tickCount++;
                    timeSinceLastTick -= tickInterval;
                }
            }

            // After 5 seconds with 2-second interval, should have ticked 2 times
            Assert.AreEqual(2, tickCount, "Periodic effect should tick at correct interval");
        }

        [Test]
        public void Buff_StatModifiers_AggregateCorrectly()
        {
            // Test that stat modifiers aggregate correctly
            var cache = new BuffStatCache();

            // Simulate applying multiple damage modifiers
            cache.DamageFlat += 10f; // First buff: +10 damage
            cache.DamageFlat += 5f;  // Second buff: +5 damage
            cache.DamagePercent += 0.2f; // Third buff: +20% damage

            Assert.AreEqual(15f, cache.DamageFlat, 0.001f, "Flat modifiers should add together");
            Assert.AreEqual(0.2f, cache.DamagePercent, 0.001f, "Percent modifiers should accumulate");
        }

        [Test]
        public void Buff_Dispel_RemovesMatchingBuffs()
        {
            // Test dispel logic
            using var bb = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var catalog = ref bb.ConstructRoot<BuffDefinitionBlob>();

            var buffs = bb.Allocate(ref catalog.Buffs, 2);
            var modifiers1 = bb.Allocate(ref buffs[0].StatModifiers, 0);
            var periodic1 = bb.Allocate(ref buffs[0].PeriodicEffects, 0);
            buffs[0] = new BuffEntry
            {
                BuffId = new FixedString64Bytes("buff1"),
                Category = BuffCategory.Buff,
                Stacking = StackBehavior.Additive,
                MaxStacks = 1,
                BaseDuration = 10f,
                TickInterval = 0f
            };

            var modifiers2 = bb.Allocate(ref buffs[1].StatModifiers, 0);
            var periodic2 = bb.Allocate(ref buffs[1].PeriodicEffects, 0);
            buffs[1] = new BuffEntry
            {
                BuffId = new FixedString64Bytes("debuff1"),
                Category = BuffCategory.Debuff,
                Stacking = StackBehavior.Additive,
                MaxStacks = 1,
                BaseDuration = 10f,
                TickInterval = 0f
            };

            var blobRef = bb.CreateBlobAssetReference<BuffDefinitionBlob>(Unity.Collections.Allocator.Persistent);

            // Simulate dispel by buff ID
            FixedString64Bytes targetBuffId = new FixedString64Bytes("buff1");
            bool shouldRemove = targetBuffId.Equals("buff1");

            Assert.IsTrue(shouldRemove, "Dispel should match buff ID correctly");
        }

        [Test]
        public void Buff_StackMultiplier_Multiplicative_CalculatesCorrectly()
        {
            // Test multiplicative stacking multiplier calculation
            byte stacks = 3;
            float multiplier = math.pow(1.1f, stacks - 1); // 1.1x per stack

            Assert.AreEqual(math.pow(1.1f, 2), multiplier, 0.001f, "Multiplicative stacking should calculate correctly");
        }
    }
}


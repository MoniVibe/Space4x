#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Space4X.Systems.Modules;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public class Space4XModuleBlueprintResolverTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("module-blueprint-resolver-tests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void ResolverDigest_IsDeterministicForFixedSeedBlueprintAndPerks()
        {
            var entity = _entityManager.CreateEntity();
            var parts = _entityManager.AddBuffer<ModuleBlueprintPartId>(entity);
            var tags = _entityManager.AddBuffer<ModuleDerivedTag>(entity);
            var effects = _entityManager.AddBuffer<ModuleDerivedEffectOp>(entity);

            var spec = new ModuleSpec
            {
                Id = new FixedString64Bytes("laser-s-1"),
                Class = ModuleClass.Laser,
                RequiredSize = MountSize.S,
                MassTons = 8f,
                PowerDrawMW = 15f,
                OffenseRating = 3,
                FunctionCapacity = 0f
            };

            var reference = new ModuleBlueprintRef
            {
                ManufacturerId = default,
                BlueprintId = default,
                StableHash = 123456u
            };

            using var perks = new NativeArray<ModuleBlueprintRunPerkOp>(0, Allocator.Temp);

            var first = Space4XModuleBlueprintResolver.Resolve(spec, reference, parts, tags, effects, perks);
            tags.Clear();
            effects.Clear();
            var second = Space4XModuleBlueprintResolver.Resolve(spec, reference, parts, tags, effects, perks);

            Assert.AreEqual(first.Digest, second.Digest, "Digest must be deterministic for fixed seed+blueprint+perk set.");
            Assert.AreEqual(3210169753u, first.Digest, "Digest constant drifted for fixed deterministic input.");
        }

        [Test]
        public void DamageConversionCapsAtOneHundredPercent()
        {
            var entity = _entityManager.CreateEntity();
            var parts = _entityManager.AddBuffer<ModuleBlueprintPartId>(entity);
            var tags = _entityManager.AddBuffer<ModuleDerivedTag>(entity);
            var effects = _entityManager.AddBuffer<ModuleDerivedEffectOp>(entity);

            var spec = new ModuleSpec
            {
                Id = new FixedString64Bytes("pd-s-1"),
                Class = ModuleClass.Kinetic,
                RequiredSize = MountSize.S,
                MassTons = 6f,
                PowerDrawMW = 8f,
                OffenseRating = 4
            };

            var reference = new ModuleBlueprintRef
            {
                ManufacturerId = default,
                BlueprintId = default,
                StableHash = 9u
            };

            var perks = new NativeArray<ModuleBlueprintRunPerkOp>(2, Allocator.Temp);
            try
            {
                perks[0] = new ModuleBlueprintRunPerkOp
                {
                    OpKind = ModuleBlueprintOpKind.ConvertDamage,
                    FromDamageType = Space4XDamageType.Kinetic,
                    ToDamageType = Space4XDamageType.Energy,
                    Value = 0.70f
                };
                perks[1] = new ModuleBlueprintRunPerkOp
                {
                    OpKind = ModuleBlueprintOpKind.ConvertDamage,
                    FromDamageType = Space4XDamageType.Kinetic,
                    ToDamageType = Space4XDamageType.Thermal,
                    Value = 0.70f
                };

                var result = Space4XModuleBlueprintResolver.Resolve(spec, reference, parts, tags, effects, perks);
                var expectedBaseDamage = math.max(1f, spec.OffenseRating * 10f);
                var scaledTotal = Space4XModuleBlueprintResolver.ComputeScaledConversionTotal(0.70f, 0.70f);

                Assert.AreEqual(1f, scaledTotal, 1e-5f, "70% + 70% conversion must scale down to 100% total.");
                Assert.AreEqual(expectedBaseDamage, result.Damage, 1e-4f, "Damage should remain conserved after capped conversion.");
            }
            finally
            {
                if (perks.IsCreated)
                {
                    perks.Dispose();
                }
            }
        }
    }
}
#endif

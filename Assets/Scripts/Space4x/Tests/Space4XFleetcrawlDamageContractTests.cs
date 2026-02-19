#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Space4x.Fleetcrawl;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public class Space4XFleetcrawlDamageContractTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XFleetcrawlDamageContractTests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void ResolvePacket_BubbleShieldAbsorbsBeforeHull()
        {
            var target = _entityManager.CreateEntity(typeof(FleetcrawlDamageDefenderState), typeof(FleetcrawlDefenseRuntimeState));
            _entityManager.SetComponentData(target, new FleetcrawlDamageDefenderState
            {
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f)
            });
            _entityManager.SetComponentData(target, new FleetcrawlDefenseRuntimeState
            {
                MassMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ReactorOutputMultiplier = 1f,
                ReflectBonusPct = 0f,
                IncomingDamageMultiplier = 1f
            });

            var shields = _entityManager.AddBuffer<FleetcrawlShieldLayerState>(target);
            shields.Add(new FleetcrawlShieldLayerState
            {
                LayerId = new FixedString32Bytes("bubble"),
                Topology = FleetcrawlShieldTopology.Bubble,
                Arc = FleetcrawlShieldArc.Any,
                Current = 50f,
                Max = 50f,
                RechargePerTick = 0f,
                ReflectPct = 0.1f,
                Resistances = FleetcrawlResistanceProfile.Identity
            });

            var hull = _entityManager.AddBuffer<FleetcrawlHullSegmentState>(target);
            hull.Add(new FleetcrawlHullSegmentState
            {
                SegmentId = new FixedString32Bytes("core"),
                HullClass = FleetcrawlHullClass.Balanced,
                Current = 100f,
                Max = 100f,
                Armor = 3f,
                Mass = 1000f,
                Resistances = FleetcrawlResistanceProfile.Identity,
                Active = 1
            });

            var pending = _entityManager.AddBuffer<FleetcrawlPendingEffect>(target);
            var payloads = _entityManager.AddBuffer<FleetcrawlDamagePayloadOp>(target);
            var packet = new FleetcrawlDamagePacket
            {
                Source = Entity.Null,
                Target = target,
                DamageType = Space4XDamageType.Energy,
                Delivery = WeaponDelivery.Beam,
                BaseDamage = 40f,
                CritMultiplier = 1f,
                Penetration01 = 0f,
                IncomingDirection = new float3(0f, 0f, -1f),
                PreferredHullSegmentIndex = -1
            };

            var result = FleetcrawlDamageContractResolver.ResolvePacket(
                packet,
                _entityManager.GetComponentData<FleetcrawlDamageDefenderState>(target),
                shields,
                hull,
                pending,
                payloads,
                tick: 10u);

            Assert.AreEqual(10f, shields[0].Current, 1e-4f);
            Assert.AreEqual(100f, hull[0].Current, 1e-4f);
            Assert.Greater(result.AppliedShieldDamage, 39.9f);
            Assert.AreEqual(0f, result.AppliedHullDamage, 1e-4f);
            Assert.IsTrue((result.Flags & FleetcrawlDamageResolutionFlags.HitBubbleShield) != 0);
        }

        [Test]
        public void ResolvePacket_ShieldResistanceConsumesIncomingDamageDeterministically()
        {
            var target = _entityManager.CreateEntity(typeof(FleetcrawlDamageDefenderState));
            _entityManager.SetComponentData(target, new FleetcrawlDamageDefenderState
            {
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f)
            });

            var shields = _entityManager.AddBuffer<FleetcrawlShieldLayerState>(target);
            shields.Add(new FleetcrawlShieldLayerState
            {
                LayerId = new FixedString32Bytes("resistant"),
                Topology = FleetcrawlShieldTopology.Bubble,
                Arc = FleetcrawlShieldArc.Any,
                Current = 50f,
                Max = 50f,
                Resistances = new FleetcrawlResistanceProfile
                {
                    Energy = 0.5f,
                    Thermal = 1f,
                    EM = 1f,
                    Radiation = 1f,
                    Kinetic = 1f,
                    Explosive = 1f,
                    Caustic = 1f
                }
            });

            var hull = _entityManager.AddBuffer<FleetcrawlHullSegmentState>(target);
            hull.Add(new FleetcrawlHullSegmentState
            {
                SegmentId = new FixedString32Bytes("core"),
                HullClass = FleetcrawlHullClass.Balanced,
                Current = 100f,
                Max = 100f,
                Armor = 0f,
                Mass = 1000f,
                Resistances = FleetcrawlResistanceProfile.Identity,
                Active = 1
            });

            var pending = _entityManager.AddBuffer<FleetcrawlPendingEffect>(target);
            var payloads = _entityManager.AddBuffer<FleetcrawlDamagePayloadOp>(target);
            var packet = new FleetcrawlDamagePacket
            {
                DamageType = Space4XDamageType.Energy,
                Delivery = WeaponDelivery.Beam,
                BaseDamage = 100f,
                CritMultiplier = 1f,
                Penetration01 = 0f,
                IncomingDirection = new float3(0f, 0f, -1f),
                PreferredHullSegmentIndex = -1
            };

            var result = FleetcrawlDamageContractResolver.ResolvePacket(
                packet,
                _entityManager.GetComponentData<FleetcrawlDamageDefenderState>(target),
                shields,
                hull,
                pending,
                payloads,
                tick: 99u);

            Assert.AreEqual(0f, shields[0].Current, 1e-4f);
            Assert.AreEqual(100f, hull[0].Current, 1e-4f);
            Assert.AreEqual(0f, result.RemainingDamage, 1e-4f);
            Assert.AreEqual(50f, result.AppliedShieldDamage, 1e-4f);
            Assert.AreEqual(0f, result.AppliedHullDamage, 1e-4f);
        }

        [Test]
        public void ResolvePacket_DirectionalShieldUsesFrontArc()
        {
            var target = _entityManager.CreateEntity(typeof(FleetcrawlDamageDefenderState));
            _entityManager.SetComponentData(target, new FleetcrawlDamageDefenderState
            {
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f)
            });

            var shields = _entityManager.AddBuffer<FleetcrawlShieldLayerState>(target);
            shields.Add(new FleetcrawlShieldLayerState
            {
                LayerId = new FixedString32Bytes("front"),
                Topology = FleetcrawlShieldTopology.Directional,
                Arc = FleetcrawlShieldArc.Front,
                Current = 20f,
                Max = 20f,
                Resistances = FleetcrawlResistanceProfile.Identity
            });
            shields.Add(new FleetcrawlShieldLayerState
            {
                LayerId = new FixedString32Bytes("rear"),
                Topology = FleetcrawlShieldTopology.Directional,
                Arc = FleetcrawlShieldArc.Rear,
                Current = 20f,
                Max = 20f,
                Resistances = FleetcrawlResistanceProfile.Identity
            });

            var hull = _entityManager.AddBuffer<FleetcrawlHullSegmentState>(target);
            hull.Add(new FleetcrawlHullSegmentState
            {
                SegmentId = new FixedString32Bytes("core"),
                HullClass = FleetcrawlHullClass.Balanced,
                Current = 50f,
                Max = 50f,
                Armor = 0f,
                Mass = 500f,
                Resistances = FleetcrawlResistanceProfile.Identity,
                Active = 1
            });

            var pending = _entityManager.AddBuffer<FleetcrawlPendingEffect>(target);
            var payloads = _entityManager.AddBuffer<FleetcrawlDamagePayloadOp>(target);
            var packet = new FleetcrawlDamagePacket
            {
                DamageType = Space4XDamageType.Kinetic,
                Delivery = WeaponDelivery.Slug,
                BaseDamage = 15f,
                CritMultiplier = 1f,
                Penetration01 = 0f,
                IncomingDirection = new float3(0f, 0f, -1f),
                PreferredHullSegmentIndex = -1
            };

            var result = FleetcrawlDamageContractResolver.ResolvePacket(
                packet,
                _entityManager.GetComponentData<FleetcrawlDamageDefenderState>(target),
                shields,
                hull,
                pending,
                payloads,
                tick: 20u);

            Assert.AreEqual(FleetcrawlShieldArc.Front, result.IncomingArc);
            Assert.AreEqual(5f, shields[0].Current, 1e-4f);
            Assert.AreEqual(20f, shields[1].Current, 1e-4f);
        }

        [Test]
        public void ResolvePacket_RegistersPayloadEffects_AndTicks()
        {
            var target = _entityManager.CreateEntity(typeof(FleetcrawlDamageDefenderState), typeof(FleetcrawlDefenseRuntimeState));
            _entityManager.SetComponentData(target, new FleetcrawlDamageDefenderState
            {
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f)
            });
            _entityManager.SetComponentData(target, new FleetcrawlDefenseRuntimeState
            {
                MassMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ReactorOutputMultiplier = 1f,
                ReflectBonusPct = 0f,
                IncomingDamageMultiplier = 1f
            });

            var shields = _entityManager.AddBuffer<FleetcrawlShieldLayerState>(target);
            var hull = _entityManager.AddBuffer<FleetcrawlHullSegmentState>(target);
            hull.Add(new FleetcrawlHullSegmentState
            {
                SegmentId = new FixedString32Bytes("core"),
                HullClass = FleetcrawlHullClass.LightChassis,
                Current = 80f,
                Max = 80f,
                Armor = 0f,
                Mass = 300f,
                Resistances = FleetcrawlResistanceProfile.Identity,
                Active = 1
            });

            var pending = _entityManager.AddBuffer<FleetcrawlPendingEffect>(target);
            var payloads = _entityManager.AddBuffer<FleetcrawlDamagePayloadOp>(target);
            payloads.Add(new FleetcrawlDamagePayloadOp
            {
                EffectId = new FixedString32Bytes("dot_thermal"),
                Kind = FleetcrawlDamageOpKind.DamageOverTime,
                DamageType = Space4XDamageType.Thermal,
                Magnitude = 3f,
                DurationTicks = 5u,
                TickInterval = 1u,
                MaxStacks = 2
            });
            payloads.Add(new FleetcrawlDamagePayloadOp
            {
                EffectId = new FixedString32Bytes("drain_em"),
                Kind = FleetcrawlDamageOpKind.PowerReduction,
                DamageType = Space4XDamageType.EM,
                Magnitude = 0.12f,
                DurationTicks = 4u,
                TickInterval = 1u,
                MaxStacks = 1
            });

            var packet = new FleetcrawlDamagePacket
            {
                DamageType = Space4XDamageType.EM,
                Delivery = WeaponDelivery.Burst,
                BaseDamage = 6f,
                CritMultiplier = 1f,
                Penetration01 = 0f,
                IncomingDirection = new float3(0f, 0f, -1f),
                PreferredHullSegmentIndex = 0
            };

            var resolve = FleetcrawlDamageContractResolver.ResolvePacket(
                packet,
                _entityManager.GetComponentData<FleetcrawlDamageDefenderState>(target),
                shields,
                hull,
                pending,
                payloads,
                tick: 30u);

            Assert.AreEqual(2, pending.Length);
            Assert.IsTrue((resolve.Flags & FleetcrawlDamageResolutionFlags.AppliedDamageOverTime) != 0);
            Assert.IsTrue((resolve.Flags & FleetcrawlDamageResolutionFlags.AppliedPowerReduction) != 0);

            var runtime = _entityManager.GetComponentData<FleetcrawlDefenseRuntimeState>(target);
            var hullBeforeTick = hull[0].Current;
            FleetcrawlDamageContractResolver.TickPendingEffects(31u, pending, hull, ref runtime);
            _entityManager.SetComponentData(target, runtime);

            Assert.Less(hull[0].Current, hullBeforeTick);
            Assert.Less(runtime.ReactorOutputMultiplier, 1f);
        }
    }
}
#endif

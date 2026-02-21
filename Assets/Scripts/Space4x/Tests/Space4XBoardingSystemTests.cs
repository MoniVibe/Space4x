#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class Space4XBoardingSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XBoardingSystemTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
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
        public void Boarding_WaitsUntilShieldsAndHullAreSuppressed()
        {
            var attacker = CreateShip(new float3(0f, 0f, 0f), hullCurrent: 120f, hullMax: 120f, shieldCurrent: 40f, shieldMax: 40f);
            var target = CreateShip(new float3(80f, 0f, 0f), hullCurrent: 120f, hullMax: 120f, shieldCurrent: 60f, shieldMax: 60f);

            _entityManager.AddComponentData(attacker, new Space4XBoardingProfile
            {
                AssaultStrength = 2f,
                DefenseStrength = 1f,
                InternalSecurity = 1f,
                CasualtyMitigation01 = 0.1f
            });

            _entityManager.AddComponentData(target, Space4XBoardingProfile.Default);
            _entityManager.AddComponentData(attacker, Space4XBoardingOrder.Create(target));

            var system = _world.GetOrCreateSystem<Space4XBoardingSystem>();
            SetTick(1u);
            system.Update(_world.Unmanaged);

            var firstPass = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingPhase.WaitingForWindow, firstPass.Phase);
            Assert.AreEqual(0f, firstPass.AttackerForce, 1e-4f);

            var shield = _entityManager.GetComponentData<Space4XShield>(target);
            shield.Current = 0f;
            _entityManager.SetComponentData(target, shield);

            var hull = _entityManager.GetComponentData<HullIntegrity>(target);
            hull.Current = 45f;
            _entityManager.SetComponentData(target, hull);

            SetTick(2u);
            system.Update(_world.Unmanaged);

            var secondPass = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingPhase.Launching, secondPass.Phase);
            Assert.Greater(secondPass.AttackerForce, 0f);
            Assert.Greater(secondPass.DefenderForce, 0f);
        }

        [Test]
        public void Boarding_CaptureTransfersAffiliationAndDisablesTargetCombat()
        {
            var attackerFaction = _entityManager.CreateEntity();
            var targetFaction = _entityManager.CreateEntity();

            var attacker = CreateShip(new float3(0f, 0f, 0f), hullCurrent: 160f, hullMax: 160f, shieldCurrent: 20f, shieldMax: 20f);
            var target = CreateShip(new float3(50f, 0f, 0f), hullCurrent: 35f, hullMax: 100f, shieldCurrent: 0f, shieldMax: 50f);

            _entityManager.AddBuffer<SubsystemDisabled>(target);
            _entityManager.SetComponentData(target, new Space4XEngagement
            {
                Phase = EngagementPhase.Engaged,
                PrimaryTarget = Entity.Null
            });

            _entityManager.AddComponentData(attacker, new Space4XBoardingProfile
            {
                AssaultStrength = 3.4f,
                DefenseStrength = 1f,
                InternalSecurity = 1f,
                CasualtyMitigation01 = 0.15f
            });

            _entityManager.AddComponentData(attacker, new Space4XFocusModifiers
            {
                BoardingEffectivenessBonus = (half)0.35f,
                CrewStressReduction = (half)0.1f
            });

            _entityManager.AddComponentData(target, new Space4XBoardingProfile
            {
                AssaultStrength = 1f,
                DefenseStrength = 0.75f,
                InternalSecurity = 0.8f,
                CasualtyMitigation01 = 0.05f
            });

            var attackerAffiliations = _entityManager.AddBuffer<AffiliationTag>(attacker);
            attackerAffiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = attackerFaction,
                Loyalty = (half)1f
            });

            var targetAffiliations = _entityManager.AddBuffer<AffiliationTag>(target);
            targetAffiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = targetFaction,
                Loyalty = (half)0.9f
            });

            _entityManager.AddComponentData(attacker, new Space4XBoardingOrder
            {
                Target = target,
                IssuedTick = 0u,
                MaxDurationTicks = 80u,
                DesiredRangeMeters = 120f,
                TroopCommitment01 = 1f,
                PodPenetration = 1f,
                ElectronicWarfareSupport = 0.75f,
                Phase = Space4XBoardingPhase.None,
                Outcome = Space4XBoardingOutcome.None
            });

            var system = _world.GetOrCreateSystem<Space4XBoardingSystem>();

            for (uint tick = 1u; tick < 40u; tick++)
            {
                SetTick(tick);
                system.Update(_world.Unmanaged);

                if (_entityManager.HasComponent<Space4XBoardingCaptureState>(target))
                {
                    break;
                }
            }

            Assert.IsTrue(_entityManager.HasComponent<Space4XBoardingCaptureState>(target), "Target should be captured.");
            var capture = _entityManager.GetComponentData<Space4XBoardingCaptureState>(target);
            Assert.AreEqual(attacker, capture.Captor);

            var finalOrder = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingOutcome.Captured, finalOrder.Outcome);
            Assert.AreEqual(Space4XBoardingPhase.Captured, finalOrder.Phase);

            var engagement = _entityManager.GetComponentData<Space4XEngagement>(target);
            Assert.AreEqual(EngagementPhase.Disabled, engagement.Phase);
            Assert.AreEqual(attacker, engagement.PrimaryTarget);

            var capturedAffiliations = _entityManager.GetBuffer<AffiliationTag>(target);
            Assert.AreEqual(1, capturedAffiliations.Length);
            Assert.AreEqual(attackerFaction, capturedAffiliations[0].Target);

            var disabled = _entityManager.GetBuffer<SubsystemDisabled>(target);
            Assert.IsTrue(HasDisable(disabled, SubsystemType.Weapons), "Weapons subsystem should be disabled post-capture.");
            Assert.IsTrue(HasDisable(disabled, SubsystemType.Engines), "Engines subsystem should be disabled post-capture.");
        }

        [Test]
        public void Boarding_WeakAssaultIsRepelled()
        {
            var attacker = CreateShip(new float3(0f, 0f, 0f), hullCurrent: 100f, hullMax: 100f, shieldCurrent: 10f, shieldMax: 10f);
            var target = CreateShip(new float3(30f, 0f, 0f), hullCurrent: 45f, hullMax: 100f, shieldCurrent: 0f, shieldMax: 40f);

            _entityManager.AddComponentData(attacker, new Space4XBoardingProfile
            {
                AssaultStrength = 0.35f,
                DefenseStrength = 1f,
                InternalSecurity = 1f,
                CasualtyMitigation01 = 0f
            });

            _entityManager.AddComponentData(target, new Space4XBoardingProfile
            {
                AssaultStrength = 1f,
                DefenseStrength = 3.5f,
                InternalSecurity = 2.5f,
                CasualtyMitigation01 = 0.35f
            });

            _entityManager.AddComponentData(attacker, new Space4XBoardingOrder
            {
                Target = target,
                IssuedTick = 0u,
                MaxDurationTicks = 60u,
                DesiredRangeMeters = 100f,
                TroopCommitment01 = 0.5f,
                PodPenetration = 0f,
                ElectronicWarfareSupport = 0f,
                Phase = Space4XBoardingPhase.None,
                Outcome = Space4XBoardingOutcome.None
            });

            var system = _world.GetOrCreateSystem<Space4XBoardingSystem>();
            for (uint tick = 1u; tick < 90u; tick++)
            {
                SetTick(tick);
                system.Update(_world.Unmanaged);

                var order = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
                if (order.Outcome != Space4XBoardingOutcome.None)
                {
                    break;
                }
            }

            var finalOrder = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingOutcome.Repelled, finalOrder.Outcome);
            Assert.AreEqual(Space4XBoardingPhase.Repelled, finalOrder.Phase);
            Assert.IsFalse(_entityManager.HasComponent<Space4XBoardingCaptureState>(target), "Repelled target must not be captured.");
        }

        [Test]
        public void Boarding_DeploymentRequestClampsToHardCap()
        {
            var attacker = CreateShip(new float3(0f, 0f, 0f), hullCurrent: 180f, hullMax: 180f, shieldCurrent: 10f, shieldMax: 10f);
            var target = CreateShip(new float3(25f, 0f, 0f), hullCurrent: 40f, hullMax: 120f, shieldCurrent: 0f, shieldMax: 50f);

            _entityManager.AddComponentData(attacker, new Space4XBoardingDeploymentProfile
            {
                AvailableBoarders = 5000,
                ReserveBoarders = 0,
                MaxDeployPerAction = 4000,
                AverageTraining01 = 0.7f,
                AverageArmor01 = 0.7f,
                AverageWeapon01 = 0.7f
            });

            _entityManager.AddComponentData(attacker, new Space4XBoardingOrder
            {
                Target = target,
                TargetKind = Space4XBoardingTargetKind.Ship,
                IssuedTick = 0u,
                RequestedBoarderCount = 2000,
                MaxDurationTicks = 80u,
                DesiredRangeMeters = 100f,
                TroopCommitment01 = 1f,
                Phase = Space4XBoardingPhase.None,
                Outcome = Space4XBoardingOutcome.None
            });

            var system = _world.GetOrCreateSystem<Space4XBoardingSystem>();
            SetTick(1u);
            system.Update(_world.Unmanaged);

            var order = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingPhase.Launching, order.Phase);
            Assert.AreEqual(1000, order.CommittedBoarderCount, "Boarders per action should clamp to hard cap 1000.");

            var deployment = _entityManager.GetComponentData<Space4XBoardingDeploymentProfile>(attacker);
            Assert.AreEqual(4000, deployment.AvailableBoarders, "Deployment profile should consume committed boarders once.");
        }

        [Test]
        public void Boarding_ManifestCountOverridesStarterPlaceholder()
        {
            var attacker = CreateShip(new float3(0f, 0f, 0f), hullCurrent: 140f, hullMax: 140f, shieldCurrent: 15f, shieldMax: 15f);
            var target = CreateShip(new float3(20f, 0f, 0f), hullCurrent: 35f, hullMax: 100f, shieldCurrent: 0f, shieldMax: 40f);
            _entityManager.AddComponentData(target, Space4XBoardingProfile.Default);

            var manifest = _entityManager.AddBuffer<Space4XBoardingManifestEntry>(attacker);
            for (var i = 0; i < 60; i++)
            {
                var marine = CreateBoardingIndividual(physique: 70f, finesse: 68f, will: 66f, boarding: 1.35f, tactics: 62f);
                manifest.Add(new Space4XBoardingManifestEntry
                {
                    Individual = marine,
                    Role = Space4XBoardingRole.Assault,
                    Readiness01 = (half)1f,
                    ArmorTier01 = (half)0.7f,
                    WeaponTier01 = (half)0.72f,
                    Active = 1
                });
            }

            _entityManager.AddComponentData(attacker, new Space4XBoardingOrder
            {
                Target = target,
                TargetKind = Space4XBoardingTargetKind.Ship,
                IssuedTick = 0u,
                RequestedBoarderCount = 0,
                MaxDurationTicks = 80u,
                DesiredRangeMeters = 100f,
                TroopCommitment01 = 0.5f,
                Phase = Space4XBoardingPhase.None,
                Outcome = Space4XBoardingOutcome.None
            });

            var system = _world.GetOrCreateSystem<Space4XBoardingSystem>();
            SetTick(1u);
            system.Update(_world.Unmanaged);

            var order = _entityManager.GetComponentData<Space4XBoardingOrder>(attacker);
            Assert.AreEqual(Space4XBoardingPhase.Launching, order.Phase);
            Assert.AreEqual(60, order.CommittedBoarderCount, "Explicit individual manifest should define boarding count (not 5-30 placeholder).");
            Assert.Greater(order.AttackerForce, 1.25f);
        }

        private Entity CreateShip(float3 position, float hullCurrent, float hullMax, float shieldCurrent, float shieldMax)
        {
            var entity = _entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(HullIntegrity),
                typeof(Space4XShield));

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.SetComponentData(entity, new HullIntegrity
            {
                Current = hullCurrent,
                Max = hullMax,
                BaseMax = hullMax,
                ArmorRating = (half)0f,
                LastDamageTick = 0u,
                LastRepairTick = 0u
            });

            _entityManager.SetComponentData(entity, new Space4XShield
            {
                Type = ShieldType.Standard,
                Current = shieldCurrent,
                Maximum = shieldMax,
                RechargeRate = 0f,
                RechargeDelay = 0,
                CurrentDelay = 0,
                EnergyResistance = (half)1f,
                ThermalResistance = (half)1f,
                EMResistance = (half)1f,
                RadiationResistance = (half)1f,
                KineticResistance = (half)1f,
                ExplosiveResistance = (half)1f,
                CausticResistance = (half)1f
            });

            return entity;
        }

        private void SetTick(uint tick)
        {
            using var query = _entityManager.CreateEntityQuery(typeof(TimeState));
            var timeEntity = query.GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = tick;
            time.IsPaused = false;
            _entityManager.SetComponentData(timeEntity, time);
        }

        private static bool HasDisable(DynamicBuffer<SubsystemDisabled> disabled, SubsystemType type)
        {
            for (var i = 0; i < disabled.Length; i++)
            {
                if (disabled[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private Entity CreateBoardingIndividual(float physique, float finesse, float will, float boarding, float tactics)
        {
            var entity = _entityManager.CreateEntity(
                typeof(PhysiqueFinesseWill),
                typeof(DerivedCapacities),
                typeof(IndividualStats));

            _entityManager.SetComponentData(entity, new PhysiqueFinesseWill
            {
                Physique = (half)physique,
                Finesse = (half)finesse,
                Will = (half)will,
                PhysiqueInclination = 6,
                FinesseInclination = 6,
                WillInclination = 6,
                GeneralXP = 0f
            });

            _entityManager.SetComponentData(entity, new DerivedCapacities
            {
                Sight = 1f,
                Manipulation = 1f,
                Consciousness = 1f,
                ReactionTime = 1f,
                Boarding = boarding
            });

            _entityManager.SetComponentData(entity, new IndividualStats
            {
                Command = (half)45f,
                Tactics = (half)tactics,
                Logistics = (half)30f,
                Diplomacy = (half)20f,
                Engineering = (half)25f,
                Resolve = (half)55f
            });

            return entity;
        }
    }
}
#endif

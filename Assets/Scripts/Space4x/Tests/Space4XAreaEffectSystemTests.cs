#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    using PDCarrierModuleSlot = PureDOTS.Runtime.Ships.CarrierModuleSlot;
    using PDShipModule = PureDOTS.Runtime.Ships.ShipModule;

    public class Space4XAreaEffectSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XAreaEffectSystemTests");
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
        public void AreaEffect_BinaryOcclusion_BlocksTargetsBehindOccluder()
        {
            var emitter = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XAreaEffectEmitter));
            _entityManager.SetComponentData(emitter, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(emitter, new Space4XAreaEffectEmitter
            {
                Scope = Space4XStatusEffectScope.Area,
                TargetMask = Space4XAreaEffectTargetMask.Ships,
                ImpactMask = Space4XAreaEffectImpactMask.HullDamage,
                OcclusionChannel = Space4XAreaOcclusionChannel.Blast,
                OcclusionMode = Space4XAreaOcclusionMode.BinaryBlock,
                DamageType = Space4XDamageType.Explosive,
                Radius = 20f,
                InnerRadius = 0f,
                Magnitude = 30f,
                FalloffExponent = 1f,
                PulseIntervalTicks = 1u,
                NextPulseTick = 0u,
                RemainingPulses = 1u,
                Active = 1,
                AffectsSource = 0
            });

            var blockedTarget = _entityManager.CreateEntity(typeof(LocalTransform), typeof(HullIntegrity));
            _entityManager.SetComponentData(blockedTarget, new LocalTransform
            {
                Position = new float3(10f, 0f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(blockedTarget, HullIntegrity.Create(100f));

            var openTarget = _entityManager.CreateEntity(typeof(LocalTransform), typeof(HullIntegrity));
            _entityManager.SetComponentData(openTarget, new LocalTransform
            {
                Position = new float3(0f, 10f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(openTarget, HullIntegrity.Create(100f));

            var occluder = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XAreaOccluder));
            _entityManager.SetComponentData(occluder, new LocalTransform
            {
                Position = new float3(5f, 0f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(occluder, new Space4XAreaOccluder
            {
                Radius = 2f,
                Strength01 = 1f,
                BlocksChannels = Space4XAreaOcclusionChannel.Blast
            });

            var system = _world.GetOrCreateSystem<Space4XAreaEffectSystem>();
            system.Update(_world.Unmanaged);

            var blockedHull = _entityManager.GetComponentData<HullIntegrity>(blockedTarget);
            var openHull = _entityManager.GetComponentData<HullIntegrity>(openTarget);

            Assert.AreEqual(100f, blockedHull.Current, 1e-3f, "Target behind occluder should be sheltered.");
            Assert.Less(openHull.Current, 100f, "Open target should take AoE damage.");
        }

        [Test]
        public void AreaEffect_DisablesWeaponAndEngineSubsystems_ForDuration()
        {
            var emitter = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XAreaEffectEmitter));
            _entityManager.SetComponentData(emitter, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(emitter, new Space4XAreaEffectEmitter
            {
                Scope = Space4XStatusEffectScope.Area,
                TargetMask = Space4XAreaEffectTargetMask.Ships,
                ImpactMask = Space4XAreaEffectImpactMask.DisableWeapons | Space4XAreaEffectImpactMask.DisableEngines,
                OcclusionChannel = Space4XAreaOcclusionChannel.EMP,
                OcclusionMode = Space4XAreaOcclusionMode.None,
                DamageType = Space4XDamageType.EM,
                Radius = 25f,
                InnerRadius = 0f,
                Magnitude = 5f,
                FalloffExponent = 1f,
                DisableDurationTicks = 12u,
                PulseIntervalTicks = 1u,
                NextPulseTick = 0u,
                RemainingPulses = 1u,
                Active = 1,
                AffectsSource = 0
            });

            var target = _entityManager.CreateEntity(typeof(LocalTransform), typeof(HullIntegrity));
            _entityManager.SetComponentData(target, new LocalTransform
            {
                Position = new float3(8f, 0f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(target, HullIntegrity.Create(100f));
            var disabled = _entityManager.AddBuffer<SubsystemDisabled>(target);

            var system = _world.GetOrCreateSystem<Space4XAreaEffectSystem>();
            system.Update(_world.Unmanaged);

            bool hasWeapons = false;
            bool hasEngines = false;
            for (var i = 0; i < disabled.Length; i++)
            {
                if (disabled[i].Type == SubsystemType.Weapons && disabled[i].UntilTick == 12u)
                {
                    hasWeapons = true;
                }

                if (disabled[i].Type == SubsystemType.Engines && disabled[i].UntilTick == 12u)
                {
                    hasEngines = true;
                }
            }

            Assert.IsTrue(hasWeapons, "AoE should disable weapon subsystem.");
            Assert.IsTrue(hasEngines, "AoE should disable engine subsystem.");
        }

        [Test]
        public void AreaEffect_ModuleLimbDamage_EmitsPerInstalledModuleEvent()
        {
            var emitter = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XAreaEffectEmitter));
            _entityManager.SetComponentData(emitter, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(emitter, new Space4XAreaEffectEmitter
            {
                Scope = Space4XStatusEffectScope.Area,
                TargetMask = Space4XAreaEffectTargetMask.Ships,
                ImpactMask = Space4XAreaEffectImpactMask.ModuleLimbDamage,
                OcclusionChannel = Space4XAreaOcclusionChannel.Blast,
                OcclusionMode = Space4XAreaOcclusionMode.None,
                DamageType = Space4XDamageType.Explosive,
                Radius = 25f,
                InnerRadius = 0f,
                Magnitude = 20f,
                ModuleDamageScale = 0.5f,
                FalloffExponent = 1f,
                PulseIntervalTicks = 1u,
                NextPulseTick = 0u,
                RemainingPulses = 1u,
                Active = 1,
                AffectsSource = 0
            });

            var ship = _entityManager.CreateEntity(typeof(LocalTransform), typeof(HullIntegrity));
            _entityManager.SetComponentData(ship, new LocalTransform
            {
                Position = new float3(6f, 0f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.SetComponentData(ship, HullIntegrity.Create(200f));

            var module = _entityManager.CreateEntity(typeof(PDShipModule));
            var limbState = _entityManager.AddBuffer<ModuleLimbState>(module);
            limbState.Add(new ModuleLimbState
            {
                LimbId = ModuleLimbId.StructuralFrame,
                Family = ModuleLimbFamily.Structural,
                Integrity = 1f,
                Exposure = 1f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot
            {
                InstalledModule = module
            });

            var system = _world.GetOrCreateSystem<Space4XAreaEffectSystem>();
            system.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasBuffer<ModuleLimbDamageEvent>(module), "AoE module damage should create limb event buffer.");
            var events = _entityManager.GetBuffer<ModuleLimbDamageEvent>(module);
            Assert.AreEqual(1, events.Length, "Exactly one limb damage event should be emitted for one installed module.");
            Assert.Greater(events[0].Damage, 0f, "Module limb damage event should carry positive damage.");
            Assert.AreEqual(ModuleLimbFamily.Structural, events[0].Family, "Explosive AoE should resolve structural limb family.");
        }
    }
}
#endif

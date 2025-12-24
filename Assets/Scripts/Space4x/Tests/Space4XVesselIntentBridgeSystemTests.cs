#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Intent;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Tests for Space4XVesselIntentBridgeSystem - validates intent-to-goal mapping and intent clearing.
    /// </summary>
    public sealed class Space4XVesselIntentBridgeSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World(nameof(Space4XVesselIntentBridgeSystemTests), WorldFlags.Game);
            _entityManager = _world.EntityManager;

            // Setup required singletons
            _entityManager.CreateEntity(typeof(GameWorldTag));
            _entityManager.SetComponentData(_entityManager.CreateEntity(typeof(TimeState)), new TimeState
            {
                Tick = 1,
                FixedDeltaTime = 0.1f,
                IsPaused = false
            });
            var rewindEntity = _entityManager.CreateEntity(typeof(RewindState));
            _entityManager.SetComponentData(rewindEntity, new RewindState
            {
                Mode = RewindMode.Record,
                TargetTick = 0,
                TickDuration = 0.1f,
                MaxHistoryTicks = 600,
                PendingStepTicks = 0
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        #region Suite 1: Destroyed Target Entity Clearing

        [Test]
        public void Space4XVesselIntentBridgeSystem_DestroysTarget_ClearsIntent()
        {
            // Create vessel with EntityIntent targeting asteroid entity
            var vessel = CreateMiningVesselWithIntent(IntentMode.Gather, Entity.Null, float3.zero);
            var asteroidEntity = _entityManager.CreateEntity(typeof(LocalTransform));
            _entityManager.SetComponentData(asteroidEntity, LocalTransform.Identity);

            // Set intent to target the asteroid
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            intent.TargetEntity = asteroidEntity;
            intent.Mode = IntentMode.Gather;
            intent.IsValid = 1;
            intent.Priority = InterruptPriority.Normal;
            intent.IntentSetTick = 1;
            _entityManager.SetComponentData(vessel, intent);

            // Set AI state based on intent
            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            aiState.CurrentGoal = VesselAIState.Goal.Mining;
            aiState.TargetEntity = asteroidEntity;
            _entityManager.SetComponentData(vessel, aiState);

            // Destroy asteroid entity
            _entityManager.DestroyEntity(asteroidEntity);
            _world.Update(); // Allow entity destruction to process

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify intent is cleared
            var updatedIntent = _entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(0, updatedIntent.IsValid, "Intent should be invalid after target destroyed");
            Assert.AreEqual(IntentMode.Idle, updatedIntent.Mode, "Intent mode should be Idle after clearing");
        }

        #endregion

        #region Suite 2: Intent Mode Mapping Verification

        [Test]
        public void Space4XVesselIntentBridgeSystem_IntentModeMappings()
        {
            // Test Gather → Mining
            TestIntentModeMapping(IntentMode.Gather, VesselAIState.Goal.Mining, VesselAIState.State.Mining);

            // Test Flee → Returning
            TestIntentModeMapping(IntentMode.Flee, VesselAIState.Goal.Returning, VesselAIState.State.Returning);

            // Test Patrol → Patrol
            TestIntentModeMapping(IntentMode.Patrol, VesselAIState.Goal.Patrol, VesselAIState.State.MovingToTarget);

            // Test Follow → Formation
            TestIntentModeMapping(IntentMode.Follow, VesselAIState.Goal.Formation, VesselAIState.State.MovingToTarget);

            // Test Defend → Escort
            TestIntentModeMapping(IntentMode.Defend, VesselAIState.Goal.Escort, VesselAIState.State.MovingToTarget);

            // Test UseAbility → Mining (for mining vessels)
            TestIntentModeMapping(IntentMode.UseAbility, VesselAIState.Goal.Mining, VesselAIState.State.Mining);

            // Test Build → None (not applicable to vessels)
            TestIntentModeMapping(IntentMode.Build, VesselAIState.Goal.None, VesselAIState.State.Idle);

            // Test Attack → None (combat not implemented)
            TestIntentModeMapping(IntentMode.Attack, VesselAIState.Goal.None, VesselAIState.State.Idle);

            // Test Custom modes → None (intentional, game-specific)
            TestIntentModeMapping(IntentMode.Custom0, VesselAIState.Goal.None, VesselAIState.State.Idle);
            TestIntentModeMapping(IntentMode.Custom1, VesselAIState.Goal.None, VesselAIState.State.Idle);
        }

        [Test]
        public void IntentBridge_GoalCompletion_Clearing()
        {
            // Create vessel with EntityIntent and VesselAIState.Goal = Mining
            var vessel = CreateMiningVesselWithIntent(IntentMode.Gather, Entity.Null, float3.zero);
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            intent.Mode = IntentMode.Gather;
            intent.IsValid = 1;
            intent.IntentSetTick = 1;
            _entityManager.SetComponentData(vessel, intent);

            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            aiState.CurrentGoal = VesselAIState.Goal.Mining;
            aiState.CurrentState = VesselAIState.State.Mining;
            _entityManager.SetComponentData(vessel, aiState);

            // Simulate goal completion (set to None and Idle)
            aiState.CurrentGoal = VesselAIState.Goal.None;
            aiState.CurrentState = VesselAIState.State.Idle;
            _entityManager.SetComponentData(vessel, aiState);

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify EntityIntent is cleared
            var updatedIntent = _entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(0, updatedIntent.IsValid, "Intent should be cleared when goal completed");
        }

        [Test]
        public void IntentBridge_IntentMode_InterruptDriven_Flow()
        {
            // Create mining vessel with interrupt buffer
            var vessel = CreateMiningVesselWithIntent(IntentMode.Idle, Entity.Null, float3.zero);
            var interruptBuffer = _entityManager.GetBuffer<Interrupt>(vessel);

            // Emit interrupt (ResourceSpotted → Gather intent)
            var asteroidEntity = _entityManager.CreateEntity(typeof(LocalTransform));
            _entityManager.SetComponentData(asteroidEntity, LocalTransform.Identity);

            InterruptUtils.Emit(
                ref interruptBuffer,
                InterruptType.ResourceSpotted,
                InterruptPriority.Normal,
                vessel,
                1,
                asteroidEntity);

            // Run InterruptHandlerSystem
            var interruptSystem = _world.GetOrCreateSystem<PureDOTS.Systems.Interrupts.InterruptHandlerSystem>();
            interruptSystem.Update(_world.Unmanaged);

            // Verify EntityIntent created
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(IntentMode.Gather, intent.Mode, "Interrupt should create Gather intent");
            Assert.AreEqual(1, intent.IsValid, "Intent should be valid");
            Assert.AreEqual(asteroidEntity, intent.TargetEntity, "Target entity should match interrupt target");

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify goal updated to Mining
            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            Assert.AreEqual(VesselAIState.Goal.Mining, aiState.CurrentGoal, "Goal should be Mining");
            Assert.AreEqual(VesselAIState.State.Mining, aiState.CurrentState, "State should be Mining");
        }

        [Test]
        public void IntentBridge_IntentMode_PriorityOverride()
        {
            // Create vessel with EntityIntent (Normal priority, Gather mode)
            var vessel = CreateMiningVesselWithIntent(IntentMode.Gather, Entity.Null, float3.zero);
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            intent.Mode = IntentMode.Gather;
            intent.Priority = InterruptPriority.Normal;
            intent.IsValid = 1;
            intent.IntentSetTick = 1;
            _entityManager.SetComponentData(vessel, intent);

            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            aiState.CurrentGoal = VesselAIState.Goal.Mining;
            _entityManager.SetComponentData(vessel, aiState);

            // Emit higher-priority interrupt (LowHealth → Flee intent, High priority)
            var interruptBuffer = _entityManager.GetBuffer<Interrupt>(vessel);

            InterruptUtils.Emit(
                ref interruptBuffer,
                InterruptType.LowHealth,
                InterruptPriority.High,
                vessel,
                2);

            // Run EnhancedInterruptHandlerSystem
            var enhancedInterruptSystem = _world.GetOrCreateSystem<PureDOTS.Systems.Intent.EnhancedInterruptHandlerSystem>();
            enhancedInterruptSystem.Update(_world.Unmanaged);

            // Verify intent overridden to Flee
            var updatedIntent = _entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(IntentMode.Flee, updatedIntent.Mode, "Intent should be overridden to Flee");
            Assert.AreEqual(InterruptPriority.High, updatedIntent.Priority, "Priority should be High");

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify goal updated to Returning
            var updatedAiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            Assert.AreEqual(VesselAIState.Goal.Returning, updatedAiState.CurrentGoal, "Goal should be updated to Returning");
        }

        [Test]
        public void IntentBridge_DestroysTarget_PositionBasedIntent_NotCleared()
        {
            // Create vessel with position-based intent (no target entity)
            var vessel = CreateMiningVesselWithIntent(IntentMode.MoveTo, Entity.Null, new float3(10f, 0f, 10f));
            var unrelatedEntity = _entityManager.CreateEntity(typeof(LocalTransform));
            _entityManager.SetComponentData(unrelatedEntity, LocalTransform.Identity);

            // Set intent with position but no entity
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            intent.TargetEntity = Entity.Null;
            intent.TargetPosition = new float3(10f, 0f, 10f);
            intent.Mode = IntentMode.MoveTo;
            intent.IsValid = 1;
            intent.Priority = InterruptPriority.Normal;
            intent.IntentSetTick = 1;
            _entityManager.SetComponentData(vessel, intent);

            // Destroy unrelated entity
            _entityManager.DestroyEntity(unrelatedEntity);
            _world.Update();

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify intent remains valid (position-based intents don't depend on entities)
            var updatedIntent = _entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(1, updatedIntent.IsValid, "Position-based intent should remain valid");
            Assert.AreEqual(IntentMode.MoveTo, updatedIntent.Mode, "Intent mode should remain MoveTo");
        }

        [Test]
        public void IntentBridge_DestroysTarget_EntityStorageInfoLookup_Works()
        {
            // Create multiple vessels with intents targeting different asteroids
            var asteroid1 = _entityManager.CreateEntity(typeof(LocalTransform));
            var asteroid2 = _entityManager.CreateEntity(typeof(LocalTransform));
            var asteroid3 = _entityManager.CreateEntity(typeof(LocalTransform));
            _entityManager.SetComponentData(asteroid1, LocalTransform.Identity);
            _entityManager.SetComponentData(asteroid2, LocalTransform.Identity);
            _entityManager.SetComponentData(asteroid3, LocalTransform.Identity);

            var vessel1 = CreateMiningVesselWithIntent(IntentMode.Gather, asteroid1, float3.zero);
            var vessel2 = CreateMiningVesselWithIntent(IntentMode.Gather, asteroid2, float3.zero);
            var vessel3 = CreateMiningVesselWithIntent(IntentMode.Gather, asteroid3, float3.zero);

            // Destroy subset of asteroids
            _entityManager.DestroyEntity(asteroid1);
            _entityManager.DestroyEntity(asteroid3);
            _world.Update();

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify only intents with destroyed targets are cleared
            var intent1 = _entityManager.GetComponentData<EntityIntent>(vessel1);
            var intent2 = _entityManager.GetComponentData<EntityIntent>(vessel2);
            var intent3 = _entityManager.GetComponentData<EntityIntent>(vessel3);

            Assert.AreEqual(0, intent1.IsValid, "Intent 1 should be cleared (target destroyed)");
            Assert.AreEqual(1, intent2.IsValid, "Intent 2 should remain valid (target exists)");
            Assert.AreEqual(0, intent3.IsValid, "Intent 3 should be cleared (target destroyed)");
        }

        #endregion

        #region Helper Methods

        private Entity CreateMiningVesselWithIntent(IntentMode mode, Entity targetEntity, float3 targetPosition)
        {
            var entity = _entityManager.CreateEntity(
                typeof(MiningVessel),
                typeof(VesselAIState),
                typeof(EntityIntent),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, LocalTransform.Identity);

            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new Unity.Collections.FixedString64Bytes("TestVessel"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 0.5f,
                Speed = 5f,
                CargoCapacity = 50f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            _entityManager.SetComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            _entityManager.SetComponentData(entity, new EntityIntent
            {
                Mode = mode,
                TargetEntity = targetEntity,
                TargetPosition = targetPosition,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 1,
                Priority = InterruptPriority.Normal,
                IsValid = 1
            });

            // Add interrupt buffer (required for interrupt-driven flow)
            _entityManager.AddBuffer<Interrupt>(entity);

            return entity;
        }

        private void TestIntentModeMapping(IntentMode intentMode, VesselAIState.Goal expectedGoal, VesselAIState.State expectedState)
        {
            var vessel = CreateMiningVesselWithIntent(intentMode, Entity.Null, float3.zero);
            var intent = _entityManager.GetComponentData<EntityIntent>(vessel);
            intent.Mode = intentMode;
            intent.IsValid = 1;
            intent.IntentSetTick = 1;
            _entityManager.SetComponentData(vessel, intent);

            // Run bridge system
            var bridgeSystem = _world.GetOrCreateSystem<Space4XVesselIntentBridgeSystem>();
            bridgeSystem.Update(_world.Unmanaged);

            // Verify mapping
            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            Assert.AreEqual(expectedGoal, aiState.CurrentGoal,
                $"IntentMode.{intentMode} should map to Goal.{expectedGoal}");
            Assert.AreEqual(expectedState, aiState.CurrentState,
                $"Goal.{expectedGoal} should map to State.{expectedState}");
        }

        #endregion
    }
}
#endif


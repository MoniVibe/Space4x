#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems;
using Space4X.Systems.AI;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UCamera = UnityEngine.Camera;
using UnityObject = UnityEngine.Object;

namespace Space4X.Tests
{
    public sealed class Space4XAttackMoveLoopTests
    {
        private GameObject _cameraObject;
        private bool _restoreRendering;

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_restoreRendering)
            {
                RuntimeMode.ForceRenderingEnabled(false, "AttackMoveInputTestCleanup");
                _restoreRendering = false;
            }
#endif
            if (_cameraObject != null)
            {
                UnityObject.DestroyImmediate(_cameraObject);
                _cameraObject = null;
            }
        }

        [Test]
        public void ContextOrder_CtrlConvertsMoveToAttackMove()
        {
            using var world = new World("Space4XAttackMoveInputTests");
            var entityManager = world.EntityManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _restoreRendering = RuntimeMode.IsRenderingEnabled == false;
            if (_restoreRendering)
            {
                RuntimeMode.ForceRenderingEnabled(true, "AttackMoveInputTest");
            }
#endif

            CreateMainCamera();

            var selected = entityManager.CreateEntity(typeof(SelectedTag), typeof(SelectionOwner));
            entityManager.SetComponentData(selected, new SelectionOwner { PlayerId = 0 });

            var inputEntity = entityManager.CreateEntity(typeof(RtsInputSingletonTag));
            var clicks = entityManager.AddBuffer<RightClickEvent>(inputEntity);

            var system = world.GetOrCreateSystem<Space4XContextOrderSystem>();

            clicks.Add(new RightClickEvent
            {
                ScreenPos = new float2(400f, 300f),
                Queue = 0,
                Ctrl = 0,
                PlayerId = 0
            });
            system.Update(world.Unmanaged);

            var orders = entityManager.GetBuffer<OrderQueueElement>(selected);
            Assert.AreEqual(1, orders.Length, "Move order should be queued");
            Assert.AreEqual(OrderKind.Move, orders[0].Order.Kind, "Move order should stay Move without Ctrl");
            Assert.AreEqual((byte)OrderFlags.None, orders[0].Order.Flags, "Move order should not set AttackMove flag");

            clicks.Add(new RightClickEvent
            {
                ScreenPos = new float2(400f, 300f),
                Queue = 0,
                Ctrl = 1,
                PlayerId = 0
            });
            system.Update(world.Unmanaged);

            orders = entityManager.GetBuffer<OrderQueueElement>(selected);
            Assert.AreEqual(1, orders.Length, "AttackMove order should replace previous order");
            Assert.AreEqual(OrderKind.Attack, orders[0].Order.Kind, "Ctrl should convert Move to Attack order");
            Assert.AreEqual((byte)OrderFlags.AttackMove, orders[0].Order.Flags, "AttackMove flag should be set");
        }

        [Test]
        public void AttackMove_FiresWhileInRange_StopsOutOfRange_ContinuesMoving()
        {
            using var harness = new ISystemTestHarness(fixedDelta: 0.1f);
            var entityManager = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);

            harness.Add<VesselAttackMoveGuidanceSystem>();
            harness.Add<VesselMovementSystem>();

            var weaponSystem = harness.World.GetOrCreateSystem<Space4XWeaponSystem>();
            harness.Sim.AddSystemToUpdateList(weaponSystem);

            var target = entityManager.CreateEntity();
            entityManager.AddComponentData(target, new LocalTransform
            {
                Position = new float3(0f, 0f, -5f),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            var ship = entityManager.CreateEntity();
            entityManager.AddComponentData(ship, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            entityManager.AddComponentData(ship, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 6f,
                CurrentSpeed = 0f,
                Acceleration = 6f,
                Deceleration = 6f,
                TurnSpeed = 2f,
                SlowdownDistance = 2f,
                ArrivalDistance = 1f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });
            entityManager.AddComponentData(ship, new VesselAIState
            {
                CurrentState = VesselAIState.State.MovingToTarget,
                CurrentGoal = VesselAIState.Goal.Patrol,
                TargetEntity = target,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            entityManager.AddComponentData(ship, new AttackMoveIntent
            {
                Destination = new float3(0f, 0f, 30f),
                DestinationRadius = 0f,
                EngageTarget = target,
                AcquireTargetsAlongRoute = 0,
                KeepFiringWhileInRange = 1,
                StartTick = 0
            });
            entityManager.AddComponentData(ship, new Space4XEngagement
            {
                PrimaryTarget = target,
                Phase = EngagementPhase.Engaged
            });
            entityManager.AddComponentData(ship, new SupplyStatus
            {
                Ammunition = 10f,
                AmmunitionCapacity = 10f
            });

            var weapons = entityManager.AddBuffer<WeaponMount>(ship);
            weapons.Add(new WeaponMount
            {
                IsEnabled = 1,
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = 5f,
                    OptimalRange = 5f,
                    MaxRange = 8f,
                    BaseAccuracy = (half)1f,
                    CooldownTicks = 2,
                    CurrentCooldown = 0,
                    AmmoPerShot = 0,
                    ShieldModifier = (half)1f,
                    ArmorPenetration = (half)0f
                }
            });

            harness.Step(1);
            var weaponBuffer = entityManager.GetBuffer<WeaponMount>(ship);
            Assert.Greater(weaponBuffer[0].Weapon.CurrentCooldown, 0, "Weapon should fire while in range");

            const float maxRange = 8f;
            bool leftRange = false;
            float3 lastPos = entityManager.GetComponentData<LocalTransform>(ship).Position;
            for (int i = 0; i < 100; i++)
            {
                harness.Step(1);
                var shipPos = entityManager.GetComponentData<LocalTransform>(ship).Position;
                var targetPos = entityManager.GetComponentData<LocalTransform>(target).Position;
                var distance = math.distance(shipPos, targetPos);
                if (distance > maxRange + 0.5f)
                {
                    leftRange = true;
                    lastPos = shipPos;
                    break;
                }
            }

            Assert.IsTrue(leftRange, "Ship should leave weapon range while moving to destination");

            for (int i = 0; i < 5; i++)
            {
                harness.Step(1);
            }

            weaponBuffer = entityManager.GetBuffer<WeaponMount>(ship);
            Assert.AreEqual(0, weaponBuffer[0].Weapon.CurrentCooldown, "Weapon should stop firing out of range");

            harness.Step(1);
            var posAfter = entityManager.GetComponentData<LocalTransform>(ship).Position;
            Assert.Greater(posAfter.z, lastPos.z, "Ship should keep moving toward destination");
        }

        [Test]
        public void AttackMoveCompletion_ClearsIntent_ResumesPatrol()
        {
            using var harness = new ISystemTestHarness(fixedDelta: 0.1f);
            var entityManager = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);

            var vessel = entityManager.CreateEntity();
            entityManager.AddComponentData(vessel, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            entityManager.AddComponentData(vessel, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 4f,
                Acceleration = 4f,
                Deceleration = 4f,
                TurnSpeed = 2f,
                SlowdownDistance = 2f,
                ArrivalDistance = 1f,
                PatrolCenter = float3.zero,
                PatrolRadius = 20f
            });
            entityManager.AddComponentData(vessel, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 0f,
                WaitTimer = 0f
            });
            entityManager.AddComponentData(vessel, new MovementCommand
            {
                TargetPosition = float3.zero,
                ArrivalThreshold = 1f
            });
            entityManager.AddComponentData(vessel, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 4f,
                CurrentSpeed = 0f,
                Acceleration = 4f,
                Deceleration = 4f,
                TurnSpeed = 2f,
                SlowdownDistance = 2f,
                ArrivalDistance = 1f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });
            entityManager.AddComponentData(vessel, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Patrol,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            entityManager.AddComponentData(vessel, new EntityIntent
            {
                Mode = IntentMode.MoveTo,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 1,
                Priority = InterruptPriority.Low,
                IsValid = 1
            });
            entityManager.AddComponentData(vessel, new AttackMoveIntent
            {
                Destination = float3.zero,
                DestinationRadius = 1f,
                EngageTarget = Entity.Null,
                AcquireTargetsAlongRoute = 0,
                KeepFiringWhileInRange = 1,
                StartTick = 1
            });
            entityManager.AddComponentData(vessel, new AttackMoveOrigin
            {
                WasPatrolling = 1
            });

            entityManager.AddComponentData(vessel, WaypointPath.Default);
            var pathPoints = entityManager.AddBuffer<WaypointPathPoint>(vessel);
            pathPoints.Add(new WaypointPathPoint { Position = new float3(10f, 0f, 0f) });
            pathPoints.Add(new WaypointPathPoint { Position = new float3(20f, 0f, 0f) });

            var lifecycle = harness.World.GetOrCreateSystem<VesselAttackMoveLifecycleSystem>();
            lifecycle.Update(harness.World.Unmanaged);

            Assert.IsFalse(entityManager.HasComponent<AttackMoveIntent>(vessel), "AttackMove intent should clear at destination");
            Assert.IsFalse(entityManager.HasComponent<AttackMoveOrigin>(vessel), "AttackMove origin should clear at destination");
            var intent = entityManager.GetComponentData<EntityIntent>(vessel);
            Assert.AreEqual(0, intent.IsValid, "Intent should clear to allow patrol resume");

            var patrol = harness.World.GetOrCreateSystem<CarrierPatrolSystem>();
            patrol.Update(harness.World.Unmanaged);

            var movement = entityManager.GetComponentData<MovementCommand>(vessel);
            Assert.AreEqual(new float3(10f, 0f, 0f), movement.TargetPosition, "Patrol should resume with next waypoint");
        }

        private void CreateMainCamera()
        {
            _cameraObject = new GameObject("AttackMoveTestCamera");
            var camera = _cameraObject.AddComponent<UCamera>();
            _cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.transform.LookAt(Vector3.zero);
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
        }
    }
}
#endif

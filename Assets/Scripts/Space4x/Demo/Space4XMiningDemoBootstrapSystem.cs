using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;
using Space4X.Runtime;
using Shared.Demo;
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Spawns a minimal mining scene: one miner vessel + one resource node,
    /// wired into existing mining systems for a complete gameplay loop.
    /// Runs once at startup, then disables itself.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Shared.Demo.SharedDemoRenderBootstrap))]
    public partial class Space4XMiningDemoBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnCreate()
        {
            Debug.Log("[Space4XMiningDemoBootstrapSystem] OnCreate");
            // TEMP: disable gating to confirm system runs
            // RequireForUpdate<DemoRenderReady>();
        }

        protected override void OnUpdate()
        {
            // TEMP: disable this demo spawner to keep Space4X debug orbit cubes clear
            Enabled = false;
            return;

            Debug.Log($"[Space4XMiningDemoBootstrapSystem] OnUpdate enter (frame {UnityEngine.Time.frameCount})");

            if (_initialized)
            {
                Debug.Log("[Space4XMiningDemoBootstrapSystem] Already initialized, disabling.");
                Enabled = false;
                return;
            }

            var entityManager = EntityManager;

            // Super obvious debug cube at (0, 0, 0) - copy TestSpawnSystem pattern exactly
            var debugEntity = entityManager.CreateEntity();
            // Position (0,0,0), Scale 2
            var transform = LocalTransform.FromPosition(float3.zero);
            transform.Scale = 2f;
            entityManager.AddComponentData(debugEntity, transform);
            // Render component - magenta color (1, 0, 1, 1)
            DemoRenderUtil.MakeRenderable(entityManager, debugEntity, new float4(1f, 0f, 1f, 1f));
            Debug.Log("[Space4XMiningDemoBootstrapSystem] Spawned debug magenta cube at origin.");

            /*
            // TEMPORARILY DISABLED: Mining spawn - enable after debug cube works
            // Spawn a resource node (asteroid)
            var resourceNode = entityManager.CreateEntity();
            entityManager.AddComponentData(resourceNode, LocalTransform.FromPosition(new float3(30f, 0f, 40f)));
            entityManager.AddComponentData(resourceNode, new ResourceSourceState
            {
                UnitsRemaining = 1000f,
                LastHarvestTick = 0
            });
            entityManager.AddComponentData(resourceNode, new ResourceSourceConfig
            {
                GatherRatePerWorker = 10f,
                MaxSimultaneousWorkers = 3,
                RespawnSeconds = 0f,
                Flags = 0
            });
            entityManager.AddComponentData(resourceNode, new ResourceTypeId { Value = "ore" });

            // Make it visually distinct (gray rock-like color)
            DemoRenderUtil.MakeRenderable(entityManager, resourceNode, new float4(0.5f, 0.5f, 0.5f, 1f));

            // Spawn a miner vessel
            var minerVessel = entityManager.CreateEntity();
            entityManager.AddComponentData(minerVessel, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            entityManager.AddComponentData(minerVessel, new MiningVessel
            {
                VesselId = "demo-miner-01",
                CarrierEntity = Entity.Null, // No carrier for this demo
                MiningEfficiency = 1f,
                Speed = 15f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Ore
            });
            entityManager.AddComponentData(minerVessel, new MiningOrder
            {
                ResourceId = "ore",
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = resourceNode,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });
            entityManager.AddComponentData(minerVessel, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 1f
            });
            entityManager.AddComponentData(minerVessel, new MiningYield
            {
                ResourceId = "ore",
                PendingAmount = 0f,
                SpawnThreshold = 50f,
                SpawnReady = 0
            });

            // Add vessel AI and movement components
            entityManager.AddComponentData(minerVessel, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            entityManager.AddComponentData(minerVessel, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 15f,
                CurrentSpeed = 0f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            // Make it visually distinct (yellow miner color)
            DemoRenderUtil.MakeRenderable(entityManager, minerVessel, new float4(1f, 1f, 0f, 1f));

            Debug.Log("[Space4XMiningDemoBootstrapSystem] Spawned 1 miner vessel and 1 resource node for mining demo loop.");
            */

            _initialized = true;
            Enabled = false;
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Systems
{
    public struct MovementDiagnosticsToggle : IComponentData {}

    /// <summary>
    /// Comprehensive diagnostic system to identify why units aren't moving.
    /// </summary>
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial class MovementDiagnosticSystem : SystemBase
    {
        private int _frameCount;
        private const int LogInterval = 120; // Log every 2 seconds

        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<MovementDiagnosticsToggle>();
        }

        protected override void OnUpdate()
        {
            _frameCount++;
            if (_frameCount % LogInterval != 0)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                Debug.LogWarning("[MovementDiagnostic] Game is PAUSED! Units won't move.");
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                Debug.LogWarning($"[MovementDiagnostic] RewindMode is {rewindState.Mode}, not Record! Units won't move.");
                return;
            }

            Debug.Log($"=== MOVEMENT DIAGNOSTIC (Frame {_frameCount}, Tick {timeState.Tick}) ===");

            // Check vessels
            var vesselCount = 0;
            var vesselsWithTargets = 0;
            var vesselsWithPositions = 0;
            var vesselsMoving = 0;
            var vesselsWaitingForPosition = 0;

            foreach (var (transform, aiState, movement, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<VesselAIState>, RefRO<VesselMovement>>()
                .WithEntityAccess())
            {
                vesselCount++;
                var targetEntity = aiState.ValueRO.TargetEntity;
                var targetPos = aiState.ValueRO.TargetPosition;
                var state = aiState.ValueRO.CurrentState;
                var isMoving = movement.ValueRO.IsMoving != 0;

                if (targetEntity != Entity.Null)
                {
                    vesselsWithTargets++;
                }

                if (!targetPos.Equals(float3.zero))
                {
                    vesselsWithPositions++;
                }
                else if (targetEntity != Entity.Null)
                {
                    vesselsWaitingForPosition++;
                }

                if (isMoving)
                {
                    vesselsMoving++;
                }

                if (vesselCount <= 2)
                {
                    Debug.Log($"Vessel {vesselCount}: State={state}, Goal={aiState.ValueRO.CurrentGoal}, " +
                             $"TargetEntity={(targetEntity == Entity.Null ? "NULL" : targetEntity.ToString())}, " +
                             $"TargetPos={(targetPos.Equals(float3.zero) ? "ZERO" : targetPos.ToString())}, " +
                             $"Pos={transform.ValueRO.Position}, " +
                             $"Velocity={movement.ValueRO.Velocity}, " +
                             $"IsMoving={isMoving}, Speed={movement.ValueRO.CurrentSpeed}");
                }
            }

            Debug.Log($"Vessels: Total={vesselCount}, WithTargets={vesselsWithTargets}, WithPositions={vesselsWithPositions}, WaitingForPosition={vesselsWaitingForPosition}, Moving={vesselsMoving}");

            if (vesselCount == 0)
            {
                Debug.LogError("[MovementDiagnostic] NO VESSEL ENTITIES FOUND! This means:");
                Debug.LogError("  1. GameObjects aren't in a subscene (must be in subscene for DOTS baking)");
                Debug.LogError("  2. MiningVesselAuthoring components missing or not baked");
                Debug.LogError("  3. Check Entities window (Window > Entities > Hierarchy)");
            }
            else if (vesselsWithTargets == 0)
            {
                Debug.LogWarning("[MovementDiagnostic] Vessels exist but have NO TARGETS! This means:");
                Debug.LogWarning("  1. VesselAISystem isn't finding asteroids");
                Debug.LogWarning("  2. Resource registry might be empty");
                Debug.LogWarning("  3. Check [ResourceCatalogDebug] output");
            }
            else if (vesselsWaitingForPosition > 0)
            {
                Debug.LogWarning($"[MovementDiagnostic] {vesselsWaitingForPosition} vessels waiting for TargetPosition to be resolved!");
                Debug.LogWarning("  VesselTargetingSystem might not be running or failing to resolve positions");
            }

            // Check villagers
            var villagerCount = 0;
            var villagersWithTargets = 0;
            var villagersMoving = 0;

            foreach (var (transform, aiState, movement, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PureDOTS.Runtime.Components.VillagerAIState>, 
                RefRO<PureDOTS.Runtime.Components.VillagerMovement>>()
                .WithEntityAccess())
            {
                villagerCount++;
                var targetEntity = aiState.ValueRO.TargetEntity;
                var targetPos = aiState.ValueRO.TargetPosition;
                var isMoving = movement.ValueRO.IsMoving != 0;

                if (targetEntity != Entity.Null || !targetPos.Equals(float3.zero))
                {
                    villagersWithTargets++;
                }

                if (isMoving)
                {
                    villagersMoving++;
                }

                if (villagerCount <= 2)
                {
                    Debug.Log($"Villager {villagerCount}: State={aiState.ValueRO.CurrentState}, Goal={aiState.ValueRO.CurrentGoal}, " +
                             $"TargetEntity={(targetEntity == Entity.Null ? "NULL" : targetEntity.ToString())}, " +
                             $"TargetPos={(targetPos.Equals(float3.zero) ? "ZERO" : targetPos.ToString())}, " +
                             $"IsMoving={isMoving}");
                }
            }

            Debug.Log($"Villagers: Total={villagerCount}, WithTargets={villagersWithTargets}, Moving={villagersMoving}");

            if (villagerCount == 0)
            {
                Debug.LogError("[MovementDiagnostic] NO VILLAGER ENTITIES FOUND! Check VillagerAuthoring components are baked.");
            }

            // Check resource registry
            if (SystemAPI.HasSingleton<ResourceRegistry>())
            {
                var registry = SystemAPI.GetSingleton<ResourceRegistry>();
                var registryEntity = SystemAPI.GetSingletonEntity<ResourceRegistry>();
                if (EntityManager.HasBuffer<PureDOTS.Runtime.Components.ResourceRegistryEntry>(registryEntity))
                {
                    var entries = EntityManager.GetBuffer<PureDOTS.Runtime.Components.ResourceRegistryEntry>(registryEntity);
                    Debug.Log($"ResourceRegistry: Total={registry.TotalResources}, Active={registry.TotalActiveResources}, Entries={entries.Length}");
                    
                    if (entries.Length == 0)
                    {
                        Debug.LogWarning("[MovementDiagnostic] ResourceRegistry is EMPTY! Resources won't be found.");
                    }
                }
            }
            else
            {
                Debug.LogError("[MovementDiagnostic] ResourceRegistry singleton NOT FOUND!");
            }

            // Check carriers
            var carrierCount = 0;
            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Space4X.Registry.Carrier>>().WithEntityAccess())
            {
                carrierCount++;
            }
            Debug.Log($"Carriers: Total={carrierCount}");

            Debug.Log("=== END DIAGNOSTIC ===");
        }
    }
}


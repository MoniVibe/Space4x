using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Systems
{
    public struct VesselMovementDebugToggle : IComponentData {}

    /// <summary>
    /// Debug system to log detailed vessel movement information.
    /// </summary>
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial class VesselMovementDebugSystem : SystemBase
    {
        private int _frameCount;
        private const int LogInterval = 120; // Log every 2 seconds

        protected override void OnCreate()
        {
            RequireForUpdate<VesselMovement>();
            RequireForUpdate<VesselMovementDebugToggle>();
        }

        protected override void OnUpdate()
        {
            _frameCount++;
            if (_frameCount % LogInterval != 0)
                return;

            var vesselCount = 0;
            foreach (var (transform, aiState, movement, vessel, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<VesselAIState>, RefRO<VesselMovement>, RefRO<MiningVessel>>()
                .WithEntityAccess())
            {
                vesselCount++;
                
                if (vesselCount <= 2) // Log first 2 vessels in detail
                {
                    var targetEntity = aiState.ValueRO.TargetEntity;
                    var targetPos = aiState.ValueRO.TargetPosition;
                    var currentPos = transform.ValueRO.Position;
                    var distanceToTarget = targetEntity != Entity.Null && !targetPos.Equals(float3.zero) 
                        ? math.distance(currentPos, targetPos) 
                        : -1f;
                    
                    Debug.Log($"[VesselMovementDebug] Vessel {vesselCount}: " +
                             $"Pos={currentPos}, " +
                             $"State={aiState.ValueRO.CurrentState}, " +
                             $"Goal={aiState.ValueRO.CurrentGoal}, " +
                             $"TargetEntity={(targetEntity == Entity.Null ? "NULL" : targetEntity.ToString())}, " +
                             $"TargetPos={targetPos}, " +
                             $"DistanceToTarget={distanceToTarget:F2}, " +
                             $"Velocity={movement.ValueRO.Velocity}, " +
                             $"IsMoving={movement.ValueRO.IsMoving}, " +
                             $"Speed={movement.ValueRO.CurrentSpeed}, " +
                             $"Cargo={vessel.ValueRO.CurrentCargo}/{vessel.ValueRO.CargoCapacity}");
                }
            }

            if (vesselCount == 0)
            {
                Debug.LogWarning("[VesselMovementDebug] NO VESSELS FOUND! Check if MiningVesselAuthoring components are baked into entities.");
            }
            else
            {
                Debug.Log($"[VesselMovementDebug] Total vessels: {vesselCount}");
            }
        }
    }
}


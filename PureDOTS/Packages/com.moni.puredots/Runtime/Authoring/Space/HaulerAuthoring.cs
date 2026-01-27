#if UNITY_EDITOR
using PureDOTS.Runtime.Space;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class HaulerAuthoring : MonoBehaviour
    {
        [Header("Cargo & Rates")]
        [Min(1f)] public float maxCargo = 200f;
        [Min(0.1f)] public float loadRatePerSecond = 20f;
        [Min(0.1f)] public float unloadRatePerSecond = 25f;
        [Min(0.1f)] public float travelSpeedMetersPerSecond = 60f;

        [Header("Parent Carrier (optional)")]
        public GameObject parentCarrier;
    }

    public sealed class HaulerBaker : Baker<HaulerAuthoring>
    {
        public override void Bake(HaulerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new HaulingLoopConfig
            {
                MaxCargo = math.max(1f, authoring.maxCargo),
                LoadRatePerSecond = math.max(0.1f, authoring.loadRatePerSecond),
                UnloadRatePerSecond = math.max(0.1f, authoring.unloadRatePerSecond),
                TravelSpeedMetersPerSecond = math.max(0.1f, authoring.travelSpeedMetersPerSecond)
            });

            AddComponent(entity, new HaulingLoopState
            {
                Phase = HaulingLoopPhase.Idle,
                PhaseTimer = 0f,
                CurrentCargo = 0f
            });

            if (authoring.parentCarrier != null)
            {
                var parentEntity = GetEntity(authoring.parentCarrier, TransformUsageFlags.Dynamic);
                AddComponent(entity, new ParentCarrierRef { Carrier = parentEntity });
            }

            AddComponent(entity, new HaulerRole
            {
                IsDedicatedFreighter = 1
            });

            AddComponent(entity, new HaulingJob
            {
                Priority = HaulingJobPriority.Normal,
                SourceEntity = Entity.Null,
                DestinationEntity = Entity.Null,
                RequestedAmount = 0f,
                Urgency = 0f,
                ResourceValue = 0f
            });
        }
    }
}
#endif

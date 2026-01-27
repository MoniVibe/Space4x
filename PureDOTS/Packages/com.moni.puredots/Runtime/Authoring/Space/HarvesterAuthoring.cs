#if UNITY_EDITOR
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Space;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class HarvesterAuthoring : MonoBehaviour
    {
        [Header("Harvesting")]
        [Min(10f)] public float harvestRadiusMeters = 2000f;
        [Min(10f)] public float returnDistanceMeters = 2000f;
        [Min(0.1f)] public float dropoffIntervalSeconds = 30f;

        [Header("Cargo & Rates")]
        [Min(1f)] public float maxCargo = 50f;
        [Min(0.1f)] public float harvestRatePerSecond = 5f;
        [Min(0.1f)] public float dropoffRatePerSecond = 10f;
        [Min(0.1f)] public float travelSpeedMetersPerSecond = 40f;
        public bool canSelfDeliver = true;

        [Header("Drop Zone (for drop-only miners)")]
        public bool dropOnlyHarvester;
        public string resourceTypeId = "resource.ore";
        [Min(1f)] public float dropRadiusMeters = 50f;
        [Min(1f)] public float dropDecaySeconds = 120f;
        [Min(1f)] public float dropMaxStack = 200f;
        [Min(0.1f)] public float dropIntervalSeconds = 5f;

        [Header("Parent Carrier (optional)")]
        public GameObject parentCarrier;

        [Header("LOD & Rendering")]
        public bool enableLOD = true;
        [Range(100f, 1000f)] public float cullDistance = 300f;
    }

    public sealed class HarvesterBaker : Baker<HarvesterAuthoring>
    {
        public override void Bake(HarvesterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new HarvesterConfig
            {
                HarvestRadiusMeters = math.max(1f, authoring.harvestRadiusMeters),
                ReturnDistanceMeters = math.max(1f, authoring.returnDistanceMeters),
                DefaultDropoffIntervalSeconds = math.max(0.1f, authoring.dropoffIntervalSeconds)
            });

            AddComponent(entity, new MiningLoopConfig
            {
                MaxCargo = math.max(1f, authoring.maxCargo),
                HarvestRatePerSecond = math.max(0.1f, authoring.harvestRatePerSecond),
                DropoffRatePerSecond = math.max(0.1f, authoring.dropoffRatePerSecond),
                TravelSpeedMetersPerSecond = math.max(0.1f, authoring.travelSpeedMetersPerSecond),
                CanSelfDeliver = (byte)((authoring.dropOnlyHarvester ? false : authoring.canSelfDeliver) ? 1 : 0)
            });

            AddComponent(entity, new MiningLoopState
            {
                Phase = MiningLoopPhase.Idle,
                PhaseTimer = 0f,
                CurrentCargo = 0f
            });

            if (authoring.parentCarrier != null)
            {
                var parentEntity = GetEntity(authoring.parentCarrier, TransformUsageFlags.Dynamic);
                AddComponent(entity, new ParentCarrierRef { Carrier = parentEntity });
            }

            if (authoring.dropOnlyHarvester)
            {
                AddComponent<DropOnlyHarvesterTag>(entity);
                var resourceId = string.IsNullOrWhiteSpace(authoring.resourceTypeId)
                    ? new FixedString64Bytes("resource.ore")
                    : new FixedString64Bytes(authoring.resourceTypeId.Trim());

                AddComponent(entity, new ResourceDropConfig
                {
                    ResourceTypeId = resourceId,
                    DropRadiusMeters = math.max(1f, authoring.dropRadiusMeters),
                    DecaySeconds = math.max(1f, authoring.dropDecaySeconds),
                    MaxStack = math.max(1f, authoring.dropMaxStack),
                    DropIntervalSeconds = math.max(0.1f, authoring.dropIntervalSeconds),
                    TimeSinceLastDrop = 0f
                });
            }

            // Add LOD components for performance scaling
            if (authoring.enableLOD)
            {
                AddComponent(entity, new RenderLODData
                {
                    CameraDistance = 0f,
                    ImportanceScore = 0.5f,
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });

                AddComponent(entity, new RenderCullable
                {
                    CullDistance = authoring.cullDistance,
                    Priority = 128
                });

                var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
                AddComponent(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = 100,
                    ShouldRender = 1
                });
            }
        }
    }
}
#endif

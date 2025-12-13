using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Refit Repair Tuning")]
    public sealed class RefitRepairTuningAuthoring : MonoBehaviour
    {
        [Header("Refit Timing")]
        [Tooltip("Base refit overhead in seconds")]
        public float baseRefitSeconds = 60f;

        [Tooltip("Additional seconds per ton of module mass")]
        public float massSecPerTon = 1.5f;

        [Header("Size Multipliers")]
        public float sizeMultS = 1f;
        public float sizeMultM = 1.6f;
        public float sizeMultL = 2.4f;

        [Header("Location Multipliers")]
        [Tooltip("Time multiplier at station facilities")]
        public float stationTimeMult = 1f;

        [Tooltip("Time multiplier for field refits")]
        public float fieldTimeMult = 1.5f;

        [Header("Field Refit Control")]
        public bool globalFieldRefitEnabled = true;

        [Header("Repair Rates")]
        [Tooltip("Efficiency restored per second at station (0.01 = 1% per second)")]
        public float repairRateEffPerSecStation = 0.01f;

        [Tooltip("Efficiency restored per second in field (0.005 = 0.5% per second)")]
        public float repairRateEffPerSecField = 0.005f;

        [Header("Rewiring")]
        [Tooltip("Extra seconds if mount type or size changes")]
        public float rewirePenaltySeconds = 20f;

        public sealed class Baker : Unity.Entities.Baker<RefitRepairTuningAuthoring>
        {
            public override void Bake(RefitRepairTuningAuthoring authoring)
            {
                if (authoring == null)
                {
                    Debug.LogWarning("RefitRepairTuningAuthoring is null.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var tuningBlob = ref builder.ConstructRoot<RefitRepairTuning>();
                tuningBlob = new RefitRepairTuning
                {
                    BaseRefitSeconds = math.max(0f, authoring.baseRefitSeconds),
                    MassSecPerTon = math.max(0f, authoring.massSecPerTon),
                    SizeMultS = math.max(0.1f, authoring.sizeMultS),
                    SizeMultM = math.max(0.1f, authoring.sizeMultM),
                    SizeMultL = math.max(0.1f, authoring.sizeMultL),
                    StationTimeMult = math.max(0.1f, authoring.stationTimeMult),
                    FieldTimeMult = math.max(0.1f, authoring.fieldTimeMult),
                    GlobalFieldRefitEnabled = authoring.globalFieldRefitEnabled,
                    RepairRateEffPerSecStation = math.max(0f, authoring.repairRateEffPerSecStation),
                    RepairRateEffPerSecField = math.max(0f, authoring.repairRateEffPerSecField),
                    RewirePenaltySeconds = math.max(0f, authoring.rewirePenaltySeconds)
                };

                var blobAsset = builder.CreateBlobAssetReference<RefitRepairTuning>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new RefitRepairTuningSingleton { Tuning = blobAsset });
            }
        }
    }
}


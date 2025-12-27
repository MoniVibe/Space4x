using Space4X.Registry;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    public class AsteroidAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        public string AsteroidId = "Asteroid_01";

        [Header("Resources")]
        public ResourceType ResourceType = ResourceType.Minerals;
        public float ResourceAmount = 1000f;
        public float MaxResourceAmount = 1000f;
        public float MiningRate = 10f;

        [Header("Volume")]
        [Min(0.1f)] public float VolumeRadius = 20f;
        [Range(0f, 1f)] public float CoreRadiusRatio = 0.3f;
        [Range(0f, 1f)] public float MantleRadiusRatio = 0.7f;
        [Range(0, 255)] public int CrustMaterialId = 1;
        [Range(0, 255)] public int MantleMaterialId = 2;
        [Range(0, 255)] public int CoreMaterialId = 3;
        [Range(0, 255)] public int CoreDepositId = 1;
        [Range(0, 255)] public int CoreOreGrade = 200;
        [Min(0.1f)] public float OreGradeExponent = 2f;
        [Min(0)] public int VolumeSeed = 1;

        public class Baker : Baker<AsteroidAuthoring>
        {
            public override void Bake(AsteroidAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                AddComponent(entity, new Asteroid
                {
                    AsteroidId = new FixedString64Bytes(authoring.AsteroidId),
                    ResourceType = authoring.ResourceType,
                    ResourceAmount = math.max(0f, authoring.ResourceAmount),
                    MaxResourceAmount = math.max(authoring.ResourceAmount, authoring.MaxResourceAmount),
                    MiningRate = math.max(0.1f, authoring.MiningRate)
                });

                var resourceTypeId = GetResourceTypeId(authoring.ResourceType);
                if (!resourceTypeId.IsEmpty)
                {
                    AddComponent(entity, new PureDOTS.Runtime.Components.ResourceTypeId { Value = resourceTypeId });
                }

                var maxWorkers = math.clamp((int)math.ceil(authoring.MiningRate / 5f), 1, 16);
                AddComponent(entity, new PureDOTS.Runtime.Components.ResourceSourceConfig
                {
                    GatherRatePerWorker = math.max(0.1f, authoring.MiningRate),
                    MaxSimultaneousWorkers = (ushort)maxWorkers,
                    RespawnSeconds = 0f,
                    Flags = 0
                });

                AddComponent(entity, new PureDOTS.Runtime.Components.ResourceSourceState
                {
                    UnitsRemaining = math.max(0f, authoring.ResourceAmount)
                });

                AddComponent(entity, new LastRecordedTick { Tick = 0 });
                AddComponent<RewindableTag>(entity);
                AddComponent(entity, new HistoryTier
                {
                    Tier = HistoryTier.TierType.LowVisibility,
                    OverrideStrideSeconds = 0f
                });
                AddBuffer<ResourceHistorySample>(entity);
                
                AddComponent<SpatialIndexedTag>(entity);

                AddComponent(entity, new Space4XAsteroidVolumeConfig
                {
                    Radius = math.max(0.1f, authoring.VolumeRadius),
                    CoreRadiusRatio = math.clamp(authoring.CoreRadiusRatio, 0f, 1f),
                    MantleRadiusRatio = math.clamp(authoring.MantleRadiusRatio, 0f, 1f),
                    CrustMaterialId = (byte)math.clamp(authoring.CrustMaterialId, 0, 255),
                    MantleMaterialId = (byte)math.clamp(authoring.MantleMaterialId, 0, 255),
                    CoreMaterialId = (byte)math.clamp(authoring.CoreMaterialId, 0, 255),
                    CoreDepositId = (byte)math.clamp(authoring.CoreDepositId, 0, 255),
                    CoreOreGrade = (byte)math.clamp(authoring.CoreOreGrade, 0, 255),
                    OreGradeExponent = math.max(0.1f, authoring.OreGradeExponent),
                    Seed = (uint)math.max(0, authoring.VolumeSeed)
                });

                AddComponent(entity, new Space4XAsteroidCenter
                {
                    Position = authoring.transform.position
                });

            }

            private static FixedString64Bytes GetResourceTypeId(ResourceType resourceType)
            {
                switch (resourceType)
                {
                    case ResourceType.Minerals:
                        return new FixedString64Bytes("space4x.resource.minerals");
                    case ResourceType.RareMetals:
                        return new FixedString64Bytes("space4x.resource.rare_metals");
                    case ResourceType.EnergyCrystals:
                        return new FixedString64Bytes("space4x.resource.energy_crystals");
                    case ResourceType.OrganicMatter:
                        return new FixedString64Bytes("space4x.resource.organic_matter");
                    default:
                        return new FixedString64Bytes("space4x.resource.unknown");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Station Catalog")]
    public sealed class StationCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class StationSpecData
        {
            public string id;
            [Header("Facility")]
            public bool hasRefitFacility = false;
            [Min(0f)]
            public float facilityZoneRadius = 50f;
            [Header("Prefab Metadata")]
            [Tooltip("Presentation archetype (e.g., 'station', 'refit-facility')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
        }

        public List<StationSpecData> stations = new List<StationSpecData>();

        public sealed class Baker : Unity.Entities.Baker<StationCatalogAuthoring>
        {
            public override void Bake(StationCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.stations == null || authoring.stations.Count == 0)
                {
                    UnityDebug.LogWarning("StationCatalogAuthoring has no stations defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<StationCatalogBlob>();
                var stationArray = builder.Allocate(ref catalogBlob.Stations, authoring.stations.Count);

                for (int i = 0; i < authoring.stations.Count; i++)
                {
                    var stationData = authoring.stations[i];
                    stationArray[i] = new StationSpec
                    {
                        Id = new FixedString64Bytes(stationData.id ?? string.Empty),
                        HasRefitFacility = stationData.hasRefitFacility,
                        FacilityZoneRadius = math.max(0f, stationData.facilityZoneRadius),
                        PresentationArchetype = new FixedString64Bytes(stationData.presentationArchetype ?? string.Empty),
                        DefaultStyleTokens = new StyleTokens
                        {
                            Palette = stationData.defaultPalette,
                            Roughness = stationData.defaultRoughness,
                            Pattern = stationData.defaultPattern
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<StationCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StationCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


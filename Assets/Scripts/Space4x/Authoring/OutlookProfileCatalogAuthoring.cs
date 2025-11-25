using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Outlook Profile Catalog")]
    public sealed class OutlookProfileCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class OutlookProfileData
        {
            public string id;
            [Header("Behavioral Traits")]
            [Range(-1f, 1f)] public float aggression = 0f;
            [Range(-1f, 1f)] public float tradeBias = 0f;
            [Range(-1f, 1f)] public float diplomacy = 0f;
            [Header("Doctrine Weights")]
            [Range(0f, 1f)] public float doctrineMissile = 0.33f;
            [Range(0f, 1f)] public float doctrineLaser = 0.33f;
            [Range(0f, 1f)] public float doctrineHangar = 0.34f;
            [Header("Refit")]
            [Range(0.5f, 2f)] public float fieldRefitMult = 1f;
        }

        public List<OutlookProfileData> profiles = new List<OutlookProfileData>();

        public sealed class Baker : Unity.Entities.Baker<OutlookProfileCatalogAuthoring>
        {
            public override void Bake(OutlookProfileCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.profiles == null || authoring.profiles.Count == 0)
                {
                    Debug.LogWarning("OutlookProfileCatalogAuthoring has no profiles defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<OutlookProfileCatalogBlob>();
                var profileArray = builder.Allocate(ref catalogBlob.Profiles, authoring.profiles.Count);

                for (int i = 0; i < authoring.profiles.Count; i++)
                {
                    var profileData = authoring.profiles[i];
                    profileArray[i] = new OutlookProfile
                    {
                        Id = new FixedString32Bytes(profileData.id ?? string.Empty),
                        Aggression = math.clamp(profileData.aggression, -1f, 1f),
                        TradeBias = math.clamp(profileData.tradeBias, -1f, 1f),
                        Diplomacy = math.clamp(profileData.diplomacy, -1f, 1f),
                        DoctrineMissile = math.clamp(profileData.doctrineMissile, 0f, 1f),
                        DoctrineLaser = math.clamp(profileData.doctrineLaser, 0f, 1f),
                        DoctrineHangar = math.clamp(profileData.doctrineHangar, 0f, 1f),
                        FieldRefitMult = math.clamp(profileData.fieldRefitMult, 0.5f, 2f)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<OutlookProfileCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new OutlookProfileCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


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
    [AddComponentMenu("Space4X/Alignment Profile Catalog")]
    public sealed class AlignmentProfileCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AlignmentProfileData
        {
            public string id;
            [Header("Ethics & Order")]
            [Range(-1f, 1f)] public float ethics = 0f;
            [Range(-1f, 1f)] public float order = 0f;
            [Header("Policy Limits")]
            [Range(0f, 1f)] public float collateralLimit = 0.5f;
            [Range(0f, 1f)] public float piracyTolerance = 0f;
            [Header("Diplomacy")]
            [Range(-1f, 1f)] public float diplomacyBias = 0f;
        }

        public List<AlignmentProfileData> profiles = new List<AlignmentProfileData>();

        public sealed class Baker : Unity.Entities.Baker<AlignmentProfileCatalogAuthoring>
        {
            public override void Bake(AlignmentProfileCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.profiles == null || authoring.profiles.Count == 0)
                {
                    Debug.LogWarning("AlignmentProfileCatalogAuthoring has no profiles defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<AlignmentProfileCatalogBlob>();
                var profileArray = builder.Allocate(ref catalogBlob.Profiles, authoring.profiles.Count);

                for (int i = 0; i < authoring.profiles.Count; i++)
                {
                    var profileData = authoring.profiles[i];
                    profileArray[i] = new AlignmentProfile
                    {
                        Id = new FixedString32Bytes(profileData.id ?? string.Empty),
                        Ethics = math.clamp(profileData.ethics, -1f, 1f),
                        Order = math.clamp(profileData.order, -1f, 1f),
                        CollateralLimit = math.clamp(profileData.collateralLimit, 0f, 1f),
                        PiracyTolerance = math.clamp(profileData.piracyTolerance, 0f, 1f),
                        DiplomacyBias = math.clamp(profileData.diplomacyBias, -1f, 1f)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<AlignmentProfileCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AlignmentProfileCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


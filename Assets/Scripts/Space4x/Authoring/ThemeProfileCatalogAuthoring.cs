using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Theme Profile Catalog")]
    public sealed class ThemeProfileCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ThemeProfileData
        {
            public string id;
            [Header("Style Tokens")]
            [Range(0, 255)] public byte palette = 0;
            [Range(0, 255)] public byte emblem = 0;
            [Range(0, 255)] public byte pattern = 0;
        }

        public List<ThemeProfileData> profiles = new List<ThemeProfileData>();

        public sealed class Baker : Unity.Entities.Baker<ThemeProfileCatalogAuthoring>
        {
            public override void Bake(ThemeProfileCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.profiles == null || authoring.profiles.Count == 0)
                {
                    Debug.LogWarning("ThemeProfileCatalogAuthoring has no profiles defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ThemeProfileCatalogBlob>();
                var profileArray = builder.Allocate(ref catalogBlob.Profiles, authoring.profiles.Count);

                for (int i = 0; i < authoring.profiles.Count; i++)
                {
                    var profileData = authoring.profiles[i];
                    profileArray[i] = new ThemeProfile
                    {
                        Id = new FixedString32Bytes(profileData.id ?? string.Empty),
                        Palette = profileData.palette,
                        Emblem = profileData.emblem,
                        Pattern = profileData.pattern
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ThemeProfileCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ThemeProfileCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


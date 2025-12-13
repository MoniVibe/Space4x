using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Tech Catalog")]
    public sealed class TechCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class TechSpecData
        {
            public string id;
            [Range(0, 255)] public byte tier = 0;
            public List<string> unlocks = new List<string>(); // IDs of things this tech unlocks
            public List<string> requires = new List<string>(); // Prerequisite tech IDs
        }

        public List<TechSpecData> techs = new List<TechSpecData>();

        public sealed class Baker : Unity.Entities.Baker<TechCatalogAuthoring>
        {
            public override void Bake(TechCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.techs == null || authoring.techs.Count == 0)
                {
                    Debug.LogWarning("TechCatalogAuthoring has no techs defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<TechCatalogBlob>();
                var techArray = builder.Allocate(ref catalogBlob.Techs, authoring.techs.Count);

                for (int i = 0; i < authoring.techs.Count; i++)
                {
                    var techData = authoring.techs[i];
                    var unlocksCount = techData.unlocks != null ? techData.unlocks.Count : 0;
                    var requiresCount = techData.requires != null ? techData.requires.Count : 0;
                    
                    var unlocksArray = builder.Allocate(ref techArray[i].Unlocks, unlocksCount);
                    var requiresArray = builder.Allocate(ref techArray[i].Requires, requiresCount);

                    for (int j = 0; j < unlocksCount; j++)
                    {
                        unlocksArray[j] = new FixedString64Bytes(techData.unlocks[j] ?? string.Empty);
                    }

                    for (int j = 0; j < requiresCount; j++)
                    {
                        requiresArray[j] = new FixedString64Bytes(techData.requires[j] ?? string.Empty);
                    }

                    techArray[i] = new TechSpec
                    {
                        Id = new FixedString64Bytes(techData.id ?? string.Empty),
                        Tier = techData.tier
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<TechCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TechCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


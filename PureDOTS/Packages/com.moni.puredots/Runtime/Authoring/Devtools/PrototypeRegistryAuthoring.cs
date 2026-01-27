using PureDOTS.Runtime.Devtools;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Devtools
{
    /// <summary>
    /// Authoring component that bakes prototype registry blob from prefab references.
    /// </summary>
    public class PrototypeRegistryAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class PrototypeEntry
        {
            public string Name;
            public GameObject Prefab;
            public PrototypeStatsDefault StatsDefault;
            public Alignment AlignmentDefault;
            public Outlook OutlookDefault;
        }

        [SerializeField] PrototypeEntry[] prototypes = new PrototypeEntry[0];

        class Baker : Baker<PrototypeRegistryAuthoring>
        {
            public override void Bake(PrototypeRegistryAuthoring authoring)
            {
                if (authoring.prototypes == null || authoring.prototypes.Length == 0)
                {
                    return;
                }

                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BlobArray<PureDOTS.Runtime.Devtools.PrototypeEntry>>();
                var entries = builder.Allocate(ref root, authoring.prototypes.Length);

                for (int i = 0; i < authoring.prototypes.Length; i++)
                {
                    var proto = authoring.prototypes[i];
                    if (proto.Prefab == null)
                    {
                        continue;
                    }

                    int prototypeId = PrototypeId.FromString(proto.Name).Value;
                    Entity prefabEntity = GetEntity(proto.Prefab, TransformUsageFlags.Dynamic);

                    entries[i] = new PureDOTS.Runtime.Devtools.PrototypeEntry
                    {
                        PrototypeId = prototypeId,
                        PrefabEntity = prefabEntity,
                        Name = new FixedString128Bytes(proto.Name),
                        StatsDefault = proto.StatsDefault,
                        AlignmentDefault = proto.AlignmentDefault,
                        OutlookDefault = proto.OutlookDefault
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<BlobArray<PureDOTS.Runtime.Devtools.PrototypeEntry>>(Allocator.Persistent);
                builder.Dispose();

                // Create singleton entity with registry blob
                var entity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(entity, new PrototypeRegistryBlob { Entries = blobAsset });
            }
        }
    }
}



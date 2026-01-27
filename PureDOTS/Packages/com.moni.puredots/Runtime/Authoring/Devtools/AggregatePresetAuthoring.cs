#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace PureDOTS.Authoring.Devtools
{
    /// <summary>
    /// ScriptableObject for authoring aggregate presets.
    /// </summary>
    [CreateAssetMenu(fileName = "AggregatePreset", menuName = "PureDOTS/Devtools/Aggregate Preset")]
    public class AggregatePresetAsset : ScriptableObject
    {
        [System.Serializable]
        public class MemberEntry
        {
            public string PrototypeName;
            public int MinCount = 1;
            public int MaxCount = 1;
            public PrototypeStatsDefault StatsOverrides;
            public Alignment AlignmentOverride;
            public Outlook OutlookOverride;
        }

        public string PresetName;
        public FormationType FormationType;
        public float FormationSpacing = 2f;
        public MemberEntry[] Members = new MemberEntry[0];
    }

    /// <summary>
    /// Authoring component that bakes aggregate preset blob from ScriptableObject.
    /// </summary>
    public class AggregatePresetAuthoring : MonoBehaviour
    {
        [SerializeField] AggregatePresetAsset presetAsset;

        class Baker : Baker<AggregatePresetAuthoring>
        {
            public override void Bake(AggregatePresetAuthoring authoring)
            {
                if (authoring.presetAsset == null || authoring.presetAsset.Members == null || authoring.presetAsset.Members.Length == 0)
                {
                    return;
                }

                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<AggregatePresetBlob>();
                root.Name = new FixedString128Bytes(authoring.presetAsset.PresetName);
                root.FormationType = authoring.presetAsset.FormationType;
                root.FormationSpacing = authoring.presetAsset.FormationSpacing;

                var members = builder.Allocate(ref root.Members, authoring.presetAsset.Members.Length);
                for (int i = 0; i < authoring.presetAsset.Members.Length; i++)
                {
                    var member = authoring.presetAsset.Members[i];
                    members[i] = new AggregateMemberEntry
                    {
                        PrototypeId = PrototypeId.FromString(member.PrototypeName).Value,
                        MinCount = member.MinCount,
                        MaxCount = member.MaxCount,
                        StatsOverrides = member.StatsOverrides,
                        AlignmentOverride = member.AlignmentOverride,
                        OutlookOverride = member.OutlookOverride
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<AggregatePresetBlob>(Allocator.Persistent);
                builder.Dispose();

                // Store blob reference (would need a registry singleton or component to store all presets)
                // For now, create a component on the entity
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PureDOTS.Runtime.Devtools.AggregatePresetBlobReference { Blob = blobAsset });
            }
        }
    }
}
#endif


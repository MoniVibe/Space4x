using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class StreamingSectionAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Human-readable identifier used for debugging and analytics. Defaults to the GameObject name.")]
        private string sectionId = string.Empty;

        [Tooltip("Optional SubScene reference that will be loaded/unloaded for this section.")]
        public SubScene subScene;

        [Tooltip("World-space radius where this section should be considered for loading.")]
        public float enterRadius = 128f;

        [Tooltip("World-space radius where the section may safely unload (must be >= enter radius).")]
        public float exitRadius = 150f;

        [Tooltip("Section flags (Manual sections are ignored by automatic streaming).")]
        public StreamingSectionFlags flags = StreamingSectionFlags.None;

        [Tooltip("Sections with higher priority are loaded before lower priority ones when competing.")]
        public int priority;

        [Tooltip("Optional estimated cost (seconds or MB) for prioritisation heuristics.")]
        public float estimatedCost;

        [Tooltip("Use the authoring transform as the section center. Disable to specify a manual center.")]
        public bool useTransformCenter = true;

        [Tooltip("Manual center position used when 'Use Transform Center' is disabled.")]
        public Vector3 manualCenter = Vector3.zero;

        public string SectionId => sectionId;

        private sealed class StreamingSectionBaker : Baker<StreamingSectionAuthoring>
        {
            public override void Bake(StreamingSectionAuthoring authoring)
            {
                var usage = authoring.useTransformCenter ? TransformUsageFlags.Dynamic : TransformUsageFlags.None;
                var entity = GetEntity(usage);

                var descriptor = new StreamingSectionDescriptor
                {
                    Identifier = CreateIdentifier(authoring),
                    SceneGuid = ResolveSceneGuid(authoring),
                    Center = authoring.useTransformCenter
                        ? authoring.transform.position
                        : authoring.manualCenter,
                    EnterRadius = math.max(0f, authoring.enterRadius),
                    ExitRadius = math.max(0f, authoring.exitRadius),
                    Flags = authoring.flags,
                    Priority = authoring.priority,
                    EstimatedCost = math.max(0f, authoring.estimatedCost)
                };

                if (descriptor.ExitRadius < descriptor.EnterRadius)
                {
                    descriptor.ExitRadius = descriptor.EnterRadius + 10f;
                }

                AddComponent(entity, descriptor);
                AddComponent(entity, new StreamingSectionState
                {
                    Status = StreamingSectionStatus.Unloaded,
                    LastSeenTick = 0,
                    CooldownUntilTick = 0,
                    PinCount = 0
                });
                AddComponent(entity, new StreamingSectionRuntime { SceneEntity = Entity.Null });
            }

            private static FixedString64Bytes CreateIdentifier(StreamingSectionAuthoring authoring)
            {
                var name = authoring.sectionId;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = authoring.gameObject.name;
                }

                var id = new FixedString64Bytes();
                id.Append(name.Trim());
                return id;
            }

            private static Unity.Entities.Hash128 ResolveSceneGuid(StreamingSectionAuthoring authoring)
            {
                Unity.Entities.Hash128 guid = default;
#if UNITY_EDITOR
                if (authoring.subScene != null && authoring.subScene.SceneAsset != null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(authoring.subScene.SceneAsset);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var guidString = AssetDatabase.AssetPathToGUID(assetPath);
                        if (!string.IsNullOrEmpty(guidString))
                        {
                            guid = new Unity.Entities.Hash128(guidString);
                        }
                    }
                }
#endif
                return guid;
            }
        }
    }
}

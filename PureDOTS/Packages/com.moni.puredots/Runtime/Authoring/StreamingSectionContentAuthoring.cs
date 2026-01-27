using System.Collections.Generic;
using PureDOTS.Runtime.Streaming;
using Unity.Entities;
#if ENABLE_ENTITIES_CONTENT
using Unity.Entities.Content;
#endif
using Unity.Scenes;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Optional authoring companion for <see cref="StreamingSectionAuthoring"/> that preloads prefabs or weak GameObject assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamingSectionContentAuthoring : MonoBehaviour
    {
#if ENABLE_ENTITIES_CONTENT
        [Tooltip("Entity prefabs converted via EntityPrefabReference and kept warm while the section is active.")]
        public List<GameObject> entityPrefabs = new();
#endif

#if ENABLE_ENTITIES_CONTENT
        [Tooltip("WeakObjectReference assets (GameObjects) loaded while the section is active; release when the section unloads.")]
        public List<WeakObjectReference<GameObject>> weakGameObjectAssets = new();
#endif

        private sealed class StreamingSectionContentBaker : Baker<StreamingSectionContentAuthoring>
        {
            public override void Bake(StreamingSectionContentAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);

#if ENABLE_ENTITIES_CONTENT
                if (authoring.entityPrefabs.Count > 0)
                {
                    var prefabBuffer = AddBuffer<StreamingSectionPrefabReference>(entity);
                    foreach (var prefab in authoring.entityPrefabs)
                    {
                        if (prefab == null)
                        {
                            continue;
                        }

                        var prefabReference = new EntityPrefabReference(prefab);
                        prefabBuffer.Add(new StreamingSectionPrefabReference
                        {
                            Prefab = prefabReference,
                            PrefabSceneEntity = Entity.Null
                        });
                    }
                }
#endif

#if ENABLE_ENTITIES_CONTENT
                if (authoring.weakGameObjectAssets.Count > 0)
                {
                    var weakBuffer = AddBuffer<StreamingSectionWeakGameObjectReference>(entity);
                    foreach (var weakReference in authoring.weakGameObjectAssets)
                    {
                        if (!weakReference.IsReferenceValid)
                        {
                            continue;
                        }

                        weakBuffer.Add(new StreamingSectionWeakGameObjectReference
                        {
                            Reference = weakReference
                        });
                    }
                }
#endif
            }
        }
    }
}


#if UNITY_EDITOR
using System;
using PureDOTS.Runtime.Knowledge;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Lightweight hook that turns a shared KnowledgeLessonEffectAuthoring prefab into a runtime catalog singleton.
    /// Keeps bootstrap scenes clean by referencing the prefab instead of duplicating lesson data per scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnowledgeLessonCatalogReferenceAuthoring : MonoBehaviour
    {
        [Tooltip("Prefab or GameObject that contains the canonical KnowledgeLessonEffectAuthoring component.")]
        public KnowledgeLessonEffectAuthoring catalogPrefab;

        [Tooltip("Optional inline override when you want to author lessons on this GameObject.")]
        public KnowledgeLessonEffectAuthoring inlineOverride;

        class Baker : Baker<KnowledgeLessonCatalogReferenceAuthoring>
        {
            public override void Bake(KnowledgeLessonCatalogReferenceAuthoring authoring)
            {
                var source = ResolveSource(authoring);
                if (source == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("KnowledgeLessonCatalogReferenceAuthoring could not locate a KnowledgeLessonEffectAuthoring source.", authoring);
#endif
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.None);
                var blobRef = KnowledgeLessonCatalogBuilder.BuildCatalog(source);
                var catalog = new KnowledgeLessonEffectCatalog { Blob = blobRef };

                try
                {
                    AddComponent(entity, catalog);
                }
                catch (InvalidOperationException ex) when (ex.Message.IndexOf("duplicate component", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetComponent(entity, catalog);
                }
            }

            private static KnowledgeLessonEffectAuthoring ResolveSource(KnowledgeLessonCatalogReferenceAuthoring authoring)
            {
                if (authoring.catalogPrefab != null)
                {
                    return authoring.catalogPrefab;
                }

                if (authoring.inlineOverride != null)
                {
                    return authoring.inlineOverride;
                }

                return authoring.GetComponent<KnowledgeLessonEffectAuthoring>();
            }
        }
    }
}
#endif

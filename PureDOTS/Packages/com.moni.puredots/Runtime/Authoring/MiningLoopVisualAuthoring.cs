using PureDOTS.Runtime.Visuals;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class MiningLoopVisualAuthoring : MonoBehaviour
    {
        [Header("Mining Visual Settings")]
        public MiningVisualType visualType = MiningVisualType.Villager;
        [Min(0f)] public float baseScale = 1f;
        [Tooltip("Prefab used for the spawned visual entity.")]
        public GameObject visualPrefab;
        [Tooltip("Optional FX prefab baked alongside the visual.")]
        public GameObject fxPrefab;

        public class Baker : Unity.Entities.Baker<MiningLoopVisualAuthoring>
        {
            public override void Bake(MiningLoopVisualAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);

                var prefabSource = authoring.visualPrefab != null ? authoring.visualPrefab : authoring.gameObject;
                var prefabEntity = GetEntity(prefabSource, TransformUsageFlags.Renderable);
                AddComponent<Prefab>(prefabEntity);

                var prefab = new MiningVisualPrefab
                {
                    VisualType = authoring.visualType,
                    BaseScale = Mathf.Max(0f, authoring.baseScale),
                    Prefab = prefabEntity,
                    FxPrefab = Entity.Null
                };

                if (authoring.fxPrefab != null)
                {
                    var fxEntity = GetEntity(authoring.fxPrefab, TransformUsageFlags.Dynamic);
                    AddComponent<Prefab>(fxEntity);

                    prefab.FxPrefab = fxEntity;
                }

                AddComponent(entity, prefab);
            }
        }
    }
}


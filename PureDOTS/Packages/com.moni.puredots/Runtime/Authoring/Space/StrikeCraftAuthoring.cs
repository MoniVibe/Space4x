#if UNITY_EDITOR
using PureDOTS.Runtime.Space;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class StrikeCraftAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        public string craftId = "strikecraft.default";

        [Header("Behaviour")]
        [Min(0f)] public float defaultPatrolRadiusKm = 25f;
        [Min(0f)] public float undockWarmupSeconds = 5f;
    }

    public sealed class StrikeCraftBaker : Baker<StrikeCraftAuthoring>
    {
        public override void Bake(StrikeCraftAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var craftId = new FixedString64Bytes(string.IsNullOrWhiteSpace(authoring.craftId) ? "strikecraft.default" : authoring.craftId.Trim());
            AddComponent(entity, new StrikeCraftConfig
            {
                CraftId = craftId,
                DefaultPatrolRadiusKm = math.max(0f, authoring.defaultPatrolRadiusKm),
                UndockWarmupSeconds = math.max(0f, authoring.undockWarmupSeconds)
            });
        }
    }
}
#endif

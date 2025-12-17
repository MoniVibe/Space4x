using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Authoring component that identifies an effect/FX by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Effect ID")]
    public sealed class EffectIdAuthoring : MonoBehaviour
    {
        [Tooltip("Effect ID from the catalog")]
        public string effectId = string.Empty;

        private void OnValidate()
        {
            effectId = string.IsNullOrWhiteSpace(effectId) ? string.Empty : effectId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<EffectIdAuthoring>
        {
            public override void Bake(EffectIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.effectId))
                {
                    UnityDebug.LogWarning($"EffectIdAuthoring on '{authoring.name}' has no effectId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.EffectId
                {
                    Id = new FixedString64Bytes(authoring.effectId)
                });
            }
        }
    }
}


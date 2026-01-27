using PureDOTS.Runtime.Agency;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component to assign an AgencySelf preset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgencySelfPresetAuthoring : MonoBehaviour
    {
        public AgencySelfPresetKind presetKind = AgencySelfPresetKind.Sentient;
        [Range(0f, 1f)] public float baseResistance = 0.65f;
        [Range(0f, 1f)] public float selfNeedUrgency = 0.45f;
        [Range(0f, 1f)] public float dominationAffinity = 0.1f;

        private sealed class Baker : Baker<AgencySelfPresetAuthoring>
        {
            public override void Bake(AgencySelfPresetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AgencySelfPreset
                {
                    Kind = authoring.presetKind,
                    Self = new AgencySelf
                    {
                        BaseResistance = authoring.baseResistance,
                        SelfNeedUrgency = authoring.selfNeedUrgency,
                        DominationAffinity = authoring.dominationAffinity
                    }
                });
            }
        }
    }
}

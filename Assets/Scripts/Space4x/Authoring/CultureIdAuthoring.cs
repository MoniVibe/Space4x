using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that identifies an individual's culture profile.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Culture ID")]
    public sealed class CultureIdAuthoring : MonoBehaviour
    {
        [Tooltip("Culture ID (references culture catalog)")]
        public ushort cultureId = 0;

        public sealed class Baker : Unity.Entities.Baker<CultureIdAuthoring>
        {
            public override void Bake(CultureIdAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new CultureId { Value = authoring.cultureId });
            }
        }
    }
}


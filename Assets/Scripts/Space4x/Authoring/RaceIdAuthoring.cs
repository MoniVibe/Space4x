using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that identifies an individual's race.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Race ID")]
    public sealed class RaceIdAuthoring : MonoBehaviour
    {
        [Tooltip("Race ID (references race catalog)")]
        public ushort raceId = 0;

        public sealed class Baker : Unity.Entities.Baker<RaceIdAuthoring>
        {
            public override void Bake(RaceIdAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RaceId { Value = authoring.raceId });
            }
        }
    }
}


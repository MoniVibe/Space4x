using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that specifies hangar capacity for capital ships and carriers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Hangar Capacity")]
    public sealed class HangarCapacityAuthoring : MonoBehaviour
    {
        [Tooltip("Total hangar capacity (number of craft that can be docked)")]
        [Min(0f)]
        public float capacity = 0f;

        private void OnValidate()
        {
            capacity = Mathf.Max(0f, capacity);
        }

        public sealed class Baker : Unity.Entities.Baker<HangarCapacityAuthoring>
        {
            public override void Bake(HangarCapacityAuthoring authoring)
            {
                if (authoring.capacity <= 0f)
                {
                    return; // Don't add component if capacity is zero
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.HangarCapacity
                {
                    Capacity = authoring.capacity
                });
            }
        }
    }
}


using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that specifies module function and attributes.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Function")]
    public sealed class ModuleFunctionAuthoring : MonoBehaviour
    {
        [Tooltip("Primary function of this module")]
        public ModuleFunction function = ModuleFunction.None;

        [Tooltip("Function-specific capacity (e.g., hangar capacity, cargo capacity)")]
        [Min(0f)]
        public float capacity = 0f;

        [Tooltip("Human-readable function description")]
        [TextArea(2, 4)]
        public string description = string.Empty;

        private void OnValidate()
        {
            capacity = Mathf.Max(0f, capacity);
        }

        public sealed class Baker : Unity.Entities.Baker<ModuleFunctionAuthoring>
        {
            public override void Bake(ModuleFunctionAuthoring authoring)
            {
                if (authoring.function == ModuleFunction.None)
                {
                    return; // Don't add component if no function
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleFunctionData
                {
                    Function = authoring.function,
                    Capacity = authoring.capacity,
                    Description = new FixedString64Bytes(authoring.description ?? string.Empty)
                });
            }
        }
    }
}


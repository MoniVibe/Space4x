using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for module rarity (affects availability, black-market value, diplomatic leverage).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Rarity")]
    public sealed class ModuleRarityAuthoring : MonoBehaviour
    {
        [Tooltip("Rarity level (affects availability, black-market value, diplomatic leverage)")]
        public ModuleRarity rarity = ModuleRarity.Common;

        public sealed class Baker : Unity.Entities.Baker<ModuleRarityAuthoring>
        {
            public override void Bake(ModuleRarityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleRarityComponent { Value = authoring.rarity });
            }
        }
    }
}


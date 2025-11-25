using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Optional authoring component for visual style tokens (palette, roughness, pattern).
    /// These are used for presentation binding but don't affect gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Style Tokens")]
    public sealed class StyleTokensAuthoring : MonoBehaviour
    {
        [Tooltip("Palette index (0-255)")]
        [Range(0, 255)]
        public byte palette = 0;

        [Tooltip("Roughness index (0-255)")]
        [Range(0, 255)]
        public byte roughness = 128;

        [Tooltip("Pattern index (0-255)")]
        [Range(0, 255)]
        public byte pattern = 0;

        public sealed class Baker : Unity.Entities.Baker<StyleTokensAuthoring>
        {
            public override void Bake(StyleTokensAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.StyleTokens
                {
                    Palette = authoring.palette,
                    Roughness = authoring.roughness,
                    Pattern = authoring.pattern
                });
            }
        }
    }
}


using Space4X.Presentation;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Asteroid Chunk Render Config")]
    public sealed class Space4XAsteroidChunkRenderConfigAuthoring : MonoBehaviour
    {
        public Material ChunkMaterial;
        public Texture2D MaterialPalette;

        private sealed class Baker : Baker<Space4XAsteroidChunkRenderConfigAuthoring>
        {
            public override void Bake(Space4XAsteroidChunkRenderConfigAuthoring authoring)
            {
                if (authoring == null)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new Space4XAsteroidChunkRenderConfig
                {
                    Material = authoring.ChunkMaterial
                });

                if (authoring.MaterialPalette != null)
                {
                    AddComponentObject(entity, new Space4XAsteroidChunkPaletteConfig
                    {
                        Palette = authoring.MaterialPalette
                    });
                }
            }
        }
    }
}

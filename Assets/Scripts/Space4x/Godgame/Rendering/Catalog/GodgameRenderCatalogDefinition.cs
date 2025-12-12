using System;
using UnityEngine;

namespace Godgame.Rendering.Catalog
{
    [CreateAssetMenu(
        fileName = "GodgameRenderCatalog",
        menuName = "Godgame/Rendering/RenderCatalog")]
    public class GodgameRenderCatalogDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ushort Key;
            public Mesh Mesh;
            public Material Material;
            public Vector3 BoundsCenter;
            public Vector3 BoundsExtents;
            public ushort SubMesh;
        }

        public Entry[] Entries;
    }
}

using System;
using UnityEngine;

namespace Space4X.Rendering.Catalog
{
    [CreateAssetMenu(fileName = "Space4XRenderCatalog", menuName = "Space4X/Rendering/RenderCatalog")]
    public class Space4XRenderCatalogDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int ArchetypeId;
            public Mesh Mesh;
            public Material Material;
            public ushort SubMesh;
            public Vector3 BoundsCenter;
            public Vector3 BoundsExtents;
        }

        public Entry[] Entries;
    }
}






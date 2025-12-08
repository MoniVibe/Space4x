using PureDOTS.Runtime.Platform.Blobs;
using UnityEngine;

namespace Space4X.Platform.Authoring
{
    /// <summary>
    /// Catalog asset holding references to all hull and module definitions.
    /// Used by PlatformRegistryBootstrap to build blob registries.
    /// </summary>
    [CreateAssetMenu(fileName = "PlatformRegistryCatalog", menuName = "Space4X/Platform/Registry Catalog")]
    public sealed class PlatformRegistryCatalog : ScriptableObject
    {
        [SerializeField]
        private HullAuthoring[] hulls = System.Array.Empty<HullAuthoring>();

        [SerializeField]
        private ModuleAuthoring[] modules = System.Array.Empty<ModuleAuthoring>();

        public HullAuthoring[] Hulls => hulls;
        public ModuleAuthoring[] Modules => modules;
    }
}






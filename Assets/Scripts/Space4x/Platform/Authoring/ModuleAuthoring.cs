using PureDOTS.Runtime.Platform.Blobs;
using UnityEngine;

namespace Space4X.Platform.Authoring
{
    /// <summary>
    /// ScriptableObject for module definitions.
    /// Contributes to ModuleDefRegistry blob.
    /// </summary>
    [CreateAssetMenu(fileName = "Module", menuName = "Space4X/Platform/Module")]
    public sealed class ModuleAuthoring : ScriptableObject
    {
        [SerializeField]
        private int moduleId;

        [SerializeField]
        private ModuleCategory category;

        [SerializeField]
        private float mass;

        [SerializeField]
        private float powerDraw;

        [SerializeField]
        private float volume;

        [SerializeField]
        private bool allowedInternal = true;

        [SerializeField]
        private bool allowedExternal = false;

        [SerializeField]
        private bool allowedMassMode = true;

        [SerializeField]
        private bool allowedHardpointMode = true;

        [SerializeField]
        private bool allowedVoxelMode = true;

        [SerializeField]
        private float[] capabilityPayload = System.Array.Empty<float>();

        public int ModuleId => moduleId;
        public ModuleCategory Category => category;
        public float Mass => mass;
        public float PowerDraw => powerDraw;
        public float Volume => volume;
        public bool AllowedInternal => allowedInternal;
        public bool AllowedExternal => allowedExternal;
        public bool AllowedMassMode => allowedMassMode;
        public bool AllowedHardpointMode => allowedHardpointMode;
        public bool AllowedVoxelMode => allowedVoxelMode;
        public float[] CapabilityPayload => capabilityPayload;
    }
}


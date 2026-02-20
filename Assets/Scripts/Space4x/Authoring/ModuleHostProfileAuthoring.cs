using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    /// <summary>
    /// Optional explicit module host profile authoring.
    /// Use this to override inferred host behavior (for example station mount-agnostic hosts),
    /// and to reserve future host kinds like Titan without runtime specialization yet.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Host Profile")]
    public sealed class ModuleHostProfileAuthoring : MonoBehaviour
    {
        [Tooltip("Host kind this entity should use for module compatibility rules.")]
        public Registry.Space4XModuleHostKind hostKind = Registry.Space4XModuleHostKind.Ship;

        [Tooltip("Host profile id. For Ship/Titan this is normally a hull id. For Station this can be station id (mount-agnostic) or hull id (hull-driven).")]
        public string hostId = string.Empty;

        [Header("Validation Flags")]
        [Tooltip("If true, resolve mount types from hull slots.")]
        public bool usesHullSlots = true;

        [Tooltip("If true, module mount type must match resolved socket mount.")]
        public bool validateMountType = true;

        [Tooltip("If true, segment assembly validation is enabled for this host.")]
        public bool validateSegments = true;

        private void OnValidate()
        {
            hostId = string.IsNullOrWhiteSpace(hostId) ? string.Empty : hostId.Trim();

            if (hostKind == Registry.Space4XModuleHostKind.Ship || hostKind == Registry.Space4XModuleHostKind.Titan)
            {
                usesHullSlots = true;
                validateMountType = true;
                validateSegments = true;
            }
            else if (hostKind == Registry.Space4XModuleHostKind.Station && !usesHullSlots)
            {
                validateMountType = false;
                validateSegments = false;
            }
        }

        public sealed class Baker : Unity.Entities.Baker<ModuleHostProfileAuthoring>
        {
            public override void Bake(ModuleHostProfileAuthoring authoring)
            {
                if (authoring.hostKind == Registry.Space4XModuleHostKind.None)
                {
                    UnityDebug.LogWarning($"ModuleHostProfileAuthoring on '{authoring.name}' has HostKind=None and will be ignored.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(authoring.hostId))
                {
                    UnityDebug.LogWarning($"ModuleHostProfileAuthoring on '{authoring.name}' has no hostId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.Space4XModuleHostProfile
                {
                    Kind = authoring.hostKind,
                    HostId = new FixedString64Bytes(authoring.hostId),
                    UsesHullSlots = authoring.usesHullSlots ? (byte)1 : (byte)0,
                    ValidateMountType = authoring.validateMountType ? (byte)1 : (byte)0,
                    ValidateSegments = authoring.validateSegments ? (byte)1 : (byte)0
                });
            }
        }
    }
}

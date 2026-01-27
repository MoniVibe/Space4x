using PureDOTS.Runtime.Agency;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for hostile control overrides (hijacks).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HostileControlOverrideAuthoring : MonoBehaviour
    {
        public GameObject controller;
        public AgencyDomain domains = AgencyDomain.Movement | AgencyDomain.Combat;
        [Range(0f, 3f)] public float pressure = 1.4f;
        [Range(0f, 1f)] public float legitimacy = 0.1f;
        [Range(0f, 1f)] public float hostility = 0.9f;
        [Range(0f, 1f)] public float consent = 0.05f;
        [Min(0)] public int durationTicks = 0;
        public string reason = "override";
        public bool active = false;

        private sealed class Baker : Baker<HostileControlOverrideAuthoring>
        {
            public override void Bake(HostileControlOverrideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var controllerEntity = authoring.controller != null
                    ? GetEntity(authoring.controller, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(entity, new HostileControlOverride
                {
                    Controller = controllerEntity,
                    Domains = authoring.domains,
                    Pressure = math.max(0f, authoring.pressure),
                    Legitimacy = math.saturate(authoring.legitimacy),
                    Hostility = math.saturate(authoring.hostility),
                    Consent = math.saturate(authoring.consent),
                    DurationTicks = (uint)math.max(0, authoring.durationTicks),
                    EstablishedTick = 0u,
                    ExpireTick = 0u,
                    Active = (byte)(authoring.active ? 1 : 0),
                    LastReportedActive = 0,
                    Reason = authoring.reason != null
                        ? new Unity.Collections.FixedString64Bytes(authoring.reason)
                        : default
                });
            }
        }
    }
}

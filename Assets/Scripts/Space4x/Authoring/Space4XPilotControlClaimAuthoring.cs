using PureDOTS.Runtime.Agency;
using Space4X.Runtime;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Pilot Control Claim Config")]
    public sealed class Space4XPilotControlClaimAuthoring : MonoBehaviour
    {
        public AgencyDomain domains = AgencyDomain.Movement | AgencyDomain.Combat | AgencyDomain.Sensors | AgencyDomain.Communications;
        [Range(0f, 3f)] public float pressure = 1.2f;
        [Range(0f, 1f)] public float legitimacy = 0.8f;
        [Range(0f, 1f)] public float hostility = 0.05f;
        [Range(0f, 1f)] public float consent = 0.6f;

        private sealed class Baker : Baker<Space4XPilotControlClaimAuthoring>
        {
            public override void Bake(Space4XPilotControlClaimAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PilotControlClaimConfig
                {
                    Domains = authoring.domains,
                    Pressure = Mathf.Max(0f, authoring.pressure),
                    Legitimacy = Mathf.Clamp01(authoring.legitimacy),
                    Hostility = Mathf.Clamp01(authoring.hostility),
                    Consent = Mathf.Clamp01(authoring.consent)
                });
            }
        }
    }
}

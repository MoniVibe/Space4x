using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Authoring component that identifies a station by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Station ID")]
    public sealed class StationIdAuthoring : MonoBehaviour
    {
        [Tooltip("Station ID from the catalog")]
        public string stationId = string.Empty;

        [Header("Facility")]
        [Tooltip("If true, this station has refit facility capabilities")]
        public bool isRefitFacility = false;

        [Tooltip("Facility zone radius in meters (if isRefitFacility is true)")]
        [Min(0f)]
        public float facilityZoneRadius = 50f;

        [Header("Service Profile Override")]
        [Tooltip("If true, this station instance overrides catalog/inferred service profile.")]
        public bool overrideServiceProfile = false;
        public Registry.Space4XStationSpecialization specialization = Registry.Space4XStationSpecialization.General;
        [EnumFlags]
        public Registry.Space4XStationServiceFlags services = Registry.Space4XStationServiceFlags.None;
        [Range(1, 8)]
        public int tier = 1;
        [Min(0.1f)]
        public float serviceScale = 1f;

        [Header("Access Policy Override")]
        [Tooltip("If true, this station instance overrides catalog/inferred access policy.")]
        public bool overrideAccessPolicy = false;
        [Range(0f, 1f)] public float minStandingForApproach = 0.1f;
        [Range(0f, 1f)] public float minStandingForDock = 0.15f;
        [Min(0f)] public float warningRadiusMeters = 120f;
        [Min(0f)] public float noFlyRadiusMeters = 70f;
        public bool enforceNoFlyZone = true;
        public bool denyDockingWithoutStanding = true;

        private void OnValidate()
        {
            stationId = string.IsNullOrWhiteSpace(stationId) ? string.Empty : stationId.Trim();
            facilityZoneRadius = Mathf.Max(0f, facilityZoneRadius);
            tier = Mathf.Clamp(tier, 1, 8);
            serviceScale = Mathf.Max(0.1f, serviceScale);
            minStandingForApproach = Mathf.Clamp01(minStandingForApproach);
            minStandingForDock = Mathf.Clamp01(minStandingForDock);
            warningRadiusMeters = Mathf.Max(0f, warningRadiusMeters);
            noFlyRadiusMeters = Mathf.Max(0f, noFlyRadiusMeters);
        }

        public sealed class Baker : Unity.Entities.Baker<StationIdAuthoring>
        {
            public override void Bake(StationIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.stationId))
                {
                    UnityDebug.LogWarning($"StationIdAuthoring on '{authoring.name}' has no stationId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.StationId
                {
                    Id = new FixedString64Bytes(authoring.stationId)
                });

                if (authoring.isRefitFacility)
                {
                    AddComponent<Registry.RefitFacilityTag>(entity);
                    AddComponent(entity, new Registry.FacilityZone
                    {
                        RadiusMeters = authoring.facilityZoneRadius
                    });
                }

                if (authoring.overrideServiceProfile)
                {
                    AddComponent(entity, new Registry.Space4XStationServiceProfileOverride
                    {
                        Enabled = 1,
                        Specialization = authoring.specialization,
                        Services = authoring.services,
                        Tier = (byte)Mathf.Clamp(authoring.tier, 1, 8),
                        ServiceScale = Mathf.Max(0.1f, authoring.serviceScale)
                    });
                }

                if (authoring.overrideAccessPolicy)
                {
                    AddComponent(entity, new Registry.Space4XStationAccessPolicyOverride
                    {
                        Enabled = 1,
                        MinStandingForApproach = Mathf.Clamp01(authoring.minStandingForApproach),
                        MinStandingForDock = Mathf.Clamp01(authoring.minStandingForDock),
                        WarningRadiusMeters = Mathf.Max(0f, authoring.warningRadiusMeters),
                        NoFlyRadiusMeters = Mathf.Max(0f, authoring.noFlyRadiusMeters),
                        EnforceNoFlyZone = authoring.enforceNoFlyZone ? (byte)1 : (byte)0,
                        DenyDockingWithoutStanding = authoring.denyDockingWithoutStanding ? (byte)1 : (byte)0
                    });
                }
            }
        }
    }
}

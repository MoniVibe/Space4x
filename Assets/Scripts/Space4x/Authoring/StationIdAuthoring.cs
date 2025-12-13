using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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

        private void OnValidate()
        {
            stationId = string.IsNullOrWhiteSpace(stationId) ? string.Empty : stationId.Trim();
            facilityZoneRadius = Mathf.Max(0f, facilityZoneRadius);
        }

        public sealed class Baker : Unity.Entities.Baker<StationIdAuthoring>
        {
            public override void Bake(StationIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.stationId))
                {
                    Debug.LogWarning($"StationIdAuthoring on '{authoring.name}' has no stationId set.");
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
            }
        }
    }
}


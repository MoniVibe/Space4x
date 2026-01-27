#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [DisallowMultipleComponent]
    public sealed class ClimateHazardAuthoring : MonoBehaviour
    {
        [SerializeField]
        private ClimateHazardProfileAsset profile;

        [SerializeField]
        private bool applyProfileOnValidate = true;

        [Header("Identity")]
        [SerializeField] private string hazardName = "Storm";
        [SerializeField] private ClimateHazardType hazardType = ClimateHazardType.Storm;

        [Header("Behaviour")]
        [SerializeField, Range(0f, 1f)] private float currentIntensity = 0.5f;
        [SerializeField, Range(0f, 1f)] private float maxIntensity = 1f;
        [SerializeField, Min(0f)] private float radius = 15f;
        [SerializeField, Min(0)] private uint startTick;
        [SerializeField, Min(1)] private uint durationTicks = 600u;
        [SerializeField] private EnvironmentChannelMask affectedChannels = EnvironmentChannelMask.Moisture | EnvironmentChannelMask.Wind;

        [Header("Options")]
        [Tooltip("Add SpatialIndexedTag so hazards participate in spatial queries.")]
        public bool addSpatialIndexedTag = true;

        public string HazardName
        {
            get => hazardName;
            set => hazardName = value;
        }

        public ClimateHazardType HazardType
        {
            get => hazardType;
            set => hazardType = value;
        }

        public float CurrentIntensity
        {
            get => currentIntensity;
            set => currentIntensity = Mathf.Clamp01(value);
        }

        public float MaxIntensity
        {
            get => maxIntensity;
            set => maxIntensity = Mathf.Clamp01(value);
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public uint StartTick
        {
            get => startTick;
            set => startTick = value;
        }

        public uint DurationTicks
        {
            get => durationTicks;
            set => durationTicks = math.max(1u, value);
        }

        public EnvironmentChannelMask AffectedChannels
        {
            get => affectedChannels;
            set => affectedChannels = value;
        }

        private void OnValidate()
        {
            if (!applyProfileOnValidate || profile == null)
            {
                return;
            }

            profile.CopyTo(this);
        }

        internal ClimateHazardState BuildComponent()
        {
            var name = new FixedString64Bytes(hazardName ?? string.Empty);
            return new ClimateHazardState
            {
                HazardType = hazardType,
                CurrentIntensity = currentIntensity,
                Radius = radius,
                MaxIntensity = Mathf.Max(maxIntensity, currentIntensity),
                StartTick = startTick,
                DurationTicks = durationTicks,
                HazardName = name,
                AffectedEnvironmentChannels = affectedChannels
            };
        }
    }

    public sealed class ClimateHazardAuthoringBaker : Baker<ClimateHazardAuthoring>
    {
        public override void Bake(ClimateHazardAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var state = authoring.BuildComponent();
            AddComponent(entity, state);

            if (authoring.addSpatialIndexedTag)
            {
                AddComponent<SpatialIndexedTag>(entity);
            }
        }
    }
}
#endif


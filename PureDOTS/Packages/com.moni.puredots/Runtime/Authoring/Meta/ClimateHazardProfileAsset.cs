#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [CreateAssetMenu(menuName = "PureDOTS/Meta Registries/Climate Hazard Profile", fileName = "ClimateHazardProfile")]
    public sealed class ClimateHazardProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        public string hazardName = "Storm";
        public ClimateHazardType hazardType = ClimateHazardType.Storm;

        [Header("Behaviour")]
        [Range(0f, 1f)] public float currentIntensity = 0.5f;
        [Range(0f, 1f)] public float maxIntensity = 1f;
        [Min(0f)] public float radius = 15f;
        [Min(0)] public uint startTick;
        [Min(1)] public uint durationTicks = 600u;
        public EnvironmentChannelMask affectedChannels = EnvironmentChannelMask.Moisture | EnvironmentChannelMask.Wind;

        public void CopyTo(ClimateHazardAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            authoring.HazardName = hazardName;
            authoring.HazardType = hazardType;
            authoring.CurrentIntensity = currentIntensity;
            authoring.MaxIntensity = Mathf.Max(currentIntensity, maxIntensity);
            authoring.Radius = radius;
            authoring.StartTick = startTick;
            authoring.DurationTicks = durationTicks;
            authoring.AffectedChannels = affectedChannels;
        }
    }
}
#endif



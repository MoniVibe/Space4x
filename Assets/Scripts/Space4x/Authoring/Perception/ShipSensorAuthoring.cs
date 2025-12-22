using PureDOTS.Runtime.Perception;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring.Perception
{
    /// <summary>
    /// Authoring component for ship sensors.
    /// Maps Space4x concepts (EM/Gravitic/Exotic) to PureDOTS Perception channels.
    /// </summary>
    public class ShipSensorAuthoring : MonoBehaviour
    {
        [Header("EM Sensors")]
        [Tooltip("EM detection range (radar/optical)")]
        public float EMRange = 200f;

        [Tooltip("EM field of view (degrees)")]
        public float EMFOV = 360f; // Omnidirectional for space

        [Tooltip("EM acuity (0-1)")]
        [Range(0f, 1f)]
        public float EMAcuity = 1f;

        [Header("Gravitic Sensors")]
        [Tooltip("Gravitic detection range")]
        public float GraviticRange = 500f;

        [Tooltip("Gravitic acuity (0-1)")]
        [Range(0f, 1f)]
        public float GraviticAcuity = 0.8f;

        [Header("Exotic Sensors")]
        [Tooltip("Exotic detection range (for exotic physics phenomena)")]
        public float ExoticRange = 1000f;

        [Tooltip("Exotic acuity (0-1)")]
        [Range(0f, 1f)]
        public float ExoticAcuity = 0.6f;

        [Header("Emissions")]
        [Tooltip("EM emission strength (0-1)")]
        [Range(0f, 1f)]
        public float EMEmission = 0.6f;

        [Header("Settings")]
        [Tooltip("Sensor update interval (seconds)")]
        public float UpdateInterval = 0.25f;

        [Tooltip("Maximum entities to track")]
        public byte MaxTrackedTargets = 16;
    }

    /// <summary>
    /// Baker for ShipSensorAuthoring.
    /// </summary>
    public class ShipSensorBaker : Baker<ShipSensorAuthoring>
    {
        public override void Bake(ShipSensorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var baseRange = math.max(authoring.EMRange, math.max(authoring.GraviticRange, authoring.ExoticRange));
            var rangeDenom = math.max(baseRange, 0.01f);

            // Add SenseCapability with Space4x channel mappings
            AddComponent(entity, new SenseCapability
            {
                EnabledChannels = PerceptionChannel.EM | 
                                 PerceptionChannel.Gravitic | 
                                 PerceptionChannel.Exotic,
                Range = baseRange,
                FieldOfView = authoring.EMFOV,
                Acuity = 1f,
                UpdateInterval = authoring.UpdateInterval,
                MaxTrackedTargets = authoring.MaxTrackedTargets,
                Flags = 0
            });

            var organs = AddBuffer<SenseOrganState>(entity);
            organs.Add(new SenseOrganState
            {
                OrganType = SenseOrganType.EMSuite,
                Channels = PerceptionChannel.EM,
                Gain = 1f,
                Condition = authoring.EMAcuity,
                NoiseFloor = 1f - authoring.EMAcuity,
                RangeMultiplier = authoring.EMRange / rangeDenom
            });
            organs.Add(new SenseOrganState
            {
                OrganType = SenseOrganType.GraviticArray,
                Channels = PerceptionChannel.Gravitic,
                Gain = 1f,
                Condition = authoring.GraviticAcuity,
                NoiseFloor = 1f - authoring.GraviticAcuity,
                RangeMultiplier = authoring.GraviticRange / rangeDenom
            });
            organs.Add(new SenseOrganState
            {
                OrganType = SenseOrganType.ExoticSensor,
                Channels = PerceptionChannel.Exotic,
                Gain = 1f,
                Condition = authoring.ExoticAcuity,
                NoiseFloor = 1f - authoring.ExoticAcuity,
                RangeMultiplier = authoring.ExoticRange / rangeDenom
            });

            AddComponent(entity, new SensorySignalEmitter
            {
                Channels = PerceptionChannel.EM,
                SmellStrength = 0f,
                SoundStrength = 0f,
                EMStrength = authoring.EMEmission,
                IsActive = (byte)(authoring.EMEmission > 0f ? 1 : 0)
            });

            // Add PerceptionState buffer
            AddBuffer<PerceivedEntity>(entity);

            // Add PerceptionState component
            AddComponent<PerceptionState>(entity);

            AddComponent<SignalPerceptionState>(entity);
        }
    }
}

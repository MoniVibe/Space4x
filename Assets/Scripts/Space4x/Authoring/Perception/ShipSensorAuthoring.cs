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

            // Add SenseCapability with Space4x channel mappings
            AddComponent(entity, new SenseCapability
            {
                EnabledChannels = PerceptionChannel.EM | 
                                 PerceptionChannel.Gravitic | 
                                 PerceptionChannel.Exotic,
                Range = math.max(authoring.EMRange, math.max(authoring.GraviticRange, authoring.ExoticRange)),
                FieldOfView = authoring.EMFOV,
                Acuity = 1f, // Use average or max - Phase 1: simple
                UpdateInterval = authoring.UpdateInterval,
                MaxTrackedTargets = authoring.MaxTrackedTargets,
                Flags = 0
            });

            // Add PerceptionState buffer
            AddBuffer<PerceivedEntity>(entity);

            // Add PerceptionState component
            AddComponent<PerceptionState>(entity);
        }
    }
}


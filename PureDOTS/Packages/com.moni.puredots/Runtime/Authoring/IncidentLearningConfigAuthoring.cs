#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Knowledge;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for incident learning configuration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IncidentLearningConfigAuthoring : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Tooltip("Maximum number of incident categories remembered per agent.")]
        [Range(0, 16)]
        private int maxEntries = 4;

        [Header("Memory Gain")]
        [SerializeField, Tooltip("Bias gain when directly hit by an incident.")]
        [Range(0f, 1f)]
        private float memoryGainOnHit = 0.35f;

        [SerializeField, Tooltip("Bias gain when narrowly avoiding an incident.")]
        [Range(0f, 1f)]
        private float memoryGainOnNearMiss = 0.15f;

        [SerializeField, Tooltip("Bias gain from observing an incident.")]
        [Range(0f, 1f)]
        private float memoryGainOnObservation = 0.05f;

        [SerializeField, Tooltip("Bias gain for uncategorized incidents.")]
        [Range(0f, 1f)]
        private float memoryGainDefault = 0.1f;

        [Header("Decay + Cooldown")]
        [SerializeField, Tooltip("Bias decay per second.")]
        [Range(0f, 1f)]
        private float memoryDecayPerSecond = 0.003f;

        [SerializeField, Tooltip("Minimum seconds between incidents per category.")]
        [Range(0f, 10f)]
        private float incidentCooldownSeconds = 1.5f;

        [SerializeField, Tooltip("Minimum severity to record an incident.")]
        [Range(0f, 1f)]
        private float minSeverity = 0.01f;

        [Header("Bias Limits")]
        [SerializeField, Tooltip("Minimum bias floor after decay.")]
        [Range(0f, 1f)]
        private float minBias = 0f;

        [SerializeField, Tooltip("Maximum bias cap.")]
        [Range(0f, 1f)]
        private float maxBias = 1f;

        public int MaxEntries => maxEntries;
        public float MemoryGainOnHit => memoryGainOnHit;
        public float MemoryGainOnNearMiss => memoryGainOnNearMiss;
        public float MemoryGainOnObservation => memoryGainOnObservation;
        public float MemoryGainDefault => memoryGainDefault;
        public float MemoryDecayPerSecond => memoryDecayPerSecond;
        public float IncidentCooldownSeconds => incidentCooldownSeconds;
        public float MinSeverity => minSeverity;
        public float MinBias => minBias;
        public float MaxBias => maxBias;
    }

    public sealed class IncidentLearningConfigAuthoringBaker : Baker<IncidentLearningConfigAuthoring>
    {
        public override void Bake(IncidentLearningConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new IncidentLearningConfig
            {
                MaxEntries = Mathf.Max(0, authoring.MaxEntries),
                MemoryGainOnHit = Mathf.Clamp01(authoring.MemoryGainOnHit),
                MemoryGainOnNearMiss = Mathf.Clamp01(authoring.MemoryGainOnNearMiss),
                MemoryGainOnObservation = Mathf.Clamp01(authoring.MemoryGainOnObservation),
                MemoryGainDefault = Mathf.Clamp01(authoring.MemoryGainDefault),
                MemoryDecayPerSecond = Mathf.Max(0f, authoring.MemoryDecayPerSecond),
                IncidentCooldownSeconds = Mathf.Max(0f, authoring.IncidentCooldownSeconds),
                MinSeverity = Mathf.Clamp01(authoring.MinSeverity),
                MinBias = Mathf.Clamp01(authoring.MinBias),
                MaxBias = Mathf.Clamp01(authoring.MaxBias)
            });
        }
    }
}
#endif

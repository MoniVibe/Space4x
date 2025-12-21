using Space4X.Registry;
using PureDOTS.Runtime.Individual;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for individual entity stats (Command, Tactics, Logistics, Diplomacy, Engineering, Resolve).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Individual Stats")]
    public sealed class IndividualStatsAuthoring : MonoBehaviour
    {
        [Header("Command Stats")]
        [Range(0f, 100f)]
        [Tooltip("Leadership, authority, chain-of-command compliance")]
        public float command = 50f;

        [Range(0f, 100f)]
        [Tooltip("Moment-to-moment combat insight")]
        public float tactics = 50f;

        [Range(0f, 100f)]
        [Tooltip("Supply, fabrication, facility synergy")]
        public float logistics = 50f;

        [Range(0f, 100f)]
        [Tooltip("Negotiation, cross-faction relations")]
        public float diplomacy = 50f;

        [Range(0f, 100f)]
        [Tooltip("Technical mastery, repair ingenuity")]
        public float engineering = 50f;

        [Range(0f, 100f)]
        [Tooltip("Morale backbone, risk tolerance")]
        public float resolve = 50f;

        public sealed class Baker : Unity.Entities.Baker<IndividualStatsAuthoring>
        {
            public override void Bake(IndividualStatsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SimIndividualTag>(entity);
                AddComponent(entity, new Registry.IndividualStats
                {
                    Command = (half)math.clamp(authoring.command, 0f, 100f),
                    Tactics = (half)math.clamp(authoring.tactics, 0f, 100f),
                    Logistics = (half)math.clamp(authoring.logistics, 0f, 100f),
                    Diplomacy = (half)math.clamp(authoring.diplomacy, 0f, 100f),
                    Engineering = (half)math.clamp(authoring.engineering, 0f, 100f),
                    Resolve = (half)math.clamp(authoring.resolve, 0f, 100f)
                });
            }
        }
    }
}

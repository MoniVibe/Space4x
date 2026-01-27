#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Traversal;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class TraversalLinkAuthoring : MonoBehaviour
    {
        public TraversalType type = TraversalType.Jump;
        public Transform startPoint;
        public Transform endPoint;
        [Min(0f)] public float maxRadius = 0.5f;
        [Min(0f)] public float maxHeight = 1.6f;
        public TraversalStance requiredStance = TraversalStance.Standing;
        public TraversalRequirementFlags requirements = TraversalRequirementFlags.None;
        [Min(0f)] public float cost = 1f;
        public bool bidirectional = true;

        [Header("Jump Execution")]
        [Min(0f)] public float arcHeight = 1.2f;
        [Min(0.01f)] public float duration = 0.6f;
        [Min(0f)] public float landingSnapDistance = 0.15f;
        [Min(0f)] public float landingSnapVerticalTolerance = 0.5f;
    }

    public sealed class TraversalLinkBaker : Baker<TraversalLinkAuthoring>
    {
        public override void Bake(TraversalLinkAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var start = authoring.startPoint != null ? authoring.startPoint.position : authoring.transform.position;
            var end = authoring.endPoint != null ? authoring.endPoint.position : authoring.transform.position;

            AddComponent(entity, new TraversalLink
            {
                Type = authoring.type,
                StartPosition = start,
                EndPosition = end,
                MaxRadius = math.max(0f, authoring.maxRadius),
                MaxHeight = math.max(0f, authoring.maxHeight),
                RequiredStance = authoring.requiredStance,
                Requirements = authoring.requirements,
                Cost = math.max(0f, authoring.cost),
                Execution = new TraversalExecutionParams
                {
                    ArcHeight = math.max(0f, authoring.arcHeight),
                    Duration = math.max(0.01f, authoring.duration),
                    LandingSnapDistance = math.max(0f, authoring.landingSnapDistance),
                    LandingSnapVerticalTolerance = math.max(0f, authoring.landingSnapVerticalTolerance)
                },
                IsBidirectional = (byte)(authoring.bidirectional ? 1 : 0)
            });
        }
    }
}
#endif

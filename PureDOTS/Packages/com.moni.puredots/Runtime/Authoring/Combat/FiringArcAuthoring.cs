#if UNITY_EDITOR
using PureDOTS.Runtime.Combat;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Combat
{
    [DisallowMultipleComponent]
    public sealed class FiringArcAuthoring : MonoBehaviour
    {
        public Vector3 forward = Vector3.forward;
        public float angleDegrees = 45f;
        public float range = 200f;
        public float antiCraftThreat = 1f;
        public float capitalThreat = 0.5f;
    }

    public sealed class FiringArcBaker : Baker<FiringArcAuthoring>
    {
        public override void Bake(FiringArcAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new FiringArc
            {
                Forward = math.normalize((float3)authoring.forward),
                AngleDegrees = authoring.angleDegrees,
                Range = authoring.range,
                AntiCraftThreat = authoring.antiCraftThreat,
                CapitalThreat = authoring.capitalThreat
            });
        }
    }
}
#endif

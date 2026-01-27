#if UNITY_EDITOR
using PureDOTS.Runtime.Communication;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Communication
{
    /// <summary>
    /// Authoring component for comms jammers that degrade link integrity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommJammerAuthoring : MonoBehaviour
    {
        [Min(0f)] public float radius = 25f;
        [Range(0f, 1f)] public float strength = 0.5f;
        public bool isActive = true;
    }

    public sealed class CommJammerBaker : Baker<CommJammerAuthoring>
    {
        public override void Bake(CommJammerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(entity, new CommJammer
            {
                Radius = math.max(0f, authoring.radius),
                Strength = math.saturate(authoring.strength),
                IsActive = authoring.isActive ? (byte)1 : (byte)0
            });
        }
    }
}
#endif

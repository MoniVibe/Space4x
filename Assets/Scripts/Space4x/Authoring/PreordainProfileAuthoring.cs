using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for preordain track (career path focus).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Preordain Profile")]
    public sealed class PreordainProfileAuthoring : MonoBehaviour
    {
        [Tooltip("Preordain track (career path focus)")]
        public PreordainTrack track = PreordainTrack.None;

        public sealed class Baker : Unity.Entities.Baker<PreordainProfileAuthoring>
        {
            public override void Bake(PreordainProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.PreordainProfile { Track = authoring.track });
            }
        }
    }
}


using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for preordain track (career path focus).
    /// Sets PreordainProfile component on entity.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Preordain Track")]
    public sealed class PreordainTrackAuthoring : MonoBehaviour
    {
        [Tooltip("Career path focus (Combat Ace, Logistics Maven, Diplomatic Envoy, Engineering Savant)")]
        public PreordainTrack Track = PreordainTrack.None;

        public sealed class Baker : Unity.Entities.Baker<PreordainTrackAuthoring>
        {
            public override void Bake(PreordainTrackAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PreordainProfile
                {
                    Track = authoring.Track
                });
            }
        }
    }
}


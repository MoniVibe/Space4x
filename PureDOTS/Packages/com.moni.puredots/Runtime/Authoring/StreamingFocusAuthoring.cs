using PureDOTS.Runtime.Streaming;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class StreamingFocusAuthoring : MonoBehaviour
    {
        [Tooltip("If enabled, the focus position follows this transform each frame. Otherwise, Manual Position is used.")]
        public bool followTransform = true;

        [Tooltip("Manual world position used when Follow Transform is disabled.")]
        public Vector3 manualPosition = Vector3.zero;

        [Tooltip("Offset applied to the load radius checks for this focus.")]
        public float loadRadiusOffset;

        [Tooltip("Offset applied to the unload radius checks for this focus.")]
        public float unloadRadiusOffset;

        [Tooltip("Multiplier applied to section radii for this focus (useful for zoomed cameras).")]
        public float radiusScale = 1f;

        private sealed class StreamingFocusBaker : Baker<StreamingFocusAuthoring>
        {
            public override void Bake(StreamingFocusAuthoring authoring)
            {
                var usage = authoring.followTransform ? TransformUsageFlags.Dynamic : TransformUsageFlags.None;
                var entity = GetEntity(usage);

                var position = authoring.followTransform ? authoring.transform.position : authoring.manualPosition;

                AddComponent(entity, new StreamingFocus
                {
                    Position = position,
                    Velocity = float3.zero,
                    RadiusScale = math.max(0.01f, authoring.radiusScale),
                    LoadRadiusOffset = authoring.loadRadiusOffset,
                    UnloadRadiusOffset = authoring.unloadRadiusOffset
                });

                if (authoring.followTransform)
                {
                    AddComponent(entity, new StreamingFocusFollow { UseTransform = true });
                }
            }
        }
    }
}

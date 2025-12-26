using Space4X.Runtime;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Vessel Mobility Profile")]
    public sealed class Space4XVesselMobilityProfileAuthoring : MonoBehaviour
    {
        public VesselThrustMode thrustMode = VesselThrustMode.ForwardOnly;
        [Range(0f, 2f)] public float reverseSpeedMultiplier = 0f;
        [Range(0f, 2f)] public float strafeSpeedMultiplier = 0f;
        public bool allowKiting = false;
        [Range(0.5f, 2.5f)] public float turnMultiplier = 1f;

        public sealed class Baker : Baker<Space4XVesselMobilityProfileAuthoring>
        {
            public override void Bake(Space4XVesselMobilityProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VesselMobilityProfile
                {
                    ThrustMode = authoring.thrustMode,
                    ReverseSpeedMultiplier = Mathf.Max(0f, authoring.reverseSpeedMultiplier),
                    StrafeSpeedMultiplier = Mathf.Max(0f, authoring.strafeSpeedMultiplier),
                    AllowKiting = (byte)(authoring.allowKiting ? 1 : 0),
                    TurnMultiplier = Mathf.Clamp(authoring.turnMultiplier, 0.5f, 2.5f)
                });
            }
        }
    }
}

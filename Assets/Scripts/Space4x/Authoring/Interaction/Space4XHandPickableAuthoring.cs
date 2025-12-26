using PureDOTS.Runtime.Components;
using Space4X.Runtime.Interaction;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring.Interaction
{
    public sealed class Space4XHandPickableAuthoring : MonoBehaviour
    {
        [SerializeField] private float maxMass = 500f;
        [SerializeField] private float maxHoldDistance = 50f;
        [SerializeField] private float throwSpeedMultiplier = 1f;
        [SerializeField] private float slingshotSpeedMultiplier = 1.5f;
        [SerializeField] private float followLerp = 0.35f;

        private sealed class Baker : Baker<Space4XHandPickableAuthoring>
        {
            public override void Bake(Space4XHandPickableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PickableTag>(entity);
                AddComponent(entity, new HandPickable
                {
                    Mass = 1f,
                    MaxHoldDistance = authoring.maxHoldDistance,
                    ThrowImpulseMultiplier = authoring.throwSpeedMultiplier,
                    FollowLerp = authoring.followLerp
                });
                AddComponent(entity, new Space4XHandPickable
                {
                    MaxMass = authoring.maxMass,
                    ThrowSpeedMultiplier = authoring.throwSpeedMultiplier,
                    SlingshotSpeedMultiplier = authoring.slingshotSpeedMultiplier
                });
            }
        }
    }
}

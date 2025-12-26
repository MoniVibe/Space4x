using Space4X.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Debris Spall Config")]
    public sealed class Space4XDebrisSpallConfigAuthoring : MonoBehaviour
    {
        [Header("Counts")]
        [SerializeField, Range(0f, 0.5f)] private float piecesPerVoxel = 0.01f;
        [SerializeField, Range(0, 128)] private int maxPiecesPerEvent = 18;
        [SerializeField, Range(0, 256)] private int maxPiecesPerFrame = 48;

        [Header("Impulse")]
        [SerializeField, Range(0f, 20f)] private float impulseMin = 1.5f;
        [SerializeField, Range(0f, 20f)] private float impulseMax = 6f;
        [SerializeField, Range(0f, 3f)] private float drillImpulseMultiplier = 0.9f;
        [SerializeField, Range(0f, 3f)] private float laserImpulseMultiplier = 1.2f;
        [SerializeField, Range(0f, 3f)] private float microwaveImpulseMultiplier = 1.05f;

        [Header("Lifetime")]
        [SerializeField, Range(0.1f, 10f)] private float lifetimeMin = 1.5f;
        [SerializeField, Range(0.1f, 10f)] private float lifetimeMax = 4.5f;

        [Header("Scale")]
        [SerializeField, Range(0.05f, 2f)] private float scaleMin = 0.15f;
        [SerializeField, Range(0.05f, 2f)] private float scaleMax = 0.45f;

        [Header("Drag")]
        [SerializeField, Range(0f, 5f)] private float drag = 0.4f;

        private void OnValidate()
        {
            piecesPerVoxel = math.clamp(piecesPerVoxel, 0f, 0.5f);
            maxPiecesPerEvent = math.max(0, maxPiecesPerEvent);
            maxPiecesPerFrame = math.max(0, maxPiecesPerFrame);
            impulseMin = math.max(0f, impulseMin);
            impulseMax = math.max(impulseMin, impulseMax);
            drillImpulseMultiplier = math.max(0f, drillImpulseMultiplier);
            laserImpulseMultiplier = math.max(0f, laserImpulseMultiplier);
            microwaveImpulseMultiplier = math.max(0f, microwaveImpulseMultiplier);
            lifetimeMin = math.max(0.1f, lifetimeMin);
            lifetimeMax = math.max(lifetimeMin, lifetimeMax);
            scaleMin = math.max(0.05f, scaleMin);
            scaleMax = math.max(scaleMin, scaleMax);
            drag = math.max(0f, drag);
        }

        private sealed class Baker : Baker<Space4XDebrisSpallConfigAuthoring>
        {
            public override void Bake(Space4XDebrisSpallConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Space4XDebrisSpallConfig
                {
                    PiecesPerVoxel = math.max(0f, authoring.piecesPerVoxel),
                    MaxPiecesPerEvent = math.max(0, authoring.maxPiecesPerEvent),
                    MaxPiecesPerFrame = math.max(0, authoring.maxPiecesPerFrame),
                    ImpulseMin = math.max(0f, authoring.impulseMin),
                    ImpulseMax = math.max(authoring.impulseMin, authoring.impulseMax),
                    LifetimeMin = math.max(0.1f, authoring.lifetimeMin),
                    LifetimeMax = math.max(authoring.lifetimeMin, authoring.lifetimeMax),
                    ScaleMin = math.max(0.05f, authoring.scaleMin),
                    ScaleMax = math.max(authoring.scaleMin, authoring.scaleMax),
                    Drag = math.max(0f, authoring.drag),
                    DrillImpulseMultiplier = math.max(0f, authoring.drillImpulseMultiplier),
                    LaserImpulseMultiplier = math.max(0f, authoring.laserImpulseMultiplier),
                    MicrowaveImpulseMultiplier = math.max(0f, authoring.microwaveImpulseMultiplier)
                });
            }
        }
    }
}

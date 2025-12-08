using PureDOTS.Runtime.Components.Orbital;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for orbital objects with 6-DoF motion.
    /// Bakes SixDoFState, ShellMembership, and OrbitalFrame components.
    /// Links to existing OrbitalObjectTag/OrbitalObjectState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrbitalAuthoring : MonoBehaviour
    {
        [Header("6-DoF State")]
        [SerializeField] private Vector3 initialPosition = Vector3.zero;
        [SerializeField] private Vector3 initialLinearVelocity = Vector3.zero;
        [SerializeField] private Vector3 initialAngularVelocity = Vector3.zero;

        [Header("Shell Membership")]
        [SerializeField] private ShellType shellType = ShellType.Inner;
        [SerializeField] private double innerRadius = 1000.0;
        [SerializeField] private double outerRadius = 10000.0;

        [Header("Frame Hierarchy")]
        [SerializeField] private bool isRootFrame = false;
        [SerializeField] private Transform parentFrame;

        private sealed class Baker : Unity.Entities.Baker<OrbitalAuthoring>
        {
            public override void Bake(OrbitalAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add SixDoFState
                var sixDoF = new SixDoFState
                {
                    Position = authoring.initialPosition,
                    Orientation = quaternion.identity,
                    LinearVelocity = authoring.initialLinearVelocity,
                    AngularVelocity = authoring.initialAngularVelocity
                };
                AddComponent(entity, sixDoF);

                // Add ShellMembership
                var shell = new ShellMembership
                {
                    ShellIndex = (int)authoring.shellType,
                    InnerRadius = authoring.innerRadius,
                    OuterRadius = authoring.outerRadius,
                    UpdateFrequency = authoring.shellType switch
                    {
                        ShellType.Core => 1.0f,
                        ShellType.Inner => 0.1f,
                        ShellType.Outer => 0.01f,
                        _ => 0.1f
                    },
                    LastUpdateTick = 0
                };
                AddComponent(entity, shell);

                // Add AdaptivePrecision
                var precision = new AdaptivePrecision
                {
                    Level = PrecisionLevel.Float,
                    DistanceThreshold = authoring.shellType == ShellType.Core ? 1e20 : 1e12
                };
                AddComponent(entity, precision);

                // Add OrbitalFrame if this is a frame entity
                if (authoring.isRootFrame || authoring.parentFrame != null)
                {
                    var frame = new OrbitalFrame
                    {
                        Origin = authoring.initialPosition,
                        Orientation = quaternion.identity,
                        Scale = 1.0f,
                        PreviousOrientation = quaternion.identity,
                        DeltaThreshold = 0.001f
                    };
                    AddComponent(entity, frame);

                    if (!authoring.isRootFrame && authoring.parentFrame != null)
                    {
                        var parentEntity = GetEntity(authoring.parentFrame, TransformUsageFlags.Dynamic);
                        var frameParent = new FrameParent
                        {
                            ParentFrameEntity = parentEntity
                        };
                        AddComponent(entity, frameParent);
                    }
                }

                // Link to existing OrbitalObjectTag if present
                // (OrbitalObjectTag would be added by another authoring component)
            }
        }
    }
}



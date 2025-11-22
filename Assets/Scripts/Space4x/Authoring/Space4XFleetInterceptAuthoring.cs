using Space4X.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring hook that tags fleets for broadcast/intercept routing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XFleetInterceptAuthoring : MonoBehaviour
    {
        [Header("Broadcast")]
        public bool allowsInterception = true;
        public byte techTier = 1;
        public Vector3 initialVelocity = Vector3.zero;

        [Header("Intercept Capability (optional)")]
        public bool addInterceptCourse = false;
        public float interceptSpeed = 10f;

        public class Baker : Unity.Entities.Baker<Space4XFleetInterceptAuthoring>
        {
            public override void Bake(Space4XFleetInterceptAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                var position = (float3)authoring.transform.position;
                var velocity = (float3)authoring.initialVelocity;

                AddComponent(entity, new FleetMovementBroadcast
                {
                    Position = position,
                    Velocity = velocity,
                    LastUpdateTick = 0,
                    AllowsInterception = (byte)(authoring.allowsInterception ? 1 : 0),
                    TechTier = authoring.techTier
                });

                AddComponent<SpatialIndexedTag>(entity);
                AddComponent(entity, new SpatialGridResidency
                {
                    CellId = 0,
                    LastPosition = position,
                    Version = 0
                });

                if (math.lengthsq(velocity) > 0f)
                {
                    AddComponent(entity, new FleetKinematics { Velocity = velocity });
                }

                if (authoring.addInterceptCourse)
                {
                    AddComponent(entity, new InterceptCourse
                    {
                        TargetFleet = Entity.Null,
                        InterceptPoint = position,
                        EstimatedInterceptTick = 0,
                        UsesInterception = 0
                    });

                    AddComponent(entity, new InterceptCapability
                    {
                        MaxSpeed = math.max(0.1f, authoring.interceptSpeed),
                        TechTier = authoring.techTier,
                        AllowIntercept = 1
                    });
                }
            }
        }
    }
}

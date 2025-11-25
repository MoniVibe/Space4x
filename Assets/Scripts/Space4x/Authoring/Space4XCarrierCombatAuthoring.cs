using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that adds fleet registry and combat components to carriers.
    /// Use this alongside Space4XMiningDemoAuthoring to make carriers visible to registry bridge and intercept systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCarrierCombatAuthoring : MonoBehaviour
    {
        [Header("Fleet Registry")]
        public string fleetId = "FLEET-1";
        public int shipCount = 1;
        public Space4XFleetPosture posture = Space4XFleetPosture.Patrol;
        public int taskForce = 0;

        [Header("Stance")]
        public VesselStance initialStance = VesselStance.Neutral;
        public VesselStance desiredStance = VesselStance.Neutral;

        [Header("Formation (Optional)")]
        public bool addFormationData = false;
        public GameObject formationLeader;  // Set in inspector
        public float formationRadius = 50f;

        public class Baker : Unity.Entities.Baker<Space4XCarrierCombatAuthoring>
        {
            public override void Bake(Space4XCarrierCombatAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                // Add Space4XFleet component for registry bridge visibility
                AddComponent(entity, new Space4XFleet
                {
                    FleetId = new FixedString64Bytes(authoring.fleetId),
                    ShipCount = authoring.shipCount,
                    Posture = authoring.posture,
                    TaskForce = authoring.taskForce
                });

                // Add VesselStanceComponent for stance-based AI
                AddComponent(entity, new VesselStanceComponent
                {
                    CurrentStance = authoring.initialStance,
                    DesiredStance = authoring.desiredStance,
                    StanceChangeTick = 0
                });

                // Optionally add FormationData
                if (authoring.addFormationData)
                {
                    var leaderEntity = Entity.Null;
                    if (authoring.formationLeader != null)
                    {
                        leaderEntity = GetEntity(authoring.formationLeader, TransformUsageFlags.None);
                    }

                    AddComponent(entity, new FormationData
                    {
                        FormationTightness = (half)0.7f,
                        FormationRadius = authoring.formationRadius,
                        FormationLeader = leaderEntity,
                        FormationUpdateTick = 0
                    });
                }
            }
        }
    }
}


using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Sample authoring component that seeds a few Space4X colonies and fleets for prototype scenes or tests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XSampleRegistryAuthoring : MonoBehaviour
    {
        [SerializeField]
        private ColonyDefinition[] colonies =
        {
            new ColonyDefinition
            {
                ColonyId = "SOL-1",
                Population = 250_000f,
                StoredResources = 1200f,
                SectorId = 1,
                Status = Space4XColonyStatus.Growing,
                Position = new float3(0f, 0f, 0f)
            }
        };

        [SerializeField]
        private FleetDefinition[] fleets =
        {
            new FleetDefinition
            {
                FleetId = "FLEET-ALPHA",
                ShipCount = 5,
                Posture = Space4XFleetPosture.Patrol,
                TaskForce = 101,
                Position = new float3(35f, 0f, -12f)
            }
        };

        public ColonyDefinition[] Colonies => colonies;

        public FleetDefinition[] Fleets => fleets;

        [Serializable]
        public struct ColonyDefinition
        {
            public string ColonyId;
            public float Population;
            public float StoredResources;
            public int SectorId;
            public Space4XColonyStatus Status;
            public float3 Position;
        }

        [Serializable]
        public struct FleetDefinition
        {
            public string FleetId;
            public int ShipCount;
            public int TaskForce;
            public Space4XFleetPosture Posture;
            public float3 Position;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XSampleRegistryAuthoring>
        {
            public override void Bake(Space4XSampleRegistryAuthoring authoring)
            {
                BakeColonies(authoring);
                BakeFleets(authoring);
            }

            private void BakeColonies(Space4XSampleRegistryAuthoring authoring)
            {
                if (authoring.Colonies == null || authoring.Colonies.Length == 0)
                {
                    return;
                }

                foreach (var colony in authoring.Colonies)
                {
                    if (string.IsNullOrWhiteSpace(colony.ColonyId))
                    {
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(colony.Position, quaternion.identity, 1f));
                    AddComponent(entity, new Space4XColony
                    {
                        ColonyId = new FixedString64Bytes(colony.ColonyId),
                        Population = math.max(0f, colony.Population),
                        StoredResources = math.max(0f, colony.StoredResources),
                        SectorId = colony.SectorId,
                        Status = colony.Status
                    });
                }
            }

            private void BakeFleets(Space4XSampleRegistryAuthoring authoring)
            {
                if (authoring.Fleets == null || authoring.Fleets.Length == 0)
                {
                    return;
                }

                foreach (var fleet in authoring.Fleets)
                {
                    if (string.IsNullOrWhiteSpace(fleet.FleetId))
                    {
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(fleet.Position, quaternion.identity, 1f));
                    AddComponent(entity, new Space4XFleet
                    {
                        FleetId = new FixedString64Bytes(fleet.FleetId),
                        ShipCount = math.max(0, fleet.ShipCount),
                        Posture = fleet.Posture,
                        TaskForce = fleet.TaskForce
                    });
                }
            }
        }
    }
}


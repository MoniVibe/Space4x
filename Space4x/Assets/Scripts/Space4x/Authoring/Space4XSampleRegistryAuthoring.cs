using System;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;
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
    [RequireComponent(typeof(PureDotsConfigAuthoring))]
    [RequireComponent(typeof(SpatialPartitionAuthoring))]
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

        [SerializeField]
        private LogisticsRouteDefinition[] logisticsRoutes =
        {
            new LogisticsRouteDefinition
            {
                RouteId = "ROUTE-SOL-ALPHA",
                OriginColonyId = "SOL-1",
                DestinationColonyId = "ALPHA-2",
                DailyThroughput = 180f,
                Risk = 0.15f,
                Priority = 1,
                Status = Space4XLogisticsRouteStatus.Operational,
                Position = new float3(16f, 0f, 6f)
            }
        };

        [SerializeField]
        private AnomalyDefinition[] anomalies =
        {
            new AnomalyDefinition
            {
                AnomalyId = "ANOM-PRIME",
                Classification = "Gravitic Rift",
                Severity = Space4XAnomalySeverity.Severe,
                State = Space4XAnomalyState.Active,
                Instability = 0.78f,
                SectorId = 4,
                Position = new float3(-18f, 0f, 22f)
            }
        };

        public ColonyDefinition[] Colonies => colonies;

        public FleetDefinition[] Fleets => fleets;

        public LogisticsRouteDefinition[] LogisticsRoutes => logisticsRoutes;

        public AnomalyDefinition[] Anomalies => anomalies;

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

        [Serializable]
        public struct LogisticsRouteDefinition
        {
            public string RouteId;
            public string OriginColonyId;
            public string DestinationColonyId;
            public float DailyThroughput;
            public float Risk;
            public int Priority;
            public Space4XLogisticsRouteStatus Status;
            public float3 Position;
        }

        [Serializable]
        public struct AnomalyDefinition
        {
            public string AnomalyId;
            public string Classification;
            public Space4XAnomalySeverity Severity;
            public Space4XAnomalyState State;
            public float Instability;
            public int SectorId;
            public float3 Position;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XSampleRegistryAuthoring>
        {
            public override void Bake(Space4XSampleRegistryAuthoring authoring)
            {
                BakeColonies(authoring);
                BakeFleets(authoring);
                BakeLogisticsRoutes(authoring);
                BakeAnomalies(authoring);
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
                    AddComponent<SpatialIndexedTag>(entity);
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
                    AddComponent<SpatialIndexedTag>(entity);
                    AddComponent(entity, new Space4XFleet
                    {
                        FleetId = new FixedString64Bytes(fleet.FleetId),
                        ShipCount = math.max(0, fleet.ShipCount),
                        Posture = fleet.Posture,
                        TaskForce = fleet.TaskForce
                    });
                }
            }

            private void BakeLogisticsRoutes(Space4XSampleRegistryAuthoring authoring)
            {
                if (authoring.LogisticsRoutes == null || authoring.LogisticsRoutes.Length == 0)
                {
                    return;
                }

                foreach (var route in authoring.LogisticsRoutes)
                {
                    if (string.IsNullOrWhiteSpace(route.RouteId))
                    {
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(route.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);
                    AddComponent(entity, new Space4XLogisticsRoute
                    {
                        RouteId = new FixedString64Bytes(route.RouteId),
                        OriginColonyId = new FixedString64Bytes(route.OriginColonyId ?? string.Empty),
                        DestinationColonyId = new FixedString64Bytes(route.DestinationColonyId ?? string.Empty),
                        DailyThroughput = math.max(0f, route.DailyThroughput),
                        Risk = math.clamp(route.Risk, 0f, 1f),
                        Priority = route.Priority,
                        Status = route.Status
                    });
                }
            }

            private void BakeAnomalies(Space4XSampleRegistryAuthoring authoring)
            {
                if (authoring.Anomalies == null || authoring.Anomalies.Length == 0)
                {
                    return;
                }

                foreach (var anomaly in authoring.Anomalies)
                {
                    if (string.IsNullOrWhiteSpace(anomaly.AnomalyId))
                    {
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(anomaly.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);
                    AddComponent(entity, new Space4XAnomaly
                    {
                        AnomalyId = new FixedString64Bytes(anomaly.AnomalyId),
                        Classification = new FixedString64Bytes(anomaly.Classification ?? string.Empty),
                        Severity = anomaly.Severity,
                        State = anomaly.State,
                        Instability = math.max(0f, anomaly.Instability),
                        SectorId = anomaly.SectorId
                    });
                }
            }
        }
    }
}

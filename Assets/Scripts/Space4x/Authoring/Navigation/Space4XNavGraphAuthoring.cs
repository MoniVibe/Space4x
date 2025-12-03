using PureDOTS.Runtime.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring.Navigation
{
    /// <summary>
    /// Authoring component for building NavGraph from waypoints and hyperlanes.
    /// Phase 1: Simple waypoint-based graph.
    /// Phase 2: Hyperlane network, gravity well avoidance, etc.
    /// </summary>
    public class Space4XNavGraphAuthoring : MonoBehaviour
    {
        [Tooltip("Graph bounds min (world space)")]
        public Vector3 BoundsMin = new Vector3(-10000f, -10000f, -10000f);

        [Tooltip("Graph bounds max (world space)")]
        public Vector3 BoundsMax = new Vector3(10000f, 10000f, 10000f);

        [Tooltip("Waypoint objects (will be converted to NavNodes)")]
        public Transform[] Waypoints;

        [Tooltip("Hyperlane connections (pairs of waypoint indices)")]
        public Vector2Int[] HyperlaneConnections;
    }

    /// <summary>
    /// Baker for Space4XNavGraphAuthoring.
    /// Creates NavGraph singleton with nodes and edges from waypoints.
    /// </summary>
    public class Space4XNavGraphBaker : Baker<Space4XNavGraphAuthoring>
    {
        public override void Bake(Space4XNavGraphAuthoring authoring)
        {
            // Create graph singleton entity
            var graphEntity = CreateAdditionalEntity(TransformUsageFlags.None);

            // Add NavGraph component
            AddComponent(graphEntity, new NavGraph
            {
                Version = 1,
                NodeCount = authoring.Waypoints != null ? authoring.Waypoints.Length : 0,
                EdgeCount = authoring.HyperlaneConnections != null ? authoring.HyperlaneConnections.Length : 0,
                BoundsMin = authoring.BoundsMin,
                BoundsMax = authoring.BoundsMax
            });

            // Add NavNode buffer
            var nodeBuffer = AddBuffer<NavNode>(graphEntity);
            if (authoring.Waypoints != null)
            {
                for (int i = 0; i < authoring.Waypoints.Length; i++)
                {
                    var waypoint = authoring.Waypoints[i];
                    if (waypoint != null)
                    {
                        nodeBuffer.Add(new NavNode
                        {
                            Position = waypoint.position,
                            Flags = NavNodeFlags.Waypoint,
                            BaseCost = 1f,
                            NodeId = i
                        });
                    }
                }
            }

            // Add NavEdge buffer
            var edgeBuffer = AddBuffer<NavEdge>(graphEntity);
            if (authoring.HyperlaneConnections != null)
            {
                foreach (var connection in authoring.HyperlaneConnections)
                {
                    var fromIdx = connection.x;
                    var toIdx = connection.y;
                    if (fromIdx >= 0 && fromIdx < nodeBuffer.Length &&
                        toIdx >= 0 && toIdx < nodeBuffer.Length)
                    {
                        var fromNode = nodeBuffer[fromIdx];
                        var toNode = nodeBuffer[toIdx];
                        var distance = math.distance(fromNode.Position, toNode.Position);

                        // Hyperlane edges allow Space, SubLight, and FTL modes
                        edgeBuffer.Add(new NavEdge
                        {
                            FromNode = fromIdx,
                            ToNode = toIdx,
                            Cost = distance,
                            AllowedModes = LocomotionMode.Space | LocomotionMode.SubLight | LocomotionMode.FTL,
                            Flags = NavEdgeFlags.None,
                            IsBidirectional = 1
                        });
                    }
                }
            }
        }
    }
}


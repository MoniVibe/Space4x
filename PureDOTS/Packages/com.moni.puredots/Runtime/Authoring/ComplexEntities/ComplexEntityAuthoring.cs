using PureDOTS.Runtime.ComplexEntities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring for a Complex Entity "entity-of-record".
    /// This bakes the minimal canonical component set; operational/narrative expansions are runtime-driven.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComplexEntityAuthoring : MonoBehaviour
    {
        [Tooltip("Stable numeric ID (persistence/pooling key). Keep deterministic; do not auto-generate from instance IDs.")]
        [SerializeField] public ulong StableId;

        [SerializeField] public ComplexEntityType EntityType = ComplexEntityType.Other;

        [Tooltip("Cold-state crew aggregate (roster is externalized and only loaded when needed).")]
        [SerializeField] public ushort CrewCount;

        [Tooltip("If true, starts inside the operational bubble (adds ActiveBubbleTag).")]
        [SerializeField] public bool StartOperational;

        [Tooltip("If true, starts inspected (adds InspectionRequest).")]
        [SerializeField] public bool StartNarrative;
    }

    public sealed class ComplexEntityAuthoringBaker : Baker<ComplexEntityAuthoring>
    {
        // Game-defined: keep as authoring-only default; games can override by writing core axes at runtime.
        private const float DefaultCellSizeMeters = 1024f;

        public override void Bake(ComplexEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ComplexEntityIdentity
            {
                StableId = authoring.StableId,
                CreationTick = 0,
                EntityType = authoring.EntityType,
                Reserved0 = 0,
                Reserved1 = 0
            });

            var pos = (float3)authoring.transform.position;
            var cellF = math.floor(pos / DefaultCellSizeMeters);
            var cell = new int3((int)cellF.x, (int)cellF.y, (int)cellF.z);
            var local = pos - (cellF * DefaultCellSizeMeters);

            // Default mapping is X/Z into LocalX/LocalY (Y is vertical layer via Cell.y).
            ushort localX = QuantizeU16(local.x / DefaultCellSizeMeters);
            ushort localY = QuantizeU16(local.z / DefaultCellSizeMeters);

            var forward = (float3)authoring.transform.forward;
            var yaw = math.atan2(forward.x, forward.z); // radians
            ushort headingQ = QuantizeU16((yaw + math.PI) / (2f * math.PI));

            AddComponent(entity, new ComplexEntityCoreAxes
            {
                Cell = cell,
                LocalX = localX,
                LocalY = localY,
                VelX = 0,
                VelY = 0,
                HeadingQ = headingQ,
                HealthQ = 65535,
                MassQ = 0,
                CapacityQ = 0,
                LoadQ = 0,
                Flags = 0,
                CrewCount = authoring.CrewCount,
                Reserved0 = 0
            });

            if (authoring.StartOperational)
            {
                AddComponent<ActiveBubbleTag>(entity);
            }

            if (authoring.StartNarrative)
            {
                AddComponent<InspectionRequest>(entity);
            }
        }

        private static ushort QuantizeU16(float normalized01)
        {
            var v = math.clamp(normalized01, 0f, 1f);
            return (ushort)math.clamp((int)math.round(v * 65535f), 0, 65535);
        }
    }
}


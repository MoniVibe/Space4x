#if UNITY_EDITOR
using PureDOTS.Runtime.Ships;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Ships
{
    [DisallowMultipleComponent]
    public sealed class ShipAggregateAuthoring : MonoBehaviour
    {
        public ShipRole role = ShipRole.Carrier;
        public int housingCapacity = 100;
        public int serviceLevel = 3;
        public int facilitySlots = 4;
        public ShipStatus status = ShipStatus.ColdStorage;
    }

    public sealed class ShipAggregateBaker : Baker<ShipAggregateAuthoring>
    {
        public override void Bake(ShipAggregateAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new ShipAggregate
            {
                Role = authoring.role,
                HousingCapacity = authoring.housingCapacity,
                ServiceLevel = authoring.serviceLevel,
                FacilitySlots = authoring.facilitySlots
            });

            AddComponent(entity, new ShipOwnership
            {
                OwnerEntity = Entity.Null,
                Status = authoring.status
            });
        }
    }
}
#endif

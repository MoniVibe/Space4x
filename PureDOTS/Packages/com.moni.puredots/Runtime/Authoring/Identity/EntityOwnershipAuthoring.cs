using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    /// <summary>
    /// Assigns owner + membership/seat metadata for blank entities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityOwnershipAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private MembershipEntry[] memberships = Array.Empty<MembershipEntry>();
        [SerializeField] private SeatEntry[] seats = Array.Empty<SeatEntry>();

        [Serializable]
        public struct MembershipEntry
        {
            public GameObject Group;
            public string Role;
            [Range(0, 255)] public int Weight;
        }

        [Serializable]
        public struct SeatEntry
        {
            public string SeatId;
            [Range(1, 8)] public int Capacity;
            public GameObject Occupant;
        }

        private sealed class Baker : Baker<EntityOwnershipAuthoring>
        {
            public override void Bake(EntityOwnershipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (authoring.owner != null)
                {
                    AddComponent(entity, new EntityOwner
                    {
                        Value = GetEntity(authoring.owner, TransformUsageFlags.Dynamic)
                    });
                }

                if (authoring.memberships is { Length: > 0 })
                {
                    var buffer = AddBuffer<EntityMembership>(entity);
                    foreach (var entry in authoring.memberships)
                    {
                        if (entry.Group == null)
                        {
                            continue;
                        }

                        buffer.Add(new EntityMembership
                        {
                            Group = GetEntity(entry.Group, TransformUsageFlags.Dynamic),
                            Role = new FixedString64Bytes(entry.Role ?? string.Empty),
                            Weight = (byte)math.clamp(entry.Weight, 0, 255),
                            SinceTick = 0
                        });
                    }
                }

                if (authoring.seats is { Length: > 0 })
                {
                    var buffer = AddBuffer<EntitySeat>(entity);
                    foreach (var seat in authoring.seats)
                    {
                        if (string.IsNullOrWhiteSpace(seat.SeatId))
                        {
                            continue;
                        }

                        var entry = new EntitySeat
                        {
                            SeatId = new FixedString64Bytes(seat.SeatId.Trim()),
                            Capacity = (byte)math.clamp(seat.Capacity, 1, 8),
                            Occupant = seat.Occupant != null
                                ? GetEntity(seat.Occupant, TransformUsageFlags.Dynamic)
                                : Entity.Null
                        };
                        buffer.Add(entry);
                    }
                }
            }
        }
    }
}


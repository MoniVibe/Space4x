using Unity.Entities;

namespace PureDOTS.Runtime.IntergroupRelations
{
    // NOTE: OrgKind, OrgTag, and OrgId are defined in Runtime/IntergroupRelations/Components/OrgComponents.cs
    // Duplicate definitions removed to avoid conflicts

    /// <summary>
    /// Organization alignment (moral, order, purity axes).
    /// </summary>
    public struct OrgAlignment : IComponentData
    {
        public float Moral;    // -1 = Evil, 0 = Neutral, 1 = Good
        public float Order;    // -1 = Chaos, 0 = Neutral, 1 = Order
        public float Purity;   // -1 = Impure, 0 = Neutral, 1 = Pure
    }

    /// <summary>
    /// Organization outlook (primary and secondary traits).
    /// </summary>
    public struct OrgOutlook : IComponentData
    {
        public byte Primary;    // e.g. Warlike, Peaceful, Spiritual, Material
        public byte Secondary;
    }

    /// <summary>
    /// Reference to owning organization (on villages, carriers, etc.).
    /// </summary>
    public struct OwnerOrg : IComponentData
    {
        public Entity OrgEntity;
    }

    // NOTE: OrgRelation and OrgRelationTag are defined in Runtime/IntergroupRelations/Components/OrgComponents.cs
    // Duplicate definitions removed to avoid conflicts
}


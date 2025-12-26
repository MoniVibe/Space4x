using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Per-individual policy for allowing dire tactics (suicidal runs, extreme risk).
    /// </summary>
    public struct DireTacticsPolicy : IComponentData
    {
        public byte AllowKamikaze;
        public byte AllowExtremeOrders;

        public static DireTacticsPolicy Default => new DireTacticsPolicy
        {
            AllowKamikaze = 0,
            AllowExtremeOrders = 0
        };
    }

    /// <summary>
    /// Per-culture defaults for dire tactics permissions.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CultureDireTacticsPolicy : IBufferElementData
    {
        public ushort CultureId;
        public byte AllowKamikaze;
        public byte AllowExtremeOrders;
    }

    /// <summary>
    /// Singleton tag for the culture dire tactics catalog entity.
    /// </summary>
    public struct CultureDireTacticsPolicyCatalog : IComponentData
    {
    }
}

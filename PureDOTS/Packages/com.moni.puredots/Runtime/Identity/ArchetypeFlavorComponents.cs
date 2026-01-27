using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Derived identity flavor used for game-level theme mapping and presentation.
    /// Values are normalized to [-1..1] and intentionally agnostic to any specific theme.
    /// </summary>
    public struct ArchetypeFlavor : IComponentData
    {
        public float Order;
        public float Moral;
        public float Purity;

        public float Warlike;
        public float Authority;
        public float Materialism;
        public float Xenophobia;

        public float MightMagic;
        public float Cooperation;
        public float Vengeful;
        public float Bold;
    }
}

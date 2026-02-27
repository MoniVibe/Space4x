using Unity.Entities;

namespace Space4X.Runtime.Interaction
{
    /// <summary>
    /// Tuning knobs for Divine Hand pickup/throw access and influence radius.
    /// </summary>
    public struct Space4XDivineHandPolicy : IComponentData
    {
        public float BaseInfluenceRadius;
        public float BonusRadiusPerInfluencePoint;
        public float MaxInfluenceRadius;
        public float OutOfInfluenceGraceRadius;
        public byte AllowOwnedAnywhere;
        public byte AllowCelestialDirectPick;

        public static Space4XDivineHandPolicy CreateDefault()
        {
            return new Space4XDivineHandPolicy
            {
                BaseInfluenceRadius = 260f,
                BonusRadiusPerInfluencePoint = 0.8f,
                MaxInfluenceRadius = 1400f,
                OutOfInfluenceGraceRadius = 42f,
                AllowOwnedAnywhere = 1,
                AllowCelestialDirectPick = 1
            };
        }
    }
}

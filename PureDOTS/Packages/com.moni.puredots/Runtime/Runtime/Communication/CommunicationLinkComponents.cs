using Unity.Entities;

namespace PureDOTS.Runtime.Communication
{
    /// <summary>
    /// Scalar modifiers applied to link integrity (e.g., jamming/interference).
    /// </summary>
    public struct CommLinkQuality : IComponentData
    {
        public float IntegrityMultiplier;
        public float Interference;

        public static CommLinkQuality Default => new CommLinkQuality
        {
            IntegrityMultiplier = 1f,
            Interference = 0f
        };
    }

    /// <summary>
    /// Simple jammer source that degrades comm link integrity in a radius.
    /// </summary>
    public struct CommJammer : IComponentData
    {
        public float Radius;
        public float Strength;
        public byte IsActive;

        public static CommJammer Default => new CommJammer
        {
            Radius = 25f,
            Strength = 0.5f,
            IsActive = 1
        };
    }
}

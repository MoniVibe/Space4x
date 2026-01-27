using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Profile
{
    /// <summary>
    /// Cached, normalized profile inputs for fast decision checkpoints.
    /// Values are [0..1], where 0.5 is neutral.
    /// </summary>
    public struct ResolvedBehaviorProfile : IComponentData
    {
        public float Chaos01;
        public float Risk01;
        public float Obedience01;

        public static ResolvedBehaviorProfile Neutral => new ResolvedBehaviorProfile
        {
            Chaos01 = 0.5f,
            Risk01 = 0.5f,
            Obedience01 = 0.5f
        };

        public ResolvedBehaviorProfile Sanitized()
        {
            return new ResolvedBehaviorProfile
            {
                Chaos01 = math.saturate(Chaos01),
                Risk01 = math.saturate(Risk01),
                Obedience01 = math.saturate(Obedience01)
            };
        }
    }
}

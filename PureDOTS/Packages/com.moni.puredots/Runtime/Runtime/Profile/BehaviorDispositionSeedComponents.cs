using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Profile
{
    public struct BehaviorDispositionSeedRequest : IComponentData
    {
        public uint Seed;
        public uint SeedSalt;
    }

    public struct BehaviorDispositionSeedConfig : IComponentData
    {
        public BehaviorDispositionDistribution Distribution;
        public uint SeedSalt;

        public static BehaviorDispositionSeedConfig Default => new BehaviorDispositionSeedConfig
        {
            Distribution = BehaviorDispositionDistribution.Default,
            SeedSalt = 0u
        };
    }

    public struct BehaviorDispositionDistribution : IComponentData
    {
        public float2 Compliance;
        public float2 Caution;
        public float2 FormationAdherence;
        public float2 RiskTolerance;
        public float2 Aggression;
        public float2 Patience;

        public static BehaviorDispositionDistribution Default => new BehaviorDispositionDistribution
        {
            Compliance = new float2(0.35f, 0.65f),
            Caution = new float2(0.35f, 0.65f),
            FormationAdherence = new float2(0.35f, 0.65f),
            RiskTolerance = new float2(0.35f, 0.65f),
            Aggression = new float2(0.35f, 0.65f),
            Patience = new float2(0.35f, 0.65f)
        };

        public BehaviorDispositionDistribution Sanitize()
        {
            return new BehaviorDispositionDistribution
            {
                Compliance = NormalizeRange(Compliance),
                Caution = NormalizeRange(Caution),
                FormationAdherence = NormalizeRange(FormationAdherence),
                RiskTolerance = NormalizeRange(RiskTolerance),
                Aggression = NormalizeRange(Aggression),
                Patience = NormalizeRange(Patience)
            };
        }

        public BehaviorDisposition Sample(ref Random random)
        {
            var ranges = Sanitize();
            return BehaviorDisposition.FromValues(
                random.NextFloat(ranges.Compliance.x, ranges.Compliance.y),
                random.NextFloat(ranges.Caution.x, ranges.Caution.y),
                random.NextFloat(ranges.FormationAdherence.x, ranges.FormationAdherence.y),
                random.NextFloat(ranges.RiskTolerance.x, ranges.RiskTolerance.y),
                random.NextFloat(ranges.Aggression.x, ranges.Aggression.y),
                random.NextFloat(ranges.Patience.x, ranges.Patience.y));
        }

        private static float2 NormalizeRange(float2 range)
        {
            var min = math.clamp(range.x, 0f, 1f);
            var max = math.clamp(range.y, 0f, 1f);
            if (min > max)
            {
                (min, max) = (max, min);
            }
            return new float2(min, max);
        }
    }
}

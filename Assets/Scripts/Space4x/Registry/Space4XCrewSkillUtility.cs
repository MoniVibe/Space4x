using Unity.Mathematics;

namespace Space4X.Registry
{
    public static class Space4XSkillUtility
    {
        private const float XpCurve = 0.02f;

        public static float XpToSkill(float xp)
        {
            return math.saturate(1f - math.exp(-math.max(0f, xp) * XpCurve));
        }

        public static float SkillToXp(float skill)
        {
            var clamped = math.saturate(skill);
            if (clamped >= 0.999f)
            {
                return 500f; // Large value without inf
            }

            return clamped <= 0f ? 0f : -math.log(1f - clamped) / XpCurve;
        }

        public static float ComputeDeltaXp(SkillDomain domain, float amount)
        {
            var magnitude = math.max(0.1f, amount);
            return domain switch
            {
                SkillDomain.Hauling => magnitude * 0.07f,
                SkillDomain.Combat => magnitude * 0.12f,
                SkillDomain.Repair => magnitude * 0.08f,
                SkillDomain.Exploration => magnitude * 0.05f,
                _ => magnitude * 0.1f
            };
        }
    }
}

using Unity.Entities;

namespace PureDOTS.Runtime.Skills
{
    /// <summary>
    /// Data-driven modifiers that shape how quickly each experience pool advances.
    /// Values greater than 1 accelerate gains, values less than 1 slow them down.
    /// </summary>
    public struct SkillXpCurveConfig : IComponentData
    {
        public float PhysiqueCurve;
        public float FinesseCurve;
        public float WillCurve;
        public float GeneralCurve;

        public static SkillXpCurveConfig CreateDefaults()
        {
            return new SkillXpCurveConfig
            {
                PhysiqueCurve = 1f,
                FinesseCurve = 1f,
                WillCurve = 1f,
                GeneralCurve = 1f
            };
        }

        public readonly float GetScalar(XpPool pool)
        {
            return pool switch
            {
                XpPool.Physique => PhysiqueCurve,
                XpPool.Finesse => FinesseCurve,
                XpPool.Will => WillCurve,
                _ => GeneralCurve
            };
        }
    }
}

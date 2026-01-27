using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    /// <summary>
    /// Tuning for authority seat control claims emitted onto the authority body.
    /// </summary>
    public struct AuthorityControlClaimConfig : IComponentData
    {
        public float BasePressure;
        public float BaseLegitimacy;
        public float BaseHostility;
        public float BaseConsent;
        public float ExecutePressureBonus;
        public float OverridePressureBonus;
        public float ExecutiveLegitimacyBonus;
        public float ActingLegitimacyMultiplier;

        public static AuthorityControlClaimConfig Default => new AuthorityControlClaimConfig
        {
            BasePressure = 1.1f,
            BaseLegitimacy = 0.75f,
            BaseHostility = 0.05f,
            BaseConsent = 0.6f,
            ExecutePressureBonus = 0.35f,
            OverridePressureBonus = 0.6f,
            ExecutiveLegitimacyBonus = 0.1f,
            ActingLegitimacyMultiplier = 0.75f
        };
    }
}

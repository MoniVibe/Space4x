using PureDOTS.Runtime.Agency;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for authority seat control claim tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthorityControlClaimAuthoring : MonoBehaviour
    {
        [Range(0f, 3f)] public float basePressure = 1.1f;
        [Range(0f, 1f)] public float baseLegitimacy = 0.75f;
        [Range(0f, 1f)] public float baseHostility = 0.05f;
        [Range(0f, 1f)] public float baseConsent = 0.6f;
        [Range(0f, 2f)] public float executePressureBonus = 0.35f;
        [Range(0f, 2f)] public float overridePressureBonus = 0.6f;
        [Range(0f, 1f)] public float executiveLegitimacyBonus = 0.1f;
        [Range(0f, 1f)] public float actingLegitimacyMultiplier = 0.75f;

        private sealed class Baker : Baker<AuthorityControlClaimAuthoring>
        {
            public override void Bake(AuthorityControlClaimAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AuthorityControlClaimConfig
                {
                    BasePressure = Mathf.Max(0f, authoring.basePressure),
                    BaseLegitimacy = Mathf.Clamp01(authoring.baseLegitimacy),
                    BaseHostility = Mathf.Clamp01(authoring.baseHostility),
                    BaseConsent = Mathf.Clamp01(authoring.baseConsent),
                    ExecutePressureBonus = Mathf.Max(0f, authoring.executePressureBonus),
                    OverridePressureBonus = Mathf.Max(0f, authoring.overridePressureBonus),
                    ExecutiveLegitimacyBonus = Mathf.Max(0f, authoring.executiveLegitimacyBonus),
                    ActingLegitimacyMultiplier = Mathf.Clamp01(authoring.actingLegitimacyMultiplier)
                });
            }
        }
    }
}

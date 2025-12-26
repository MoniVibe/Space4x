using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Strike Craft Behavior Profile Config")]
    public sealed class Space4XStrikeCraftBehaviorProfileAuthoring : MonoBehaviour
    {
        [Header("Extreme Tactics Gates")]
        public bool allowKamikaze = false;
        public bool requireRewindEnabled = true;
        [Range(0, 10)] public int requireCombatTechTier = 0;
        public bool allowDirectiveDisobedience = true;
        public bool requireCaptainConsent = true;
        public bool requireCultureConsent = true;
        public bool defaultCaptainAllowsDireTactics = false;
        public bool defaultCultureAllowsDireTactics = false;

        [Header("Order Obedience")]
        [Range(0f, 1f)] public float obedienceThreshold = 0.5f;
        [Range(0f, 1f)] public float lawfulnessWeight = 0.35f;
        [Range(0f, 1f)] public float disciplineWeight = 0.35f;
        [Range(0f, 1f)] public float baseDisobeyChance = 0.1f;
        [Range(0f, 1f)] public float chaosDisobeyBonus = 0.4f;
        [Range(0f, 1f)] public float mutinyDisobeyBonus = 0.35f;

        [Header("Kamikaze Conditions")]
        [Range(0f, 1f)] public float kamikazePurityThreshold = 0.7f;
        [Range(0f, 1f)] public float kamikazeLawfulnessThreshold = 0.7f;
        [Range(0f, 1f)] public float kamikazeChaosThreshold = 0.7f;
        [Range(0f, 1f)] public float kamikazeHullThreshold = 0.35f;
        [Range(0f, 1f)] public float kamikazeChance = 0.25f;

        [Header("Kamikaze Motion")]
        [Range(0.5f, 2.5f)] public float kamikazeSpeedMultiplier = 1.35f;
        [Range(0.5f, 2.5f)] public float kamikazeTurnMultiplier = 1.2f;

        [Header("Kiting Behavior")]
        [Range(0f, 1f)] public float kitingMinExperience = 0.6f;
        [Range(0f, 1f)] public float kitingChance = 0.35f;
        [Min(0f)] public float kitingMinDistance = 15f;
        [Min(0f)] public float kitingMaxDistance = 80f;
        [Range(0f, 1f)] public float kitingStrafeStrength = 0.35f;

        public sealed class Baker : Baker<Space4XStrikeCraftBehaviorProfileAuthoring>
        {
            public override void Bake(Space4XStrikeCraftBehaviorProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StrikeCraftBehaviorProfileConfig
                {
                    AllowKamikaze = (byte)(authoring.allowKamikaze ? 1 : 0),
                    RequireRewindEnabled = (byte)(authoring.requireRewindEnabled ? 1 : 0),
                    RequireCombatTechTier = (byte)Mathf.Clamp(authoring.requireCombatTechTier, 0, 10),
                    AllowDirectiveDisobedience = (byte)(authoring.allowDirectiveDisobedience ? 1 : 0),
                    RequireCaptainConsent = (byte)(authoring.requireCaptainConsent ? 1 : 0),
                    RequireCultureConsent = (byte)(authoring.requireCultureConsent ? 1 : 0),
                    DefaultCaptainAllowsDireTactics = (byte)(authoring.defaultCaptainAllowsDireTactics ? 1 : 0),
                    DefaultCultureAllowsDireTactics = (byte)(authoring.defaultCultureAllowsDireTactics ? 1 : 0),
                    ObedienceThreshold = Mathf.Clamp01(authoring.obedienceThreshold),
                    LawfulnessWeight = Mathf.Clamp01(authoring.lawfulnessWeight),
                    DisciplineWeight = Mathf.Clamp01(authoring.disciplineWeight),
                    BaseDisobeyChance = Mathf.Clamp01(authoring.baseDisobeyChance),
                    ChaosDisobeyBonus = Mathf.Clamp01(authoring.chaosDisobeyBonus),
                    MutinyDisobeyBonus = Mathf.Clamp01(authoring.mutinyDisobeyBonus),
                    KamikazePurityThreshold = Mathf.Clamp01(authoring.kamikazePurityThreshold),
                    KamikazeLawfulnessThreshold = Mathf.Clamp01(authoring.kamikazeLawfulnessThreshold),
                    KamikazeChaosThreshold = Mathf.Clamp01(authoring.kamikazeChaosThreshold),
                    KamikazeHullThreshold = Mathf.Clamp01(authoring.kamikazeHullThreshold),
                    KamikazeChance = Mathf.Clamp01(authoring.kamikazeChance),
                    KamikazeSpeedMultiplier = Mathf.Clamp(authoring.kamikazeSpeedMultiplier, 0.5f, 2.5f),
                    KamikazeTurnMultiplier = Mathf.Clamp(authoring.kamikazeTurnMultiplier, 0.5f, 2.5f),
                    KitingMinExperience = Mathf.Clamp01(authoring.kitingMinExperience),
                    KitingChance = Mathf.Clamp01(authoring.kitingChance),
                    KitingMinDistance = Mathf.Max(0f, authoring.kitingMinDistance),
                    KitingMaxDistance = Mathf.Max(Mathf.Max(0f, authoring.kitingMaxDistance), authoring.kitingMinDistance),
                    KitingStrafeStrength = Mathf.Clamp01(authoring.kitingStrafeStrength)
                });
            }
        }
    }
}

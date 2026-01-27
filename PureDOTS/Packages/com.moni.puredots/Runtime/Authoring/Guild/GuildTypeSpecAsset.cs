#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Guild;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace PureDOTS.Authoring.Guild
{
    /// <summary>
    /// ScriptableObject for authoring individual guild type specifications.
    /// Defines recruitment rules, default governance, alignment preferences, and behaviors.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildTypeSpec", menuName = "PureDOTS/Guild/Guild Type Spec", order = 1)]
    public class GuildTypeSpecAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Type ID matching AggregateIdentity.TypeId. Must be unique.")]
        public ushort TypeId;

        [Tooltip("Label for this guild type (e.g., \"Heroes' Guild\", \"Merchants' League\").")]
        public string Label = "New Guild Type";

        [Header("Recruitment Rules")]
        [Tooltip("Rules defining which stats/achievements matter for recruitment.")]
        public List<RecruitmentRuleData> RecruitmentRules = new List<RecruitmentRuleData>();

        [Header("Default Governance")]
        [Tooltip("Default governance type for this guild type.")]
        public GuildLeadership.GovernanceType DefaultGovernance = GuildLeadership.GovernanceType.Democratic;

        [Header("Alignment Preferences")]
        [Tooltip("Minimum corrupt/pure alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MinCorruptPure = -100;

        [Tooltip("Maximum corrupt/pure alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MaxCorruptPure = 100;

        [Tooltip("Minimum chaotic/lawful alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MinChaoticLawful = -100;

        [Tooltip("Maximum chaotic/lawful alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MaxChaoticLawful = 100;

        [Tooltip("Minimum evil/good alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MinEvilGood = -100;

        [Tooltip("Maximum evil/good alignment (-100 to +100).")]
        [Range(-100, 100)]
        public sbyte MaxEvilGood = 100;

        [Header("Behaviors")]
        [Tooltip("Can this guild type declare strikes?")]
        public bool CanDeclareStrikes = false;

        [Tooltip("Can this guild type declare coups?")]
        public bool CanDeclareCoups = false;

        [Tooltip("Can this guild type declare war?")]
        public bool CanDeclareWar = false;

        [Tooltip("Is this guild type economic-only (no military actions)?")]
        public bool OnlyTrade = false;

        [Tooltip("Can this guild type open embassies?")]
        public bool CanOpenEmbassies = true;

        /// <summary>
        /// Converts this asset to a GuildTypeSpec for blob building.
        /// </summary>
        public GuildTypeSpec ToSpec(BlobBuilder builder, ref GuildTypeSpec spec)
        {
            spec.TypeId = TypeId;
            spec.Label = new Unity.Collections.FixedString64Bytes(Label);
            spec.DefaultGovernance = DefaultGovernance;
            spec.MinCorruptPure = MinCorruptPure;
            spec.MaxCorruptPure = MaxCorruptPure;
            spec.MinChaoticLawful = MinChaoticLawful;
            spec.MaxChaoticLawful = MaxChaoticLawful;
            spec.MinEvilGood = MinEvilGood;
            spec.MaxEvilGood = MaxEvilGood;
            spec.CanDeclareStrikes = CanDeclareStrikes ? (byte)1 : (byte)0;
            spec.CanDeclareCoups = CanDeclareCoups ? (byte)1 : (byte)0;
            spec.CanDeclareWar = CanDeclareWar ? (byte)1 : (byte)0;
            spec.OnlyTrade = OnlyTrade ? (byte)1 : (byte)0;
            spec.CanOpenEmbassies = CanOpenEmbassies ? (byte)1 : (byte)0;

            // Allocate recruitment rules blob array
            var rulesArray = builder.Allocate(ref spec.RecruitmentRules, RecruitmentRules.Count);
            for (int i = 0; i < RecruitmentRules.Count; i++)
            {
                rulesArray[i] = new RecruitmentRule
                {
                    StatType = RecruitmentRules[i].StatType,
                    Weight = RecruitmentRules[i].Weight,
                    Threshold = RecruitmentRules[i].Threshold
                };
            }

            return spec;
        }
    }

    /// <summary>
    /// Serializable data for a recruitment rule.
    /// </summary>
    [System.Serializable]
    public class RecruitmentRuleData
    {
        [Tooltip("Stat type enum index (SkillLevel, Achievement, Alignment, etc.).")]
        public byte StatType;

        [Tooltip("Weight for this rule in scoring.")]
        [Range(0f, 10f)]
        public float Weight = 1f;

        [Tooltip("Minimum value to consider.")]
        public float Threshold = 0f;
    }
}
#endif

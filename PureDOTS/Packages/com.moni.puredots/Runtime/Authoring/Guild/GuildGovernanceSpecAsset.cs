#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Guild;
using UnityEngine;

namespace PureDOTS.Authoring.Guild
{
    /// <summary>
    /// ScriptableObject for authoring individual governance specifications.
    /// Defines governance rules for a specific governance type.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildGovernanceSpec", menuName = "PureDOTS/Guild/Guild Governance Spec", order = 3)]
    public class GuildGovernanceSpecAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Governance type this spec applies to.")]
        public GuildLeadership.GovernanceType Type;

        [Header("Voting Rules")]
        [Tooltip("Required quorum percentage (0-100).")]
        [Range(0, 100)]
        public byte RequiresQuorum = 50;

        [Tooltip("Veto threshold percentage for oligarchic (0-100).")]
        [Range(0, 100)]
        public byte VetoThreshold = 30;

        [Header("Term Lengths")]
        [Tooltip("Leader term length in ticks.")]
        public uint LeaderTermLength = 10000;

        [Tooltip("Vote duration in ticks.")]
        public uint VoteDuration = 1000;

        [Header("Coup Thresholds")]
        [Tooltip("Support threshold for coup (0-1, fraction of members needed).")]
        [Range(0f, 1f)]
        public float CoupSupportThreshold = 0.5f;

        [Tooltip("Loyalty threshold for coup (0-1, loyalty level needed).")]
        [Range(0f, 1f)]
        public float CoupLoyaltyThreshold = 0.3f;

        /// <summary>
        /// Converts this asset to a GuildGovernanceSpec.
        /// </summary>
        public GuildGovernanceSpec ToSpec()
        {
            return new GuildGovernanceSpec
            {
                Type = Type,
                RequiresQuorum = RequiresQuorum,
                VetoThreshold = VetoThreshold,
                LeaderTermLength = LeaderTermLength,
                VoteDuration = VoteDuration,
                CoupSupportThreshold = CoupSupportThreshold,
                CoupLoyaltyThreshold = CoupLoyaltyThreshold
            };
        }
    }
}
#endif
























#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Guild;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace PureDOTS.Authoring.Guild
{
    /// <summary>
    /// ScriptableObject for authoring individual guild action specifications.
    /// Defines actions guilds can take (strike, riot, coup, declare war, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "GuildActionSpec", menuName = "PureDOTS/Guild/Guild Action Spec", order = 2)]
    public class GuildActionSpecAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique action ID.")]
        public ushort ActionId;

        [Tooltip("Label for this action (e.g., \"Strike\", \"Declare War\").")]
        public string Label = "New Action";

        [Header("Preconditions")]
        [Tooltip("Preconditions that must be met to execute this action.")]
        public List<ActionPreconditionData> Preconditions = new List<ActionPreconditionData>();

        [Header("Costs & Risks")]
        [Tooltip("Resource cost to execute this action.")]
        public float ResourceCost = 0f;

        [Tooltip("Risk level (0-1).")]
        [Range(0f, 1f)]
        public float RiskLevel = 0.5f;

        [Tooltip("Reputation impact (-1 to +1).")]
        [Range(-1f, 1f)]
        public float ReputationImpact = 0f;

        [Header("AI Strategy Tags")]
        [Tooltip("Is this a defensive action?")]
        public bool IsDefensive = false;

        [Tooltip("Is this an expansion action?")]
        public bool IsExpansion = false;

        [Tooltip("Is this an ideological action?")]
        public bool IsIdeological = false;

        [Tooltip("Is this an economic action?")]
        public bool IsEconomic = false;

        /// <summary>
        /// Converts this asset to a GuildActionSpec for blob building.
        /// </summary>
        public GuildActionSpec ToSpec(BlobBuilder builder, ref GuildActionSpec spec)
        {
            spec.ActionId = ActionId;
            spec.Label = new Unity.Collections.FixedString64Bytes(Label);
            spec.ResourceCost = ResourceCost;
            spec.RiskLevel = RiskLevel;
            spec.ReputationImpact = ReputationImpact;
            spec.IsDefensive = IsDefensive ? (byte)1 : (byte)0;
            spec.IsExpansion = IsExpansion ? (byte)1 : (byte)0;
            spec.IsIdeological = IsIdeological ? (byte)1 : (byte)0;
            spec.IsEconomic = IsEconomic ? (byte)1 : (byte)0;

            // Allocate preconditions blob array
            var preconditionsArray = builder.Allocate(ref spec.Preconditions, Preconditions.Count);
            for (int i = 0; i < Preconditions.Count; i++)
            {
                preconditionsArray[i] = new ActionPrecondition
                {
                    ConditionType = Preconditions[i].ConditionType,
                    Operator = Preconditions[i].Operator,
                    Value = Preconditions[i].Value
                };
            }

            return spec;
        }
    }

    /// <summary>
    /// Serializable data for an action precondition.
    /// </summary>
    [System.Serializable]
    public class ActionPreconditionData
    {
        [Tooltip("Condition type enum index (Alignment, Governance, Relation, CrisisTag).")]
        public byte ConditionType;

        [Tooltip("Operator enum index (Equals, GreaterThan, LessThan, HasFlag).")]
        public byte Operator;

        [Tooltip("Value to compare against.")]
        public float Value = 0f;
    }
}
#endif


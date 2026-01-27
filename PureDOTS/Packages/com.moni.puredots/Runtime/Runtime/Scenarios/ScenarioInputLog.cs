using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Per-tick log entry for agent decisions and player inputs.
    /// Used for deterministic replay validation.
    /// </summary>
    public struct ScenarioInputLogEntry : IBufferElementData
    {
        public uint Tick;
        public FixedString64Bytes AgentEntityId;  // Entity identifier or "player"
        public FixedString64Bytes DecisionType;   // "goap_action", "utility_choice", "player_input", etc.
        public FixedString128Bytes DecisionData;  // JSON or structured data about the decision
        public uint Hash;                          // Hash of decision for quick comparison
    }

    /// <summary>
    /// Input log buffer attached to scenario entity.
    /// Records all decisions made during scenario execution.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ScenarioInputLog : IBufferElementData
    {
        public ScenarioInputLogEntry Entry;
    }

    /// <summary>
    /// Helper for computing decision hashes for deterministic validation.
    /// </summary>
    public static class ScenarioInputLogHelper
    {
        public static uint ComputeHash(FixedString64Bytes agentId, FixedString64Bytes decisionType, FixedString128Bytes decisionData)
        {
            // Simple hash combining all fields
            uint hash = 0;
            hash = hash * 31 + (uint)agentId.GetHashCode();
            hash = hash * 31 + (uint)decisionType.GetHashCode();
            hash = hash * 31 + (uint)decisionData.GetHashCode();
            return hash;
        }

        public static ScenarioInputLogEntry CreateEntry(uint tick, FixedString64Bytes agentId, FixedString64Bytes decisionType, FixedString128Bytes decisionData)
        {
            return new ScenarioInputLogEntry
            {
                Tick = tick,
                AgentEntityId = agentId,
                DecisionType = decisionType,
                DecisionData = decisionData,
                Hash = ComputeHash(agentId, decisionType, decisionData)
            };
        }
    }
}




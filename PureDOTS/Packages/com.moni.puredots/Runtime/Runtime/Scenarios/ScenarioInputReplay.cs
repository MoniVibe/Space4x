using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Result of replay validation - compares recorded decisions with replayed decisions.
    /// </summary>
    public struct ScenarioReplayValidationResult
    {
        public bool IsValid;
        public int MismatchCount;
        public NativeList<FixedString128Bytes> MismatchMessages;
    }

    /// <summary>
    /// Helper for validating deterministic replay by comparing input logs.
    /// </summary>
    public static class ScenarioInputReplay
    {
        /// <summary>
        /// Validates that replayed decisions match recorded decisions.
        /// </summary>
        public static ScenarioReplayValidationResult ValidateReplay(
            in NativeList<ScenarioInputLogEntry> recordedLog,
            in NativeList<ScenarioInputLogEntry> replayedLog,
            Allocator allocator)
        {
            var result = new ScenarioReplayValidationResult
            {
                IsValid = true,
                MismatchCount = 0,
                MismatchMessages = new NativeList<FixedString128Bytes>(16, allocator)
            };

            if (recordedLog.Length != replayedLog.Length)
            {
                result.IsValid = false;
                result.MismatchCount = 1;
                result.MismatchMessages.Add(new FixedString128Bytes(
                    $"Log length mismatch: recorded={recordedLog.Length}, replayed={replayedLog.Length}"
                ));
                return result;
            }

            for (int i = 0; i < recordedLog.Length; i++)
            {
                var recorded = recordedLog[i];
                var replayed = replayedLog[i];

                if (recorded.Tick != replayed.Tick ||
                    !recorded.AgentEntityId.Equals(replayed.AgentEntityId) ||
                    !recorded.DecisionType.Equals(replayed.DecisionType) ||
                    recorded.Hash != replayed.Hash)
                {
                    result.IsValid = false;
                    result.MismatchCount++;
                    result.MismatchMessages.Add(new FixedString128Bytes(
                        $"Mismatch at index {i}, tick {recorded.Tick}: recorded_hash={recorded.Hash}, replayed_hash={replayed.Hash}"
                    ));
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts input log entries from scenario entity buffer.
        /// </summary>
        public static void ExtractLogEntries(
            DynamicBuffer<ScenarioInputLog> buffer,
            NativeList<ScenarioInputLogEntry> output)
        {
            output.Clear();
            for (int i = 0; i < buffer.Length; i++)
            {
                output.Add(buffer[i].Entry);
            }
        }
    }
}




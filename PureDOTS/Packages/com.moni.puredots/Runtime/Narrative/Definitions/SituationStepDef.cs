using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Step definition within a situation state machine.
    /// </summary>
    public struct SituationStepDef
    {
        public int StepIndex;
        public SituationStepKind Kind;
        public float MinDuration;      // simulation time units
        public float MaxDuration;
        public BlobArray<NarrativeEventDef> InlineEvents;     // logs, prompts, etc.
    }
}


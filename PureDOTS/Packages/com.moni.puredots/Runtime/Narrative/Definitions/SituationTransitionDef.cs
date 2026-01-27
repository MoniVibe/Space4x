using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Transition definition between situation steps.
    /// </summary>
    public struct SituationTransitionDef
    {
        public int FromStepIndex;
        public int ToStepIndex;
        public BlobArray<NarrativeEventConditionDef> Conditions;
        public BlobArray<NarrativeEventEffectDef> Effects;    // applied on transition
        public float Weight;                                   // for random branch selection
    }
}


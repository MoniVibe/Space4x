using Unity.Collections.LowLevel.Unsafe;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Condition definition for narrative events and situation transitions.
    /// ConditionType determines the condition logic; ParamA/ParamB are condition-specific.
    /// </summary>
    public struct NarrativeEventConditionDef
    {
        public int ConditionType;   // enum: HasResource, FactionOpinion, RandomRoll, TimeInRegion, etc.
        public int ParamA;
        public int ParamB;
    }
}


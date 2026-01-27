namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Effect definition for narrative events and situation transitions.
    /// EffectType determines the effect logic; ParamA/ParamB are effect-specific.
    /// </summary>
    public struct NarrativeEventEffectDef
    {
        public int EffectType;      // enum: AddResource, StartSituation, SetFlag, ModifyOpinion, etc.
        public int ParamA;
        public int ParamB;
    }
}


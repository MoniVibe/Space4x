using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Runtime state of an active situation instance.
    /// Each running situation gets its own controller entity with this component.
    /// </summary>
    public struct SituationInstance : IComponentData
    {
        public NarrativeId SituationId;
        public SituationPhase Phase;
        public int StepIndex;
        public double NextEvaluationTime;     // world time in your time system
        public double StartedAt;
        public double LastStepChangeAt;
        public byte IsBackground;             // 0 = foreground, 1 = background
    }
}


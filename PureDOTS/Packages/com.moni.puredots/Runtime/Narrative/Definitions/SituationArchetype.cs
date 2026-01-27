using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Static definition of a situation archetype (state machine graph).
    /// Stored in blob assets for efficient runtime access.
    /// </summary>
    public struct SituationArchetype
    {
        public NarrativeId SituationId;
        public NarrativeTagMask Tags;     // Hostage, CivilWar, LabAccident, etc.
        public BlobArray<SituationRoleDef> Roles;
        public BlobArray<SituationStepDef> Steps;
        public BlobArray<SituationTransitionDef> Transitions;
    }
}


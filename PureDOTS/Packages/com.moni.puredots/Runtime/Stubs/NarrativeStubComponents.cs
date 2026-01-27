// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    public struct SituationId : IComponentData
    {
        public int Value;
    }

    public struct NarrativeEventTicket : IComponentData
    {
        public int EventId;
        public uint RaisedTick;
    }

    public struct DialogueChoice : IBufferElementData
    {
        public FixedString64Bytes ChoiceId;
        public byte Outcome;
    }
}

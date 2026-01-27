using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Static definition of a one-shot narrative event.
    /// Stored in blob assets for efficient runtime access.
    /// </summary>
    public struct NarrativeEventDef
    {
        public NarrativeId Id;
        public NarrativeTagMask Tags;
        public NarrativeEventDeliveryKind DeliveryKind;

        public BlobArray<NarrativeEventConditionDef> Conditions;
        public BlobArray<NarrativeEventEffectDef> Effects;

        // Content pointers: resolved by game-side
        public int TitleKey;    // localization/content key
        public int BodyKey;
    }
}


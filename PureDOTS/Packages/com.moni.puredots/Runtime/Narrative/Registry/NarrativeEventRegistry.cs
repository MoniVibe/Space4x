#nullable disable
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Registry blob containing all narrative event definitions.
    /// </summary>
    public struct NarrativeEventRegistry
    {
        public BlobArray<NarrativeEventDef> Events;
    }
}


using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Per-storehouse configuration for ledger events / comm notifications.
    /// </summary>
    public struct StorehouseLedgerSettings : IComponentData
    {
        /// <summary>Minimum units withdrawn to record an event.</summary>
        public float EventThresholdUnits;

        /// <summary>Whether to emit comms acknowledgements.</summary>
        public byte EmitComms;

        /// <summary>Optional entity to receive comms (e.g., village authority).</summary>
        public Entity NotifyTarget;

        public static StorehouseLedgerSettings Default => new StorehouseLedgerSettings
        {
            EventThresholdUnits = 5f,
            EmitComms = 1,
            NotifyTarget = Entity.Null
        };
    }

    /// <summary>
    /// Ledger record for significant withdrawals.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct StorehouseLedgerEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Actor;
        public ushort ResourceTypeIndex;
        public float Amount;
        public Entity Destination; // e.g., construction site
    }
}






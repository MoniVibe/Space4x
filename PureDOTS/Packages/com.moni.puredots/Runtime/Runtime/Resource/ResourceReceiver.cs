using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    public enum ResourceReceiverType : byte
    {
        Storehouse = 0,
        Construction = 1,
        GroundPile = 2
    }

    public struct ResourceReceiver : IComponentData
    {
        public ResourceReceiverType Type;
        public ushort ResourceTypeIndex; // Optional per-receiver type (e.g., construction input)
        public float Capacity;
        public float Stored;
    }
}




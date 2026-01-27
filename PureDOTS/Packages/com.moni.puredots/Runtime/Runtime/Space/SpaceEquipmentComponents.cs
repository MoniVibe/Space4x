using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public enum SpaceEquipmentType : byte
    {
        Weapon,
        Engine,
        Shield,
        Armor,
        Sensor,
        LifeSupport,
        FireControl,
        Cockpit,
        Cooling,
        Reactor,
        Capacitor,
        Computer,
        Facility,
        Special
    }

    public struct SpaceEquipmentDefinitionData
    {
        public FixedString64Bytes EquipmentId;
        public SpaceEquipmentType Type;
        public float Mass;
        public float PowerDraw;
        public float HeatGeneration;
        public FixedString64Bytes DisplayName;
    }

    public struct SpaceVesselCapacity : IComponentData
    {
        public float BaseMassCapacity;
        public float OverCapacityPercent;
        public float CurrentMass;
    }

    public struct SpaceVesselLoadoutEntry : IBufferElementData
    {
        public FixedString64Bytes EquipmentId;
        public SpaceEquipmentType Type;
        public float Mass;
    }
}

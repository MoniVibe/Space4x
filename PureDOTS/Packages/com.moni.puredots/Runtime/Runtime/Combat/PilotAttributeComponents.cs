using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    public struct PilotAttributes : IComponentData
    {
        public float Intelligence;
        public float Finesse;
        public float Perception;
    }

    public struct InstrumentTechLevel : IComponentData
    {
        public float TechLevel;
    }
}

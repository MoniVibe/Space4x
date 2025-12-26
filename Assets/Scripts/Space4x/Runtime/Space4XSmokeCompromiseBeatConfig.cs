using PureDOTS.Runtime.Agency;
using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XSmokeCompromiseBeatConfig : IComponentData
    {
        public float CommsDropStartSeconds;
        public float CommsDropDurationSeconds;
        public float CommsQualityDuringDrop;
        public float ControllerCompromiseSeconds;
        public byte ControllerCompromiseSeverity;
        public CompromiseKind ControllerCompromiseKind;
        public float HackStartSeconds;
        public byte HackDroneCount;
        public byte HackSeverity;

        public byte Initialized;
        public byte HackApplied;
        public byte CompromiseApplied;
        public byte CommsDropApplied;

        public uint CommsDropStartTick;
        public uint CommsDropEndTick;
        public uint ControllerCompromiseTick;
        public uint HackStartTick;
        public Entity HackerEntity;
    }

    public struct Space4XSmokeHackerTag : IComponentData { }
}

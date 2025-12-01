using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Runtime state singleton for demo controls (pause, time scale, rewind).
    /// </summary>
    public struct DemoBootstrapState : IComponentData
    {
        public byte Paused;
        public float TimeScale;
        public byte RewindEnabled;
        public uint RngSeed;
    }
}


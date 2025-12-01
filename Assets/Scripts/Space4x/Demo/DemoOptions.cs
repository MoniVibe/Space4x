using Unity.Collections;
using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Core configuration component loaded at boot from UI or CLI.
    /// Read-only in Burst code, mutated only by non-Burst input handler.
    /// </summary>
    public struct DemoOptions : IComponentData
    {
        public FixedString64Bytes ScenarioPath;
        public byte BindingsSet; // 0=Minimal, 1=Fancy
        public byte Veteran;     // 0/1
    }
}


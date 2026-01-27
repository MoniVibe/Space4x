using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Headless time control proof result snapshot for bank reporting.
    /// </summary>
    public struct HeadlessTimeControlProofState : IComponentData
    {
        public byte Result; // 0 = none, 1 = pass, 2 = fail
        public uint Tick;
    }
}

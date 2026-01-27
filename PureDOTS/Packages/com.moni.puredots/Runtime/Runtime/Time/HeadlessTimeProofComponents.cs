using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Minimal probe used by headless time control proofs.
    /// </summary>
    public struct HeadlessTimeProofProbe : IComponentData
    {
        public float Accumulated;
        public float LastDelta;
        public int UpdateCount;
    }
}

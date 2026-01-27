// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Persistence
{
    public static class SaveLoadServiceStub
    {
        public static SnapshotHandle RequestSave(ref SystemState state) => default;

        public static void RequestLoad(SnapshotHandle handle) { }
    }
}

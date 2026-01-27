using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Physics
{
    public struct PhysicsColliderProfileSource
    {
        public ushort RenderSemanticKey;
        public PhysicsColliderSpec Spec;
    }

    public struct PhysicsColliderProfileBuildInput
    {
        public PhysicsColliderProfileSource[] Entries;
    }

    public static class PhysicsColliderProfileBuilder
    {
        public static bool TryBuild(in PhysicsColliderProfileBuildInput input, Allocator allocator, out BlobAssetReference<PhysicsColliderProfileBlob> blob)
        {
            blob = default;
            var entries = input.Entries;
            if (entries == null || entries.Length == 0)
            {
                return false;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsColliderProfileBlob>();
            var blobEntries = builder.Allocate(ref root.Entries, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                blobEntries[i] = new PhysicsColliderProfileEntry
                {
                    RenderSemanticKey = entries[i].RenderSemanticKey,
                    Spec = entries[i].Spec
                };
            }

            blob = builder.CreateBlobAssetReference<PhysicsColliderProfileBlob>(allocator);
            builder.Dispose();
            return true;
        }
    }
}

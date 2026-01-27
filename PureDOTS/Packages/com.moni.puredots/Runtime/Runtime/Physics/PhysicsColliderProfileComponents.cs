using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    public enum PhysicsColliderShape : byte
    {
        Sphere = 0,
        Capsule = 1,
        Box = 2
    }

    /// <summary>
    /// Describes a kinematic collider shape used for ECS-authoritative sweep tests.
    /// Dimensions are interpreted per shape:
    /// - Sphere: Dimensions.x = radius
    /// - Capsule: Dimensions.x = radius, Dimensions.y = height
    /// - Box: Dimensions = full size (not half extents)
    /// </summary>
    public struct PhysicsColliderSpec : IComponentData
    {
        public PhysicsColliderShape Shape;
        public float3 Dimensions;
        public PhysicsInteractionFlags Flags;
        public byte IsTrigger;
        public byte UseCustomFilter;
        public CollisionFilter CustomFilter;

        public static PhysicsColliderSpec CreateSphere(float radius, PhysicsInteractionFlags flags)
        {
            return new PhysicsColliderSpec
            {
                Shape = PhysicsColliderShape.Sphere,
                Dimensions = new float3(radius, 0f, 0f),
                Flags = flags,
                IsTrigger = 0,
                UseCustomFilter = 0,
                CustomFilter = default
            };
        }
    }

    /// <summary>
    /// Singleton component referencing the baked collider profile blob.
    /// </summary>
    public struct PhysicsColliderProfileComponent : IComponentData
    {
        public BlobAssetReference<PhysicsColliderProfileBlob> Profile;
    }

    public struct PhysicsColliderProfileBlob
    {
        public BlobArray<PhysicsColliderProfileEntry> Entries;
    }

    public struct PhysicsColliderProfileEntry
    {
        public ushort RenderSemanticKey;
        public PhysicsColliderSpec Spec;
    }

    public static class PhysicsColliderProfileHelpers
    {
        public static bool TryGetSpec(ref BlobArray<PhysicsColliderProfileEntry> entries, ushort renderSemanticKey, out PhysicsColliderSpec spec)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].RenderSemanticKey == renderSemanticKey)
                {
                    spec = entries[i].Spec;
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}

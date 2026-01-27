using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace PureDOTS.Runtime.Physics
{
    [CreateAssetMenu(menuName = "PureDOTS/Physics/Collider Profile", fileName = "PhysicsColliderProfile")]
    public class PhysicsColliderProfileDefinition : ScriptableObject
    {
        [Serializable]
        public struct CollisionFilterDefinition
        {
            public int BelongsTo;
            public int CollidesWith;
            public int GroupIndex;

            public CollisionFilter ToFilter()
            {
                return new CollisionFilter
                {
                    BelongsTo = (uint)BelongsTo,
                    CollidesWith = (uint)CollidesWith,
                    GroupIndex = GroupIndex
                };
            }
        }

        [Serializable]
        public struct Entry
        {
            public ushort RenderSemanticKey;
            public PhysicsColliderShape Shape;
            public Vector3 Dimensions;
            public bool IsTrigger;
            public PhysicsInteractionFlags InteractionFlags;
            public bool UseCustomFilter;
            public CollisionFilterDefinition Filter;
        }

        public List<Entry> Entries = new();

        public PhysicsColliderProfileBuildInput ToBuildInput()
        {
            var entries = new PhysicsColliderProfileSource[Entries?.Count ?? 0];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = Entries[i];
                var spec = new PhysicsColliderSpec
                {
                    Shape = entry.Shape,
                    Dimensions = (float3)entry.Dimensions,
                    Flags = entry.InteractionFlags,
                    IsTrigger = entry.IsTrigger ? (byte)1 : (byte)0,
                    UseCustomFilter = entry.UseCustomFilter ? (byte)1 : (byte)0,
                    CustomFilter = entry.Filter.ToFilter()
                };

                entries[i] = new PhysicsColliderProfileSource
                {
                    RenderSemanticKey = entry.RenderSemanticKey,
                    Spec = spec
                };
            }

            return new PhysicsColliderProfileBuildInput
            {
                Entries = entries
            };
        }
    }
}

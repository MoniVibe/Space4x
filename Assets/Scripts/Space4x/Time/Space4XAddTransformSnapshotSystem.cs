using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rewind;
using UnityEngine;

namespace Space4X
{
    /// <summary>
    /// Adds TransformSnapshot buffers to all motion entities (LocalTransform) in Default world.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XAddTransformSnapshotSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (lt, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>>()
                         .WithEntityAccess()
                         .WithNone<TransformSnapshot>())
            {
                // Ensures snapshot buffer exists for any entity with a transform.
                ecb.AddBuffer<TransformSnapshot>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // One-shot is sufficient; future entities should be baked with buffers.
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}

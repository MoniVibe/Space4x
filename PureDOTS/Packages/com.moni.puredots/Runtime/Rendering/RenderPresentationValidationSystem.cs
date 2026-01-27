#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Dev-only guard that reports missing presentation components once per archetype.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial struct RenderPresentationValidationSystem : ISystem
    {
        private EntityQuery _missingSemanticQuery;
        private EntityQuery _missingPresenterQuery;
        private NativeParallelHashSet<ulong> _reportedSemantic;
        private NativeParallelHashSet<ulong> _reportedPresenter;
        private EntityTypeHandle _entityTypeHandle;

        public void OnCreate(ref SystemState state)
        {
            _missingSemanticQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MeshPresenter>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                }
            });

            _missingPresenterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<MeshPresenter>(),
                    ComponentType.ReadOnly<SpritePresenter>(),
                    ComponentType.ReadOnly<DebugPresenter>(),
                    ComponentType.ReadOnly<TracerPresenter>()
                }
            });

            _reportedSemantic = new NativeParallelHashSet<ulong>(64, Allocator.Persistent);
            _reportedPresenter = new NativeParallelHashSet<ulong>(64, Allocator.Persistent);
            _entityTypeHandle = state.GetEntityTypeHandle();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_reportedSemantic.IsCreated)
                _reportedSemantic.Dispose();
            if (_reportedPresenter.IsCreated)
                _reportedPresenter.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PresentationReady>())
            {
                return;
            }

            _entityTypeHandle.Update(ref state);
            ReportOnce(ref state, _missingSemanticQuery, ref _reportedSemantic,
                "[RenderPresentationValidation] Entity is missing RenderSemanticKey but has a presenter component.");

            ReportOnce(ref state, _missingPresenterQuery, ref _reportedPresenter,
                "[RenderPresentationValidation] Entity has RenderSemanticKey but no presenter component (Mesh/Sprite/Tracer/Debug).");
        }

        private void ReportOnce(
            ref SystemState state,
            EntityQuery query,
            ref NativeParallelHashSet<ulong> reportedSet,
            string message)
        {
            if (query.IsEmptyIgnoreFilter)
                return;

            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);

            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                var entities = chunk.GetNativeArray(_entityTypeHandle);
                if (entities.Length == 0)
                    continue;

                var key = ComputeKey(entities[0], chunkIndex);
                if (reportedSet.Contains(key))
                    continue;

                reportedSet.Add(key);
                Debug.LogError($"{message} Example entity: {entities[0]}");
            }
        }

        private static ulong ComputeKey(Entity entity, int chunkIndex)
        {
            var index = (ulong)(uint)entity.Index;
            var version = (ulong)(uint)entity.Version;
            var chunk = (ulong)(uint)chunkIndex;
            return (index << 32) ^ version ^ (chunk << 16);
        }
    }
}
#endif

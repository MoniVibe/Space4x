using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    public static class HeadlessRewindProofUtility
    {
        public const byte DefaultRequiredMask = (byte)(HeadlessRewindProofStage.Playback | HeadlessRewindProofStage.RecordReturn);

        public static bool TryGetPhase(EntityManager entityManager, out HeadlessRewindProofPhase phase)
        {
            if (!TryGetProofEntity(entityManager, out var entity, out var resolvedEntityManager))
            {
                phase = default;
                return false;
            }

            phase = resolvedEntityManager.GetComponentData<HeadlessRewindProofState>(entity).Phase;
            return true;
        }

        public static bool TryGetState(EntityManager entityManager, out HeadlessRewindProofState state)
        {
            if (!TryGetProofEntity(entityManager, out var entity, out var resolvedEntityManager))
            {
                state = default;
                return false;
            }

            state = resolvedEntityManager.GetComponentData<HeadlessRewindProofState>(entity);
            return true;
        }

        public static bool TryEnsureSubject(EntityManager entityManager, FixedString64Bytes proofId, byte requiredMask = DefaultRequiredMask)
        {
            if (!TryGetProofEntity(entityManager, out var entity, out var resolvedEntityManager))
            {
                return false;
            }

            if (!resolvedEntityManager.HasBuffer<HeadlessRewindProofSubject>(entity))
            {
                resolvedEntityManager.AddBuffer<HeadlessRewindProofSubject>(entity);
            }

            var buffer = resolvedEntityManager.GetBuffer<HeadlessRewindProofSubject>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ProofId.Equals(proofId))
                {
                    return true;
                }
            }

            buffer.Add(new HeadlessRewindProofSubject
            {
                ProofId = proofId,
                RequiredMask = requiredMask,
                ObservedMask = 0,
                Result = 0,
                Observed = 0f,
                Expected = default
            });
            return true;
        }

        public static bool TryMarkObserved(EntityManager entityManager, FixedString64Bytes proofId, HeadlessRewindProofStage stage, byte requiredMask = DefaultRequiredMask)
        {
            if (!TryGetProofEntity(entityManager, out var entity, out var resolvedEntityManager))
            {
                return false;
            }

            if (!resolvedEntityManager.HasBuffer<HeadlessRewindProofSubject>(entity))
            {
                resolvedEntityManager.AddBuffer<HeadlessRewindProofSubject>(entity);
            }

            var buffer = resolvedEntityManager.GetBuffer<HeadlessRewindProofSubject>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].ProofId.Equals(proofId))
                {
                    continue;
                }

                var entry = buffer[i];
                entry.RequiredMask = requiredMask;
                entry.ObservedMask = (byte)(entry.ObservedMask | (byte)stage);
                buffer[i] = entry;
                return true;
            }

            buffer.Add(new HeadlessRewindProofSubject
            {
                ProofId = proofId,
                RequiredMask = requiredMask,
                ObservedMask = (byte)stage,
                Result = 0,
                Observed = 0f,
                Expected = default
            });
            return true;
        }

        public static bool TryMarkResult(EntityManager entityManager, FixedString64Bytes proofId, bool success, float observed = 0f, in FixedString32Bytes expected = default, byte requiredMask = DefaultRequiredMask)
        {
            if (!TryGetProofEntity(entityManager, out var entity, out var resolvedEntityManager))
            {
                return false;
            }

            if (!resolvedEntityManager.HasBuffer<HeadlessRewindProofSubject>(entity))
            {
                resolvedEntityManager.AddBuffer<HeadlessRewindProofSubject>(entity);
            }

            var buffer = resolvedEntityManager.GetBuffer<HeadlessRewindProofSubject>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].ProofId.Equals(proofId))
                {
                    continue;
                }

                var entry = buffer[i];
                entry.RequiredMask = requiredMask;
                entry.Result = (byte)(success ? 1 : 2);
                entry.Observed = observed;
                entry.Expected = expected;
                buffer[i] = entry;
                return true;
            }

            buffer.Add(new HeadlessRewindProofSubject
            {
                ProofId = proofId,
                RequiredMask = requiredMask,
                ObservedMask = 0,
                Result = (byte)(success ? 1 : 2),
                Observed = observed,
                Expected = expected
            });
            return true;
        }

        private static bool TryGetProofEntity(EntityManager entityManager, out Entity entity, out EntityManager resolvedEntityManager)
        {
            if (TryGetProofEntityIn(entityManager, out entity))
            {
                resolvedEntityManager = entityManager;
                return true;
            }

            foreach (var world in World.All)
            {
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var candidateManager = world.EntityManager;
                if (TryGetProofEntityIn(candidateManager, out entity))
                {
                    resolvedEntityManager = candidateManager;
                    return true;
                }
            }

            entity = Entity.Null;
            resolvedEntityManager = default;
            return false;
        }

        private static bool TryGetProofEntityIn(EntityManager entityManager, out Entity entity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HeadlessRewindProofState>());
            if (query.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            var count = query.CalculateEntityCount();
            if (count == 1)
            {
                entity = query.GetSingletonEntity();
                return true;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            entity = entities.Length > 0 ? entities[0] : Entity.Null;
            return entity != Entity.Null;
        }
    }
}

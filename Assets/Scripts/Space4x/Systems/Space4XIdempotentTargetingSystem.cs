using System;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems
{
    /// <summary>
    /// Assigns stable per-entity callsigns for targeting/readout surfaces.
    /// Writes only once per entity and safely skips archetypes that cannot grow.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSelectionBootstrapSystem))]
    public partial struct Space4XEntityCallsignBootstrapSystem : ISystem
    {
        private EntityQuery _candidateWithoutCallsignQuery;
        private NativeParallelHashSet<Entity> _overflowEntities;

        public void OnCreate(ref SystemState state)
        {
            _candidateWithoutCallsignQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<LocalTransform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<PlayerFlagshipTag>(),
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<Asteroid>(),
                    ComponentType.ReadOnly<VesselMovement>()
                },
                None = new[] { ComponentType.ReadOnly<Space4XEntityCallsign>() }
            });

            _overflowEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_overflowEntities.IsCreated)
            {
                _overflowEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            if (!_candidateWithoutCallsignQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _candidateWithoutCallsignQuery.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (entity == Entity.Null || !entityManager.Exists(entity) || _overflowEntities.Contains(entity))
                    {
                        continue;
                    }

                    var stableHash = ComputeStableHash(entity);
                    var callsign = new Space4XEntityCallsign
                    {
                        Value = BuildCallsignToken(entityManager, entity, stableHash),
                        StableHash = stableHash
                    };

                    try
                    {
                        entityManager.AddComponentData(entity, callsign);
                    }
                    catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
                    {
                        _overflowEntities.Add(entity);
                    }
                }
            }

            foreach (var (callsignRef, entity) in SystemAPI.Query<RefRW<Space4XEntityCallsign>>().WithEntityAccess())
            {
                if (callsignRef.ValueRO.Value.Length > 0)
                {
                    continue;
                }

                var stableHash = ComputeStableHash(entity);
                callsignRef.ValueRW = new Space4XEntityCallsign
                {
                    Value = BuildCallsignToken(entityManager, entity, stableHash),
                    StableHash = stableHash
                };
            }
        }

        private static FixedString64Bytes BuildCallsignToken(EntityManager entityManager, Entity entity, uint stableHash)
        {
            var prefix = ResolveCallsignPrefix(entityManager, entity);
            var serial = stableHash % 10000u;
            if (entityManager.HasComponent<ScenarioSide>(entity))
            {
                var side = entityManager.GetComponentData<ScenarioSide>(entity).Side;
                return new FixedString64Bytes($"{prefix}-S{side}-{serial:0000}");
            }

            return new FixedString64Bytes($"{prefix}-{serial:0000}");
        }

        private static string ResolveCallsignPrefix(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<PlayerFlagshipTag>(entity))
            {
                return "FLAG";
            }

            if (entityManager.HasComponent<Carrier>(entity))
            {
                return "CARR";
            }

            if (entityManager.HasComponent<MiningVessel>(entity))
            {
                return "MINE";
            }

            if (entityManager.HasComponent<Asteroid>(entity))
            {
                return "AST";
            }

            if (entityManager.HasComponent<VesselMovement>(entity))
            {
                return "SHIP";
            }

            return "OBJ";
        }

        private static uint ComputeStableHash(Entity entity)
        {
            unchecked
            {
                var hash = (uint)entity.Index;
                hash = hash * 2654435761u;
                hash ^= (uint)entity.Version * 2246822519u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static bool IsArchetypeCapacityException(InvalidOperationException exception)
        {
            if (exception == null || string.IsNullOrWhiteSpace(exception.Message))
            {
                return false;
            }

            var message = exception.Message;
            return message.IndexOf("Entity archetype component data is too large", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Maximum chunk size", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Keeps player target selection and multi-target locks deterministic and self-healing.
    /// Removes invalid/duplicate locks and maintains a single primary lock.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XEntityCallsignBootstrapSystem))]
    public partial struct Space4XPlayerTargetingIdempotenceSystem : ISystem
    {
        private BufferLookup<Space4XPlayerTargetLockEntry> _targetLockLookup;
        private ComponentLookup<TargetPriority> _targetPriorityLookup;

        public void OnCreate(ref SystemState state)
        {
            _targetLockLookup = state.GetBufferLookup<Space4XPlayerTargetLockEntry>(false);
            _targetPriorityLookup = state.GetComponentLookup<TargetPriority>(false);
            state.RequireForUpdate<Space4XPlayerTargetSelection>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _targetLockLookup.Update(ref state);
            _targetPriorityLookup.Update(ref state);
            var entityManager = state.EntityManager;

            foreach (var (selectionRef, entity) in SystemAPI.Query<RefRW<Space4XPlayerTargetSelection>>().WithEntityAccess())
            {
                var selection = selectionRef.ValueRO;
                var selectionIsValid = IsValidTargetEntity(entityManager, selection.TargetEntity);
                var selectionChanged = false;

                if (_targetLockLookup.HasBuffer(entity))
                {
                    var locks = _targetLockLookup[entity];
                    SanitizeTargetLocks(
                        entityManager,
                        ref locks,
                        selection.TargetEntity,
                        out var primaryEntity,
                        out var primaryPoint);

                    if (primaryEntity != Entity.Null)
                    {
                        if (selection.HasSelection == 0 ||
                            !selectionIsValid ||
                            selection.TargetEntity != primaryEntity)
                        {
                            selection.TargetEntity = primaryEntity;
                            selection.TargetPoint = ResolveTargetPoint(entityManager, primaryEntity, primaryPoint);
                            selection.HasSelection = 1;
                            selectionChanged = true;
                        }
                    }
                    else if (!selectionIsValid && (selection.HasSelection != 0 || selection.TargetEntity != Entity.Null))
                    {
                        selection = Space4XPlayerTargetSelection.None;
                        selectionChanged = true;
                    }
                }
                else if (!selectionIsValid && (selection.HasSelection != 0 || selection.TargetEntity != Entity.Null))
                {
                    selection = Space4XPlayerTargetSelection.None;
                    selectionChanged = true;
                }

                if (selectionChanged)
                {
                    selectionRef.ValueRW = selection;
                }

                SyncTargetPriority(entity, in selection);
            }
        }

        private void SyncTargetPriority(Entity owner, in Space4XPlayerTargetSelection selection)
        {
            if (!_targetPriorityLookup.HasComponent(owner))
            {
                return;
            }

            var priority = _targetPriorityLookup[owner];
            var desiredTarget = selection.HasSelection != 0 ? selection.TargetEntity : Entity.Null;
            if (priority.CurrentTarget == desiredTarget)
            {
                return;
            }

            priority.CurrentTarget = desiredTarget;
            priority.EngagementDuration = 0f;
            if (desiredTarget == Entity.Null)
            {
                priority.CurrentScore = 0f;
                priority.ForceReevaluate = 1;
            }
            else
            {
                priority.ForceReevaluate = 0;
            }

            _targetPriorityLookup[owner] = priority;
        }

        private static void SanitizeTargetLocks(
            EntityManager entityManager,
            ref DynamicBuffer<Space4XPlayerTargetLockEntry> locks,
            Entity preferredTarget,
            out Entity primaryEntity,
            out float3 primaryPoint)
        {
            primaryEntity = Entity.Null;
            primaryPoint = float3.zero;
            if (locks.Length == 0)
            {
                return;
            }

            using var seen = new NativeParallelHashSet<Entity>(math.max(8, locks.Length * 2), Allocator.Temp);
            var sanitized = new NativeList<Space4XPlayerTargetLockEntry>(locks.Length, Allocator.Temp);
            try
            {

                for (var i = 0; i < locks.Length; i++)
                {
                    var entry = locks[i];
                    if (!IsValidTargetEntity(entityManager, entry.TargetEntity))
                    {
                        continue;
                    }

                    if (!seen.Add(entry.TargetEntity))
                    {
                        continue;
                    }

                    entry.IsPrimary = 0;
                    sanitized.Add(entry);
                }

                if (sanitized.Length == 0)
                {
                    if (locks.Length > 0)
                    {
                        locks.Clear();
                    }

                    return;
                }

                var primaryIndex = 0;
                if (preferredTarget != Entity.Null)
                {
                    for (var i = 0; i < sanitized.Length; i++)
                    {
                        if (sanitized[i].TargetEntity == preferredTarget)
                        {
                            primaryIndex = i;
                            break;
                        }
                    }
                }

                for (var i = 0; i < sanitized.Length; i++)
                {
                    var entry = sanitized[i];
                    entry.IsPrimary = (byte)(i == primaryIndex ? 1 : 0);
                    sanitized[i] = entry;
                }

                var rewriteNeeded = locks.Length != sanitized.Length;
                if (!rewriteNeeded)
                {
                    for (var i = 0; i < sanitized.Length; i++)
                    {
                        if (!EntriesEqual(locks[i], sanitized[i]))
                        {
                            rewriteNeeded = true;
                            break;
                        }
                    }
                }

                if (rewriteNeeded)
                {
                    locks.Clear();
                    for (var i = 0; i < sanitized.Length; i++)
                    {
                        locks.Add(sanitized[i]);
                    }
                }

                var primary = sanitized[primaryIndex];
                primaryEntity = primary.TargetEntity;
                primaryPoint = primary.LastKnownPoint;
            }
            finally
            {
                if (sanitized.IsCreated)
                {
                    sanitized.Dispose();
                }
            }
        }

        private static bool EntriesEqual(Space4XPlayerTargetLockEntry lhs, Space4XPlayerTargetLockEntry rhs)
        {
            return lhs.TargetEntity == rhs.TargetEntity &&
                   math.lengthsq(lhs.LastKnownPoint - rhs.LastKnownPoint) <= 0.0001f &&
                   math.abs(lhs.Score - rhs.Score) <= 0.0001f &&
                   lhs.PurposeMask == rhs.PurposeMask &&
                   lhs.IsPrimary == rhs.IsPrimary;
        }

        private static bool IsValidTargetEntity(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<LocalTransform>(entity))
            {
                return false;
            }

            if (entityManager.HasComponent<HullIntegrity>(entity))
            {
                return entityManager.GetComponentData<HullIntegrity>(entity).Current > 0f;
            }

            return true;
        }

        private static float3 ResolveTargetPoint(EntityManager entityManager, Entity target, float3 fallback)
        {
            if (IsValidTargetEntity(entityManager, target))
            {
                return entityManager.GetComponentData<LocalTransform>(target).Position;
            }

            return fallback;
        }
    }
}

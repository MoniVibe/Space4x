using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Social;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// Helper utilities for scenario adapters to resolve registry IDs → entities → buffers.
    /// Exposes handles for ScenarioRunner assertions and applies relation/linkage seeds.
    /// </summary>
    public static class ScenarioAdapterHelpers
    {
        /// <summary>
        /// Resolves a registry ID to an entity reference using the <see cref="RegistryIdentity"/> database.
        /// </summary>
        /// <param name="entityManager">Entity manager for the current world.</param>
        /// <param name="registryId">Registry label (e.g. godgame.villager).</param>
        /// <param name="entity">Resolved entity, if found.</param>
        /// <returns>True if an entity with the registry ID was found.</returns>
        public static bool TryResolveRegistryIdToEntity(
            EntityManager entityManager,
            FixedString64Bytes registryId,
            out Entity entity)
        {
            entity = Entity.Null;
            if (!TryParseRegistryId(registryId, out var parsedId))
            {
                return false;
            }

            using var lookup = BuildRegistryLookup(entityManager, Allocator.Temp);
            return lookup.TryGetValue(parsedId, out entity) && entityManager.Exists(entity);
        }

        /// <summary>
        /// Builds registry ID → entity mappings that can be reused across assertion evaluations.
        /// </summary>
        public static void GetEntityHandlesForAssertions(
            EntityManager entityManager,
            in NativeList<ScenarioEntityCount> entityCounts,
            out NativeHashMap<FixedString64Bytes, Entity> entityHandles)
        {
            entityHandles = new NativeHashMap<FixedString64Bytes, Entity>(math.max(1, entityCounts.Length), Allocator.TempJob);
            using var lookup = BuildRegistryLookup(entityManager, Allocator.Temp);
            for (int i = 0; i < entityCounts.Length; i++)
            {
                var entry = entityCounts[i];
                if (entry.RegistryId.Length == 0 || entityHandles.ContainsKey(entry.RegistryId))
                {
                    continue;
                }

                if (!TryParseRegistryId(entry.RegistryId, out var registryId))
                {
                    continue;
                }

                if (lookup.TryGetValue(registryId, out var entity) && entityManager.Exists(entity))
                {
                    entityHandles.TryAdd(entry.RegistryId, entity);
                }
            }
        }

        /// <summary>
        /// Applies relation seeds to newly spawned entities so aggregates/leaders/members line up.
        /// Seeds are authored using registry IDs so they remain stable across deterministic runs.
        /// </summary>
        public static void ApplyRelationSeeds(
            EntityManager entityManager,
            in NativeList<RelationSeedElement> seeds)
        {
            if (!seeds.IsCreated || seeds.Length == 0)
            {
                return;
            }

            using var lookup = BuildRegistryLookup(entityManager, Allocator.Temp);
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (!TryResolveFromLookup(entityManager, lookup, seed.EntityAId, out var entityA) ||
                    !TryResolveFromLookup(entityManager, lookup, seed.EntityBId, out var entityB))
                {
                    continue;
                }

                ApplyRelation(entityManager, entityA, entityB, seed.RelationType, seed.RelationValue);
            }
        }

        private static void ApplyRelation(
            EntityManager entityManager,
            Entity entityA,
            Entity entityB,
            FixedString32Bytes relationType,
            float relationValue)
        {
            var relationToken = relationType.ToString().Trim().ToLowerInvariant();
            switch (relationToken)
            {
                case "member":
                    AddAggregateMembership(entityManager, entityA, entityB, relationValue);
                    break;

                case "leader":
                    SetAggregateLeader(entityManager, entityA, entityB);
                    break;

                case "ally":
                    AddEntityRelation(entityManager, entityA, entityB, RelationType.Ally, relationValue);
                    AddEntityRelation(entityManager, entityB, entityA, RelationType.Ally, relationValue);
                    break;

                case "hostile":
                    AddEntityRelation(entityManager, entityA, entityB, RelationType.Hostile, relationValue);
                    AddEntityRelation(entityManager, entityB, entityA, RelationType.Hostile, relationValue);
                    break;

                default:
                    AddEntityRelation(entityManager, entityA, entityB, RelationType.Neutral, relationValue);
                    break;
            }
        }

        private static void AddAggregateMembership(
            EntityManager entityManager,
            Entity memberEntity,
            Entity aggregateEntity,
            float relationValue)
        {
            var aggregateType = AggregateType.Guild;
            if (entityManager.HasComponent<AggregateEntity>(aggregateEntity))
            {
                var aggregate = entityManager.GetComponentData<AggregateEntity>(aggregateEntity);
                aggregateType = aggregate.Type;
                if (aggregate.MemberCount < ushort.MaxValue)
                {
                    aggregate.MemberCount++;
                    entityManager.SetComponentData(aggregateEntity, aggregate);
                }
            }

            var membershipBuffer = entityManager.HasBuffer<AggregateMembership>(memberEntity)
                ? entityManager.GetBuffer<AggregateMembership>(memberEntity)
                : entityManager.AddBuffer<AggregateMembership>(memberEntity);
            membershipBuffer.Add(new AggregateMembership
            {
                AggregateEntity = aggregateEntity,
                Type = aggregateType,
                ContributionWeight = math.max(0.1f, relationValue <= 0f ? 1f : relationValue),
                LoyaltyToAggregate = math.saturate(math.abs(relationValue)),
                Rank = 0,
                IsFounder = 0,
                JoinedTick = 0
            });

            var aggregateMembers = entityManager.HasBuffer<AggregateMember>(aggregateEntity)
                ? entityManager.GetBuffer<AggregateMember>(aggregateEntity)
                : entityManager.AddBuffer<AggregateMember>(aggregateEntity);
            aggregateMembers.Add(new AggregateMember
            {
                MemberEntity = memberEntity,
                ContributionWeight = math.max(0.1f, relationValue <= 0f ? 1f : relationValue),
                Rank = 0,
                IsActive = 1,
                JoinedTick = 0
            });
        }

        private static void SetAggregateLeader(EntityManager entityManager, Entity leader, Entity aggregateEntity)
        {
            if (!entityManager.HasComponent<AggregateEntity>(aggregateEntity))
            {
                return;
            }

            var aggregate = entityManager.GetComponentData<AggregateEntity>(aggregateEntity);
            aggregate.LeaderEntity = leader;
            entityManager.SetComponentData(aggregateEntity, aggregate);
        }

        private static void AddEntityRelation(
            EntityManager entityManager,
            Entity owner,
            Entity other,
            RelationType relationType,
            float relationValue)
        {
            var intensity = (sbyte)math.clamp(math.round(relationValue * 100f), -100f, 100f);
            var buffer = entityManager.HasBuffer<EntityRelation>(owner)
                ? entityManager.GetBuffer<EntityRelation>(owner)
                : entityManager.AddBuffer<EntityRelation>(owner);
            buffer.Add(new EntityRelation
            {
                OtherEntity = other,
                Type = relationType,
                Intensity = intensity,
                FirstMetTick = 0,
                LastInteractionTick = 0,
                InteractionCount = 0,
                Trust = (byte)math.clamp(intensity + 100, 0, 255),
                Familiarity = 0,
                Respect = 0,
                Fear = 0
            });
        }

        private static NativeHashMap<RegistryId, Entity> BuildRegistryLookup(EntityManager entityManager, Allocator allocator)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryIdentity>());
            var entities = query.ToEntityArray(allocator);
            var identities = query.ToComponentDataArray<RegistryIdentity>(allocator);
            var lookup = new NativeHashMap<RegistryId, Entity>(math.max(1, identities.Length), allocator);
            for (int i = 0; i < identities.Length; i++)
            {
                if (!lookup.TryAdd(identities[i].Id, entities[i]))
                {
                    lookup[identities[i].Id] = entities[i];
                }
            }

            entities.Dispose();
            identities.Dispose();
            return lookup;
        }

        private static bool TryResolveFromLookup(
            EntityManager entityManager,
            in NativeHashMap<RegistryId, Entity> lookup,
            FixedString64Bytes label,
            out Entity entity)
        {
            entity = Entity.Null;
            if (!TryParseRegistryId(label, out var registryId))
            {
                return false;
            }

            return lookup.TryGetValue(registryId, out entity) && entityManager.Exists(entity);
        }

        private static bool TryParseRegistryId(FixedString64Bytes label, out RegistryId registryId)
        {
            registryId = default;
            if (label.Length == 0)
            {
                return false;
            }

            return RegistryId.TryParse(label.ToString(), out registryId, out _);
        }
    }
}


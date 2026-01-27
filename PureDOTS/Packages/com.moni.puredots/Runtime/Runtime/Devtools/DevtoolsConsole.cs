#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Runtime console API for devtools spawn commands.
    /// </summary>
    public static class DevtoolsConsole
    {
        /// <summary>
        /// Creates a spawn request entity.
        /// </summary>
        public static Entity CreateSpawnRequest(EntityManager entityManager, string prototypeName, int count = 1, float3? position = null, SpawnPattern pattern = SpawnPattern.Point, float radiusOrSpread = 0f, int columns = 0, uint seed = 0, SpawnFlags flags = SpawnFlags.None, byte ownerPlayerId = 0)
        {
            int prototypeId = PrototypeId.FromString(prototypeName).Value;
            float3 pos = position ?? float3.zero;
            if (seed == 0)
            {
                seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            }

            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new SpawnRequest
            {
                PrototypeId = prototypeId,
                Count = count,
                Position = pos,
                Rotation = quaternion.identity,
                Pattern = pattern,
                RadiusOrSpread = radiusOrSpread,
                Columns = columns,
                Seed = seed,
                Flags = flags,
                OwnerPlayerId = ownerPlayerId
            });
            entityManager.AddBuffer<SpawnCandidate>(entity);
            entityManager.AddBuffer<SpawnValidationResult>(entity);
            entityManager.AddBuffer<StatOverride>(entity);

            return entity;
        }

        /// <summary>
        /// Creates a spawn request at cursor position.
        /// </summary>
        public static Entity CreateSpawnRequestAtCursor(EntityManager entityManager, string prototypeName, int count = 1, SpawnPattern pattern = SpawnPattern.Point, float radiusOrSpread = 0f, int columns = 0, uint seed = 0, SpawnFlags flags = SpawnFlags.None, byte ownerPlayerId = 0)
        {
            return CreateSpawnRequest(entityManager, prototypeName, count, float3.zero, pattern, radiusOrSpread, columns, seed, flags | SpawnFlags.AtCursor, ownerPlayerId);
        }

        /// <summary>
        /// Creates an aggregate spawn request.
        /// </summary>
        public static Entity CreateAggregateSpawnRequest(EntityManager entityManager, int aggregatePresetId, int totalCount = 0, float3? position = null, uint seed = 0, SpawnFlags flags = SpawnFlags.None, byte ownerPlayerId = 0)
        {
            float3 pos = position ?? float3.zero;
            if (seed == 0)
            {
                seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            }

            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new AggregateSpawnRequest
            {
                AggregatePresetId = aggregatePresetId,
                TotalCount = totalCount,
                Position = pos,
                Rotation = quaternion.identity,
                Seed = seed,
                Flags = flags,
                OwnerPlayerId = ownerPlayerId
            });

            return entity;
        }

        /// <summary>
        /// Despawns selected entities or group.
        /// </summary>
        public static void DespawnSelected(EntityManager entityManager, Entity selectedEntity)
        {
            if (entityManager.HasComponent<AggregateGroup>(selectedEntity))
            {
                // Despawn entire group
                if (entityManager.HasBuffer<AggregateMembers>(selectedEntity))
                {
                    var members = entityManager.GetBuffer<AggregateMembers>(selectedEntity);
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (entityManager.Exists(members[i].Member))
                        {
                            entityManager.DestroyEntity(members[i].Member);
                        }
                    }
                }
                entityManager.DestroyEntity(selectedEntity);
            }
            else
            {
                // Despawn single entity
                if (entityManager.Exists(selectedEntity))
                {
                    entityManager.DestroyEntity(selectedEntity);
                }
            }
        }
    }
}
#endif
























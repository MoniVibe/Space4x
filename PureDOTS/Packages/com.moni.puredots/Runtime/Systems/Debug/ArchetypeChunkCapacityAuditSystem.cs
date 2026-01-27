#if UNITY_EDITOR
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Debug
{
    /// <summary>
    /// Editor-only helper that logs entities with chunk capacity 1.
    /// Enable via PUREDOTS_ARCHETYPE_CAPACITY_AUDIT=1.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ArchetypeChunkCapacityAuditSystem : ISystem
    {
        private const string EnableEnvVar = "PUREDOTS_ARCHETYPE_CAPACITY_AUDIT";
        private EntityStorageInfoLookup _entityInfo;

        [BurstDiscard]
        public void OnCreate(ref SystemState state)
        {
            _entityInfo = state.GetEntityStorageInfoLookup();
            if (!ShouldRun())
            {
                state.Enabled = false;
            }
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!ShouldRun())
            {
                state.Enabled = false;
                return;
            }

            _entityInfo.Update(ref state);

            var entityManager = state.EntityManager;
            using var entities = entityManager.GetAllEntities(Allocator.Temp);

            var logged = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!_entityInfo.Exists(entity))
                {
                    continue;
                }

                var capacity = GetChunkCapacity(_entityInfo[entity]);
                if (capacity > 1)
                {
                    continue;
                }

                using var types = entityManager.GetComponentTypes(entity, Allocator.Temp);
                var builder = new StringBuilder();
                for (int t = 0; t < types.Length; t++)
                {
                    if (t > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(types[t].GetManagedType().Name);
                }

                Log.Info($"[ArchetypeAudit] chunkCapacity={capacity} entity={entity} components=({builder})");
                logged++;
            }

            if (logged == 0)
            {
                Log.Info("[ArchetypeAudit] No chunkCapacity=1 entities detected.");
            }

            state.Enabled = false;
        }

        private static bool ShouldRun()
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            var value = System.Environment.GetEnvironmentVariable(EnableEnvVar);
            return IsTruthy(value);
        }

        private static int GetChunkCapacity(EntityStorageInfo storageInfo)
        {
            return storageInfo.Chunk.Capacity;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif

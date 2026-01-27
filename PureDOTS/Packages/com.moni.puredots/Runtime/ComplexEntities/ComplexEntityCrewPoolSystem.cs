using PureDOTS.Runtime.ComplexEntities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.ComplexEntities
{
    /// <summary>
    /// Manages crew roster pool lifecycle for complex entities.
    /// Loads crew rosters when operational/narrative expansion activates,
    /// unloads when deactivated (with rollup to core axes).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ComplexEntityActivationSystem))]
    [UpdateAfter(typeof(ComplexEntityNarrativeDetailSystem))]
    public partial struct ComplexEntityCrewPoolSystem : ISystem
    {
        private NativeParallelHashMap<ulong, BlobAssetReference<CrewRosterBlob>> _rosterByOwnerStableId;
        private NativeParallelHashMap<ulong, int> _refCountByOwnerStableId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

            _rosterByOwnerStableId = new NativeParallelHashMap<ulong, BlobAssetReference<CrewRosterBlob>>(1024, Allocator.Persistent);
            _refCountByOwnerStableId = new NativeParallelHashMap<ulong, int>(1024, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_rosterByOwnerStableId.IsCreated)
            {
                using var keys = _rosterByOwnerStableId.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    var key = keys[i];
                    if (_rosterByOwnerStableId.TryGetValue(key, out var roster) && roster.IsCreated)
                    {
                        roster.Dispose();
                    }
                }
                _rosterByOwnerStableId.Dispose();
            }

            if (_refCountByOwnerStableId.IsCreated)
            {
                _refCountByOwnerStableId.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check feature flag
            var featureFlags = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((featureFlags.Flags & SimulationFeatureFlags.ComplexEntitiesEnabled) == 0)
                return;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
                return;
            var currentTick = tickState.Tick;

            // Load crew rosters for entities that need them
            foreach (var (identity, coreAxes, entity) in SystemAPI.Query<RefRO<ComplexEntityIdentity>, RefRW<ComplexEntityCoreAxes>>()
                .WithAll<ComplexEntityIdentity>()
                .WithNone<ComplexEntityCrewHandle>()
                .WithEntityAccess())
            {
                // Check if entity needs crew roster (operational or narrative active)
                var axes = coreAxes.ValueRW;
                bool needsCrew = (axes.Flags & (ComplexEntityFlags.OperationalActive | ComplexEntityFlags.NarrativeActive)) != 0;

                if (needsCrew)
                {
                    var stableId = identity.ValueRO.StableId;
                    var roster = AcquireOrCreateRoster(stableId);

                    ecb.AddComponent(entity, new ComplexEntityCrewHandle
                    {
                        OwnerStableId = stableId,
                        Roster = roster,
                        LastUpdateTick = currentTick,
                        CrewCount = 0
                    });

                    axes.Flags |= ComplexEntityFlags.CrewLoaded;
                    ecb.SetComponent(entity, axes);
                }
            }

            // Unload crew rosters for entities that don't need them
            foreach (var (crewHandle, coreAxes, entity) in SystemAPI.Query<
                RefRO<ComplexEntityCrewHandle>, RefRW<ComplexEntityCoreAxes>>()
                .WithEntityAccess())
            {
                // Check if entity still needs crew roster
                var axes = coreAxes.ValueRW;
                bool needsCrew = (axes.Flags & (ComplexEntityFlags.OperationalActive | ComplexEntityFlags.NarrativeActive)) != 0;

                if (!needsCrew)
                {
                    // Rollup crew data to core axes before removing
                    // In full implementation, would aggregate crew mass/capacity to core axes
                    axes.Flags &= ~ComplexEntityFlags.CrewLoaded;
                    ecb.SetComponent(entity, axes);

                    ReleaseRoster(crewHandle.ValueRO.OwnerStableId);

                    // Remove crew handle (blob will be cleaned up automatically)
                    ecb.RemoveComponent<ComplexEntityCrewHandle>(entity);
                }
            }
        }

        private BlobAssetReference<CrewRosterBlob> AcquireOrCreateRoster(ulong ownerStableId)
        {
            if (_rosterByOwnerStableId.TryGetValue(ownerStableId, out var existing))
            {
                _refCountByOwnerStableId.TryGetValue(ownerStableId, out var count);
                _refCountByOwnerStableId[ownerStableId] = count + 1;
                return existing;
            }

            // Skeleton: create an empty roster; games can replace this with streaming/persistence.
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CrewRosterBlob>();
            builder.Allocate(ref root.Members, 0);
            builder.Allocate(ref root.Roles, 0);
            builder.Allocate(ref root.Relationships, 0);

            var created = builder.CreateBlobAssetReference<CrewRosterBlob>(Allocator.Persistent);
            _rosterByOwnerStableId[ownerStableId] = created;
            _refCountByOwnerStableId[ownerStableId] = 1;
            return created;
        }

        private void ReleaseRoster(ulong ownerStableId)
        {
            if (!_refCountByOwnerStableId.TryGetValue(ownerStableId, out var count))
                return;

            count--;
            if (count > 0)
            {
                _refCountByOwnerStableId[ownerStableId] = count;
                return;
            }

            _refCountByOwnerStableId.Remove(ownerStableId);

            if (_rosterByOwnerStableId.TryGetValue(ownerStableId, out var roster))
            {
                _rosterByOwnerStableId.Remove(ownerStableId);
                if (roster.IsCreated)
                {
                    roster.Dispose();
                }
            }
        }
    }
}

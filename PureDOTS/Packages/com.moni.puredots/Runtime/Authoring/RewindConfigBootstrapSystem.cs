#if UNITY_EDITOR || UNITY_STANDALONE
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Bootstraps rewind configuration by merging all RewindConfigAuthoring track definitions
    /// into a single RewindConfigBlob and creating RewindConfigSingleton.
    /// Runs once at startup, before other rewind systems.
    /// Only available when Authoring assembly is referenced.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct RewindConfigBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindConfigTrackEntry>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Collect all track definitions from all config entities
            var allTracks = new NativeList<RewindTrackDef>(Allocator.Temp);
            var seenIds = new NativeHashSet<byte>(16, Allocator.Temp);

            // Query all config entities (by buffer presence, since RewindConfigTag is in Authoring assembly)
            foreach (var (trackBuffer, entity) in SystemAPI.Query<DynamicBuffer<RewindConfigTrackEntry>>()
                         .WithEntityAccess())
            {
                foreach (var entry in trackBuffer)
                {
                    // Skip duplicates (last one wins)
                    if (seenIds.Contains(entry.TrackId))
                    {
                        continue;
                    }

                    seenIds.Add(entry.TrackId);

                    allTracks.Add(new RewindTrackDef
                    {
                        Id = new RewindTrackId { Value = entry.TrackId },
                        Name = entry.TrackName,
                        Tier = entry.Tier,
                        RecordEveryTicks = entry.RecordEveryTicks,
                        WindowTicks = entry.WindowTicks,
                        Spatial = entry.Spatial
                    });
                }
            }

            // Create default tracks if none provided
            if (allTracks.Length == 0)
            {
                // Default "World" track
                allTracks.Add(new RewindTrackDef
                {
                    Id = new RewindTrackId { Value = 0 },
                    Name = new FixedString32Bytes("World"),
                    Tier = RewindTier.SnapshotFull,
                    RecordEveryTicks = 1,
                    WindowTicks = 3600,
                    Spatial = false
                });
            }

            // Build blob asset
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RewindConfigBlob>();
            var tracksArray = builder.Allocate(ref root.Tracks, allTracks.Length);
            for (int i = 0; i < allTracks.Length; i++)
            {
                tracksArray[i] = allTracks[i];
            }

            var blob = builder.CreateBlobAssetReference<RewindConfigBlob>(Allocator.Persistent);
            builder.Dispose();

            // Create or update singleton
            if (SystemAPI.TryGetSingletonEntity<RewindConfigSingleton>(out var singletonEntity))
            {
                // Dispose old blob if exists
                var oldConfig = SystemAPI.GetComponent<RewindConfigSingleton>(singletonEntity);
                if (oldConfig.Config.IsCreated)
                {
                    oldConfig.Config.Dispose();
                }
                SystemAPI.SetComponent(singletonEntity, new RewindConfigSingleton { Config = blob });
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(RewindConfigSingleton));
                state.EntityManager.SetComponentData(entity, new RewindConfigSingleton { Config = blob });
            }

            allTracks.Dispose();
            seenIds.Dispose();

            // Disable system after first run
            state.Enabled = false;
        }
    }
}
#endif


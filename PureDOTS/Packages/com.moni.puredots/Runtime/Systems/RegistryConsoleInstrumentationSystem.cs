using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Emits console-friendly registry summaries when enabled via <see cref="RegistryConsoleInstrumentation"/>.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct RegistryConsoleInstrumentationSystem : ISystem
    {
        private ComponentLookup<RegistryMetadata> _metadataLookup;
        private ComponentLookup<RegistryHealth> _healthLookup;
        private BufferLookup<RegistryDirectoryEntry> _directoryEntriesLookup;
        private ComponentLookup<BandRegistry> _bandRegistryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryConsoleInstrumentation>();
            state.RequireForUpdate<RegistryDirectory>();

            _metadataLookup = state.GetComponentLookup<RegistryMetadata>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<RegistryHealth>(isReadOnly: true);
            _directoryEntriesLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
            _bandRegistryLookup = state.GetComponentLookup<BandRegistry>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var instrumentation = SystemAPI.GetSingletonRW<RegistryConsoleInstrumentation>();
            var directoryEntity = SystemAPI.GetSingletonEntity<RegistryDirectory>();

            if (!_directoryEntriesLookup.HasBuffer(directoryEntity))
            {
                return;
            }

            var directory = SystemAPI.GetComponentRO<RegistryDirectory>(directoryEntity).ValueRO;
            var currentTick = SystemAPI.HasSingleton<TimeState>() ? SystemAPI.GetSingleton<TimeState>().Tick : 0u;

            var loggedVersion = instrumentation.ValueRO.LastDirectoryVersion;
            var logOnlyOnChange = instrumentation.ValueRO.ShouldLogOnlyOnChange && loggedVersion != 0u && directory.Version == loggedVersion;
            if (logOnlyOnChange)
            {
                return;
            }

            if (instrumentation.ValueRO.MinTickDelta > 0u &&
                instrumentation.ValueRO.LastLoggedTick != 0u &&
                currentTick >= instrumentation.ValueRO.LastLoggedTick &&
                currentTick - instrumentation.ValueRO.LastLoggedTick < instrumentation.ValueRO.MinTickDelta)
            {
                return;
            }

            _metadataLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _directoryEntriesLookup.Update(ref state);
            _bandRegistryLookup.Update(ref state);

            instrumentation.ValueRW.LastLoggedTick = currentTick;
            instrumentation.ValueRW.LastDirectoryVersion = directory.Version;

            var entries = _directoryEntriesLookup[directoryEntity];
            var text = new FixedString512Bytes();
            text.Append("[Registry] Tick ");
            text.Append(currentTick);
            text.Append(" Directory v");
            text.Append(directory.Version);
            text.Append(" Count ");
            text.Append(entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                text.Append(" | ");
                AppendKind(ref text, entry.Kind);

                if (_metadataLookup.HasComponent(entry.Handle.RegistryEntity))
                {
                    var metadata = _metadataLookup[entry.Handle.RegistryEntity];
                    text.Append(" v");
                    text.Append(metadata.Version);
                    text.Append(" #");
                    text.Append(metadata.EntryCount);

                    if (entry.Kind == RegistryKind.Band && _bandRegistryLookup.HasComponent(entry.Handle.RegistryEntity))
                    {
                        var bandRegistry = _bandRegistryLookup[entry.Handle.RegistryEntity];
                        text.Append(" members=");
                        text.Append(bandRegistry.TotalMembers);
                        text.Append(" bands=");
                        text.Append(bandRegistry.TotalBands);
                    }

                    if (_healthLookup.HasComponent(entry.Handle.RegistryEntity))
                    {
                        var health = _healthLookup[entry.Handle.RegistryEntity];
                        text.Append(" h=");
                        AppendHealthLevel(ref text, health.HealthLevel);

                        if (health.StaleEntryCount > 0)
                        {
                            text.Append(" stale");
                            text.Append(health.StaleEntryCount);
                            if (health.TotalEntryCount > 0)
                            {
                                text.Append("/");
                                text.Append(health.TotalEntryCount);
                            }
                        }

                        if (health.SpatialVersionDelta > 0)
                        {
                            text.Append(" Δsp=");
                            text.Append(health.SpatialVersionDelta);
                        }

                        if (health.TicksSinceLastUpdate > 0)
                        {
                            text.Append(" Δt=");
                            text.Append(health.TicksSinceLastUpdate);
                        }

                        if (health.DirectoryVersionDelta > 0)
                        {
                            text.Append(" Δdir=");
                            text.Append(health.DirectoryVersionDelta);
                        }
                    }
                }
                else
                {
                    text.Append(" (no metadata)");
                }
            }

            if (SystemAPI.TryGetSingleton<SpatialGridState>(out var spatialState))
            {
                text.Append(" | Spatial v");
                text.Append(spatialState.Version);
                text.Append(" @");
                text.Append(spatialState.LastUpdateTick);

                if (spatialState.LastRebuildMilliseconds > 0f)
                {
                    var rebuildRounded = math.round(spatialState.LastRebuildMilliseconds * 100f) / 100f;
                    text.Append(" rebuild=");
                    text.Append(rebuildRounded);
                    text.Append("ms");
                }
            }

#if UNITY_EDITOR
            UnityEngine.Debug.Log(text);
#endif
        }

        private static void AppendKind(ref FixedString512Bytes builder, RegistryKind kind)
        {
            switch (kind)
            {
                case RegistryKind.Resource:
                    builder.Append("Resource");
                    break;
                case RegistryKind.Storehouse:
                    builder.Append("Storehouse");
                    break;
                case RegistryKind.Villager:
                    builder.Append("Villager");
                    break;
                case RegistryKind.Miracle:
                    builder.Append("Miracle");
                    break;
                // Game-specific transport registries (MinerVessel, Hauler, Freighter, Wagon) handled by Space4X
                case RegistryKind.Construction:
                    builder.Append("Construction");
                    break;
                case RegistryKind.Creature:
                    builder.Append("Creature");
                    break;
                case RegistryKind.LogisticsRequest:
                    builder.Append("Logistics");
                    break;
                case RegistryKind.Band:
                    builder.Append("Band");
                    break;
                case RegistryKind.Ability:
                    builder.Append("Ability");
                    break;
                case RegistryKind.Spawner:
                    builder.Append("Spawner");
                    break;
                default:
                    builder.Append("Kind(");
                    builder.Append((int)kind);
                    builder.Append(")");
                    break;
            }
        }

        private static void AppendHealthLevel(ref FixedString512Bytes builder, RegistryHealthLevel healthLevel)
        {
            switch (healthLevel)
            {
                case RegistryHealthLevel.Healthy:
                    builder.Append("ok");
                    break;
                case RegistryHealthLevel.Warning:
                    builder.Append("warn");
                    break;
                case RegistryHealthLevel.Critical:
                    builder.Append("crit");
                    break;
                case RegistryHealthLevel.Failure:
                    builder.Append("fail");
                    break;
                default:
                    builder.Append((int)healthLevel);
                    break;
            }
        }
    }
}

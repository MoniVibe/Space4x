using System;
using System.IO;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Progression
{
    public struct Space4XPersistentProgressionState : IComponentData
    {
        public int SchemaVersion;
        public float TotalThrustGenerated;
        public uint TotalMissilesFired;
        public uint TotalKineticAmmoSpent;
        public Space4XRunMetaProficiencyState RetainedMetaProficiency;
        public Space4XRunMetaUnlockFlags RetainedMetaUnlockFlags;
        public int RetainedMetaShards;
        public int RetainedMetaRoomChallengeClears;
        public uint Revision;
        public byte Dirty;
        public double LastSavedWorldSeconds;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XPersistentWeaponMountTracker : IBufferElementData
    {
        public uint LastShotsFired;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XPersistentProgressionBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XPersistentProgressionState>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XPersistentProgressionState));
            var loaded = Space4XPersistentProgressionStorage.LoadOrDefault();
            state.EntityManager.SetComponentData(entity, loaded);
        }
    }

    public struct Space4XPersistentMetaHydratedTag : IComponentData
    {
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlBootstrapSystem))]
    public partial struct Space4XPersistentMetaHydrationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XPersistentProgressionState>();
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XPersistentProgressionState>(out var persistent))
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (meta, unlocks, resources, entity) in SystemAPI.Query<
                         RefRW<Space4XRunMetaProficiencyState>,
                         RefRW<Space4XRunMetaUnlockState>,
                         RefRW<Space4XRunMetaResourceState>>()
                     .WithAll<Space4XFleetcrawlDirectorState>()
                     .WithNone<Space4XPersistentMetaHydratedTag>()
                     .WithEntityAccess())
            {
                var runMeta = meta.ValueRO;
                Space4XPersistentProgressionMath.MergeMetaProficiencyMax(ref runMeta, in persistent.RetainedMetaProficiency);
                meta.ValueRW = runMeta;

                var mergedFlags = (Space4XRunMetaUnlockFlags)((ushort)unlocks.ValueRO.Flags | (ushort)persistent.RetainedMetaUnlockFlags);
                unlocks.ValueRW = new Space4XRunMetaUnlockState
                {
                    Flags = mergedFlags,
                    UnlockCount = (byte)math.min(255, math.countbits((uint)(ushort)mergedFlags))
                };

                var runResources = resources.ValueRO;
                runResources.Shards = math.max(runResources.Shards, persistent.RetainedMetaShards);
                runResources.RoomChallengeClears = math.max(runResources.RoomChallengeClears, persistent.RetainedMetaRoomChallengeClears);
                resources.ValueRW = runResources;

                ecb.AddComponent<Space4XPersistentMetaHydratedTag>(entity);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlMetaProficiencySystem))]
    public partial struct Space4XPersistentMetaProficiencySyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XPersistentProgressionState>();
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlDirectorState>(out var directorEntity))
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.HasComponent<Space4XRunMetaProficiencyState>(directorEntity) ||
                !em.HasComponent<Space4XRunMetaUnlockState>(directorEntity) ||
                !em.HasComponent<Space4XRunMetaResourceState>(directorEntity))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonRW<Space4XPersistentProgressionState>(out var progression))
            {
                return;
            }

            var persistent = progression.ValueRO;
            var changed = false;

            var runMeta = em.GetComponentData<Space4XRunMetaProficiencyState>(directorEntity);
            changed |= Space4XPersistentProgressionMath.MergeMetaProficiencyMax(ref persistent.RetainedMetaProficiency, in runMeta);

            var runUnlocks = em.GetComponentData<Space4XRunMetaUnlockState>(directorEntity);
            var mergedFlags = (Space4XRunMetaUnlockFlags)((ushort)persistent.RetainedMetaUnlockFlags | (ushort)runUnlocks.Flags);
            if (mergedFlags != persistent.RetainedMetaUnlockFlags)
            {
                persistent.RetainedMetaUnlockFlags = mergedFlags;
                changed = true;
            }

            var runResources = em.GetComponentData<Space4XRunMetaResourceState>(directorEntity);
            var mergedShards = math.max(persistent.RetainedMetaShards, runResources.Shards);
            if (mergedShards != persistent.RetainedMetaShards)
            {
                persistent.RetainedMetaShards = mergedShards;
                changed = true;
            }

            var mergedRoomChallengeClears = math.max(persistent.RetainedMetaRoomChallengeClears, runResources.RoomChallengeClears);
            if (mergedRoomChallengeClears != persistent.RetainedMetaRoomChallengeClears)
            {
                persistent.RetainedMetaRoomChallengeClears = mergedRoomChallengeClears;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            persistent.Revision += 1;
            persistent.Dirty = 1;
            progression.ValueRW = persistent;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XPersistentThrustProgressionTrackingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XPersistentProgressionState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (movement, entity) in SystemAPI.Query<RefRO<VesselMovement>>()
                         .WithAll<PlayerFlagshipTag>()
                         .WithNone<Space4XThrustProgressionTracker>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new Space4XThrustProgressionTracker
                {
                    LastSpeed = math.max(0f, movement.ValueRO.CurrentSpeed),
                    Initialized = 1
                });
            }

            ecb.Playback(em);
            ecb.Dispose();

            if (!SystemAPI.TryGetSingletonRW<Space4XPersistentProgressionState>(out var progression))
            {
                return;
            }

            var progressionState = progression.ValueRO;
            foreach (var (movement, tracker) in SystemAPI.Query<RefRO<VesselMovement>, RefRW<Space4XThrustProgressionTracker>>()
                         .WithAll<PlayerFlagshipTag>())
            {
                var speed = math.max(0f, movement.ValueRO.CurrentSpeed);
                var previous = math.max(0f, tracker.ValueRO.LastSpeed);
                var delta = speed - previous;
                if (delta > 1e-5f)
                {
                    progressionState.TotalThrustGenerated += delta;
                    progressionState.Revision += 1;
                    progressionState.Dirty = 1;
                }

                tracker.ValueRW.LastSpeed = speed;
                tracker.ValueRW.Initialized = 1;
            }

            progression.ValueRW = progressionState;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XWeaponSystem))]
    public partial struct Space4XPersistentWeaponProgressionTrackingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XPersistentProgressionState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonRW<Space4XPersistentProgressionState>(out var progression))
            {
                return;
            }

            var progressionState = progression.ValueRO;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<WeaponMount>>()
                         .WithAll<PlayerFlagshipTag>()
                         .WithNone<Space4XPersistentWeaponMountTracker>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<Space4XPersistentWeaponMountTracker>(entity);
            }

            ecb.Playback(em);
            ecb.Dispose();

            foreach (var (mounts, entity) in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<PlayerFlagshipTag>().WithEntityAccess())
            {
                var trackers = em.GetBuffer<Space4XPersistentWeaponMountTracker>(entity);

                while (trackers.Length < mounts.Length)
                {
                    trackers.Add(new Space4XPersistentWeaponMountTracker());
                }

                if (trackers.Length > mounts.Length)
                {
                    trackers.ResizeUninitialized(mounts.Length);
                }

                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    var tracker = trackers[i];
                    var currentShots = mount.ShotsFired;
                    var previousShots = tracker.LastShotsFired;
                    var deltaShots = currentShots >= previousShots ? currentShots - previousShots : currentShots;
                    if (deltaShots > 0)
                    {
                        if (mount.Weapon.Type == WeaponType.Missile)
                        {
                            progressionState.TotalMissilesFired += deltaShots;
                        }

                        if (mount.Weapon.Type == WeaponType.Kinetic)
                        {
                            var ammoPerShot = math.max(1, (int)mount.Weapon.AmmoPerShot);
                            progressionState.TotalKineticAmmoSpent += (uint)(deltaShots * (uint)ammoPerShot);
                        }

                        progressionState.Revision += deltaShots;
                        progressionState.Dirty = 1;
                    }

                    tracker.LastShotsFired = currentShots;
                    trackers[i] = tracker;
                }
            }

            progression.ValueRW = progressionState;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XPersistentWeaponProgressionTrackingSystem))]
    public partial struct Space4XPersistentProgressionFlushSystem : ISystem
    {
        private const float SaveIntervalSeconds = 2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XPersistentProgressionState>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<Space4XPersistentProgressionState>(out var progression))
            {
                return;
            }

            var data = progression.ValueRO;
            if (data.Dirty == 0)
            {
                return;
            }

            var worldSeconds = SystemAPI.GetSingleton<TimeState>().WorldSeconds;
            if (worldSeconds - data.LastSavedWorldSeconds < SaveIntervalSeconds)
            {
                return;
            }

            if (!Space4XPersistentProgressionStorage.TrySave(in data))
            {
                return;
            }

            data.Dirty = 0;
            data.LastSavedWorldSeconds = worldSeconds;
            progression.ValueRW = data;
        }

        public void OnDestroy(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<Space4XPersistentProgressionState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = query.GetSingletonEntity();
            var data = state.EntityManager.GetComponentData<Space4XPersistentProgressionState>(entity);
            if (data.Dirty != 0)
            {
                Space4XPersistentProgressionStorage.TrySave(in data);
            }
        }
    }

    public struct Space4XThrustProgressionTracker : IComponentData
    {
        public float LastSpeed;
        public byte Initialized;
    }

    internal static class Space4XPersistentProgressionMath
    {
        public static bool MergeMetaProficiencyMax(
            ref Space4XRunMetaProficiencyState target,
            in Space4XRunMetaProficiencyState candidate)
        {
            var changed = false;

            changed |= MaxAssign(ref target.DamageDealtEnergy, candidate.DamageDealtEnergy);
            changed |= MaxAssign(ref target.DamageDealtThermal, candidate.DamageDealtThermal);
            changed |= MaxAssign(ref target.DamageDealtEM, candidate.DamageDealtEM);
            changed |= MaxAssign(ref target.DamageDealtRadiation, candidate.DamageDealtRadiation);
            changed |= MaxAssign(ref target.DamageDealtCaustic, candidate.DamageDealtCaustic);
            changed |= MaxAssign(ref target.DamageDealtKinetic, candidate.DamageDealtKinetic);
            changed |= MaxAssign(ref target.DamageDealtExplosive, candidate.DamageDealtExplosive);

            changed |= MaxAssign(ref target.DamageMitigatedEnergy, candidate.DamageMitigatedEnergy);
            changed |= MaxAssign(ref target.DamageMitigatedThermal, candidate.DamageMitigatedThermal);
            changed |= MaxAssign(ref target.DamageMitigatedEM, candidate.DamageMitigatedEM);
            changed |= MaxAssign(ref target.DamageMitigatedRadiation, candidate.DamageMitigatedRadiation);
            changed |= MaxAssign(ref target.DamageMitigatedCaustic, candidate.DamageMitigatedCaustic);
            changed |= MaxAssign(ref target.DamageMitigatedKinetic, candidate.DamageMitigatedKinetic);
            changed |= MaxAssign(ref target.DamageMitigatedExplosive, candidate.DamageMitigatedExplosive);

            changed |= MaxAssign(ref target.CloakSeconds, candidate.CloakSeconds);
            changed |= MaxAssign(ref target.TimeStopRequestedSeconds, candidate.TimeStopRequestedSeconds);
            changed |= MaxAssign(ref target.MissileDamageDealt, candidate.MissileDamageDealt);

            changed |= MaxAssign(ref target.CraftShotDown, candidate.CraftShotDown);
            changed |= MaxAssign(ref target.CapitalShipsDestroyed, candidate.CapitalShipsDestroyed);
            changed |= MaxAssign(ref target.HiddenCachesFound, candidate.HiddenCachesFound);

            return changed;
        }

        private static bool MaxAssign(ref float target, float candidate)
        {
            if (candidate <= target)
            {
                return false;
            }

            target = candidate;
            return true;
        }

        private static bool MaxAssign(ref int target, int candidate)
        {
            if (candidate <= target)
            {
                return false;
            }

            target = candidate;
            return true;
        }
    }

    internal static class Space4XPersistentProgressionStorage
    {
        private const int CurrentSchemaVersion = 2;
        private const string FileName = "space4x_fleetcrawl_progression_v1.json";

        public static Space4XPersistentProgressionState LoadOrDefault()
        {
            var state = new Space4XPersistentProgressionState
            {
                SchemaVersion = CurrentSchemaVersion,
                TotalThrustGenerated = 0f,
                TotalMissilesFired = 0,
                TotalKineticAmmoSpent = 0,
                RetainedMetaProficiency = new Space4XRunMetaProficiencyState(),
                RetainedMetaUnlockFlags = Space4XRunMetaUnlockFlags.None,
                RetainedMetaShards = 0,
                RetainedMetaRoomChallengeClears = 0,
                Revision = 0,
                Dirty = 0,
                LastSavedWorldSeconds = 0d
            };

            try
            {
                var path = ResolvePath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return state;
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return state;
                }

                var payload = JsonUtility.FromJson<Space4XPersistentProgressionFileData>(json);
                if (payload == null || payload.schemaVersion <= 0)
                {
                    return state;
                }

                state.SchemaVersion = payload.schemaVersion;
                state.TotalThrustGenerated = math.max(0f, payload.totalThrustGenerated);
                state.TotalMissilesFired = (uint)math.max(0, payload.totalMissilesFired);
                state.TotalKineticAmmoSpent = (uint)math.max(0, payload.totalKineticAmmoSpent);
                state.RetainedMetaProficiency = new Space4XRunMetaProficiencyState
                {
                    DamageDealtEnergy = math.max(0f, payload.metaDamageDealtEnergy),
                    DamageDealtThermal = math.max(0f, payload.metaDamageDealtThermal),
                    DamageDealtEM = math.max(0f, payload.metaDamageDealtEM),
                    DamageDealtRadiation = math.max(0f, payload.metaDamageDealtRadiation),
                    DamageDealtCaustic = math.max(0f, payload.metaDamageDealtCaustic),
                    DamageDealtKinetic = math.max(0f, payload.metaDamageDealtKinetic),
                    DamageDealtExplosive = math.max(0f, payload.metaDamageDealtExplosive),
                    DamageMitigatedEnergy = math.max(0f, payload.metaDamageMitigatedEnergy),
                    DamageMitigatedThermal = math.max(0f, payload.metaDamageMitigatedThermal),
                    DamageMitigatedEM = math.max(0f, payload.metaDamageMitigatedEM),
                    DamageMitigatedRadiation = math.max(0f, payload.metaDamageMitigatedRadiation),
                    DamageMitigatedCaustic = math.max(0f, payload.metaDamageMitigatedCaustic),
                    DamageMitigatedKinetic = math.max(0f, payload.metaDamageMitigatedKinetic),
                    DamageMitigatedExplosive = math.max(0f, payload.metaDamageMitigatedExplosive),
                    CloakSeconds = math.max(0f, payload.metaCloakSeconds),
                    TimeStopRequestedSeconds = math.max(0f, payload.metaTimeStopRequestedSeconds),
                    MissileDamageDealt = math.max(0f, payload.metaMissileDamageDealt),
                    CraftShotDown = math.max(0, payload.metaCraftShotDown),
                    CapitalShipsDestroyed = math.max(0, payload.metaCapitalShipsDestroyed),
                    HiddenCachesFound = math.max(0, payload.metaHiddenCachesFound)
                };
                state.RetainedMetaUnlockFlags = (Space4XRunMetaUnlockFlags)(ushort)math.max(0, payload.metaUnlockFlags);
                state.RetainedMetaShards = math.max(0, payload.metaShards);
                state.RetainedMetaRoomChallengeClears = math.max(0, payload.metaRoomChallengeClears);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XPersistentProgression] Failed to load progression file: {ex.Message}");
            }

            return state;
        }

        public static bool TrySave(in Space4XPersistentProgressionState state)
        {
            try
            {
                var path = ResolvePath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var payload = new Space4XPersistentProgressionFileData
                {
                    schemaVersion = CurrentSchemaVersion,
                    totalThrustGenerated = math.max(0f, state.TotalThrustGenerated),
                    totalMissilesFired = (int)math.max(0u, state.TotalMissilesFired),
                    totalKineticAmmoSpent = (int)math.max(0u, state.TotalKineticAmmoSpent),
                    metaDamageDealtEnergy = math.max(0f, state.RetainedMetaProficiency.DamageDealtEnergy),
                    metaDamageDealtThermal = math.max(0f, state.RetainedMetaProficiency.DamageDealtThermal),
                    metaDamageDealtEM = math.max(0f, state.RetainedMetaProficiency.DamageDealtEM),
                    metaDamageDealtRadiation = math.max(0f, state.RetainedMetaProficiency.DamageDealtRadiation),
                    metaDamageDealtCaustic = math.max(0f, state.RetainedMetaProficiency.DamageDealtCaustic),
                    metaDamageDealtKinetic = math.max(0f, state.RetainedMetaProficiency.DamageDealtKinetic),
                    metaDamageDealtExplosive = math.max(0f, state.RetainedMetaProficiency.DamageDealtExplosive),
                    metaDamageMitigatedEnergy = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedEnergy),
                    metaDamageMitigatedThermal = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedThermal),
                    metaDamageMitigatedEM = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedEM),
                    metaDamageMitigatedRadiation = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedRadiation),
                    metaDamageMitigatedCaustic = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedCaustic),
                    metaDamageMitigatedKinetic = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedKinetic),
                    metaDamageMitigatedExplosive = math.max(0f, state.RetainedMetaProficiency.DamageMitigatedExplosive),
                    metaCloakSeconds = math.max(0f, state.RetainedMetaProficiency.CloakSeconds),
                    metaTimeStopRequestedSeconds = math.max(0f, state.RetainedMetaProficiency.TimeStopRequestedSeconds),
                    metaMissileDamageDealt = math.max(0f, state.RetainedMetaProficiency.MissileDamageDealt),
                    metaCraftShotDown = math.max(0, state.RetainedMetaProficiency.CraftShotDown),
                    metaCapitalShipsDestroyed = math.max(0, state.RetainedMetaProficiency.CapitalShipsDestroyed),
                    metaHiddenCachesFound = math.max(0, state.RetainedMetaProficiency.HiddenCachesFound),
                    metaUnlockFlags = math.max(0, (int)(ushort)state.RetainedMetaUnlockFlags),
                    metaShards = math.max(0, state.RetainedMetaShards),
                    metaRoomChallengeClears = math.max(0, state.RetainedMetaRoomChallengeClears)
                };

                var json = JsonUtility.ToJson(payload, false);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XPersistentProgression] Failed to save progression file: {ex.Message}");
                return false;
            }
        }

        private static string ResolvePath()
        {
            var root = Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            return Path.Combine(root, FileName);
        }

        [Serializable]
        private sealed class Space4XPersistentProgressionFileData
        {
            public int schemaVersion;
            public float totalThrustGenerated;
            public int totalMissilesFired;
            public int totalKineticAmmoSpent;
            public float metaDamageDealtEnergy;
            public float metaDamageDealtThermal;
            public float metaDamageDealtEM;
            public float metaDamageDealtRadiation;
            public float metaDamageDealtCaustic;
            public float metaDamageDealtKinetic;
            public float metaDamageDealtExplosive;
            public float metaDamageMitigatedEnergy;
            public float metaDamageMitigatedThermal;
            public float metaDamageMitigatedEM;
            public float metaDamageMitigatedRadiation;
            public float metaDamageMitigatedCaustic;
            public float metaDamageMitigatedKinetic;
            public float metaDamageMitigatedExplosive;
            public float metaCloakSeconds;
            public float metaTimeStopRequestedSeconds;
            public float metaMissileDamageDealt;
            public int metaCraftShotDown;
            public int metaCapitalShipsDestroyed;
            public int metaHiddenCachesFound;
            public int metaUnlockFlags;
            public int metaShards;
            public int metaRoomChallengeClears;
        }
    }
}

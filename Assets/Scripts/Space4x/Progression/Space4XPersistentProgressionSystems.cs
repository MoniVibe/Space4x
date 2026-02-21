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

    internal static class Space4XPersistentProgressionStorage
    {
        private const int CurrentSchemaVersion = 1;
        private const string FileName = "space4x_fleetcrawl_progression_v1.json";

        public static Space4XPersistentProgressionState LoadOrDefault()
        {
            var state = new Space4XPersistentProgressionState
            {
                SchemaVersion = CurrentSchemaVersion,
                TotalThrustGenerated = 0f,
                TotalMissilesFired = 0,
                TotalKineticAmmoSpent = 0,
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
                    totalKineticAmmoSpent = (int)math.max(0u, state.TotalKineticAmmoSpent)
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
        }
    }
}

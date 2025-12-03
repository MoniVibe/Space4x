using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    public enum MiningSnapshotType : byte
    {
        Asteroid = 0,
        VesselCargo = 1,
        CarrierStorage = 2,
        SpawnPickup = 3
    }

    public struct Space4XMiningTimeSpine : IComponentData
    {
        public const uint DefaultSnapshotHorizon = 512;

        public uint LastSnapshotTick;
        public uint LastPlaybackTick;
        public uint SnapshotHorizon;
    }

    public struct MiningSnapshot : IBufferElementData
    {
        public uint Tick;
        public Entity Entity;
        public Entity RelatedEntity;
        public ResourceType ResourceType;
        public float Amount;
        public float3 Position;
        public MiningSnapshotType Type;
    }

    public struct MiningTelemetrySnapshot : IBufferElementData
    {
        public uint Tick;
        public float OreInHold;
    }

    public struct SkillSnapshot : IBufferElementData
    {
        public uint Tick;
        public Entity Entity;
        public float MiningXp;
        public float HaulingXp;
        public float CombatXp;
        public float RepairXp;
        public float ExplorationXp;
    }

    /// <summary>
    /// Ensures the spine singleton and backing buffers exist before any mining/haul systems run.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial struct Space4XMiningTimeSpineBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XMiningTimeSpine>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(
                typeof(Space4XMiningTimeSpine),
                typeof(MiningSnapshot),
                typeof(MiningTelemetrySnapshot),
                typeof(MiningCommandLogEntry),
                typeof(SkillChangeLogEntry),
                typeof(SkillSnapshot));

            state.EntityManager.SetComponentData(entity, new Space4XMiningTimeSpine
            {
                LastSnapshotTick = 0,
                LastPlaybackTick = 0,
                SnapshotHorizon = Space4XMiningTimeSpine.DefaultSnapshotHorizon
            });

            state.EntityManager.GetBuffer<MiningSnapshot>(entity);
            state.EntityManager.GetBuffer<MiningTelemetrySnapshot>(entity);
            state.EntityManager.GetBuffer<MiningCommandLogEntry>(entity);
            state.EntityManager.GetBuffer<SkillChangeLogEntry>(entity);
            state.EntityManager.GetBuffer<SkillSnapshot>(entity);

            state.Enabled = false;
        }
    }

    /// <summary>
    /// Marks mining/haul entities as rewindable so playback guards can pause them cleanly.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XMiningRewindableSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<Carrier>().WithNone<RewindableTag>().WithEntityAccess())
            {
                ecb.AddComponent<RewindableTag>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<MiningVessel>().WithNone<RewindableTag>().WithEntityAccess())
            {
                ecb.AddComponent<RewindableTag>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<SpawnResource>().WithNone<RewindableTag>().WithEntityAccess())
            {
                ecb.AddComponent<RewindableTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Captures deterministic snapshots of mining/haul state after each simulation tick.
    /// Uses ring buffer pattern to cap memory usage at SnapshotHorizon capacity.
    /// Disabled by default - enable only when rewind/debugging is needed.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial struct Space4XMiningTimeSpineRecordSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMiningTimeSpine>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var spineEntity = SystemAPI.GetSingletonEntity<Space4XMiningTimeSpine>();
            var spine = SystemAPI.GetComponentRW<Space4XMiningTimeSpine>(spineEntity);

            var snapshots = state.EntityManager.GetBuffer<MiningSnapshot>(spineEntity);
            var telemetrySnapshots = state.EntityManager.GetBuffer<MiningTelemetrySnapshot>(spineEntity);
            var commandLog = state.EntityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);
            var skillLog = state.EntityManager.GetBuffer<SkillChangeLogEntry>(spineEntity);
            var skillSnapshots = state.EntityManager.GetBuffer<SkillSnapshot>(spineEntity);

            var horizon = spine.ValueRO.SnapshotHorizon > 0 ? spine.ValueRO.SnapshotHorizon : Space4XMiningTimeSpine.DefaultSnapshotHorizon;
            var cutoff = ComputeCutoff(time.Tick, horizon);

            // Prune old entries to enforce ring buffer capacity
            PruneSnapshotBuffer(snapshots, cutoff);
            PruneTelemetryBuffer(telemetrySnapshots, cutoff);
            PruneCommandLog(commandLog, cutoff);
            PruneSkillLog(skillLog, cutoff);
            PruneSkillSnapshots(skillSnapshots, cutoff);

            // Enforce capacity limits to prevent unbounded growth
            EnforceBufferCapacity(snapshots, (int)horizon);
            EnforceBufferCapacity(telemetrySnapshots, (int)horizon);
            EnforceBufferCapacity(commandLog, (int)horizon);
            EnforceBufferCapacity(skillLog, (int)horizon);
            EnforceBufferCapacity(skillSnapshots, (int)horizon);

            RemoveTickEntries(snapshots, time.Tick);
            RemoveTelemetryTick(telemetrySnapshots, time.Tick);
            RemoveSkillTick(skillSnapshots, time.Tick);

            RecordAsteroidSnapshots(ref state, snapshots, time.Tick);
            RecordVesselSnapshots(ref state, snapshots, time.Tick);
            RecordCarrierSnapshots(ref state, snapshots, time.Tick);
            RecordSpawnSnapshots(ref state, snapshots, time.Tick);
            RecordTelemetrySnapshot(ref state, telemetrySnapshots, time.Tick);
            RecordSkillSnapshot(ref state, skillSnapshots, time.Tick);

            spine.ValueRW.LastSnapshotTick = time.Tick;
        }

        private static uint ComputeCutoff(uint tick, uint horizon)
        {
            return tick > horizon ? tick - horizon : 0;
        }

        private static void PruneSnapshotBuffer(DynamicBuffer<MiningSnapshot> buffer, uint cutoffTick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void PruneTelemetryBuffer(DynamicBuffer<MiningTelemetrySnapshot> buffer, uint cutoffTick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void PruneCommandLog(DynamicBuffer<MiningCommandLogEntry> buffer, uint cutoffTick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void PruneSkillLog(DynamicBuffer<SkillChangeLogEntry> buffer, uint cutoffTick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void PruneSkillSnapshots(DynamicBuffer<SkillSnapshot> buffer, uint cutoffTick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Enforces ring buffer capacity by removing oldest entries if buffer exceeds capacity.
        /// </summary>
        private static void EnforceBufferCapacity<T>(DynamicBuffer<T> buffer, int capacity) where T : unmanaged
        {
            if (buffer.Length <= capacity)
                return;

            // Remove oldest entries (assuming they're sorted by tick or insertion order)
            var toRemove = buffer.Length - capacity;
            for (var i = 0; i < toRemove; i++)
            {
                buffer.RemoveAt(0);
            }
        }

        private static void RemoveTickEntries(DynamicBuffer<MiningSnapshot> buffer, uint tick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick == tick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void RemoveTelemetryTick(DynamicBuffer<MiningTelemetrySnapshot> buffer, uint tick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick == tick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void RemoveSkillTick(DynamicBuffer<SkillSnapshot> buffer, uint tick)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick == tick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private void RecordAsteroidSnapshots(ref SystemState state, DynamicBuffer<MiningSnapshot> snapshots, uint tick)
        {
            foreach (var (asteroid, resourceState, transform, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<ResourceSourceState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                snapshots.Add(new MiningSnapshot
                {
                    Tick = tick,
                    Entity = entity,
                    RelatedEntity = Entity.Null,
                    ResourceType = asteroid.ValueRO.ResourceType,
                    Amount = resourceState.ValueRO.UnitsRemaining,
                    Position = transform.ValueRO.Position,
                    Type = MiningSnapshotType.Asteroid
                });
            }
        }

        private void RecordVesselSnapshots(ref SystemState state, DynamicBuffer<MiningSnapshot> snapshots, uint tick)
        {
            foreach (var (vessel, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithEntityAccess())
            {
                snapshots.Add(new MiningSnapshot
                {
                    Tick = tick,
                    Entity = entity,
                    RelatedEntity = Entity.Null,
                    ResourceType = vessel.ValueRO.CargoResourceType,
                    Amount = vessel.ValueRO.CurrentCargo,
                    Position = float3.zero,
                    Type = MiningSnapshotType.VesselCargo
                });
            }
        }

        private void RecordCarrierSnapshots(ref SystemState state, DynamicBuffer<MiningSnapshot> snapshots, uint tick)
        {
            foreach (var (storage, entity) in SystemAPI.Query<DynamicBuffer<ResourceStorage>>().WithEntityAccess())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    snapshots.Add(new MiningSnapshot
                    {
                        Tick = tick,
                        Entity = entity,
                        RelatedEntity = Entity.Null,
                        ResourceType = storage[i].Type,
                        Amount = storage[i].Amount,
                        Position = float3.zero,
                        Type = MiningSnapshotType.CarrierStorage
                    });
                }
            }
        }

        private void RecordSpawnSnapshots(ref SystemState state, DynamicBuffer<MiningSnapshot> snapshots, uint tick)
        {
            foreach (var (spawn, transform, entity) in SystemAPI.Query<RefRO<SpawnResource>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                snapshots.Add(new MiningSnapshot
                {
                    Tick = tick,
                    Entity = entity,
                    RelatedEntity = spawn.ValueRO.SourceEntity,
                    ResourceType = spawn.ValueRO.Type,
                    Amount = spawn.ValueRO.Amount,
                    Position = transform.ValueRO.Position,
                    Type = MiningSnapshotType.SpawnPickup
                });
            }
        }

        private void RecordTelemetrySnapshot(ref SystemState state, DynamicBuffer<MiningTelemetrySnapshot> snapshots, uint tick)
        {
            if (!SystemAPI.TryGetSingleton<Space4XMiningTelemetry>(out var telemetry))
            {
                return;
            }

            snapshots.Add(new MiningTelemetrySnapshot
            {
                Tick = tick,
                OreInHold = telemetry.OreInHold
            });
        }

        private void RecordSkillSnapshot(ref SystemState state, DynamicBuffer<SkillSnapshot> snapshots, uint tick)
        {
            foreach (var (xp, entity) in SystemAPI.Query<RefRO<SkillExperienceGain>>().WithEntityAccess())
            {
                snapshots.Add(new SkillSnapshot
                {
                    Tick = tick,
                    Entity = entity,
                    MiningXp = xp.ValueRO.MiningXp,
                    HaulingXp = xp.ValueRO.HaulingXp,
                    CombatXp = xp.ValueRO.CombatXp,
                    RepairXp = xp.ValueRO.RepairXp,
                    ExplorationXp = xp.ValueRO.ExplorationXp
                });
            }
        }
    }

    /// <summary>
    /// Applies recorded snapshots (or the command log when snapshots are pruned) during rewind playback and catch-up.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XMiningTimeSpinePlaybackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMiningTimeSpine>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode == RewindMode.Record)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var targetTick = rewind.Mode == RewindMode.Playback ? rewind.PlaybackTick : time.Tick;
            var spineEntity = SystemAPI.GetSingletonEntity<Space4XMiningTimeSpine>();
            var snapshots = state.EntityManager.GetBuffer<MiningSnapshot>(spineEntity);
            var telemetrySnapshots = state.EntityManager.GetBuffer<MiningTelemetrySnapshot>(spineEntity);
            var commandLog = state.EntityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);
            var skillLog = state.EntityManager.GetBuffer<SkillChangeLogEntry>(spineEntity);
            var skillSnapshots = state.EntityManager.GetBuffer<SkillSnapshot>(spineEntity);
            var spine = SystemAPI.GetComponentRW<Space4XMiningTimeSpine>(spineEntity);

            if (!TryFindSnapshotTick(snapshots, targetTick, out var snapshotTick))
            {
                return;
            }

            ApplySnapshot(ref state, snapshots, telemetrySnapshots, skillSnapshots, snapshotTick);

            if (snapshotTick < targetTick)
            {
                if (commandLog.Length > 0)
                {
                    ApplyCommands(ref state, commandLog, snapshotTick, targetTick);
                }

                if (skillLog.Length > 0)
                {
                    ApplySkillCommands(ref state, skillLog, snapshotTick, targetTick);
                }
            }

            spine.ValueRW.LastPlaybackTick = targetTick;
        }

        private static bool TryFindSnapshotTick(DynamicBuffer<MiningSnapshot> snapshots, uint targetTick, out uint snapshotTick)
        {
            var found = false;
            snapshotTick = 0;

            for (var i = 0; i < snapshots.Length; i++)
            {
                var tick = snapshots[i].Tick;
                if (tick > targetTick)
                {
                    continue;
                }

                if (!found || tick > snapshotTick)
                {
                    snapshotTick = tick;
                    found = true;
                }
            }

            return found;
        }

        private void ApplySnapshot(ref SystemState state, DynamicBuffer<MiningSnapshot> snapshots, DynamicBuffer<MiningTelemetrySnapshot> telemetrySnapshots, DynamicBuffer<SkillSnapshot> skillSnapshots, uint tick)
        {
            var carrierSlots = new Dictionary<Entity, List<MiningSnapshot>>();
            var spawns = new List<MiningSnapshot>();
            var em = state.EntityManager;

            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot.Tick != tick)
                {
                    continue;
                }

                switch (snapshot.Type)
                {
                    case MiningSnapshotType.Asteroid:
                        if (em.HasComponent<ResourceSourceState>(snapshot.Entity))
                        {
                            var resourceState = em.GetComponentData<ResourceSourceState>(snapshot.Entity);
                            resourceState.UnitsRemaining = snapshot.Amount;
                            em.SetComponentData(snapshot.Entity, resourceState);
                        }
                        break;

                    case MiningSnapshotType.VesselCargo:
                        if (em.HasComponent<MiningVessel>(snapshot.Entity))
                        {
                            var vessel = em.GetComponentData<MiningVessel>(snapshot.Entity);
                            vessel.CurrentCargo = snapshot.Amount;
                            if (snapshot.Amount > 0f)
                            {
                                vessel.CargoResourceType = snapshot.ResourceType;
                            }
                            em.SetComponentData(snapshot.Entity, vessel);
                        }
                        break;

                    case MiningSnapshotType.CarrierStorage:
                        if (!carrierSlots.TryGetValue(snapshot.Entity, out var slots))
                        {
                            slots = new List<MiningSnapshot>();
                            carrierSlots[snapshot.Entity] = slots;
                        }
                        slots.Add(snapshot);
                        break;

                    case MiningSnapshotType.SpawnPickup:
                        spawns.Add(snapshot);
                        break;
                }
            }

            foreach (var kvp in carrierSlots)
            {
                if (!em.HasBuffer<ResourceStorage>(kvp.Key))
                {
                    continue;
                }

                var buffer = em.GetBuffer<ResourceStorage>(kvp.Key);
                var capacities = new Dictionary<ResourceType, float>();
                for (var i = 0; i < buffer.Length; i++)
                {
                    var slot = buffer[i];
                    capacities[slot.Type] = slot.Capacity;
                }

                buffer.Clear();
                foreach (var snapshot in kvp.Value)
                {
                    var capacity = capacities.TryGetValue(snapshot.ResourceType, out var existingCapacity)
                        ? existingCapacity
                        : ResourceStorage.Create(snapshot.ResourceType).Capacity;

                    buffer.Add(new ResourceStorage
                    {
                        Type = snapshot.ResourceType,
                        Amount = math.max(0f, snapshot.Amount),
                        Capacity = capacity
                    });
                }
            }

            RebuildSpawns(ref state, spawns);
            ApplyTelemetrySnapshot(ref state, telemetrySnapshots, tick);
            ApplySkillSnapshot(ref state, skillSnapshots, tick);
        }

        private static void RebuildSpawns(ref SystemState state, List<MiningSnapshot> spawns)
        {
            var em = state.EntityManager;
            using var existing = em.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>()).ToEntityArray(Allocator.Temp);
            foreach (var entity in existing)
            {
                em.DestroyEntity(entity);
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                var snapshot = spawns[i];
                var entity = em.CreateEntity(typeof(SpawnResource), typeof(LocalTransform));
                em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(snapshot.Position, quaternion.identity, 1f));
                em.SetComponentData(entity, new SpawnResource
                {
                    Type = snapshot.ResourceType,
                    Amount = snapshot.Amount,
                    SourceEntity = snapshot.RelatedEntity,
                    SpawnTick = snapshot.Tick
                });
            }
        }

        private static void ApplyTelemetrySnapshot(ref SystemState state, DynamicBuffer<MiningTelemetrySnapshot> telemetrySnapshots, uint targetTick)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTelemetry>());
            var entity = query.IsEmptyIgnoreFilter
                ? em.CreateEntity(typeof(Space4XMiningTelemetry))
                : query.GetSingletonEntity();

            var bestTick = 0u;
            var found = false;
            var ore = 0f;

            for (var i = 0; i < telemetrySnapshots.Length; i++)
            {
                var snapshot = telemetrySnapshots[i];
                if (snapshot.Tick > targetTick)
                {
                    continue;
                }

                if (!found || snapshot.Tick > bestTick)
                {
                    ore = snapshot.OreInHold;
                    bestTick = snapshot.Tick;
                    found = true;
                }
            }

            if (!found && telemetrySnapshots.Length > 0)
            {
                var fallback = telemetrySnapshots[telemetrySnapshots.Length - 1];
                ore = fallback.OreInHold;
                bestTick = fallback.Tick;
                found = true;
            }

            var telemetry = em.GetComponentData<Space4XMiningTelemetry>(entity);
            telemetry.OreInHold = ore;
            telemetry.LastUpdateTick = found ? bestTick : targetTick;
            em.SetComponentData(entity, telemetry);
        }

        private static void ApplySkillSnapshot(ref SystemState state, DynamicBuffer<SkillSnapshot> skillSnapshots, uint targetTick)
        {
            var em = state.EntityManager;
            var bestPerEntity = new Dictionary<Entity, SkillSnapshot>();

            for (var i = 0; i < skillSnapshots.Length; i++)
            {
                var snapshot = skillSnapshots[i];
                if (snapshot.Tick > targetTick)
                {
                    continue;
                }

                if (!bestPerEntity.TryGetValue(snapshot.Entity, out var existing) || snapshot.Tick > existing.Tick)
                {
                    bestPerEntity[snapshot.Entity] = snapshot;
                }
            }

            foreach (var kvp in bestPerEntity)
            {
                var snapshot = kvp.Value;
                if (!em.Exists(snapshot.Entity))
                {
                    continue;
                }

                if (!em.HasComponent<SkillExperienceGain>(snapshot.Entity))
                {
                    em.AddComponentData(snapshot.Entity, new SkillExperienceGain());
                }

                if (!em.HasComponent<CrewSkills>(snapshot.Entity))
                {
                    em.AddComponentData(snapshot.Entity, new CrewSkills());
                }

                var xp = em.GetComponentData<SkillExperienceGain>(snapshot.Entity);
                xp.MiningXp = snapshot.MiningXp;
                xp.HaulingXp = snapshot.HaulingXp;
                xp.CombatXp = snapshot.CombatXp;
                xp.RepairXp = snapshot.RepairXp;
                xp.ExplorationXp = snapshot.ExplorationXp;
                xp.LastProcessedTick = snapshot.Tick;
                em.SetComponentData(snapshot.Entity, xp);

                var skills = em.GetComponentData<CrewSkills>(snapshot.Entity);
                skills.MiningSkill = Space4XSkillUtility.XpToSkill(snapshot.MiningXp);
                skills.HaulingSkill = Space4XSkillUtility.XpToSkill(snapshot.HaulingXp);
                skills.CombatSkill = Space4XSkillUtility.XpToSkill(snapshot.CombatXp);
                skills.RepairSkill = Space4XSkillUtility.XpToSkill(snapshot.RepairXp);
                skills.ExplorationSkill = Space4XSkillUtility.XpToSkill(snapshot.ExplorationXp);
                em.SetComponentData(snapshot.Entity, skills);
            }
        }

        private void ApplyCommands(ref SystemState state, DynamicBuffer<MiningCommandLogEntry> commandLog, uint fromTick, uint toTick)
        {
            var em = state.EntityManager;
            var spawns = new List<SpawnStub>();

            foreach (var (spawn, transform) in SystemAPI.Query<RefRO<SpawnResource>, RefRO<LocalTransform>>())
            {
                spawns.Add(new SpawnStub
                {
                    Amount = spawn.ValueRO.Amount,
                    Position = transform.ValueRO.Position,
                    ResourceType = spawn.ValueRO.Type
                });
            }

            for (var i = 0; i < commandLog.Length; i++)
            {
                var command = commandLog[i];
                if (command.Tick <= fromTick || command.Tick > toTick)
                {
                    continue;
                }

                switch (command.CommandType)
                {
                    case MiningCommandType.Gather:
                        if (em.HasComponent<ResourceSourceState>(command.SourceEntity))
                        {
                            var resourceState = em.GetComponentData<ResourceSourceState>(command.SourceEntity);
                            resourceState.UnitsRemaining = math.max(0f, resourceState.UnitsRemaining - command.Amount);
                            em.SetComponentData(command.SourceEntity, resourceState);
                        }

                        if (em.HasComponent<MiningVessel>(command.TargetEntity))
                        {
                            var vessel = em.GetComponentData<MiningVessel>(command.TargetEntity);
                            vessel.CurrentCargo += command.Amount;
                            vessel.CargoResourceType = command.ResourceType;
                            em.SetComponentData(command.TargetEntity, vessel);
                        }
                        break;

                    case MiningCommandType.Spawn:
                        spawns.Add(new SpawnStub
                        {
                            Amount = command.Amount,
                            Position = command.Position,
                            ResourceType = command.ResourceType
                        });
                        break;

                    case MiningCommandType.Pickup:
                        ApplyPickupToCarrier(em, command.TargetEntity, command.ResourceType, command.Amount);
                        RemoveFromSpawns(spawns, command.ResourceType, command.Amount);
                        break;
                }
            }

            RebuildSpawnResources(em, spawns, toTick);

            var telemetryEntity = SystemAPI.TryGetSingletonEntity<Space4XMiningTelemetry>(out var existingTelemetry)
                ? existingTelemetry
                : em.CreateEntity(typeof(Space4XMiningTelemetry));

            var totalHeld = 0f;
            foreach (var storage in SystemAPI.Query<DynamicBuffer<ResourceStorage>>())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    totalHeld += storage[i].Amount;
                }
            }

            em.SetComponentData(telemetryEntity, new Space4XMiningTelemetry
            {
                OreInHold = totalHeld,
                LastUpdateTick = toTick
            });
        }

        private static void ApplySkillCommands(ref SystemState state, DynamicBuffer<SkillChangeLogEntry> skillLog, uint fromTick, uint toTick)
        {
            var em = state.EntityManager;
            var perEntityXp = new Dictionary<Entity, SkillExperienceGain>();

            for (var i = 0; i < skillLog.Length; i++)
            {
                var entry = skillLog[i];
                if (entry.Tick <= fromTick || entry.Tick > toTick)
                {
                    continue;
                }

                if (!em.Exists(entry.TargetEntity))
                {
                    continue;
                }

                if (!perEntityXp.TryGetValue(entry.TargetEntity, out var xp))
                {
                    xp = em.HasComponent<SkillExperienceGain>(entry.TargetEntity)
                        ? em.GetComponentData<SkillExperienceGain>(entry.TargetEntity)
                        : new SkillExperienceGain();
                }

                switch (entry.Domain)
                {
                    case SkillDomain.Mining:
                        xp.MiningXp += entry.DeltaXp;
                        break;
                    case SkillDomain.Hauling:
                        xp.HaulingXp += entry.DeltaXp;
                        break;
                    case SkillDomain.Combat:
                        xp.CombatXp += entry.DeltaXp;
                        break;
                    case SkillDomain.Repair:
                        xp.RepairXp += entry.DeltaXp;
                        break;
                    case SkillDomain.Exploration:
                        xp.ExplorationXp += entry.DeltaXp;
                        break;
                }

                xp.LastProcessedTick = entry.Tick;
                perEntityXp[entry.TargetEntity] = xp;
            }

            foreach (var kvp in perEntityXp)
            {
                var entity = kvp.Key;
                var xp = kvp.Value;

                if (!em.HasComponent<SkillExperienceGain>(entity))
                {
                    em.AddComponentData(entity, new SkillExperienceGain());
                }

                if (!em.HasComponent<CrewSkills>(entity))
                {
                    em.AddComponentData(entity, new CrewSkills());
                }

                em.SetComponentData(entity, xp);

                var skills = new CrewSkills
                {
                    MiningSkill = Space4XSkillUtility.XpToSkill(xp.MiningXp),
                    HaulingSkill = Space4XSkillUtility.XpToSkill(xp.HaulingXp),
                    CombatSkill = Space4XSkillUtility.XpToSkill(xp.CombatXp),
                    RepairSkill = Space4XSkillUtility.XpToSkill(xp.RepairXp),
                    ExplorationSkill = Space4XSkillUtility.XpToSkill(xp.ExplorationXp)
                };

                em.SetComponentData(entity, skills);
            }
        }

        private static void RemoveFromSpawns(List<SpawnStub> spawns, ResourceType type, float amount)
        {
            var remaining = amount;
            for (var i = 0; i < spawns.Count && remaining > 1e-4f; i++)
            {
                if (spawns[i].ResourceType != type || spawns[i].Amount <= 0f)
                {
                    continue;
                }

                var stub = spawns[i];
                var take = math.min(remaining, stub.Amount);
                stub.Amount -= take;
                spawns[i] = stub;
                remaining -= take;
            }
        }

        private static void ApplyPickupToCarrier(EntityManager em, Entity carrier, ResourceType type, float amount)
        {
            if (!em.HasBuffer<ResourceStorage>(carrier))
            {
                return;
            }

            var storage = em.GetBuffer<ResourceStorage>(carrier);
            for (var i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type != type)
                {
                    continue;
                }

                var slot = storage[i];
                slot.Amount += amount;
                storage[i] = slot;
                return;
            }

            if (storage.Length < 4)
            {
                var slot = ResourceStorage.Create(type);
                slot.AddAmount(amount);
                storage.Add(slot);
            }
        }

        private static void RebuildSpawnResources(EntityManager em, List<SpawnStub> spawns, uint tick)
        {
            using var existing = em.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>()).ToEntityArray(Allocator.Temp);
            foreach (var entity in existing)
            {
                em.DestroyEntity(entity);
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                if (spawns[i].Amount <= 1e-4f)
                {
                    continue;
                }

                var stub = spawns[i];
                var entity = em.CreateEntity(typeof(SpawnResource), typeof(LocalTransform));
                em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(stub.Position, quaternion.identity, 1f));
                em.SetComponentData(entity, new SpawnResource
                {
                    Type = stub.ResourceType,
                    Amount = stub.Amount,
                    SourceEntity = Entity.Null,
                    SpawnTick = tick
                });
            }
        }

        private struct SpawnStub
        {
            public float Amount;
            public float3 Position;
            public ResourceType ResourceType;
        }
    }
}

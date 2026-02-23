using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Progression;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [Flags]
    public enum Space4XRunMetaUnlockFlags : ushort
    {
        None = 0,
        DamageTypeSpecialist = 1 << 0,
        MitigationSpecialist = 1 << 1,
        CloakOperator = 1 << 2,
        ChronoOperator = 1 << 3,
        OrdnanceSpecialist = 1 << 4,
        InterceptorSpecialist = 1 << 5,
        CapitalHunter = 1 << 6,
        CacheHunter = 1 << 7
    }

    public struct Space4XRunMetaProficiencyState : IComponentData
    {
        public float DamageDealtEnergy;
        public float DamageDealtThermal;
        public float DamageDealtEM;
        public float DamageDealtRadiation;
        public float DamageDealtCaustic;
        public float DamageDealtKinetic;
        public float DamageDealtExplosive;

        public float DamageMitigatedEnergy;
        public float DamageMitigatedThermal;
        public float DamageMitigatedEM;
        public float DamageMitigatedRadiation;
        public float DamageMitigatedCaustic;
        public float DamageMitigatedKinetic;
        public float DamageMitigatedExplosive;

        public float CloakSeconds;
        public float TimeStopRequestedSeconds;
        public float MissileDamageDealt;

        public int CraftShotDown;
        public int CapitalShipsDestroyed;
        public int HiddenCachesFound;
    }

    public struct Space4XRunMetaProficiencyConfig : IComponentData
    {
        public float DamageThreshold;
        public float MitigationThreshold;
        public float CloakSecondsThreshold;
        public float TimeStopSecondsThreshold;
        public float MissileDamageThreshold;
        public int CraftShotDownThreshold;
        public int CapitalShipsDestroyedThreshold;
        public int HiddenCachesFoundThreshold;

        public static Space4XRunMetaProficiencyConfig Default => new Space4XRunMetaProficiencyConfig
        {
            DamageThreshold = 1600f,
            MitigationThreshold = 900f,
            CloakSecondsThreshold = 18f,
            TimeStopSecondsThreshold = 12f,
            MissileDamageThreshold = 450f,
            CraftShotDownThreshold = 18,
            CapitalShipsDestroyedThreshold = 2,
            HiddenCachesFoundThreshold = 3
        };
    }

    public struct Space4XRunMetaUnlockState : IComponentData
    {
        public Space4XRunMetaUnlockFlags Flags;
        public byte UnlockCount;
    }

    public struct Space4XRunMetaDamageEventCursor : IComponentData
    {
        public int ProcessedCount;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XRunMetaUnlockRecord : IBufferElementData
    {
        public FixedString64Bytes UnlockId;
        public Space4XRunMetaUnlockFlags Flag;
        public uint Tick;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XRunChainLightningSystem))]
    public partial struct Space4XFleetcrawlMetaProficiencySystem : ISystem
    {
        private ComponentLookup<Space4XRunPlayerTag> _playerTagLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _playerTagLookup = state.GetComponentLookup<Space4XRunPlayerTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlDirectorState>(out var directorEntity))
            {
                return;
            }

            EnsureDirectorState(ref state, directorEntity);

            var em = state.EntityManager;
            var meta = em.GetComponentData<Space4XRunMetaProficiencyState>(directorEntity);
            var config = em.GetComponentData<Space4XRunMetaProficiencyConfig>(directorEntity);
            var unlockState = em.GetComponentData<Space4XRunMetaUnlockState>(directorEntity);
            var unlockRecords = em.GetBuffer<Space4XRunMetaUnlockRecord>(directorEntity);
            var deltaSeconds = ResolveDeltaSeconds(time);

            _playerTagLookup.Update(ref state);
            EnsureDamageEventCursors(ref state);

            foreach (var focus in SystemAPI.Query<RefRO<ShipPowerFocus>>().WithAll<Space4XRunPlayerTag>())
            {
                if (focus.ValueRO.Mode == ShipPowerFocusMode.Stealth)
                {
                    ProgressionMath.AccumulatePositive(ref meta.CloakSeconds, deltaSeconds);
                }
            }

            foreach (var request in SystemAPI.Query<RefRO<TimeStopRequest>>().WithAll<Space4XRunPlayerTag>())
            {
                ProgressionMath.AccumulatePositive(ref meta.TimeStopRequestedSeconds, request.ValueRO.DurationSeconds);
            }

            foreach (var (events, side, cursor) in SystemAPI.Query<DynamicBuffer<DamageEvent>, RefRO<ScenarioSide>, RefRW<Space4XRunMetaDamageEventCursor>>()
                         .WithAny<Space4XRunPlayerTag, Space4XRunEnemyTag>())
            {
                var from = math.clamp(cursor.ValueRO.ProcessedCount, 0, events.Length);
                var to = events.Length;
                for (var i = from; i < to; i++)
                {
                    var evt = events[i];
                    if (evt.Source == Entity.Null)
                    {
                        continue;
                    }

                    var damageType = Space4XWeapon.ResolveDamageType(evt.WeaponType);
                    var appliedDamage = math.max(0f, evt.ShieldDamage + evt.ArmorDamage + evt.HullDamage);

                    if (side.ValueRO.Side == 1 && _playerTagLookup.HasComponent(evt.Source))
                    {
                        AccumulateDamage(ref meta, damageType, appliedDamage, dealt: true);
                        if (evt.WeaponType == WeaponType.Missile)
                        {
                            ProgressionMath.AccumulatePositive(ref meta.MissileDamageDealt, appliedDamage);
                        }
                    }
                    else if (side.ValueRO.Side == 0 && !_playerTagLookup.HasComponent(evt.Source))
                    {
                        var mitigated = math.max(0f, evt.RawDamage - appliedDamage);
                        AccumulateDamage(ref meta, damageType, mitigated, dealt: false);
                    }
                }

                cursor.ValueRW.ProcessedCount = events.Length;
            }

            var resolvedFlags = ResolveUnlockFlags(in meta, in config);
            var addedFlags = resolvedFlags & ~unlockState.Flags;
            if (addedFlags != Space4XRunMetaUnlockFlags.None)
            {
                unlockState.Flags = resolvedFlags;
                unlockState.UnlockCount = (byte)math.min(255, math.countbits((uint)(ushort)unlockState.Flags));
                AppendUnlockRecords(unlockRecords, addedFlags, time.Tick);
                Debug.Log($"[FleetcrawlMeta] UNLOCK flags={unlockState.Flags} count={unlockState.UnlockCount} tick={time.Tick}.");
            }

            em.SetComponentData(directorEntity, meta);
            em.SetComponentData(directorEntity, unlockState);
        }

        private static void EnsureDirectorState(ref SystemState state, Entity directorEntity)
        {
            var em = state.EntityManager;
            if (!em.HasComponent<Space4XRunMetaProficiencyState>(directorEntity))
            {
                em.AddComponentData(directorEntity, new Space4XRunMetaProficiencyState());
            }

            if (!em.HasComponent<Space4XRunMetaProficiencyConfig>(directorEntity))
            {
                em.AddComponentData(directorEntity, Space4XRunMetaProficiencyConfig.Default);
            }

            if (!em.HasComponent<Space4XRunMetaUnlockState>(directorEntity))
            {
                em.AddComponentData(directorEntity, new Space4XRunMetaUnlockState());
            }

            if (!em.HasBuffer<Space4XRunMetaUnlockRecord>(directorEntity))
            {
                em.AddBuffer<Space4XRunMetaUnlockRecord>(directorEntity);
            }
        }

        private void EnsureDamageEventCursors(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, _, entity) in SystemAPI.Query<DynamicBuffer<DamageEvent>, RefRO<ScenarioSide>>()
                         .WithAny<Space4XRunPlayerTag, Space4XRunEnemyTag>()
                         .WithNone<Space4XRunMetaDamageEventCursor>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new Space4XRunMetaDamageEventCursor { ProcessedCount = 0 });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static float ResolveDeltaSeconds(in TimeState time)
        {
            if (time.FixedDeltaTime > 0f)
            {
                return time.FixedDeltaTime;
            }

            if (time.DeltaSeconds > 0f)
            {
                return time.DeltaSeconds;
            }

            return 1f / 60f;
        }

        private static void AccumulateDamage(ref Space4XRunMetaProficiencyState state, Space4XDamageType damageType, float amount, bool dealt)
        {
            if (amount <= 0f)
            {
                return;
            }

            switch (damageType)
            {
                case Space4XDamageType.Energy:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtEnergy, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedEnergy, amount);
                    break;
                case Space4XDamageType.Thermal:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtThermal, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedThermal, amount);
                    break;
                case Space4XDamageType.EM:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtEM, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedEM, amount);
                    break;
                case Space4XDamageType.Radiation:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtRadiation, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedRadiation, amount);
                    break;
                case Space4XDamageType.Caustic:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtCaustic, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedCaustic, amount);
                    break;
                case Space4XDamageType.Kinetic:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtKinetic, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedKinetic, amount);
                    break;
                case Space4XDamageType.Explosive:
                    if (dealt) ProgressionMath.AccumulatePositive(ref state.DamageDealtExplosive, amount);
                    else ProgressionMath.AccumulatePositive(ref state.DamageMitigatedExplosive, amount);
                    break;
            }
        }

        private static Space4XRunMetaUnlockFlags ResolveUnlockFlags(
            in Space4XRunMetaProficiencyState state,
            in Space4XRunMetaProficiencyConfig config)
        {
            var flags = Space4XRunMetaUnlockFlags.None;

            var dealtTotal = state.DamageDealtEnergy +
                             state.DamageDealtThermal +
                             state.DamageDealtEM +
                             state.DamageDealtRadiation +
                             state.DamageDealtCaustic +
                             state.DamageDealtKinetic +
                             state.DamageDealtExplosive;
            var mitigatedTotal = state.DamageMitigatedEnergy +
                                 state.DamageMitigatedThermal +
                                 state.DamageMitigatedEM +
                                 state.DamageMitigatedRadiation +
                                 state.DamageMitigatedCaustic +
                                 state.DamageMitigatedKinetic +
                                 state.DamageMitigatedExplosive;

            if (dealtTotal >= config.DamageThreshold) flags |= Space4XRunMetaUnlockFlags.DamageTypeSpecialist;
            if (mitigatedTotal >= config.MitigationThreshold) flags |= Space4XRunMetaUnlockFlags.MitigationSpecialist;
            if (state.CloakSeconds >= config.CloakSecondsThreshold) flags |= Space4XRunMetaUnlockFlags.CloakOperator;
            if (state.TimeStopRequestedSeconds >= config.TimeStopSecondsThreshold) flags |= Space4XRunMetaUnlockFlags.ChronoOperator;
            if (state.MissileDamageDealt >= config.MissileDamageThreshold) flags |= Space4XRunMetaUnlockFlags.OrdnanceSpecialist;
            if (state.CraftShotDown >= config.CraftShotDownThreshold) flags |= Space4XRunMetaUnlockFlags.InterceptorSpecialist;
            if (state.CapitalShipsDestroyed >= config.CapitalShipsDestroyedThreshold) flags |= Space4XRunMetaUnlockFlags.CapitalHunter;
            if (state.HiddenCachesFound >= config.HiddenCachesFoundThreshold) flags |= Space4XRunMetaUnlockFlags.CacheHunter;

            return flags;
        }

        private static void AppendUnlockRecords(
            DynamicBuffer<Space4XRunMetaUnlockRecord> records,
            Space4XRunMetaUnlockFlags addedFlags,
            uint tick)
        {
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.DamageTypeSpecialist, new FixedString64Bytes("meta_unlock_damage_type_specialist"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.MitigationSpecialist, new FixedString64Bytes("meta_unlock_mitigation_specialist"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.CloakOperator, new FixedString64Bytes("meta_unlock_cloak_operator"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.ChronoOperator, new FixedString64Bytes("meta_unlock_chrono_operator"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.OrdnanceSpecialist, new FixedString64Bytes("meta_unlock_ordnance_specialist"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.InterceptorSpecialist, new FixedString64Bytes("meta_unlock_interceptor_specialist"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.CapitalHunter, new FixedString64Bytes("meta_unlock_capital_hunter"), tick);
            AppendUnlockRecord(records, addedFlags, Space4XRunMetaUnlockFlags.CacheHunter, new FixedString64Bytes("meta_unlock_cache_hunter"), tick);
        }

        private static void AppendUnlockRecord(
            DynamicBuffer<Space4XRunMetaUnlockRecord> records,
            Space4XRunMetaUnlockFlags addedFlags,
            Space4XRunMetaUnlockFlags flag,
            in FixedString64Bytes unlockId,
            uint tick)
        {
            if ((addedFlags & flag) == 0)
            {
                return;
            }

            for (var i = 0; i < records.Length; i++)
            {
                if (records[i].Flag == flag)
                {
                    return;
                }
            }

            records.Add(new Space4XRunMetaUnlockRecord
            {
                UnlockId = unlockId,
                Flag = flag,
                Tick = tick
            });
        }
    }
}

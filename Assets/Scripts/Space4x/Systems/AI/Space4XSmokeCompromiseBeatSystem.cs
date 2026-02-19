using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    [UpdateInGroup(typeof(PureDOTS.Systems.AISystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Agency.ControlLinkHealthSystem))]
    public partial struct Space4XSmokeCompromiseBeatSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XSmokeCompromiseBeatConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = timeState.Tick;
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);

            foreach (var (config, anchorEntity) in SystemAPI.Query<RefRW<Space4XSmokeCompromiseBeatConfig>>()
                         .WithEntityAccess())
            {
                if (config.ValueRO.Initialized == 0)
                {
                    InitializeConfig(ref config.ValueRW, fixedDt, ref state);
                }

                ApplyCommsDrop(anchorEntity, tick, config.ValueRO, ref state);
                ApplyControllerCompromise(anchorEntity, tick, ref config.ValueRW, ref state);
                ApplyHackBeat(anchorEntity, tick, ref config.ValueRW, ref state);
            }
        }

        private void InitializeConfig(ref Space4XSmokeCompromiseBeatConfig config, float fixedDt, ref SystemState state)
        {
            config.CommsDropStartTick = SecondsToTicks(config.CommsDropStartSeconds, fixedDt);
            config.CommsDropEndTick = config.CommsDropStartTick + SecondsToTicks(config.CommsDropDurationSeconds, fixedDt);
            config.ControllerCompromiseTick = SecondsToTicks(config.ControllerCompromiseSeconds, fixedDt);
            config.HackStartTick = SecondsToTicks(config.HackStartSeconds, fixedDt);

            if (config.HackerEntity == Entity.Null)
            {
                if (SystemAPI.TryGetSingletonEntity<Space4XSmokeHackerTag>(out var hacker))
                {
                    config.HackerEntity = hacker;
                }
                else
                {
                    var hackerEntity = state.EntityManager.CreateEntity(typeof(Space4XSmokeHackerTag));
                    config.HackerEntity = hackerEntity;
                }
            }

            config.Initialized = 1;
        }

        private void ApplyCommsDrop(Entity anchorEntity, uint tick, in Space4XSmokeCompromiseBeatConfig config, ref SystemState state)
        {
            bool inDrop = tick >= config.CommsDropStartTick && tick < config.CommsDropEndTick;

            foreach (var (link, entity) in SystemAPI.Query<RefRW<ControlLinkState>>().WithEntityAccess())
            {
                if (link.ValueRO.ControllerEntity != anchorEntity)
                {
                    continue;
                }

                if (inDrop)
                {
                    var updated = link.ValueRO;
                    updated.CommsQuality01 = config.CommsQualityDuringDrop;
                    updated.LastHeartbeatTick = 0u;
                    link.ValueRW = updated;
                }
                else if (link.ValueRO.CommsQuality01 < 1f)
                {
                    var updated = link.ValueRO;
                    updated.CommsQuality01 = 1f;
                    link.ValueRW = updated;
                }
            }
        }

        private void ApplyControllerCompromise(Entity anchorEntity, uint tick, ref Space4XSmokeCompromiseBeatConfig config, ref SystemState state)
        {
            if (config.CompromiseApplied != 0 || tick < config.ControllerCompromiseTick)
            {
                return;
            }

            if (!state.EntityManager.HasComponent<CompromiseState>(anchorEntity))
            {
                state.EntityManager.AddComponentData(anchorEntity, new CompromiseState
                {
                    IsCompromised = 1,
                    Suspicion = 255,
                    Severity = config.ControllerCompromiseSeverity,
                    Kind = config.ControllerCompromiseKind,
                    Source = config.HackerEntity,
                    SinceTick = tick,
                    LastEvidenceTick = tick
                });
            }
            else
            {
                var compromise = state.EntityManager.GetComponentData<CompromiseState>(anchorEntity);
                compromise.IsCompromised = 1;
                compromise.Suspicion = 255;
                compromise.Severity = config.ControllerCompromiseSeverity;
                compromise.Kind = config.ControllerCompromiseKind;
                compromise.Source = config.HackerEntity;
                compromise.SinceTick = tick;
                compromise.LastEvidenceTick = tick;
                state.EntityManager.SetComponentData(anchorEntity, compromise);
            }

            config.CompromiseApplied = 1;
        }

        private void ApplyHackBeat(Entity anchorEntity, uint tick, ref Space4XSmokeCompromiseBeatConfig config, ref SystemState state)
        {
            if (config.HackApplied != 0 || config.HackDroneCount == 0 || tick < config.HackStartTick)
            {
                return;
            }

            var selected = new NativeList<Entity>(config.HackDroneCount, Allocator.Temp);

            foreach (var (link, entity) in SystemAPI.Query<RefRO<ControlLinkState>>().WithEntityAccess())
            {
                if (link.ValueRO.ControllerEntity != anchorEntity)
                {
                    continue;
                }

                InsertByIndex(selected, entity, config.HackDroneCount);
            }

            for (int i = 0; i < selected.Length; i++)
            {
                var drone = selected[i];
                if (!state.EntityManager.HasComponent<ControlLinkState>(drone))
                {
                    continue;
                }

                var link = state.EntityManager.GetComponentData<ControlLinkState>(drone);
                link.ControllerEntity = config.HackerEntity;
                link.IsCompromised = 1;
                link.CompromiseSource = config.HackerEntity;
                link.CommsQuality01 = 1f;
                link.LastHeartbeatTick = tick;
                state.EntityManager.SetComponentData(drone, link);

                if (!state.EntityManager.HasComponent<CompromiseState>(drone))
                {
                    state.EntityManager.AddComponentData(drone, new CompromiseState
                    {
                        IsCompromised = 1,
                        Suspicion = 255,
                        Severity = config.HackSeverity,
                        Kind = CompromiseKind.HostileOverride,
                        Source = config.HackerEntity,
                        SinceTick = tick,
                        LastEvidenceTick = tick
                    });
                }
                else
                {
                    var compromise = state.EntityManager.GetComponentData<CompromiseState>(drone);
                    compromise.IsCompromised = 1;
                    compromise.Suspicion = 255;
                    compromise.Severity = config.HackSeverity;
                    compromise.Kind = CompromiseKind.HostileOverride;
                    compromise.Source = config.HackerEntity;
                    compromise.SinceTick = tick;
                    compromise.LastEvidenceTick = tick;
                    state.EntityManager.SetComponentData(drone, compromise);
                }
            }

            selected.Dispose();
            config.HackApplied = 1;
        }

        private static void InsertByIndex(NativeList<Entity> list, Entity candidate, int maxCount)
        {
            int insertIndex = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                if (candidate.Index < list[i].Index)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex >= maxCount)
            {
                return;
            }

            if (list.Length < maxCount)
            {
                list.Add(candidate);
            }

            for (int i = list.Length - 1; i > insertIndex; i--)
            {
                list[i] = list[i - 1];
            }

            list[insertIndex] = candidate;
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            return (uint)math.max(1, math.ceil(seconds / fixedDt));
        }
    }
}

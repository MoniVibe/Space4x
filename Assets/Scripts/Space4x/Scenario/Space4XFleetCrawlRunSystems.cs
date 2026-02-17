using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XFleetCrawlRunBootstrapSystem : ISystem
    {
        private const int BossCadenceRooms = 5;
        private const int InitialRoomIndex = 0;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo scenarioInfo) ||
                !scenarioInfo.ScenarioId.Equals(Space4XFleetCrawlScenario.ScenarioId))
            {
                return;
            }

            if (SystemAPI.HasSingleton<Space4XFleetCrawlRunTag>())
            {
                state.Enabled = false;
                return;
            }

            var runSeed = scenarioInfo.Seed != 0u ? scenarioInfo.Seed : 0xF17ECAFEu;
            var now = SystemAPI.GetSingleton<TimeState>().Tick;

            var runEntity = state.EntityManager.CreateEntity(
                typeof(Space4XFleetCrawlRunTag),
                typeof(Space4XFleetCrawlRunSeed),
                typeof(Space4XFleetCrawlRunProgress),
                typeof(Space4XFleetCrawlPendingGatePick),
                typeof(Space4XFleetCrawlPendingBoonPick),
                typeof(Space4XFleetCrawlCurrency),
                typeof(Space4XFleetCrawlUpgradePoints),
                typeof(Space4XFleetCrawlReliefCount));

            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlRunSeed
            {
                Value = runSeed
            });

            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlRunProgress
            {
                RoomIndex = InitialRoomIndex,
                BossEveryRooms = BossCadenceRooms,
                RoomsUntilBoss = BossCadenceRooms,
                AwaitingGateResolve = 0,
                Digest = Space4XFleetCrawlMath.Mix(0x811C9DC5u, runSeed)
            });

            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlPendingGatePick
            {
                PickedIndex = 0,
                HasPick = 0
            });
            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlPendingBoonPick
            {
                PickedIndex = 0,
                HasPick = 0
            });
            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlCurrency { Credits = 0 });
            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlUpgradePoints { Value = 0 });
            state.EntityManager.SetComponentData(runEntity, new Space4XFleetCrawlReliefCount { Value = 0 });

            var bag = state.EntityManager.AddBuffer<Space4XFleetCrawlRewardBagItem>(runEntity);
            var gates = state.EntityManager.AddBuffer<Space4XFleetCrawlGateOption>(runEntity);
            var boonChoices = state.EntityManager.AddBuffer<Space4XFleetCrawlBoonChoice>(runEntity);
            var rewardsApplied = state.EntityManager.AddBuffer<Space4XFleetCrawlRewardApplied>(runEntity);
            gates.Clear();
            boonChoices.Clear();
            rewardsApplied.Clear();

            Space4XFleetCrawlRewards.RefillRewardBag(bag);

            var roomEntity = state.EntityManager.CreateEntity(
                typeof(Space4XFleetCrawlRoomTag),
                typeof(Space4XFleetCrawlRoomOwner),
                typeof(Space4XFleetCrawlRoomState));
            state.EntityManager.SetComponentData(roomEntity, new Space4XFleetCrawlRoomOwner
            {
                RunEntity = runEntity
            });
            state.EntityManager.SetComponentData(roomEntity, new Space4XFleetCrawlRoomState
            {
                Kind = Space4XFleetCrawlRoomKind.Combat,
                StartTick = now,
                EndTick = now + Space4XFleetCrawlRewards.RoomDurationTicks(Space4XFleetCrawlRoomKind.Combat),
                Completed = 0
            });

            Debug.Log($"[Space4XFleetCrawl] run_bootstrap=1 seed={runSeed} room=0 kind=Combat");
            state.Enabled = false;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFleetCrawlRoomCompletionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XFleetCrawlRunTag>();
            state.RequireForUpdate<Space4XFleetCrawlRoomTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo scenarioInfo) ||
                !scenarioInfo.ScenarioId.Equals(Space4XFleetCrawlScenario.ScenarioId))
            {
                return;
            }

            var runEntity = SystemAPI.GetSingletonEntity<Space4XFleetCrawlRunTag>();
            var roomEntity = SystemAPI.GetSingletonEntity<Space4XFleetCrawlRoomTag>();
            var owner = state.EntityManager.GetComponentData<Space4XFleetCrawlRoomOwner>(roomEntity);
            if (owner.RunEntity != runEntity)
            {
                return;
            }

            var roomState = state.EntityManager.GetComponentData<Space4XFleetCrawlRoomState>(roomEntity);
            if (roomState.Completed != 0)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            if (tick < roomState.EndTick)
            {
                return;
            }

            roomState.Completed = 1;
            state.EntityManager.SetComponentData(roomEntity, roomState);

            var runSeed = state.EntityManager.GetComponentData<Space4XFleetCrawlRunSeed>(runEntity).Value;
            var progress = state.EntityManager.GetComponentData<Space4XFleetCrawlRunProgress>(runEntity);
            var gateBuffer = state.EntityManager.GetBuffer<Space4XFleetCrawlGateOption>(runEntity);
            var bagBuffer = state.EntityManager.GetBuffer<Space4XFleetCrawlRewardBagItem>(runEntity);

            if (gateBuffer.Length == 0)
            {
                Space4XFleetCrawlRewards.GenerateGateOptions(runSeed, progress.RoomIndex, ref progress, bagBuffer, gateBuffer);
            }

            progress.AwaitingGateResolve = 1;
            state.EntityManager.SetComponentData(runEntity, progress);

            Debug.Log($"[Space4XFleetCrawl] room_complete=1 room={progress.RoomIndex} gates={gateBuffer.Length}");
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetCrawlRoomCompletionSystem))]
    public partial struct Space4XFleetCrawlGateResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XFleetCrawlRunTag>();
            state.RequireForUpdate<Space4XFleetCrawlRoomTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo scenarioInfo) ||
                !scenarioInfo.ScenarioId.Equals(Space4XFleetCrawlScenario.ScenarioId))
            {
                return;
            }

            var runEntity = SystemAPI.GetSingletonEntity<Space4XFleetCrawlRunTag>();
            var roomEntity = SystemAPI.GetSingletonEntity<Space4XFleetCrawlRoomTag>();
            var owner = state.EntityManager.GetComponentData<Space4XFleetCrawlRoomOwner>(roomEntity);
            if (owner.RunEntity != runEntity)
            {
                return;
            }

            var progress = state.EntityManager.GetComponentData<Space4XFleetCrawlRunProgress>(runEntity);
            if (progress.AwaitingGateResolve == 0)
            {
                return;
            }

            var gateBuffer = state.EntityManager.GetBuffer<Space4XFleetCrawlGateOption>(runEntity);
            if (gateBuffer.Length == 0)
            {
                return;
            }

            var runSeed = state.EntityManager.GetComponentData<Space4XFleetCrawlRunSeed>(runEntity).Value;
            var pendingGatePick = state.EntityManager.GetComponentData<Space4XFleetCrawlPendingGatePick>(runEntity);
            if (pendingGatePick.HasPick == 0)
            {
                // Temporary default behavior until UI input is wired.
                pendingGatePick.PickedIndex = Space4XFleetCrawlMath.DeterministicRange(
                    runSeed,
                    progress.RoomIndex,
                    Space4XFleetCrawlRewards.SaltAutoPick,
                    0,
                    gateBuffer.Length);
                pendingGatePick.HasPick = 1;
            }

            var pickedIndex = math.clamp(pendingGatePick.PickedIndex, 0, gateBuffer.Length - 1);
            var pickedGate = gateBuffer[pickedIndex];
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;

            ApplyRewardScaffold(ref state, runEntity, progress.RoomIndex, tick, pickedGate);

            progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)pickedIndex);
            progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)pickedGate.RewardKind);
            progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)pickedGate.BoonGod);

            gateBuffer.Clear();
            pendingGatePick.PickedIndex = 0;
            pendingGatePick.HasPick = 0;
            state.EntityManager.SetComponentData(runEntity, pendingGatePick);

            var pendingBoonPick = state.EntityManager.GetComponentData<Space4XFleetCrawlPendingBoonPick>(runEntity);
            pendingBoonPick.PickedIndex = 0;
            pendingBoonPick.HasPick = 0;
            state.EntityManager.SetComponentData(runEntity, pendingBoonPick);

            progress.RoomIndex += 1;
            progress.RoomsUntilBoss = math.max(0, progress.RoomsUntilBoss - 1);
            progress.AwaitingGateResolve = 0;

            var nextKind = Space4XFleetCrawlRoomKind.Combat;
            if (progress.RoomsUntilBoss == 0)
            {
                nextKind = Space4XFleetCrawlRoomKind.Boss;
                progress.RoomsUntilBoss = math.max(1, progress.BossEveryRooms);
            }
            else if (pickedGate.RewardKind == Space4XFleetCrawlRewardKind.ReliefNode)
            {
                nextKind = Space4XFleetCrawlRoomKind.Relief;
            }

            var roomState = state.EntityManager.GetComponentData<Space4XFleetCrawlRoomState>(roomEntity);
            roomState.Kind = nextKind;
            roomState.StartTick = tick;
            roomState.EndTick = tick + Space4XFleetCrawlRewards.RoomDurationTicks(nextKind);
            roomState.Completed = 0;

            state.EntityManager.SetComponentData(roomEntity, roomState);
            state.EntityManager.SetComponentData(runEntity, progress);

            Debug.Log($"[Space4XFleetCrawl] gate_resolve=1 picked={pickedIndex} reward={pickedGate.RewardKind} room={progress.RoomIndex} next={nextKind} digest={progress.Digest}");
        }

        private static void ApplyRewardScaffold(ref SystemState state, Entity runEntity, int roomIndex, uint tick, Space4XFleetCrawlGateOption pickedGate)
        {
            var rewardsApplied = state.EntityManager.GetBuffer<Space4XFleetCrawlRewardApplied>(runEntity);
            var boonChoices = state.EntityManager.GetBuffer<Space4XFleetCrawlBoonChoice>(runEntity);
            boonChoices.Clear();

            var amount = 0;
            switch (pickedGate.RewardKind)
            {
                case Space4XFleetCrawlRewardKind.Boon:
                    Space4XFleetCrawlRewards.PopulateBoonChoices(roomIndex, pickedGate, boonChoices);
                    break;
                case Space4XFleetCrawlRewardKind.Money:
                {
                    var currency = state.EntityManager.GetComponentData<Space4XFleetCrawlCurrency>(runEntity);
                    amount = 20 + (int)(pickedGate.RollSalt % 31u);
                    currency.Credits += amount;
                    state.EntityManager.SetComponentData(runEntity, currency);
                    break;
                }
                case Space4XFleetCrawlRewardKind.Upgrade:
                {
                    var upgrades = state.EntityManager.GetComponentData<Space4XFleetCrawlUpgradePoints>(runEntity);
                    amount = 1;
                    upgrades.Value += amount;
                    state.EntityManager.SetComponentData(runEntity, upgrades);
                    break;
                }
                case Space4XFleetCrawlRewardKind.ReliefNode:
                {
                    var relief = state.EntityManager.GetComponentData<Space4XFleetCrawlReliefCount>(runEntity);
                    amount = 1;
                    relief.Value += amount;
                    state.EntityManager.SetComponentData(runEntity, relief);
                    break;
                }
            }

            rewardsApplied.Add(new Space4XFleetCrawlRewardApplied
            {
                RoomIndex = roomIndex,
                RewardKind = pickedGate.RewardKind,
                BoonGod = pickedGate.BoonGod,
                Amount = amount,
                Tick = tick
            });
        }
    }

    internal static class Space4XFleetCrawlRewards
    {
        public const uint SaltGateCount = 0xA1020001u;
        public const uint SaltGateRollBase = 0xA1021000u;
        public const uint SaltAutoPick = 0xA1022000u;

        public static uint RoomDurationTicks(Space4XFleetCrawlRoomKind kind)
        {
            switch (kind)
            {
                case Space4XFleetCrawlRoomKind.Relief:
                    return 120u;
                case Space4XFleetCrawlRoomKind.Boss:
                    return 360u;
                default:
                    return 240u;
            }
        }

        public static void RefillRewardBag(DynamicBuffer<Space4XFleetCrawlRewardBagItem> bag)
        {
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Boon });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Money });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Upgrade });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.ReliefNode });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Boon });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Money });
            bag.Add(new Space4XFleetCrawlRewardBagItem { RewardKind = Space4XFleetCrawlRewardKind.Upgrade });
        }

        public static void GenerateGateOptions(
            uint runSeed,
            int roomIndex,
            ref Space4XFleetCrawlRunProgress progress,
            DynamicBuffer<Space4XFleetCrawlRewardBagItem> bag,
            DynamicBuffer<Space4XFleetCrawlGateOption> options)
        {
            options.Clear();
            if (bag.Length == 0)
            {
                RefillRewardBag(bag);
            }

            var gateCount = 2 + Space4XFleetCrawlMath.DeterministicRange(runSeed, roomIndex, SaltGateCount, 0, 2);
            var offeredRewardMask = 0u;
            var offeredGodMask = 0u;

            for (var slot = 0; slot < gateCount; slot++)
            {
                var emitted = false;
                for (var attempt = 0; attempt < 48 && !emitted; attempt++)
                {
                    if (bag.Length == 0)
                    {
                        RefillRewardBag(bag);
                    }

                    var salt = SaltGateRollBase + (uint)(slot * 131 + attempt * 17);
                    var index = Space4XFleetCrawlMath.DeterministicRange(runSeed, roomIndex, salt, 0, bag.Length);
                    var candidate = bag[index].RewardKind;

                    Space4XFleetCrawlBoonGod god = Space4XFleetCrawlBoonGod.None;
                    if (candidate == Space4XFleetCrawlRewardKind.Boon)
                    {
                        god = RollDistinctBoonGod(runSeed, roomIndex, slot, attempt, offeredGodMask);
                        if (god == Space4XFleetCrawlBoonGod.None)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        var bit = 1u << (int)candidate;
                        if ((offeredRewardMask & bit) != 0u)
                        {
                            continue;
                        }

                        offeredRewardMask |= bit;
                    }

                    if (candidate == Space4XFleetCrawlRewardKind.Boon)
                    {
                        offeredGodMask |= 1u << (int)god;
                    }

                    options.Add(new Space4XFleetCrawlGateOption
                    {
                        RewardKind = candidate,
                        BoonGod = god,
                        RollSalt = salt
                    });
                    bag.RemoveAt(index);

                    progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)candidate);
                    progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)god);
                    progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, salt);
                    emitted = true;
                }

                if (!emitted)
                {
                    ForceAddFallbackOption(runSeed, roomIndex, slot, ref offeredRewardMask, ref offeredGodMask, bag, options, ref progress);
                }
            }
        }

        public static void PopulateBoonChoices(
            int roomIndex,
            Space4XFleetCrawlGateOption gate,
            DynamicBuffer<Space4XFleetCrawlBoonChoice> boonChoices)
        {
            for (var i = 0; i < 3; i++)
            {
                var boonSeed = Space4XFleetCrawlMath.Mix(gate.RollSalt, (uint)(roomIndex * 13 + i * 97));
                boonChoices.Add(new Space4XFleetCrawlBoonChoice
                {
                    God = gate.BoonGod,
                    BoonId = new FixedString64Bytes($"boon.{gate.BoonGod}.{boonSeed & 0xFFFFu}")
                });
            }
        }

        private static Space4XFleetCrawlBoonGod RollDistinctBoonGod(uint runSeed, int roomIndex, int slot, int attempt, uint offeredGodMask)
        {
            const int godCount = 6;
            var start = Space4XFleetCrawlMath.DeterministicRange(
                runSeed,
                roomIndex,
                SaltGateRollBase + (uint)(0x500 + slot * 31 + attempt * 17),
                0,
                godCount);

            for (var i = 0; i < godCount; i++)
            {
                var candidate = BoonGodByIndex((start + i) % godCount);
                var bit = 1u << (int)candidate;
                if ((offeredGodMask & bit) == 0u)
                {
                    return candidate;
                }
            }

            return Space4XFleetCrawlBoonGod.None;
        }

        private static Space4XFleetCrawlBoonGod BoonGodByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return Space4XFleetCrawlBoonGod.Athena;
                case 1:
                    return Space4XFleetCrawlBoonGod.Ares;
                case 2:
                    return Space4XFleetCrawlBoonGod.Artemis;
                case 3:
                    return Space4XFleetCrawlBoonGod.Hermes;
                case 4:
                    return Space4XFleetCrawlBoonGod.Poseidon;
                default:
                    return Space4XFleetCrawlBoonGod.Zeus;
            }
        }

        private static void ForceAddFallbackOption(
            uint runSeed,
            int roomIndex,
            int slot,
            ref uint offeredRewardMask,
            ref uint offeredGodMask,
            DynamicBuffer<Space4XFleetCrawlRewardBagItem> bag,
            DynamicBuffer<Space4XFleetCrawlGateOption> options,
            ref Space4XFleetCrawlRunProgress progress)
        {
            if (bag.Length == 0)
            {
                RefillRewardBag(bag);
            }

            for (var index = 0; index < bag.Length; index++)
            {
                var candidate = bag[index].RewardKind;
                var salt = SaltGateRollBase + (uint)(0x900 + slot * 53 + index);
                Space4XFleetCrawlBoonGod god = Space4XFleetCrawlBoonGod.None;

                if (candidate == Space4XFleetCrawlRewardKind.Boon)
                {
                    god = RollDistinctBoonGod(runSeed, roomIndex, slot, index, offeredGodMask);
                    if (god == Space4XFleetCrawlBoonGod.None)
                    {
                        continue;
                    }

                    offeredGodMask |= 1u << (int)god;
                }
                else
                {
                    var bit = 1u << (int)candidate;
                    if ((offeredRewardMask & bit) != 0u)
                    {
                        continue;
                    }

                    offeredRewardMask |= bit;
                }

                options.Add(new Space4XFleetCrawlGateOption
                {
                    RewardKind = candidate,
                    BoonGod = god,
                    RollSalt = salt
                });
                bag.RemoveAt(index);

                progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)candidate);
                progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, (uint)god);
                progress.Digest = Space4XFleetCrawlMath.Mix(progress.Digest, salt);
                return;
            }
        }
    }

    internal static class Space4XFleetCrawlMath
    {
        public static uint Mix(uint digest, uint value)
        {
            return math.hash(new uint4(
                digest ^ 0x9E3779B9u,
                value + 0x85EBCA6Bu,
                digest * 1664525u + 1013904223u,
                value ^ 0xC2B2AE35u));
        }

        public static int DeterministicRange(uint runSeed, int roomIndex, uint salt, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            var hash = math.hash(new uint4(
                runSeed,
                (uint)math.max(0, roomIndex + 1),
                salt,
                runSeed ^ salt ^ 0xD1B54A35u));
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(hash % span);
        }
    }
}

using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Resolves boarding combat per segment.
    /// Adjusts control levels based on team combat resolution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlatformBoardingResolutionSystem : ISystem
    {
        private BufferLookup<BoardingTeam> _boardingTeamsLookup;
        private BufferLookup<SegmentControl> _segmentControlsLookup;
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BoardingState>();
            _boardingTeamsLookup = state.GetBufferLookup<BoardingTeam>(false);
            _segmentControlsLookup = state.GetBufferLookup<SegmentControl>(false);
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _boardingTeamsLookup.Update(ref state);
            _segmentControlsLookup.Update(ref state);
            _segmentStatesLookup.Update(ref state);

            foreach (var (boardingState, entity) in SystemAPI.Query<
                RefRW<BoardingState>>().WithEntityAccess())
            {
                if (boardingState.ValueRO.Phase != BoardingPhase.Fighting)
                {
                    continue;
                }

                if (!_boardingTeamsLookup.HasBuffer(entity) ||
                    !_segmentControlsLookup.HasBuffer(entity) ||
                    !_segmentStatesLookup.HasBuffer(entity))
                {
                    continue;
                }

                var boardingTeams = _boardingTeamsLookup[entity];
                var segmentControls = _segmentControlsLookup[entity];
                var segmentStates = _segmentStatesLookup[entity];

                ResolveBoardingCombat(
                    ref boardingState.ValueRW,
                    ref boardingTeams,
                    ref segmentControls,
                    ref segmentStates);
            }
        }

        [BurstCompile]
        private static void ResolveBoardingCombat(
            ref BoardingState boardingState,
            ref DynamicBuffer<BoardingTeam> boardingTeams,
            ref DynamicBuffer<SegmentControl> segmentControls,
            ref DynamicBuffer<PlatformSegmentState> segmentStates)
        {
            var attackerFactionId = boardingState.AttackerFactionId;
            var defenderFactionId = boardingState.DefenderFactionId;

            var segmentsByIndex = new NativeHashMap<int, int>(segmentStates.Length, Allocator.Temp);

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                if ((segmentState.Status & SegmentStatusFlags.Destroyed) != 0)
                {
                    continue;
                }

                segmentsByIndex[segmentState.SegmentIndex] = i;
            }

            for (int i = 0; i < segmentControls.Length; i++)
            {
                var segmentControl = segmentControls[i];
                var segmentIndex = segmentControl.SegmentIndex;

                if (!segmentsByIndex.TryGetValue(segmentIndex, out var segmentStateIndex))
                {
                    continue;
                }

                var segmentState = segmentStates[segmentStateIndex];
                var environmentPenalty = GetEnvironmentPenalty(segmentState.Status);

                var attackerStrength = 0f;
                var defenderStrength = 0f;

                for (int j = 0; j < boardingTeams.Length; j++)
                {
                    var team = boardingTeams[j];
                    if (team.SegmentIndex != segmentIndex)
                    {
                        continue;
                    }

                    var strength = team.Count * team.Morale;

                    if (team.FactionId == attackerFactionId)
                    {
                        attackerStrength += strength;
                    }
                    else if (team.FactionId == defenderFactionId)
                    {
                        defenderStrength += strength;
                    }
                }

                defenderStrength *= (1f - environmentPenalty);

                var totalStrength = attackerStrength + defenderStrength;
                if (totalStrength > 0f)
                {
                    var attackerRatio = attackerStrength / totalStrength;
                    segmentControl.ControlLevel = math.lerp(segmentControl.ControlLevel, attackerRatio * 2f - 1f, 0.1f);
                }

                if (segmentControl.ControlLevel > 0.5f)
                {
                    segmentControl.FactionId = attackerFactionId;
                    segmentState.ControlFactionId = attackerFactionId;
                    segmentState.Status |= SegmentStatusFlags.Boarded;
                }
                else if (segmentControl.ControlLevel < -0.5f)
                {
                    segmentControl.FactionId = defenderFactionId;
                    segmentState.ControlFactionId = defenderFactionId;
                    segmentState.Status &= ~SegmentStatusFlags.Boarded;
                }

                segmentControls[i] = segmentControl;
                segmentStates[segmentStateIndex] = segmentState;
            }
        }

        [BurstCompile]
        private static float GetEnvironmentPenalty(SegmentStatusFlags status)
        {
            float penalty = 0f;

            if ((status & SegmentStatusFlags.Breached) != 0)
            {
                penalty += 0.2f;
            }

            if ((status & SegmentStatusFlags.Depressurized) != 0)
            {
                penalty += 0.3f;
            }

            if ((status & SegmentStatusFlags.OnFire) != 0)
            {
                penalty += 0.2f;
            }

            return math.min(penalty, 0.7f);
        }
    }
}


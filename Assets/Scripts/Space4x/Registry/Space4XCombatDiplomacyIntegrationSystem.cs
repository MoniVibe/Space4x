using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCombatOutcomeCollectionSystem))]
    public partial struct Space4XCombatDiplomacyIntegrationSystem : ISystem
    {
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<DiplomaticStatusEntry> _statusLookup;
        private BufferLookup<RelationModifier> _modifierLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<ScenarioSide> _sideLookup;
        private ComponentLookup<CompromiseState> _compromiseLookup;
        private ComponentLookup<CompromiseDoctrine> _doctrineLookup;
        private ComponentLookup<RogueToolState> _rogueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XCombatOutcomeStream>();
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _statusLookup = state.GetBufferLookup<DiplomaticStatusEntry>(true);
            _modifierLookup = state.GetBufferLookup<RelationModifier>(false);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _sideLookup = state.GetComponentLookup<ScenarioSide>(true);
            _compromiseLookup = state.GetComponentLookup<CompromiseState>(true);
            _doctrineLookup = state.GetComponentLookup<CompromiseDoctrine>(true);
            _rogueLookup = state.GetComponentLookup<RogueToolState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _factionLookup.Update(ref state);
            _statusLookup.Update(ref state);
            _modifierLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _sideLookup.Update(ref state);
            _compromiseLookup.Update(ref state);
            _doctrineLookup.Update(ref state);
            _rogueLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var factionMap = BuildFactionMap(ref state);

            if (!SystemAPI.TryGetSingletonEntity<Space4XCombatOutcomeStream>(out var streamEntity))
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<Space4XCombatOutcomeEvent>(streamEntity))
            {
                return;
            }

            var outcomes = state.EntityManager.GetBuffer<Space4XCombatOutcomeEvent>(streamEntity);
            if (outcomes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < outcomes.Length; i++)
            {
                var outcome = outcomes[i];
                if (outcome.Outcome != Space4XCombatOutcomeType.Destroyed)
                {
                    continue;
                }

                var attacker = outcome.Attacker;
                var victimEntity = outcome.Victim;
                var victimFactionId = ResolveFactionId(victimEntity, ref state);
                var attackerFactionId = ResolveFactionId(attacker, ref state);

                if (victimFactionId == 0 || attackerFactionId == 0 || victimFactionId == attackerFactionId)
                {
                    continue;
                }

                if (!IsFriendlyRelation(victimFactionId, attackerFactionId, factionMap, ref state))
                {
                    continue;
                }

                if (ShouldSkipPenalty(victimEntity, ref state, out var doctrine))
                {
                    if (doctrine.FriendlyFirePenaltyMode == FriendlyFirePenaltyMode.CommendedIfCompromised)
                    {
                        ApplyRelationModifier(victimFactionId, attackerFactionId, RelationModifierType.CrisisHelp, timeState.Tick, factionMap, ref state, ref ecb);
                    }

                    continue;
                }

                ApplyRelationModifier(victimFactionId, attackerFactionId, RelationModifierType.AllyAttacked, timeState.Tick, factionMap, ref state, ref ecb);
            }

            outcomes.Clear();

            factionMap.Dispose();
            ecb.Playback(state.EntityManager);
        }

        private bool ShouldSkipPenalty(Entity victim, ref SystemState state, out CompromiseDoctrine doctrine)
        {
            doctrine = new CompromiseDoctrine
            {
                QuarantineThreshold = 0,
                PurgeThreshold = 0,
                PreferredResponse = CompromiseResponseMode.Disconnect,
                FriendlyFirePenaltyMode = FriendlyFirePenaltyMode.Normal,
                RecoveryBudgetTicks = 0u
            };

            if (victim == Entity.Null)
            {
                return false;
            }

            if (_doctrineLookup.HasComponent(victim))
            {
                doctrine = _doctrineLookup[victim];
            }
            else if (_affiliationLookup.HasBuffer(victim))
            {
                var affiliations = _affiliationLookup[victim];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var target = affiliations[i].Target;
                    if (target != Entity.Null && _doctrineLookup.HasComponent(target))
                    {
                        doctrine = _doctrineLookup[target];
                        break;
                    }
                }
            }

            bool compromised = _compromiseLookup.HasComponent(victim) && _compromiseLookup[victim].IsCompromised != 0;
            bool rogue = _rogueLookup.HasComponent(victim) && _rogueLookup[victim].AllowFriendlyDestructionNoPenalty != 0;

            if (!compromised && !rogue)
            {
                return false;
            }

            return doctrine.FriendlyFirePenaltyMode == FriendlyFirePenaltyMode.WaivedIfCompromised ||
                   doctrine.FriendlyFirePenaltyMode == FriendlyFirePenaltyMode.CommendedIfCompromised;
        }

        private bool IsFriendlyRelation(ushort victimFactionId, ushort attackerFactionId, NativeHashMap<ushort, Entity> factionMap, ref SystemState state)
        {
            if (!factionMap.TryGetValue(victimFactionId, out var victimFactionEntity))
            {
                return false;
            }

            if (!_statusLookup.HasBuffer(victimFactionEntity))
            {
                return false;
            }

            var statuses = _statusLookup[victimFactionEntity];
            for (int i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i].Status;
                if (status.OtherFactionId != attackerFactionId)
                {
                    continue;
                }

                return status.Stance >= DiplomaticStance.Friendly;
            }

            return false;
        }

        private void ApplyRelationModifier(
            ushort victimFactionId,
            ushort attackerFactionId,
            RelationModifierType type,
            uint tick,
            NativeHashMap<ushort, Entity> factionMap,
            ref SystemState state,
            ref EntityCommandBuffer ecb)
        {
            if (!factionMap.TryGetValue(victimFactionId, out var victimFactionEntity))
            {
                return;
            }

            DynamicBuffer<RelationModifier> buffer;
            if (_modifierLookup.HasBuffer(victimFactionEntity))
            {
                buffer = _modifierLookup[victimFactionEntity];
            }
            else
            {
                buffer = ecb.AddBuffer<RelationModifier>(victimFactionEntity);
            }

            buffer.Add(new RelationModifier
            {
                Type = type,
                ScoreChange = ResolveScoreChange(type),
                DecayRate = (half)0.1f,
                RemainingTicks = 0,
                SourceFactionId = attackerFactionId,
                AppliedTick = tick
            });
        }

        private static sbyte ResolveScoreChange(RelationModifierType type)
        {
            return type switch
            {
                RelationModifierType.AllyAttacked => -60,
                RelationModifierType.CrisisHelp => 20,
                _ => 0
            };
        }

        private ushort ResolveFactionId(Entity entity, ref SystemState state)
        {
            if (entity == Entity.Null || !state.EntityManager.Exists(entity))
            {
                return 0;
            }

            if (_factionLookup.HasComponent(entity))
            {
                return _factionLookup[entity].FactionId;
            }

            if (_affiliationLookup.HasBuffer(entity))
            {
                var affiliations = _affiliationLookup[entity];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type != AffiliationType.Faction && tag.Type != AffiliationType.Fleet)
                    {
                        continue;
                    }

                    if (tag.Target != Entity.Null && _factionLookup.HasComponent(tag.Target))
                    {
                        return _factionLookup[tag.Target].FactionId;
                    }
                }
            }

            if (_sideLookup.HasComponent(entity))
            {
                return _sideLookup[entity].Side;
            }

            return 0;
        }

        private NativeHashMap<ushort, Entity> BuildFactionMap(ref SystemState state)
        {
            var map = new NativeHashMap<ushort, Entity>(32, Allocator.Temp);
            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (!map.TryAdd(faction.ValueRO.FactionId, entity))
                {
                    map[faction.ValueRO.FactionId] = entity;
                }
            }

            return map;
        }
    }
}

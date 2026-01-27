using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Systems.Social.Companions
{
    /// <summary>
    /// System that listens for companion-related events (death, injury, betrayal) and applies
    /// morale/focus/trait effects to companions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CompanionEvolutionSystem))]
    public partial struct CompanionEventSystem : ISystem
    {
        ComponentLookup<CompanionBond> _bondLookup;
        BufferLookup<CompanionLink> _companionLinkLookup;
        ComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes> _personalityLookup;
        ComponentLookup<MoraleState> _moraleLookup;
        ComponentLookup<FocusState> _focusLookup;
        ComponentLookup<MentalState> _mentalStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _bondLookup = state.GetComponentLookup<CompanionBond>(true);
            _companionLinkLookup = state.GetBufferLookup<CompanionLink>(true);
            _personalityLookup = state.GetComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes>(true);
            _moraleLookup = state.GetComponentLookup<MoraleState>(false);
            _focusLookup = state.GetComponentLookup<FocusState>(false);
            _mentalStateLookup = state.GetComponentLookup<MentalState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
                return;

            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenarioState) &&
                (!scenarioState.IsInitialized))
                return;

            _bondLookup.Update(ref state);
            _companionLinkLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _moraleLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _mentalStateLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Process death events
            var deathJob = new ProcessDeathEventsJob
            {
                CurrentTick = currentTick,
                BondLookup = _bondLookup,
                CompanionLinkLookup = _companionLinkLookup,
                PersonalityLookup = _personalityLookup,
                MoraleLookup = _moraleLookup,
                FocusLookup = _focusLookup,
                MentalStateLookup = _mentalStateLookup
            };
            deathJob.ScheduleParallel();

            // Process damage events for companion injury reactions
            var injuryJob = new ProcessInjuryEventsJob
            {
                CurrentTick = currentTick,
                BondLookup = _bondLookup,
                CompanionLinkLookup = _companionLinkLookup,
                PersonalityLookup = _personalityLookup,
                FocusLookup = _focusLookup
            };
            injuryJob.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessDeathEventsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<CompanionBond> BondLookup;
            [ReadOnly] public BufferLookup<CompanionLink> CompanionLinkLookup;
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes> PersonalityLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MoraleState> MoraleLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<FocusState> FocusLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MentalState> MentalStateLookup;

            void Execute(DynamicBuffer<DeathEvent> deathEvents)
            {
                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var deathEvent = deathEvents[i];
                    Entity deadEntity = deathEvent.DeadEntity;

                    // Find all companions of the dead entity
                    if (!CompanionLinkLookup.HasBuffer(deadEntity))
                        continue;

                    var links = CompanionLinkLookup[deadEntity];
                    for (int j = 0; j < links.Length; j++)
                    {
                        Entity bondEntity = links[j].Bond;
                        if (!BondLookup.HasComponent(bondEntity))
                            continue;

                        var bond = BondLookup[bondEntity];
                        Entity companion = (bond.A == deadEntity) ? bond.B : bond.A;

                        if (companion == Entity.Null)
                            continue;

                        // Apply companion death effects
                        ApplyCompanionDeathEffects(companion, bond, deadEntity);
                    }
                }
            }

            void ApplyCompanionDeathEffects(Entity companion, CompanionBond bond, Entity deadEntity)
            {
                // Immediate morale hit based on bond intensity
                float moraleHit = -0.3f - (bond.Intensity * 0.4f); // -0.3 to -0.7
                if (MoraleLookup.HasComponent(companion))
                {
                    var morale = MoraleLookup[companion];
                    morale.ApplyModifier(moraleHit);
                    MoraleLookup[companion] = morale;
                }

                // Get personality for reaction type
                PureDOTS.Runtime.Individual.PersonalityAxes personality = PersonalityLookup.HasComponent(companion)
                    ? PersonalityLookup[companion]
                    : default;

                // Personality-based reactions
                float boldness = personality.Boldness; // Using Boldness from PersonalityAxes
                float vengefulness = personality.Vengefulness;

                // Bold + Vengeful → Berserk state
                if (boldness > 0.5f && vengefulness > 0.5f)
                {
                    if (MentalStateLookup.HasComponent(companion))
                    {
                        var mentalState = MentalStateLookup[companion];
                        mentalState.State = MentalBreakState.Berserk;
                        mentalState.LastStateChangeTick = CurrentTick;
                        MentalStateLookup[companion] = mentalState;
                    }

                    // Spike focus for berserk
                    if (FocusLookup.HasComponent(companion))
                    {
                        var focus = FocusLookup[companion];
                        focus.Current = math.min(focus.Max, focus.Current + focus.Max * 0.5f); // 50% focus boost
                        FocusLookup[companion] = focus;
                    }
                }
                // Craven + Attached → Panic state
                else if (boldness < -0.5f && bond.Intensity > 0.7f)
                {
                    if (MentalStateLookup.HasComponent(companion))
                    {
                        var mentalState = MentalStateLookup[companion];
                        mentalState.State = MentalBreakState.Panicked;
                        mentalState.LastStateChangeTick = CurrentTick;
                        MentalStateLookup[companion] = mentalState;
                    }

                    // Drain focus from panic
                    if (FocusLookup.HasComponent(companion))
                    {
                        var focus = FocusLookup[companion];
                        focus.Current = math.max(0f, focus.Current - focus.Max * 0.3f); // 30% focus drain
                        FocusLookup[companion] = focus;
                    }
                }
                // Stoic → muted reaction (no special state change, just morale hit)
            }
        }

        [BurstCompile]
        partial struct ProcessInjuryEventsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<CompanionBond> BondLookup;
            [ReadOnly] public BufferLookup<CompanionLink> CompanionLinkLookup;
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes> PersonalityLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<FocusState> FocusLookup;

            void Execute(Entity entity, DynamicBuffer<DamageEvent> damageEvents)
            {
                // Check if this entity has companions
                if (!CompanionLinkLookup.HasBuffer(entity))
                    return;

                var links = CompanionLinkLookup[entity];
                for (int i = 0; i < links.Length; i++)
                {
                    Entity bondEntity = links[i].Bond;
                    if (!BondLookup.HasComponent(bondEntity))
                        continue;

                    var bond = BondLookup[bondEntity];
                    Entity companion = (bond.A == entity) ? bond.B : bond.A;

                    if (companion == Entity.Null)
                        continue;

                    float totalDamage = 0f;
                    for (int j = 0; j < damageEvents.Length; j++)
                    {
                        if (damageEvents[j].TargetEntity == entity)
                        {
                            totalDamage += damageEvents[j].RawDamage;
                        }
                    }

                    // Apply companion injury effects (panic, focus spike)
                    if (totalDamage > 50f) // Threshold for "near death"
                    {
                        ApplyCompanionInjuryEffects(companion, bond);
                    }
                }
            }

            void ApplyCompanionInjuryEffects(Entity companion, CompanionBond bond)
            {
                // Spike focus (panic, tunnel vision)
                if (FocusLookup.HasComponent(companion))
                {
                    var focus = FocusLookup[companion];
                    // Add temporary focus spike
                    focus.Current = math.min(focus.Max, focus.Current + focus.Max * 0.2f * bond.Intensity);
                    FocusLookup[companion] = focus;
                }

                // Modify morale based on personality
                PureDOTS.Runtime.Individual.PersonalityAxes personality = PersonalityLookup.HasComponent(companion)
                    ? PersonalityLookup[companion]
                    : default;

                float boldness = personality.Boldness;
                // Bold + Vengeful → heroic rage (morale up)
                if (boldness > 0.3f)
                {
                    // Morale boost handled by other systems reading companion state
                }
                // Craven + Attached → fear (morale down)
                else if (boldness < -0.3f && bond.Intensity > 0.6f)
                {
                    // Morale penalty handled by other systems
                }
            }
        }
    }
}


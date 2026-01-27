using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Builder for creating narrative registry blob assets.
    /// </summary>
    public static class NarrativeRegistryBuilder
    {
        // Tag indices (examples)
        public const int TagHostage = 0;
        public const int TagCivilWar = 1;
        public const int TagHorror = 2;
        public const int TagSocial = 3;
        public const int TagWar = 4;

        // Condition types
        public const int ConditionTypePlayerChoiceEquals = 0;
        public const int ConditionTypeRandomRoll = 1;
        public const int ConditionTypeHasResource = 2;
        public const int ConditionTypeTraitCheck = 3;
        public const int ConditionTypeTimeElapsed = 4;

        // Effect types
        public const int EffectTypeStartSituation = 0;
        public const int EffectTypeAddResource = 1;
        public const int EffectTypeSetFlag = 2;
        public const int EffectTypeModifyOpinion = 3;

        // Role IDs for hostage situation
        public const int RoleHostage = 1;
        public const int RoleCaptor = 2;
        public const int RoleAuthority = 3;

        /// <summary>
        /// Creates a test event registry with 1-2 sample events.
        /// </summary>
        public static BlobAssetReference<NarrativeEventRegistry> CreateTestEventRegistry(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<NarrativeEventRegistry>();

            // Create one test event: "AI scientist log found"
            var events = builder.Allocate(ref root.Events, 1);

            // Event 0: AI scientist log found
            events[0].Id = NarrativeId.FromString("Event_AI_Scientist_Log");
            events[0].Tags = NarrativeTagMask.FromTag(TagHorror) | NarrativeTagMask.FromTag(TagSocial);
            events[0].DeliveryKind = NarrativeEventDeliveryKind.LogOnly;
            events[0].TitleKey = 1001; // Content key
            events[0].BodyKey = 1002;

            // No conditions (always fires)
            builder.Allocate(ref events[0].Conditions, 0);

            // Effect: Start hostage situation
            var effects = builder.Allocate(ref events[0].Effects, 1);
            effects[0].EffectType = EffectTypeStartSituation;
            effects[0].ParamA = "Situation_Hostage".GetHashCode(); // Situation ID hash
            effects[0].ParamB = 0;

            var blob = builder.CreateBlobAssetReference<NarrativeEventRegistry>(allocator);
            builder.Dispose();
            return blob;
        }

        /// <summary>
        /// Creates a test situation registry with HostageSituation archetype.
        /// </summary>
        public static BlobAssetReference<SituationArchetypeRegistry> CreateTestSituationRegistry(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SituationArchetypeRegistry>();

            var archetypes = builder.Allocate(ref root.Archetypes, 1);

            // Hostage Situation Archetype
            archetypes[0].SituationId = NarrativeId.FromString("Situation_Hostage");
            archetypes[0].Tags = NarrativeTagMask.FromTag(TagHostage) | NarrativeTagMask.FromTag(TagSocial);

            // Roles: Hostage, Captor, Authority
            var roles = builder.Allocate(ref archetypes[0].Roles, 3);
            roles[0].RoleId = RoleHostage;
            roles[0].CompatibleEntityTags = NarrativeTagMask.FromTag(3); // "Villager" tag
            roles[0].Required = 1;

            roles[1].RoleId = RoleCaptor;
            roles[1].CompatibleEntityTags = NarrativeTagMask.FromTag(4); // "Band" tag
            roles[1].Required = 1;

            roles[2].RoleId = RoleAuthority;
            roles[2].CompatibleEntityTags = NarrativeTagMask.FromTag(5); // "Fleet" tag
            roles[2].Required = 0;

            // Steps: 4 steps (Seizure, Ultimatum, Resolution, Aftermath)
            var steps = builder.Allocate(ref archetypes[0].Steps, 4);

            // Step 0: Seizure (Intro, AutoAdvance)
            steps[0].StepIndex = 0;
            steps[0].Kind = SituationStepKind.AutoAdvance;
            steps[0].MinDuration = 0f;
            steps[0].MaxDuration = 0f;
            builder.Allocate(ref steps[0].InlineEvents, 0);

            // Step 1: Ultimatum (WaitForChoice)
            steps[1].StepIndex = 1;
            steps[1].Kind = SituationStepKind.WaitForChoice;
            steps[1].MinDuration = 0f;
            steps[1].MaxDuration = 0f;
            builder.Allocate(ref steps[1].InlineEvents, 0);

            // Step 2: Resolution (AutoAdvance / TimedTick)
            steps[2].StepIndex = 2;
            steps[2].Kind = SituationStepKind.AutoAdvance;
            steps[2].MinDuration = 10f;
            steps[2].MaxDuration = 30f;
            builder.Allocate(ref steps[2].InlineEvents, 0);

            // Step 3: Aftermath (Checkpoint/Finished)
            steps[3].StepIndex = 3;
            steps[3].Kind = SituationStepKind.Checkpoint;
            steps[3].MinDuration = 0f;
            steps[3].MaxDuration = 0f;
            builder.Allocate(ref steps[3].InlineEvents, 0);

            // Transitions: 6-8 transitions
            var transitions = builder.Allocate(ref archetypes[0].Transitions, 7);

            // Transition 0: Seizure -> Ultimatum (always)
            transitions[0].FromStepIndex = 0;
            transitions[0].ToStepIndex = 1;
            transitions[0].Weight = 1f;
            builder.Allocate(ref transitions[0].Conditions, 0);
            builder.Allocate(ref transitions[0].Effects, 0);

            // Transition 1: Ultimatum -> Resolution (negotiation choice)
            transitions[1].FromStepIndex = 1;
            transitions[1].ToStepIndex = 2;
            transitions[1].Weight = 1f;
            var cond1 = builder.Allocate(ref transitions[1].Conditions, 1);
            cond1[0].ConditionType = ConditionTypePlayerChoiceEquals;
            cond1[0].ParamA = 0; // Option 0 = negotiation
            cond1[0].ParamB = 0;
            builder.Allocate(ref transitions[1].Effects, 0);

            // Transition 2: Ultimatum -> Resolution (assault choice)
            transitions[2].FromStepIndex = 1;
            transitions[2].ToStepIndex = 2;
            transitions[2].Weight = 1f;
            var cond2 = builder.Allocate(ref transitions[2].Conditions, 1);
            cond2[0].ConditionType = ConditionTypePlayerChoiceEquals;
            cond2[0].ParamA = 1; // Option 1 = assault
            cond2[0].ParamB = 0;
            builder.Allocate(ref transitions[2].Effects, 0);

            // Transition 3: Ultimatum -> Resolution (give in choice)
            transitions[3].FromStepIndex = 1;
            transitions[3].ToStepIndex = 2;
            transitions[3].Weight = 1f;
            var cond3 = builder.Allocate(ref transitions[3].Conditions, 1);
            cond3[0].ConditionType = ConditionTypePlayerChoiceEquals;
            cond3[0].ParamA = 2; // Option 2 = give in
            cond3[0].ParamB = 0;
            builder.Allocate(ref transitions[3].Effects, 0);

            // Transition 4: Ultimatum -> Resolution (stall choice)
            transitions[4].FromStepIndex = 1;
            transitions[4].ToStepIndex = 2;
            transitions[4].Weight = 1f;
            var cond4 = builder.Allocate(ref transitions[4].Conditions, 1);
            cond4[0].ConditionType = ConditionTypePlayerChoiceEquals;
            cond4[0].ParamA = 3; // Option 3 = stall
            cond4[0].ParamB = 0;
            builder.Allocate(ref transitions[4].Effects, 0);

            // Transition 5: Resolution -> Aftermath (time elapsed)
            transitions[5].FromStepIndex = 2;
            transitions[5].ToStepIndex = 3;
            transitions[5].Weight = 1f;
            var cond5 = builder.Allocate(ref transitions[5].Conditions, 1);
            cond5[0].ConditionType = ConditionTypeTimeElapsed;
            cond5[0].ParamA = 0; // Min duration check
            cond5[0].ParamB = 0;
            builder.Allocate(ref transitions[5].Effects, 0);

            // Transition 6: Aftermath -> Finished (always)
            transitions[6].FromStepIndex = 3;
            transitions[6].ToStepIndex = 3; // Self-loop marks finished
            transitions[6].Weight = 1f;
            builder.Allocate(ref transitions[6].Conditions, 0);
            var effects6 = builder.Allocate(ref transitions[6].Effects, 1);
            effects6[0].EffectType = EffectTypeSetFlag;
            effects6[0].ParamA = "HostageCrisisHandled".GetHashCode();
            effects6[0].ParamB = 1;

            var blob = builder.CreateBlobAssetReference<SituationArchetypeRegistry>(allocator);
            builder.Dispose();
            return blob;
        }
    }
}


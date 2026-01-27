using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Computes BehaviorTuning from AlignmentTriplet, PersonalityAxes, and MoraleState.
    /// Reads alignment + personality + morale → writes BehaviorTuning biases.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BehaviorTuningSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new CalculateTuningJob();
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct CalculateTuningJob : IJobEntity
        {
            void Execute(
                ref BehaviorTuning tuning,
                in AlignmentTriplet alignment,
                in PersonalityAxes personality,
                in MoraleState morale)
            {
                // Aggression bias: influenced by Order (lawful = less aggressive), Boldness, Morale
                // Chaotic + Bold + High Morale = more aggressive
                float aggressionBase = (1f - alignment.Order) * 0.5f; // Chaos increases aggression
                float aggressionPersonality = (personality.Boldness + 1f) * 0.5f; // Boldness increases aggression
                float aggressionMorale = (morale.Current + 1f) * 0.5f; // High morale increases aggression
                tuning.AggressionBias = math.clamp(0.5f + aggressionBase * 0.3f + aggressionPersonality * 0.3f + aggressionMorale * 0.2f, 0f, 2f);

                // Social bias: influenced by Moral (good = more social), Selflessness, Morale
                float socialBase = (alignment.Moral + 1f) * 0.5f; // Good increases social
                float socialPersonality = (personality.Selflessness + 1f) * 0.5f; // Selflessness increases social
                float socialMorale = (morale.Current + 1f) * 0.5f; // High morale increases social
                tuning.SocialBias = math.clamp(0.5f + socialBase * 0.3f + socialPersonality * 0.3f + socialMorale * 0.2f, 0f, 2f);

                // Greed bias: influenced by Moral (evil = more greedy), Selflessness (inverse), Morale (inverse)
                float greedBase = (1f - alignment.Moral) * 0.5f; // Evil increases greed
                float greedPersonality = (1f - personality.Selflessness) * 0.5f; // Selfishness increases greed
                float greedMorale = (1f - morale.Current) * 0.5f; // Low morale increases greed
                tuning.GreedBias = math.clamp(0.5f + greedBase * 0.3f + greedPersonality * 0.3f + greedMorale * 0.2f, 0f, 2f);

                // Curiosity bias: influenced by Order (chaos = more curious), RiskTolerance, Morale
                float curiosityBase = (1f - alignment.Order) * 0.5f; // Chaos increases curiosity
                float curiosityPersonality = (personality.RiskTolerance + 1f) * 0.5f; // Risk tolerance increases curiosity
                float curiosityMorale = (morale.Current + 1f) * 0.5f; // High morale increases curiosity
                tuning.CuriosityBias = math.clamp(0.5f + curiosityBase * 0.3f + curiosityPersonality * 0.3f + curiosityMorale * 0.2f, 0f, 2f);

                // Obedience bias: influenced by Order (lawful = more obedient), Conviction, Morale
                float obedienceBase = (alignment.Order + 1f) * 0.5f; // Order increases obedience
                float obediencePersonality = (personality.Conviction + 1f) * 0.5f; // Conviction increases obedience
                float obedienceMorale = (morale.Current + 1f) * 0.5f; // High morale increases obedience
                tuning.ObedienceBias = math.clamp(0.5f + obedienceBase * 0.3f + obediencePersonality * 0.3f + obedienceMorale * 0.2f, 0f, 2f);
            }
        }
    }
}


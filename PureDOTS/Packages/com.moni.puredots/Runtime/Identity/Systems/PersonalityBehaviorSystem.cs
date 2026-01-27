using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Personality-driven behavior system. Personality axes affect action selection weights
    /// and decision-making, but don't change WHEN actions occur (that's Initiative's job).
    /// </summary>
    [BurstCompile]
    public partial struct PersonalityBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // This system is a framework - games implement specific action selection logic
            // that calls GetActionWeight() to modify action probabilities based on personality
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system provides helper methods for other systems to use
            // Actual behavior is implemented by game-specific systems that query personality
        }

        /// <summary>
        /// Get action weight modifier based on personality. Used by action selection systems.
        /// </summary>
        [BurstCompile]
        public static float GetActionWeight(in PersonalityAxes personality, ActionType actionType)
        {
            return actionType switch
            {
                ActionType.AdventureQuest => GetAdventureWeight(in personality),
                ActionType.StartFamily => GetFamilyWeight(in personality),
                ActionType.OpenBusiness => GetBusinessWeight(in personality),
                ActionType.ChallengeAuthority => GetAuthorityWeight(in personality),
                ActionType.JoinMilitary => GetMilitaryWeight(in personality),
                ActionType.PlotRevenge => GetRevengeWeight(in personality),
                ActionType.ForgiveEnemy => GetForgiveWeight(in personality),
                ActionType.SeekSafety => GetSafetyWeight(in personality),
                _ => 1.0f // Default neutral weight
            };
        }

        [BurstCompile]
        private static float GetAdventureWeight(in PersonalityAxes personality)
        {
            // Bold seeks adventure, Craven avoids it
            return 0.5f + (personality.CravenBold * 0.005f); // -100 → 0.0, +100 → 1.0
        }

        [BurstCompile]
        private static float GetFamilyWeight(in PersonalityAxes personality)
        {
            // Bold commits quickly, Craven is cautious
            // Vengeful avoids if active grudge exists (handled elsewhere)
            return 0.7f + (personality.CravenBold * 0.002f);
        }

        [BurstCompile]
        private static float GetBusinessWeight(in PersonalityAxes personality)
        {
            // Bold chooses risky ventures, Craven chooses safe ones
            // Base weight is neutral, personality shifts risk level
            return 0.8f; // Base, risk level determined by personality elsewhere
        }

        [BurstCompile]
        private static float GetAuthorityWeight(in PersonalityAxes personality)
        {
            // Bold challenges, Craven avoids
            return 0.2f + (personality.CravenBold * 0.006f); // -100 → 0.0, +100 → 0.8
        }

        [BurstCompile]
        private static float GetMilitaryWeight(in PersonalityAxes personality)
        {
            // Bold volunteers, Craven avoids
            return 0.1f + (personality.CravenBold * 0.009f); // -100 → 0.0, +100 → 1.0
        }

        [BurstCompile]
        private static float GetRevengeWeight(in PersonalityAxes personality)
        {
            // Vengeful plots revenge, Forgiving avoids
            return 0.9f - (personality.VengefulForgiving * 0.009f); // -100 → 1.8, +100 → 0.0
        }

        [BurstCompile]
        private static float GetForgiveWeight(in PersonalityAxes personality)
        {
            // Forgiving seeks reconciliation, Vengeful avoids
            return 0.3f + (personality.VengefulForgiving * 0.007f); // -100 → 0.0, +100 → 1.0
        }

        [BurstCompile]
        private static float GetSafetyWeight(in PersonalityAxes personality)
        {
            // Craven seeks safety, Bold avoids
            return 0.8f - (personality.CravenBold * 0.008f); // -100 → 1.6, +100 → 0.0
        }
    }

    /// <summary>
    /// Action types that personality can influence.
    /// Games can extend this enum with their own action types.
    /// </summary>
    public enum ActionType : byte
    {
        AdventureQuest,
        StartFamily,
        OpenBusiness,
        ChallengeAuthority,
        JoinMilitary,
        PlotRevenge,
        ForgiveEnemy,
        SeekSafety
    }
}


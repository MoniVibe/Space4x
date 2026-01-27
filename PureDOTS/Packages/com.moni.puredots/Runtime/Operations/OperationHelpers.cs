using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Static helpers for operation management.
    /// </summary>
    [BurstCompile]
    public static class OperationHelpers
    {
        /// <summary>
        /// Creates default operation rules based on persona traits.
        /// </summary>
        public static OperationRules CreateDefaultRules(
            float vengefulForgiving,
            float cravenBold,
            OperationKind kind)
        {
            var rules = new OperationRules
            {
                Severity = CalculateSeverity(vengefulForgiving, cravenBold, kind),
                Stance = CalculateStance(vengefulForgiving, cravenBold, kind),
                AllowHumanitarianCorridors = CalculateHumanitarianCorridors(vengefulForgiving),
                AllowBombardment = CalculateBombardment(vengefulForgiving, cravenBold),
                TimeLimitTicks = 0, // No limit by default
                SuccessThreshold = 0.7f,
                FailureThreshold = 0.3f
            };

            return rules;
        }

        /// <summary>
        /// Calculates operation severity from persona.
        /// Lawful+Peaceful+Forgiving → minimal, Chaotic+Warlike+Vengeful → harsh.
        /// </summary>
        private static float CalculateSeverity(float vengefulForgiving, float cravenBold, OperationKind kind)
        {
            // Base severity depends on operation kind
            float baseSeverity = kind switch
            {
                OperationKind.Blockade => 0.3f,
                OperationKind.Siege => 0.6f,
                OperationKind.Occupation => 0.5f,
                OperationKind.Riot => 0.8f,
                OperationKind.Protest => 0.2f,
                _ => 0.5f
            };

            // Vengeful orgs increase severity
            float severity = baseSeverity + (vengefulForgiving * 0.3f);
            
            // Bold orgs slightly increase severity
            severity += (cravenBold * 0.1f);

            return math.clamp(severity, 0f, 1f);
        }

        /// <summary>
        /// Calculates operation stance from persona.
        /// </summary>
        private static float CalculateStance(float vengefulForgiving, float cravenBold, OperationKind kind)
        {
            float stance = vengefulForgiving * 0.5f + cravenBold * 0.3f;
            
            // Some operations default to heavier stance
            if (kind == OperationKind.Occupation || kind == OperationKind.Siege)
            {
                stance += 0.2f;
            }

            return math.clamp(stance, 0f, 1f);
        }

        /// <summary>
        /// Determines if humanitarian corridors are allowed.
        /// Forgiving orgs allow them, vengeful orgs don't.
        /// </summary>
        private static byte CalculateHumanitarianCorridors(float vengefulForgiving)
        {
            // Forgiving (< 0.3) allows corridors
            return vengefulForgiving < 0.3f ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Determines if bombardment is allowed.
        /// Vengeful + Bold orgs allow it.
        /// </summary>
        private static byte CalculateBombardment(float vengefulForgiving, float cravenBold)
        {
            // Vengeful (> 0.6) and Bold (> 0.5) allows bombardment
            return (vengefulForgiving > 0.6f && cravenBold > 0.5f) ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Gets participant count.
        /// </summary>
        public static int GetParticipantCount(in DynamicBuffer<OperationParticipant> participants)
        {
            return participants.Length;
        }

        /// <summary>
        /// Adds a participant to an operation.
        /// </summary>
        public static bool AddParticipant(
            ref DynamicBuffer<OperationParticipant> participants,
            Entity participantEntity,
            FixedString32Bytes role,
            float contribution)
        {
            // Check if already participant
            for (int i = 0; i < participants.Length; i++)
            {
                if (participants[i].ParticipantEntity == participantEntity)
                    return false;
            }

            if (participants.Length >= participants.Capacity)
                return false;

            participants.Add(new OperationParticipant
            {
                ParticipantEntity = participantEntity,
                Role = role,
                Contribution = math.clamp(contribution, 0f, 1f)
            });

            return true;
        }

        /// <summary>
        /// Removes a participant from an operation.
        /// </summary>
        public static bool RemoveParticipant(
            ref DynamicBuffer<OperationParticipant> participants,
            Entity participantEntity)
        {
            for (int i = 0; i < participants.Length; i++)
            {
                if (participants[i].ParticipantEntity == participantEntity)
                {
                    participants.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates total contribution from all participants.
        /// </summary>
        public static float GetTotalContribution(in DynamicBuffer<OperationParticipant> participants)
        {
            float total = 0f;
            for (int i = 0; i < participants.Length; i++)
            {
                total += participants[i].Contribution;
            }
            return total;
        }

        /// <summary>
        /// Checks if operation should transition to Resolving state.
        /// </summary>
        public static bool ShouldResolve(
            in OperationProgress progress,
            in OperationRules rules)
        {
            // Check time limit
            if (rules.TimeLimitTicks > 0 && progress.ElapsedTicks >= rules.TimeLimitTicks)
                return true;

            // Check success threshold
            if (progress.SuccessMetric >= rules.SuccessThreshold)
                return true;

            // Check failure threshold
            if (progress.SuccessMetric <= rules.FailureThreshold)
                return true;

            return false;
        }

        /// <summary>
        /// Determines operation outcome (success, failure, stalemate).
        /// </summary>
        public static OperationState DetermineOutcome(
            in OperationProgress progress,
            in OperationRules rules)
        {
            if (progress.SuccessMetric >= rules.SuccessThreshold)
                return OperationState.Ended; // Success

            if (progress.SuccessMetric <= rules.FailureThreshold)
                return OperationState.Ended; // Failure

            // Stalemate - close to thresholds but not quite
            if (math.abs(progress.SuccessMetric - 0.5f) < 0.1f)
                return OperationState.Ended; // Stalemate

            return OperationState.Resolving; // Still resolving
        }

        /// <summary>
        /// Creates default blockade parameters.
        /// </summary>
        public static BlockadeParams CreateBlockadeParams(float severity, float stance)
        {
            return new BlockadeParams
            {
                Scope = 1, // City by default
                BlockedTargets = 0xFFFF, // Everyone by default
                RiskMultiplier = 1f + (severity * 2f), // 1.0 to 3.0
                DelayMultiplier = 1f + (severity * 1.5f), // 1.0 to 2.5
                HardDenyThreshold = severity * 0.8f // 0.0 to 0.8
            };
        }

        /// <summary>
        /// Creates default siege parameters.
        /// </summary>
        public static SiegeParams CreateSiegeParams(float severity, float stance)
        {
            return new SiegeParams
            {
                EncirclementLevel = 0f, // Starts at 0, increases as participants join
                MinEncirclementRequired = 0.7f, // Need 70% encirclement
                AttritionRate = severity * 0.01f, // 0.0 to 0.01 per tick
                FamineThreshold = 0.2f, // Below 20% supply triggers famine
                DiseaseRiskMultiplier = 1f + (severity * 0.5f) // 1.0 to 1.5
            };
        }

        /// <summary>
        /// Creates default occupation parameters.
        /// </summary>
        public static OccupationParams CreateOccupationParams(float stance)
        {
            return new OccupationParams
            {
                Stance = stance,
                LawOrderModifier = stance > 0.5f ? 0.3f : -0.2f, // Heavy = more order, light = less
                CrimeModifier = stance > 0.5f ? -0.3f : 0.1f, // Heavy = less crime, light = more
                UnrestModifier = stance > 0.5f ? 0.4f : 0.1f, // Heavy = more unrest
                ResistanceSpawnProbability = stance * 0.001f // 0.0 to 0.001 per tick
            };
        }

        /// <summary>
        /// Creates default protest/riot parameters.
        /// </summary>
        public static ProtestRiotParams CreateProtestRiotParams(float grievanceLevel)
        {
            return new ProtestRiotParams
            {
                GrievanceLevel = grievanceLevel,
                CrowdSize = 0.3f, // Starts small
                OrganizationLevel = 0f, // Starts spontaneous
                EscalationThreshold = 0.7f, // 70% grievance triggers riot
                IsRiot = 0
            };
        }

        /// <summary>
        /// Creates default cult ritual parameters.
        /// </summary>
        public static CultRitualParams CreateCultRitualParams(int sacrificeCount)
        {
            return new CultRitualParams
            {
                SacrificeCount = sacrificeCount,
                CompletionProgress = 0f,
                ManaGained = 0f,
                AreaTaint = 0f,
                IsDiscovered = 0
            };
        }

        /// <summary>
        /// Creates default festival parameters.
        /// </summary>
        public static FestivalParams CreateFestivalParams(byte festivalType, uint durationTicks)
        {
            return new FestivalParams
            {
                FestivalType = festivalType,
                DurationTicks = durationTicks,
                TradeMultiplier = 1.5f,
                JoyModifier = 0.3f,
                CrimeProbabilityMultiplier = 1.2f,
                RecruitmentProbability = 0.0001f // Low probability per tick
            };
        }
    }
}






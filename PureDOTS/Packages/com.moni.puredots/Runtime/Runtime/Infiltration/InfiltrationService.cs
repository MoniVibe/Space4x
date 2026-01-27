using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Infiltration
{
    /// <summary>
    /// Service for managing infiltration operations.
    /// Provides high-level API for starting infiltration, leveling up, detecting exposure, and extraction.
    /// </summary>
    [BurstCompile]
    public static class InfiltrationService
    {
        /// <summary>
        /// Starts infiltration of a target organization.
        /// Call this when a spy band begins an infiltration mission or an individual spy is assigned to infiltrate.
        /// </summary>
        /// <param name="spy">The entity performing infiltration (spy, spy band leader, etc.)</param>
        /// <param name="targetOrganization">The organization being infiltrated (village, guild, faction, etc.)</param>
        /// <param name="method">Infiltration method (Conscription, Celebrity, Hacking, etc.)</param>
        /// <param name="coverStrength">Initial cover strength (0-1). Higher = better cover identity.</param>
        /// <param name="startTick">Current simulation tick when infiltration begins</param>
        /// <param name="state">System state for entity operations</param>
        public static void StartInfiltration(
            Entity spy,
            Entity targetOrganization,
            InfiltrationMethod method,
            float coverStrength,
            uint startTick,
            ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                state.EntityManager.AddComponent<InfiltrationState>(spy);
            }

            var infiltration = new InfiltrationState
            {
                TargetOrganization = targetOrganization,
                Level = InfiltrationLevel.Contact,
                Method = method,
                Progress = 0f,
                SuspicionLevel = 0f,
                CoverStrength = coverStrength,
                InfiltrationStartTick = startTick,
                LastActivityTick = startTick,
                IsExposed = 0,
                IsExtracting = 0
            };

            state.EntityManager.SetComponentData(spy, infiltration);
        }

        /// <summary>
        /// Levels up infiltration to the next tier.
        /// </summary>
        public static void LevelUpInfiltration(Entity spy, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            if (infiltration.IsExposed != 0 || infiltration.Level >= InfiltrationLevel.Subverted)
            {
                return;
            }

            infiltration = InfiltrationHelpers.LevelUp(infiltration);
            state.EntityManager.SetComponentData(spy, infiltration);
        }

        /// <summary>
        /// Checks if agent has been exposed.
        /// </summary>
        public static bool DetectExposure(Entity spy, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return false;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            return infiltration.IsExposed != 0;
        }

        /// <summary>
        /// Initiates extraction for an exposed agent.
        /// </summary>
        public static void Extract(
            Entity spy,
            Entity targetOrganization,
            float3 extractionPoint,
            Entity exfilContact,
            byte planQuality,
            uint plannedTick,
            ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            if (infiltration.IsExtracting != 0)
            {
                return; // Already extracting
            }

            // Create or update extraction plan
            if (!state.EntityManager.HasComponent<ExtractionPlan>(spy))
            {
                state.EntityManager.AddComponent<ExtractionPlan>(spy);
            }

            var extractionPlan = new ExtractionPlan
            {
                SafeHouseEntity = Entity.Null,
                ExfilContactEntity = exfilContact,
                ExtractionPoint = extractionPoint,
                ExfilPosition = extractionPoint,
                SuccessChance = 0f, // Will be calculated by system
                PlanQuality = planQuality,
                PlannedExtractionTick = plannedTick,
                Status = ExtractionStatus.Planned,
                IsActivated = 0
            };

            state.EntityManager.SetComponentData(spy, extractionPlan);

            // Mark as extracting
            infiltration.IsExtracting = 1;
            infiltration.IsExposed = 1;
            state.EntityManager.SetComponentData(spy, infiltration);
        }

        /// <summary>
        /// Gets current infiltration level.
        /// </summary>
        public static byte GetInfiltrationLevel(Entity spy, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return (byte)InfiltrationLevel.None;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            return (byte)infiltration.Level;
        }

        /// <summary>
        /// Gets current suspicion level.
        /// </summary>
        public static float GetSuspicionLevel(Entity spy, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return 0f;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            return infiltration.SuspicionLevel;
        }

        /// <summary>
        /// Adds suspicion from a risky activity.
        /// Call this when spy performs theft, sabotage, assassination, or other suspicious actions.
        /// Activity risk should be 0.0 (safe) to 1.0 (very risky).
        /// </summary>
        /// <param name="spy">The infiltrating entity</param>
        /// <param name="activityRisk">Risk level of the activity (0.0-1.0). Examples: Theft=0.3, Sabotage=0.6, Assassination=0.9</param>
        /// <param name="state">System state for entity operations</param>
        public static void AddSuspicion(
            Entity spy,
            float activityRisk,
            ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<InfiltrationState>(spy))
            {
                return;
            }

            var infiltration = state.EntityManager.GetComponentData<InfiltrationState>(spy);
            if (infiltration.IsExposed != 0)
            {
                return;
            }

            var targetOrg = infiltration.TargetOrganization;
            if (targetOrg == Entity.Null || !state.EntityManager.HasComponent<CounterIntelligence>(targetOrg))
            {
                return;
            }

            var counterIntel = state.EntityManager.GetComponentData<CounterIntelligence>(targetOrg);

            float suspicionGain = InfiltrationHelpers.CalculateSuspicionGain(
                activityRisk,
                infiltration.CoverStrength,
                counterIntel.InvestigationPower,
                infiltration.SuspicionLevel);

            infiltration.SuspicionLevel = math.saturate(infiltration.SuspicionLevel + suspicionGain);
            state.EntityManager.SetComponentData(spy, infiltration);
        }

        /// <summary>
        /// Records a risky activity performed by infiltrator.
        /// Convenience wrapper around AddSuspicion with clearer naming for game code.
        /// Call this when spy performs theft, sabotage, assassination, intel gathering, etc.
        /// </summary>
        /// <param name="spy">The infiltrating entity</param>
        /// <param name="activityRisk">Risk level of the activity (0.0-1.0)</param>
        /// <param name="state">System state for entity operations</param>
        /// <remarks>
        /// Activity risk guidelines:
        /// - Info gathering: 0.1-0.2 (low risk, passive observation)
        /// - Theft: 0.3-0.4 (moderate risk, property crime)
        /// - Sabotage: 0.5-0.7 (high risk, destructive actions)
        /// - Assassination: 0.8-0.9 (very high risk, murder)
        /// - Intel extraction: 0.2-0.3 (moderate risk, accessing secrets)
        /// </remarks>
        public static void RecordRiskyActivity(Entity spy, float activityRisk, ref SystemState state)
        {
            AddSuspicion(spy, activityRisk, ref state);
        }
    }
}


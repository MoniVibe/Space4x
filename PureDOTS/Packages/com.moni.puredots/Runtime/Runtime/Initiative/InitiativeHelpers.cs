using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Initiative
{
    /// <summary>
    /// Static helpers for initiative calculations.
    /// </summary>
    [BurstCompile]
    public static class InitiativeHelpers
    {
        /// <summary>
        /// Default initiative configuration.
        /// </summary>
        public static InitiativeConfig DefaultConfig => new InitiativeConfig
        {
            BaseActionInterval = 1.0f,
            UrgencyBoostMax = 0.5f,
            MinActionInterval = 0.1f,
            MaxActionInterval = 5.0f,
            InitiativeScale = 0.01f,
            BaseInitiative = 100f
        };

        /// <summary>
        /// Checks if entity can act based on cooldown.
        /// </summary>
        public static bool CanAct(in EntityInitiative init, float currentTime)
        {
            return currentTime >= init.LastActionTime + init.ActionCooldown;
        }

        /// <summary>
        /// Gets the action interval based on initiative and modifiers.
        /// </summary>
        public static float GetActionInterval(in EntityInitiative init, in InitiativeConfig config)
        {
            // Calculate effective initiative
            float effectiveInit = init.CurrentInitiative * init.SpeedModifier;
            effectiveInit *= (1f - init.FatigueModifier);
            effectiveInit *= (1f + init.MoraleModifier);
            
            // Initiative difference from base
            float initDiff = effectiveInit - config.BaseInitiative;
            
            // Speed modifier: higher initiative = faster (shorter interval)
            float speedMod = 1f - (initDiff * config.InitiativeScale);
            
            // Urgency boost
            float urgencyBoost = (init.Urgency / 100f) * config.UrgencyBoostMax;
            speedMod *= (1f - urgencyBoost);
            
            // Calculate final interval
            float interval = config.BaseActionInterval * speedMod;
            
            return math.clamp(interval, config.MinActionInterval, config.MaxActionInterval);
        }

        /// <summary>
        /// Gets effective initiative for sorting/comparison.
        /// </summary>
        public static float GetEffectiveInitiative(in EntityInitiative init)
        {
            float effective = init.CurrentInitiative * init.SpeedModifier;
            effective *= (1f - init.FatigueModifier);
            effective *= (1f + init.MoraleModifier);
            
            // Urgency adds directly to effective initiative
            effective += init.Urgency * 0.5f;
            
            return effective;
        }

        /// <summary>
        /// Compares two entities by initiative for turn order.
        /// Returns positive if a should act before b.
        /// </summary>
        public static int CompareInitiative(in EntityInitiative a, in EntityInitiative b)
        {
            float effA = GetEffectiveInitiative(a);
            float effB = GetEffectiveInitiative(b);
            
            // Higher initiative acts first
            if (effA > effB) return 1;
            if (effA < effB) return -1;
            
            // Tie-breaker: higher urgency first
            if (a.Urgency > b.Urgency) return 1;
            if (a.Urgency < b.Urgency) return -1;
            
            return 0;
        }

        /// <summary>
        /// Applies an action and sets cooldown.
        /// </summary>
        public static void ApplyAction(ref EntityInitiative init, float actionCost, float currentTime, in InitiativeConfig config)
        {
            float baseInterval = GetActionInterval(init, config);
            init.ActionCooldown = baseInterval * actionCost;
            init.LastActionTime = currentTime;
            init.IsReady = false;
            init.ActionsThisTurn++;
        }

        /// <summary>
        /// Updates initiative readiness.
        /// </summary>
        public static void UpdateReadiness(ref EntityInitiative init, float currentTime, uint currentTick)
        {
            bool wasReady = init.IsReady;
            init.IsReady = CanAct(init, currentTime);
            
            if (init.IsReady && !wasReady)
            {
                init.LastReadyTick = currentTick;
            }
        }

        /// <summary>
        /// Calculates current initiative from base and modifiers.
        /// </summary>
        public static float CalculateCurrentInitiative(float baseInit, float speedMod, float fatigueMod, float moraleMod)
        {
            float current = baseInit * speedMod;
            current *= (1f - fatigueMod);
            current *= (1f + moraleMod);
            return math.max(1f, current); // Minimum 1 initiative
        }

        /// <summary>
        /// Creates default entity initiative.
        /// </summary>
        public static EntityInitiative CreateDefault(float baseInitiative = 100f)
        {
            return new EntityInitiative
            {
                BaseInitiative = baseInitiative,
                CurrentInitiative = baseInitiative,
                ActionCooldown = 0f,
                LastActionTime = 0f,
                Urgency = 0,
                SpeedModifier = 1f,
                FatigueModifier = 0f,
                MoraleModifier = 0f,
                IsReady = true,
                ActionsThisTurn = 0
            };
        }

        /// <summary>
        /// Resets turn-based counters (call at start of new turn/round).
        /// </summary>
        public static void ResetTurn(ref EntityInitiative init)
        {
            init.ActionsThisTurn = 0;
        }

        /// <summary>
        /// Gets time until entity can act.
        /// </summary>
        public static float GetTimeUntilReady(in EntityInitiative init, float currentTime)
        {
            float readyTime = init.LastActionTime + init.ActionCooldown;
            return math.max(0f, readyTime - currentTime);
        }

        /// <summary>
        /// Sets urgency based on situation (health, danger, etc.).
        /// </summary>
        public static byte CalculateUrgency(float healthPercent, bool inDanger, bool hasTarget)
        {
            byte urgency = 0;
            
            // Low health increases urgency
            if (healthPercent < 0.25f)
                urgency += 50;
            else if (healthPercent < 0.5f)
                urgency += 25;
            
            // Danger increases urgency
            if (inDanger)
                urgency += 30;
            
            // Having a target increases urgency
            if (hasTarget)
                urgency += 20;
            
            return (byte)math.min(100, urgency);
        }
    }
}


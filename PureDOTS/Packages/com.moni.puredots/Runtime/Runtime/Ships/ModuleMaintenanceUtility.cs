using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Ships
{
    /// <summary>
    /// Stateless helpers shared by module degradation/repair systems and tests.
    /// </summary>
    public static class ModuleMaintenanceUtility
    {
        public static ModuleHealthState ResolveState(ref ModuleHealth health)
        {
            if (health.Health <= 0f)
            {
                health.State = ModuleHealthState.Destroyed;
                return health.State;
            }

            if (health.Health <= health.FailureThreshold)
            {
                health.State = ModuleHealthState.Failed;
                return health.State;
            }

            if (health.Health < health.MaxHealth)
            {
                health.State = ModuleHealthState.Degraded;
                return health.State;
            }

            health.State = ModuleHealthState.Nominal;
            return health.State;
        }

        public static float CalculateSeverity(in ModuleHealth health)
        {
            if (health.MaxHealth <= 0f)
            {
                return 1f;
            }

            var ratio = math.saturate(1f - (health.Health / health.MaxHealth));
            if (health.State == ModuleHealthState.Failed || health.State == ModuleHealthState.Destroyed)
            {
                return math.max(0.75f, ratio);
            }

            return ratio;
        }

        public static void ApplyDegradation(ref ModuleHealth health, in ModuleOperationalState opState, uint currentTick)
        {
            if (currentTick == health.LastProcessedTick)
            {
                return;
            }

            var deltaTicks = health.LastProcessedTick == 0 ? 1u : currentTick - health.LastProcessedTick;
            var rate = health.DegradationPerTick;
            var load = math.max(0f, opState.LoadFactor);
            rate *= 1f + load;
            if (opState.InCombat != 0)
            {
                rate *= 5f;
            }

            var loss = rate * deltaTicks;
            health.Health = math.max(0f, health.Health - loss);
            health.LastProcessedTick = currentTick;
            ResolveState(ref health);
        }

        public static int SelectTicketIndex(DynamicBuffer<ModuleRepairTicket> queue)
        {
            var bestIndex = -1;
            var bestPriority = -1;
            var bestSeverity = -1f;
            uint bestTick = 0;

            for (int i = 0; i < queue.Length; i++)
            {
                var ticket = queue[i];
                if (ticket.Module == Entity.Null)
                {
                    continue;
                }

                var priority = ticket.Priority;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestSeverity = ticket.Severity;
                    bestTick = ticket.RequestedTick;
                    bestIndex = i;
                    continue;
                }

                if (priority != bestPriority)
                {
                    continue;
                }

                if (ticket.Severity > bestSeverity)
                {
                    bestSeverity = ticket.Severity;
                    bestTick = ticket.RequestedTick;
                    bestIndex = i;
                    continue;
                }

                if (math.abs(ticket.Severity - bestSeverity) > 0.0001f)
                {
                    continue;
                }

                if (bestIndex < 0 || ticket.RequestedTick < bestTick)
                {
                    bestTick = ticket.RequestedTick;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public static float CalculateRepairRate(in ModuleRepairSettings settings, ModuleRepairKind kind, byte skillLevel)
        {
            var baseRate = kind == ModuleRepairKind.Station ? settings.StationRepairRate : settings.FieldRepairRate;
            var skillScalar = 1f + math.min(0.5f, skillLevel * 0.01f);
            return baseRate * skillScalar;
        }

        public static uint CalculateRefitDuration(in ShipModule module, in CarrierRefitSettings settings, byte skillLevel, float speedMultiplier)
        {
            var duration = settings.BaseRefitDurationTicks + (module.Mass * settings.MassDurationFactor);
            var skillScalar = math.clamp(1f - (skillLevel * 0.01f), 0.35f, 1f);
            var speed = speedMultiplier <= 0f ? 1f : speedMultiplier;
            var total = duration * skillScalar;
            total /= speed;
            return (uint)math.max(1f, math.ceil(total));
        }

        public static bool IsRefitAllowed(ModuleRepairKind requestedKind, in CarrierRefitSettings settings, in CarrierRefitState state)
        {
            if (requestedKind == ModuleRepairKind.Station)
            {
                return state.InRefitFacility != 0;
            }

            if (settings.AllowFieldRefit != 0)
            {
                return true;
            }

            return state.InRefitFacility != 0;
        }
    }
}

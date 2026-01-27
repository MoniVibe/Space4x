using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Contract
{
    /// <summary>
    /// Static helpers for contract and assignment calculations.
    /// </summary>
    [BurstCompile]
    public static class ContractHelpers
    {
        /// <summary>
        /// Checks if contract is near expiry.
        /// </summary>
        public static bool IsNearExpiry(
            in Contract contract,
            uint currentTick,
            uint warningThreshold)
        {
            if (contract.EndTick == 0) return false; // Indefinite
            if (contract.Status != ContractStatus.Active) return false;
            
            return contract.EndTick - currentTick <= warningThreshold;
        }

        /// <summary>
        /// Checks if payment is due.
        /// </summary>
        public static bool IsPaymentDue(
            in Contract contract,
            uint currentTick)
        {
            if (contract.PaymentAmount <= 0) return false;
            if (contract.PaymentPeriod <= 0) return false;
            
            return currentTick - contract.LastPaymentTick >= contract.PaymentPeriod;
        }

        /// <summary>
        /// Calculates total payment owed.
        /// </summary>
        public static float CalculateOwedPayment(
            in Contract contract,
            uint currentTick)
        {
            if (contract.PaymentPeriod <= 0) return 0;
            
            uint ticksSincePayment = currentTick - contract.LastPaymentTick;
            uint periodsOwed = ticksSincePayment / (uint)contract.PaymentPeriod;
            
            return periodsOwed * contract.PaymentAmount;
        }

        /// <summary>
        /// Checks if all obligations are met.
        /// </summary>
        public static bool AreObligationsMet(
            in DynamicBuffer<ContractObligation> obligations,
            uint currentTick)
        {
            for (int i = 0; i < obligations.Length; i++)
            {
                if (obligations[i].DeadlineTick > 0 && 
                    currentTick > obligations[i].DeadlineTick &&
                    obligations[i].IsMet == 0)
                {
                    return false;
                }
                
                if (obligations[i].CurrentValue < obligations[i].RequiredValue &&
                    obligations[i].IsMet == 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Updates contract status based on conditions.
        /// </summary>
        public static ContractStatus UpdateContractStatus(
            in Contract contract,
            in DynamicBuffer<ContractObligation> obligations,
            in DynamicBuffer<ContractBreach> breaches,
            uint currentTick,
            uint expiryWarningThreshold)
        {
            // Check for termination from breaches
            float totalBreachSeverity = 0;
            for (int i = 0; i < breaches.Length; i++)
            {
                if (breaches[i].WasResolved == 0)
                    totalBreachSeverity += breaches[i].Severity;
            }
            if (totalBreachSeverity >= 1f)
                return ContractStatus.Breached;
            
            // Check expiry
            if (contract.EndTick > 0)
            {
                if (currentTick >= contract.EndTick)
                    return ContractStatus.Expired;
                
                if (IsNearExpiry(contract, currentTick, expiryWarningThreshold))
                    return ContractStatus.Expiring;
            }
            
            // Check obligations
            if (!AreObligationsMet(obligations, currentTick))
                return ContractStatus.Suspended;
            
            return ContractStatus.Active;
        }

        /// <summary>
        /// Records a contract breach.
        /// </summary>
        public static void RecordBreach(
            ref DynamicBuffer<ContractBreach> breaches,
            FixedString32Bytes breachType,
            float severity,
            uint currentTick)
        {
            breaches.Add(new ContractBreach
            {
                BreachType = breachType,
                Severity = math.saturate(severity),
                OccurredTick = currentTick,
                WasResolved = 0,
                PenaltyPaid = 0
            });
        }

        /// <summary>
        /// Calculates assignment efficiency.
        /// </summary>
        public static float CalculateAssignmentEfficiency(
            float skillMatch,
            float alignmentMatch,
            float happinessLevel)
        {
            // Skill matters most
            float skillFactor = skillMatch * 0.5f;
            
            // Alignment affects willingness
            float alignmentFactor = alignmentMatch * 0.3f;
            
            // Happiness affects effort
            float happinessFactor = happinessLevel * 0.2f;
            
            return math.saturate(skillFactor + alignmentFactor + happinessFactor);
        }

        /// <summary>
        /// Calculates dividend payment from ownership.
        /// </summary>
        public static float CalculateDividends(
            float ownershipPercentage,
            float totalProfits,
            float retentionRate)
        {
            float distributableProfits = totalProfits * (1f - retentionRate);
            return distributableProfits * ownershipPercentage;
        }

        /// <summary>
        /// Evaluates contract negotiation outcome.
        /// </summary>
        public static bool EvaluateNegotiation(
            in ContractNegotiation negotiation,
            float employerWillingness,
            float contractorWillingness)
        {
            // Both parties must find it acceptable
            float proposedValue = negotiation.ProposedPayment * negotiation.ProposedDuration;
            float counterValue = negotiation.CounterOfferPayment * negotiation.CounterOfferDuration;
            
            float midpoint = (proposedValue + counterValue) / 2f;
            
            float employerAcceptance = proposedValue > 0 ? midpoint / proposedValue : 0;
            float contractorAcceptance = counterValue > 0 ? midpoint / counterValue : 0;
            
            return employerAcceptance * employerWillingness >= 0.5f &&
                   contractorAcceptance * contractorWillingness >= 0.5f;
        }

        /// <summary>
        /// Calculates notice period remaining.
        /// </summary>
        public static uint GetRemainingNoticePeriod(
            in Contract contract,
            uint terminationRequestTick)
        {
            if (contract.RequiresNotice == 0) return 0;
            
            uint noticeEndTick = terminationRequestTick + contract.NoticePeriod;
            return noticeEndTick;
        }

        /// <summary>
        /// Gets total ownership percentage for an owner.
        /// </summary>
        public static float GetTotalOwnership(
            in DynamicBuffer<OwnershipStake> stakes,
            Entity ownerEntity)
        {
            float total = 0;
            for (int i = 0; i < stakes.Length; i++)
            {
                if (stakes[i].OwnerEntity == ownerEntity)
                    total += stakes[i].Percentage;
            }
            return total;
        }
    }
}


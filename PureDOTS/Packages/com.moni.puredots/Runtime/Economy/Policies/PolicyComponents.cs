using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Tax policy component.
    /// Income brackets, business tax, transaction tax rates.
    /// </summary>
    public struct TaxPolicy : IComponentData
    {
        public Entity TargetEntity; // Village, Guild, etc.
        public float BusinessProfitTaxRate;
    }

    /// <summary>
    /// Budget policy component.
    /// Allocation percentages: education, welfare, military, etc.
    /// </summary>
    public struct BudgetPolicy : IComponentData
    {
        public Entity TargetEntity;
        public float EducationAllocation;
        public float WelfareAllocation;
        public float MilitaryAllocation;
        public float InfrastructureAllocation;
    }

    /// <summary>
    /// Tariff policy component.
    /// Import/export tariffs by GoodType.
    /// </summary>
    public struct TariffPolicy : IComponentData
    {
        public Entity TargetEntity;
        public float ImportTariffRate;
        public float ExportTariffRate;
    }

    /// <summary>
    /// Embargo policy component.
    /// Forbidden goods/partners, enforcement level.
    /// </summary>
    public struct EmbargoPolicy : IComponentData
    {
        public Entity TargetEntity;
        public float EnforcementLevel; // 0-1
    }

    /// <summary>
    /// Loan record component.
    /// Lender, borrower, principal, interest, payment schedule.
    /// </summary>
    public struct LoanRecord : IComponentData
    {
        public Entity Lender;
        public Entity Borrower;
        public float Principal;
        public float InterestRate;
        public float RemainingPrincipal;
        public float AccruedInterest;
        public uint NextPaymentTick;
        public uint MaturityTick;
    }

    /// <summary>
    /// Enforcement profile component.
    /// Patrol strength, corruption, legal severity.
    /// </summary>
    public struct EnforcementProfile : IComponentData
    {
        public Entity TargetEntity;
        public float PatrolStrength;
        public float Corruption;
        public float LegalSeverity;
    }
}


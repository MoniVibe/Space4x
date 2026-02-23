using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Which profile should steer the effective tax rate.
    /// </summary>
    public enum Space4XTaxProfileSource : byte
    {
        Ruler = 0,
        SocietyAverage = 1,
        Blend = 2
    }

    /// <summary>
    /// Configurable taxation policy for a faction.
    /// </summary>
    public struct Space4XTaxPolicy : IComponentData
    {
        public byte Enabled;
        public uint CollectionIntervalTicks;
        public Space4XTaxProfileSource ProfileSource;
        public half SocietyBlendWeight01;

        public half BaseRate01;
        public half MinRate01;
        public half MaxRate01;

        public half LawfulnessRateWeight01;
        public half IntegrityRateWeight01;

        public half AuthoritarianRateBias01;
        public half EgalitarianRateBias01;

        public half BaseLeakRate01;
        public half CorruptLeakBonus01;

        public half BusinessAssetTaxRate01;
        public float BusinessCreditsExemption;

        public static Space4XTaxPolicy Default => new Space4XTaxPolicy
        {
            Enabled = 1,
            CollectionIntervalTicks = 30u,
            ProfileSource = Space4XTaxProfileSource.Blend,
            SocietyBlendWeight01 = (half)0.5f,
            BaseRate01 = (half)0.12f,
            MinRate01 = (half)0.02f,
            MaxRate01 = (half)0.55f,
            LawfulnessRateWeight01 = (half)0.08f,
            IntegrityRateWeight01 = (half)0.04f,
            AuthoritarianRateBias01 = (half)0.03f,
            EgalitarianRateBias01 = (half)(-0.02f),
            BaseLeakRate01 = (half)0.01f,
            CorruptLeakBonus01 = (half)0.08f,
            BusinessAssetTaxRate01 = (half)0.05f,
            BusinessCreditsExemption = 120f
        };
    }

    /// <summary>
    /// Progressive or regressive adjustment band applied by income bucket.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XTaxBand : IBufferElementData
    {
        public float MinIncomePerTick;
        public float MaxIncomePerTick;
        public half RateOffset01;
    }

    /// <summary>
    /// Optional per-entity income that can be taxed by an authority.
    /// </summary>
    public struct Space4XTaxableIncome : IComponentData
    {
        public Entity TaxAuthority;
        public float GrossIncomePerTick;
        public float ShieldIncomePerTick;
        public byte IsExempt;
        public byte WithholdFromWallet;
    }

    /// <summary>
    /// Optional group/sub-group membership for nuanced tax offsets.
    /// </summary>
    public struct Space4XTaxBandMembership : IComponentData
    {
        public ushort BandId;
        public ushort SubBandId;
        public half RateOffset01;
    }

    /// <summary>
    /// Runtime taxation snapshot per faction.
    /// </summary>
    public struct Space4XTaxRuntime : IComponentData
    {
        public uint LastCollectionTick;
        public float LastCollectedCredits;
        public float LastLeakageCredits;
        public float LastEffectiveRate01;
        public float LastProfileLaw;
        public float LastProfileIntegrity;
        public uint LastTaxpayerCount;
        public float TotalCollectedCredits;
        public float TotalLeakageCredits;
    }
}

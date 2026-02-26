using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures factions have default taxation policy data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XTaxCollectionSystem))]
    public partial struct Space4XTaxBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FactionResources>();
            state.RequireForUpdate<Space4XFaction>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var em = state.EntityManager;

            foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XFaction>>()
                         .WithAll<FactionResources>()
                         .WithEntityAccess())
            {
                if (!em.HasComponent<Space4XTaxPolicy>(entity))
                {
                    ecb.AddComponent(entity, Space4XTaxPolicy.Default);
                }

                if (!em.HasComponent<Space4XTaxRuntime>(entity))
                {
                    ecb.AddComponent(entity, new Space4XTaxRuntime
                    {
                        LastCollectionTick = 0u,
                        LastCollectedCredits = 0f,
                        LastLeakageCredits = 0f,
                        LastEffectiveRate01 = 0f,
                        LastProfileLaw = 0f,
                        LastProfileIntegrity = 0f,
                        LastTaxpayerCount = 0u,
                        TotalCollectedCredits = 0f,
                        TotalLeakageCredits = 0f
                    });
                }

                if (!em.HasBuffer<Space4XTaxBand>(entity))
                {
                    var bands = ecb.AddBuffer<Space4XTaxBand>(entity);
                    bands.Add(new Space4XTaxBand { MinIncomePerTick = 0f, MaxIncomePerTick = 4f, RateOffset01 = (half)(-0.03f) });
                    bands.Add(new Space4XTaxBand { MinIncomePerTick = 4f, MaxIncomePerTick = 16f, RateOffset01 = (half)0f });
                    bands.Add(new Space4XTaxBand { MinIncomePerTick = 16f, MaxIncomePerTick = 64f, RateOffset01 = (half)0.03f });
                    bands.Add(new Space4XTaxBand { MinIncomePerTick = 64f, MaxIncomePerTick = 0f, RateOffset01 = (half)0.06f });
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Collects taxes into faction treasuries using policy bands and profile-influenced rates.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFactionEconomySystem))]
    public partial struct Space4XTaxCollectionSystem : ISystem
    {
        private struct TaxpayerRecord
        {
            public Entity Taxpayer;
            public float GrossIncomePerTick;
            public float ShieldIncomePerTick;
            public float MembershipRateOffset;
            public byte WithholdFromWallet;
        }

        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<Space4XBusinessState> _businessLookup;
        private ComponentLookup<Space4XTaxBandMembership> _membershipLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FactionResources>();
            state.RequireForUpdate<Space4XFaction>();
            state.RequireForUpdate<Space4XTaxPolicy>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _businessLookup = state.GetComponentLookup<Space4XBusinessState>(false);
            _membershipLookup = state.GetComponentLookup<Space4XTaxBandMembership>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _alignmentLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _businessLookup.Update(ref state);
            _membershipLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var tick = (uint)SystemAPI.Time.ElapsedTime;

            var societySums = new NativeParallelHashMap<Entity, float2>(256, Allocator.Temp);
            var societyCounts = new NativeParallelHashMap<Entity, int>(256, Allocator.Temp);
            var businessesByAuthority = new NativeParallelMultiHashMap<Entity, Entity>(8192, Allocator.Temp);
            var taxpayersByAuthority = new NativeParallelMultiHashMap<Entity, TaxpayerRecord>(8192, Allocator.Temp);

            foreach (var (alignment, affiliations) in SystemAPI.Query<RefRO<AlignmentTriplet>, DynamicBuffer<AffiliationTag>>())
            {
                var law = (float)alignment.ValueRO.Law;
                var integrity = (float)alignment.ValueRO.Integrity;
                var affiliationBuffer = affiliations;
                for (int i = 0; i < affiliationBuffer.Length; i++)
                {
                    var affiliation = affiliationBuffer[i];
                    if (affiliation.Type != AffiliationType.Faction ||
                        !IsFactionEntity(affiliation.Target))
                    {
                        continue;
                    }

                    if (societySums.TryGetValue(affiliation.Target, out var existing))
                    {
                        societySums[affiliation.Target] = existing + new float2(law, integrity);
                        societyCounts[affiliation.Target] = societyCounts[affiliation.Target] + 1;
                    }
                    else
                    {
                        societySums.TryAdd(affiliation.Target, new float2(law, integrity));
                        societyCounts.TryAdd(affiliation.Target, 1);
                    }
                }
            }

            foreach (var (business, businessEntity) in SystemAPI.Query<RefRO<Space4XBusinessState>>().WithEntityAccess())
            {
                var authority = ResolveTaxAuthority(business.ValueRO.Owner, business.ValueRO.Colony);
                if (authority != Entity.Null)
                {
                    businessesByAuthority.Add(authority, businessEntity);
                }
            }

            foreach (var (taxable, entity) in SystemAPI.Query<RefRO<Space4XTaxableIncome>>().WithEntityAccess())
            {
                var data = taxable.ValueRO;
                if (data.IsExempt != 0 ||
                    data.TaxAuthority == Entity.Null ||
                    !IsFactionEntity(data.TaxAuthority))
                {
                    continue;
                }

                var memberOffset = 0f;
                if (_membershipLookup.HasComponent(entity))
                {
                    memberOffset = (float)_membershipLookup[entity].RateOffset01;
                }

                taxpayersByAuthority.Add(data.TaxAuthority, new TaxpayerRecord
                {
                    Taxpayer = entity,
                    GrossIncomePerTick = data.GrossIncomePerTick,
                    ShieldIncomePerTick = data.ShieldIncomePerTick,
                    MembershipRateOffset = memberOffset,
                    WithholdFromWallet = data.WithholdFromWallet
                });
            }

            foreach (var (resources, faction, policy, runtime, bands, factionEntity) in
                     SystemAPI.Query<RefRW<FactionResources>, RefRO<Space4XFaction>, RefRO<Space4XTaxPolicy>, RefRW<Space4XTaxRuntime>, DynamicBuffer<Space4XTaxBand>>()
                         .WithEntityAccess())
            {
                var policyData = policy.ValueRO;
                if (policyData.Enabled == 0)
                {
                    continue;
                }

                var interval = math.max(1u, policyData.CollectionIntervalTicks);
                if (runtime.ValueRO.LastCollectionTick > 0u)
                {
                    var elapsed = tick - runtime.ValueRO.LastCollectionTick;
                    if (elapsed < interval)
                    {
                        continue;
                    }
                }

                var elapsedTicks = runtime.ValueRO.LastCollectionTick == 0u
                    ? interval
                    : math.max(1u, tick - runtime.ValueRO.LastCollectionTick);

                var rulerProfile = ResolveRulerProfile(factionEntity, faction.ValueRO);
                var societyProfile = rulerProfile;
                if (societySums.TryGetValue(factionEntity, out var societySum) &&
                    societyCounts.TryGetValue(factionEntity, out var societyCount) &&
                    societyCount > 0)
                {
                    var invCount = 1f / societyCount;
                    societyProfile = societySum * invCount;
                }

                var blendWeight = math.saturate((float)policyData.SocietyBlendWeight01);
                var selectedProfile = policyData.ProfileSource switch
                {
                    Space4XTaxProfileSource.Ruler => rulerProfile,
                    Space4XTaxProfileSource.SocietyAverage => societyProfile,
                    _ => math.lerp(rulerProfile, societyProfile, blendWeight)
                };

                var baseRate = (float)policyData.BaseRate01 +
                               selectedProfile.x * (float)policyData.LawfulnessRateWeight01 +
                               selectedProfile.y * (float)policyData.IntegrityRateWeight01 +
                               ResolveOutlookRateBias(faction.ValueRO.Outlook, policyData);
                baseRate = math.clamp(baseRate, (float)policyData.MinRate01, (float)policyData.MaxRate01);

                var collectedGross = 0f;
                var taxpayerCount = 0u;

                if (taxpayersByAuthority.TryGetFirstValue(factionEntity, out var taxpayer, out var taxpayerIterator))
                {
                    do
                    {
                        var taxablePerTick = math.max(0f, taxpayer.GrossIncomePerTick - taxpayer.ShieldIncomePerTick);
                        if (taxablePerTick <= 0f)
                        {
                            continue;
                        }

                        var rate = ResolveBandRate(
                            baseRate,
                            taxablePerTick,
                            taxpayer.MembershipRateOffset,
                            bands,
                            policyData.MinRate01,
                            policyData.MaxRate01);

                        var due = taxablePerTick * elapsedTicks * rate;
                        if (due <= 0f)
                        {
                            continue;
                        }

                        var paid = due;
                        if (taxpayer.WithholdFromWallet != 0)
                        {
                            paid = TryWithholdFromBusinessWallet(taxpayer.Taxpayer, due);
                        }

                        if (paid > 0f)
                        {
                            collectedGross += paid;
                            taxpayerCount++;
                        }
                    }
                    while (taxpayersByAuthority.TryGetNextValue(out taxpayer, ref taxpayerIterator));
                }

                if ((float)policyData.BusinessAssetTaxRate01 > 0f &&
                    businessesByAuthority.TryGetFirstValue(factionEntity, out var businessEntity, out var businessIterator))
                {
                    do
                    {
                        if (!_businessLookup.HasComponent(businessEntity))
                        {
                            continue;
                        }

                        var business = _businessLookup[businessEntity];
                        var taxableCredits = math.max(0f, business.Credits - policyData.BusinessCreditsExemption);
                        if (taxableCredits <= 0f)
                        {
                            continue;
                        }

                        var perTickBasis = taxableCredits / math.max(1f, elapsedTicks);
                        var rate = ResolveBandRate(
                            baseRate + (float)policyData.BusinessAssetTaxRate01,
                            perTickBasis,
                            0f,
                            bands,
                            policyData.MinRate01,
                            policyData.MaxRate01);

                        var due = taxableCredits * rate;
                        var paid = math.min(math.max(0f, business.Credits), due);
                        if (paid <= 0f)
                        {
                            continue;
                        }

                        business.Credits = math.max(0f, business.Credits - paid);
                        _businessLookup[businessEntity] = business;

                        collectedGross += paid;
                        taxpayerCount++;
                    }
                    while (businessesByAuthority.TryGetNextValue(out businessEntity, ref businessIterator));
                }

                var leakRate = ResolveLeakRate(faction.ValueRO.Outlook, selectedProfile.y, policyData);
                var leakage = collectedGross * leakRate;
                var collectedNet = math.max(0f, collectedGross - leakage);

                resources.ValueRW.Credits = math.max(0f, resources.ValueRO.Credits + collectedNet);

                runtime.ValueRW.LastCollectionTick = tick;
                runtime.ValueRW.LastCollectedCredits = collectedGross;
                runtime.ValueRW.LastLeakageCredits = leakage;
                runtime.ValueRW.LastEffectiveRate01 = baseRate;
                runtime.ValueRW.LastProfileLaw = selectedProfile.x;
                runtime.ValueRW.LastProfileIntegrity = selectedProfile.y;
                runtime.ValueRW.LastTaxpayerCount = taxpayerCount;
                runtime.ValueRW.TotalCollectedCredits += collectedGross;
                runtime.ValueRW.TotalLeakageCredits += leakage;
            }

            societySums.Dispose();
            societyCounts.Dispose();
            businessesByAuthority.Dispose();
            taxpayersByAuthority.Dispose();
        }

        private bool IsFactionEntity(Entity entity)
        {
            return entity != Entity.Null &&
                   _entityLookup.Exists(entity) &&
                   _factionLookup.HasComponent(entity);
        }

        private Entity ResolveTaxAuthority(Entity owner, Entity colony)
        {
            if (IsFactionEntity(owner))
            {
                return owner;
            }

            if (colony == Entity.Null ||
                !_entityLookup.Exists(colony) ||
                !_affiliationLookup.HasBuffer(colony))
            {
                return Entity.Null;
            }

            var affiliations = _affiliationLookup[colony];
            for (int i = 0; i < affiliations.Length; i++)
            {
                var affiliation = affiliations[i];
                if (affiliation.Type == AffiliationType.Faction && IsFactionEntity(affiliation.Target))
                {
                    return affiliation.Target;
                }
            }

            return Entity.Null;
        }

        private float2 ResolveRulerProfile(Entity factionEntity, Space4XFaction faction)
        {
            if (_alignmentLookup.HasComponent(factionEntity))
            {
                var alignment = _alignmentLookup[factionEntity];
                return new float2((float)alignment.Law, (float)alignment.Integrity);
            }

            var law = 0f;
            var integrity = 0f;

            if ((faction.Outlook & FactionOutlook.Authoritarian) != 0)
            {
                law += 0.6f;
            }

            if ((faction.Outlook & FactionOutlook.Egalitarian) != 0)
            {
                law -= 0.2f;
            }

            if ((faction.Outlook & FactionOutlook.Honorable) != 0)
            {
                integrity += 0.6f;
            }

            if ((faction.Outlook & FactionOutlook.Corrupt) != 0)
            {
                integrity -= 0.7f;
            }

            if ((faction.Outlook & FactionOutlook.Spiritualist) != 0)
            {
                integrity += 0.1f;
            }

            return math.clamp(new float2(law, integrity), new float2(-1f, -1f), new float2(1f, 1f));
        }

        private static float ResolveOutlookRateBias(FactionOutlook outlook, Space4XTaxPolicy policy)
        {
            var bias = 0f;
            if ((outlook & FactionOutlook.Authoritarian) != 0)
            {
                bias += (float)policy.AuthoritarianRateBias01;
            }

            if ((outlook & FactionOutlook.Egalitarian) != 0)
            {
                bias += (float)policy.EgalitarianRateBias01;
            }

            return bias;
        }

        private static float ResolveLeakRate(FactionOutlook outlook, float profileIntegrity, Space4XTaxPolicy policy)
        {
            var leak = (float)policy.BaseLeakRate01;
            if ((outlook & FactionOutlook.Corrupt) != 0)
            {
                leak += (float)policy.CorruptLeakBonus01;
            }

            if (profileIntegrity < 0f)
            {
                leak += math.abs(profileIntegrity) * 0.05f;
            }

            return math.clamp(leak, 0f, 0.9f);
        }

        private float TryWithholdFromBusinessWallet(Entity taxpayer, float due)
        {
            if (due <= 0f || !_businessLookup.HasComponent(taxpayer))
            {
                return 0f;
            }

            var business = _businessLookup[taxpayer];
            var paid = math.min(math.max(0f, business.Credits), due);
            if (paid <= 0f)
            {
                return 0f;
            }

            business.Credits = math.max(0f, business.Credits - paid);
            _businessLookup[taxpayer] = business;
            return paid;
        }

        private static float ResolveBandRate(
            float baseRate,
            float incomePerTick,
            float extraOffset,
            DynamicBuffer<Space4XTaxBand> bands,
            half minRate,
            half maxRate)
        {
            var bandOffset = ResolveBandOffset(incomePerTick, bands);
            var raw = baseRate + bandOffset + extraOffset;
            return math.clamp(raw, (float)minRate, (float)maxRate);
        }

        private static float ResolveBandOffset(float incomePerTick, DynamicBuffer<Space4XTaxBand> bands)
        {
            if (bands.Length == 0)
            {
                return 0f;
            }

            var fallback = (float)bands[0].RateOffset01;
            var hasFallback = false;

            for (int i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                var inLower = incomePerTick >= band.MinIncomePerTick;
                var inUpper = band.MaxIncomePerTick <= 0f || incomePerTick < band.MaxIncomePerTick;
                if (inLower && inUpper)
                {
                    return (float)band.RateOffset01;
                }

                if (incomePerTick >= band.MinIncomePerTick)
                {
                    fallback = (float)band.RateOffset01;
                    hasFallback = true;
                }
            }

            return hasFallback ? fallback : (float)bands[0].RateOffset01;
        }
    }
}

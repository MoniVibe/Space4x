using PureDOTS.Runtime;
using PureDOTS.Runtime.Economy.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Assigns primary currencies to factions and businesses.
    /// Empires issue currency; businesses inherit empire currency unless overridden.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XOrganizationRelationSystem))]
    [UpdateBefore(typeof(Space4XTradeMatchingSystem))]
    public partial struct Space4XCurrencyAssignmentSystem : ISystem
    {
        private const float DefaultGuildDiscount = 0.1f;
        private const float DefaultGuildStanding = 0.4f;

        private ComponentLookup<PrimaryCurrency> _primaryLookup;
        private ComponentLookup<CurrencyIssuer> _issuerLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<EmpireMembership> _empireMembershipLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<BusinessCurrencyOverride> _businessOverrideLookup;
        private ComponentLookup<ColonyFacilityLink> _facilityLinkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<Space4XFaction>();
            _primaryLookup = state.GetComponentLookup<PrimaryCurrency>(true);
            _issuerLookup = state.GetComponentLookup<CurrencyIssuer>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _empireMembershipLookup = state.GetComponentLookup<EmpireMembership>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _businessOverrideLookup = state.GetComponentLookup<BusinessCurrencyOverride>(true);
            _facilityLinkLookup = state.GetComponentLookup<ColonyFacilityLink>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            _primaryLookup.Update(ref state);
            _issuerLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _empireMembershipLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _businessOverrideLookup.Update(ref state);
            _facilityLinkLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                var currencyId = ResolveFactionCurrency(entity, faction.ValueRO);
                EnsurePrimaryCurrency(entity, currencyId, ref ecb);

                if (faction.ValueRO.Type == FactionType.Empire)
                {
                    EnsureIssuer(entity, CurrencyIssuerType.Empire, currencyId, 0f, 0f, ref ecb);
                }
                else if (faction.ValueRO.Type == FactionType.Guild)
                {
                    EnsureIssuer(entity, CurrencyIssuerType.Guild, currencyId, DefaultGuildDiscount, DefaultGuildStanding, ref ecb);
                }
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<BusinessProduction>>().WithEntityAccess())
            {
                if (_primaryLookup.HasComponent(entity) && !_businessOverrideLookup.HasComponent(entity))
                {
                    continue;
                }

                var currencyId = ResolveBusinessCurrency(entity);
                EnsurePrimaryCurrency(entity, currencyId, ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private FixedString64Bytes ResolveFactionCurrency(Entity factionEntity, in Space4XFaction faction)
        {
            if (faction.Type == FactionType.Empire)
            {
                return BuildCurrencyId(CurrencyIssuerType.Empire, faction.FactionId);
            }

            if (faction.Type == FactionType.Guild)
            {
                return BuildCurrencyId(CurrencyIssuerType.Guild, faction.FactionId);
            }

            if (_empireMembershipLookup.HasComponent(factionEntity))
            {
                var membership = _empireMembershipLookup[factionEntity];
                if (membership.Empire != Entity.Null)
                {
                    if (_primaryLookup.HasComponent(membership.Empire))
                    {
                        var currency = _primaryLookup[membership.Empire].CurrencyId;
                        if (currency.Length > 0)
                        {
                            return currency;
                        }
                    }

                    if (_factionLookup.HasComponent(membership.Empire))
                    {
                        var empire = _factionLookup[membership.Empire];
                        return BuildCurrencyId(CurrencyIssuerType.Empire, empire.FactionId);
                    }
                }
            }

            return BuildCurrencyId(CurrencyIssuerType.Faction, faction.FactionId);
        }

        private FixedString64Bytes ResolveBusinessCurrency(Entity businessEntity)
        {
            if (_businessOverrideLookup.HasComponent(businessEntity))
            {
                var overrideCurrency = _businessOverrideLookup[businessEntity].CurrencyId;
                if (overrideCurrency.Length > 0)
                {
                    return overrideCurrency;
                }
            }

            if (TryResolveAffiliationCurrency(businessEntity, out var currency))
            {
                return currency;
            }

            if (_facilityLinkLookup.HasComponent(businessEntity))
            {
                var colony = _facilityLinkLookup[businessEntity].Colony;
                if (TryResolveAffiliationCurrency(colony, out currency))
                {
                    return currency;
                }
            }

            return default;
        }

        private bool TryResolveAffiliationCurrency(Entity entity, out FixedString64Bytes currency)
        {
            currency = default;
            if (entity == Entity.Null)
            {
                return false;
            }

            if (_primaryLookup.HasComponent(entity))
            {
                var existing = _primaryLookup[entity].CurrencyId;
                if (existing.Length > 0)
                {
                    currency = existing;
                    return true;
                }
            }

            if (_factionLookup.HasComponent(entity))
            {
                currency = ResolveFactionCurrency(entity, _factionLookup[entity]);
                return currency.Length > 0;
            }

            if (!_affiliationLookup.HasBuffer(entity))
            {
                return false;
            }

            var affiliations = _affiliationLookup[entity];
            Entity fallbackFaction = Entity.Null;

            for (int i = 0; i < affiliations.Length; i++)
            {
                var tag = affiliations[i];
                if (tag.Type == AffiliationType.Empire && tag.Target != Entity.Null)
                {
                    if (_primaryLookup.HasComponent(tag.Target))
                    {
                        currency = _primaryLookup[tag.Target].CurrencyId;
                        if (currency.Length > 0)
                        {
                            return true;
                        }
                    }

                    if (_factionLookup.HasComponent(tag.Target))
                    {
                        var empire = _factionLookup[tag.Target];
                        currency = BuildCurrencyId(CurrencyIssuerType.Empire, empire.FactionId);
                        return true;
                    }
                }

                if (tag.Type == AffiliationType.Faction || tag.Type == AffiliationType.Corporation)
                {
                    fallbackFaction = tag.Target;
                }
            }

            if (fallbackFaction != Entity.Null)
            {
                if (_primaryLookup.HasComponent(fallbackFaction))
                {
                    currency = _primaryLookup[fallbackFaction].CurrencyId;
                    if (currency.Length > 0)
                    {
                        return true;
                    }
                }

                if (_factionLookup.HasComponent(fallbackFaction))
                {
                    var faction = _factionLookup[fallbackFaction];
                    currency = BuildCurrencyId(CurrencyIssuerType.Faction, faction.FactionId);
                    return true;
                }
            }

            return false;
        }

        private void EnsurePrimaryCurrency(Entity entity, FixedString64Bytes currencyId, ref EntityCommandBuffer ecb)
        {
            if (entity == Entity.Null || currencyId.Length == 0)
            {
                return;
            }

            if (_primaryLookup.HasComponent(entity))
            {
                var current = _primaryLookup[entity];
                if (!current.CurrencyId.Equals(currencyId))
                {
                    current.CurrencyId = currencyId;
                    ecb.SetComponent(entity, current);
                }
                return;
            }

            ecb.AddComponent(entity, new PrimaryCurrency
            {
                CurrencyId = currencyId
            });
        }

        private void EnsureIssuer(
            Entity entity,
            CurrencyIssuerType issuerType,
            FixedString64Bytes currencyId,
            float discount,
            float requiredStanding,
            ref EntityCommandBuffer ecb)
        {
            if (entity == Entity.Null || currencyId.Length == 0)
            {
                return;
            }

            if (_issuerLookup.HasComponent(entity))
            {
                var issuer = _issuerLookup[entity];
                issuer.CurrencyId = currencyId;
                issuer.IssuerType = issuerType;
                issuer.MemberDiscount = (half)math.clamp(discount, 0f, 0.9f);
                issuer.RequiredStanding = (half)math.clamp(requiredStanding, 0f, 1f);
                ecb.SetComponent(entity, issuer);
                return;
            }

            ecb.AddComponent(entity, new CurrencyIssuer
            {
                CurrencyId = currencyId,
                IssuerType = issuerType,
                MemberDiscount = (half)math.clamp(discount, 0f, 0.9f),
                RequiredStanding = (half)math.clamp(requiredStanding, 0f, 1f)
            });
        }

        private static FixedString64Bytes BuildCurrencyId(CurrencyIssuerType issuerType, ushort issuerId)
        {
            var id = new FixedString64Bytes();
            switch (issuerType)
            {
                case CurrencyIssuerType.Empire:
                    id.Append((FixedString32Bytes)"empire-");
                    break;
                case CurrencyIssuerType.Guild:
                    id.Append((FixedString32Bytes)"guild-");
                    break;
                default:
                    id.Append((FixedString32Bytes)"faction-");
                    break;
            }

            id.Append((int)issuerId);
            return id;
        }
    }

    /// <summary>
    /// Syncs faction credits into currency balances for the primary currency.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFactionEconomySystem))]
    public partial struct Space4XCurrencyBalanceSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FactionResources>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (resources, currency, entity) in SystemAPI.Query<RefRO<FactionResources>, RefRO<PrimaryCurrency>>()
                         .WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<CurrencyBalanceEntry>(entity))
                {
                    var buffer = ecb.AddBuffer<CurrencyBalanceEntry>(entity);
                    buffer.Add(new CurrencyBalanceEntry
                    {
                        CurrencyId = currency.ValueRO.CurrencyId,
                        Balance = resources.ValueRO.Credits
                    });
                    continue;
                }

                var balances = state.EntityManager.GetBuffer<CurrencyBalanceEntry>(entity);
                bool updated = false;
                for (int i = 0; i < balances.Length; i++)
                {
                    if (!balances[i].CurrencyId.Equals(currency.ValueRO.CurrencyId))
                    {
                        continue;
                    }

                    var entry = balances[i];
                    entry.Balance = resources.ValueRO.Credits;
                    balances[i] = entry;
                    updated = true;
                    break;
                }

                if (!updated)
                {
                    balances.Add(new CurrencyBalanceEntry
                    {
                        CurrencyId = currency.ValueRO.CurrencyId,
                        Balance = resources.ValueRO.Credits
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

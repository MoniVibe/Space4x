# Economy System Concepts

## Goals
- Provide a shared economic framework covering currency, markets, trade routes, taxation, credit, and policy for both Godgame and Space4x.
- Integrate with industrial sectors, mobile settlements, production chains, buffs, and the metric engine while staying deterministic and data-driven.

## Core Components
- `CurrencyRegistry` singleton:
  - List of currencies (coins, credits, barter tokens) with exchange rates, inflation policies.
  - Base currency for metrics; PPP adjustments for cross-settlement comparisons.
- `MarketRegistry`:
  ```csharp
  public struct MarketEntry : IBufferElementData
  {
      public ResourceTypeIndex Resource;
      public float Supply;
      public float Demand;
      public float Price;
      public float LastPrice;
      public float ScarcityIndex;
      public MarketFlags Flags; // essential, luxury, contraband
  }
  ```
  - Per settlement/sector markets storing supply/demand curves.
- `TradeRoute` component:
  - `RouteId`, `Origin`, `Destination`, `Mode` (caravan, ship, fleet), `Capacity`, `Risk`, `Tariff`.
- `FiscalPolicy` component:
  - Tax rates (income, trade, property), subsidies, upkeep modifiers.
- `SettlementTreasury` component:
  - `Wealth`, `ReservedFunds`, `TaxArrears`, `PreferredPayment` (currency, goods/services, direct tribute).
  - `Ownership` entries for buildings/businesses; active lease contracts.
- `TerritoryClaim` component:
  - `RegionId`, `DesirabilityScore` (resources, aesthetics), `ClaimantSettlement`, `DisputeStatus`.
- `CreditLedger` component:
  - Outstanding loans, interest rates, collateral, default timers.
- `EconomicBuff` definitions referencing existing Buff system (embargoes, booms, recessions).

## Systems Overview
1. `MarketUpdateSystem`:
   - Runs per tick or cadence; adjusts price using supply/demand (e.g., tâtonnement or elasticity curves).
   - Applies scarcity penalties/bonuses, currency inflation.
2. `TradeExecutionSystem`:
   - Processes trade orders from players/AI/mobile settlements.
   - Moves goods along `TradeRoute`, consuming logistics capacity, applying tariffs and route risk (using navigation).
3. `FiscalSystem`:
   - Collects taxes, pays subsidies/upkeep, updates settlement budgets.
   - Supports payment modes: currency, in-kind contributions (goods/services), elite tribute.
   - Handles settlement-owned asset leases.
   - Triggers policy-driven buffs/debuffs (industrial incentives, austerity penalties).
4. `CreditSystem`:
   - Issues loans, accrues interest, handles repayments/defaults.
   - Impacts industrial upgrades, mobile ship improvements, economic buffs (credit crunch).
5. `BlackMarketSystem`:
   - Handles contraband listings, stealth trade routes, perception visibility checks.
6. `EconomicEventSystem`:
   - Applies scheduled or narrative-driven events (booms, embargoes, sanctions) via buff system and market adjustments.

## Currency & Units
- Authoring asset `CurrencyProfile` defines:
  - Currency id, symbol, base value, inflation target, backing (metal, energy, influence).
- Exchange rates updated via metric engine; integrate with industrial sectors for PPP adjustments.
- Provide conversion utilities for UI/metrics.

## Market Mechanics
- Supply/demand updates using incremental counters:
  - Supply increments on production/harvest, decrements on trade/consumption.
  - Demand tracked from consumption orders, industrial requirements.
- Price update formula example:
  ```
  float excess = (Demand - Supply) / max(1f, Supply + Demand);
  Price = Price * (1f + priceElasticity * excess);
  Price = math.clamp(Price, minPrice, maxPrice);
  ```
- Scarcity index derived from resource rarity, event modifiers.
- Support price smoothing (EMA) to avoid oscillations.

## Trade Routes & Logistics
- Route definitions tie into navigation volumes (ocean lanes, interstellar corridors).
- Risk factors (pirates, storms) draw from danger layers/environment effects.
- Tariffs/tolls apply via fiscal policy; route profitability influences AI decisions.
- Nomadic settlements generate dynamic routes (self-managed logistics).

## Policies & Governance
- Policy effects modeled as buffs applied to markets or industries (e.g., `IndustrialSubsidy`, `TradeEmbargo`).
- Captain outlooks influence policy adoption for mobile settlements.
- Settlements may own assets and lease them; policies govern rent, upkeep, tenant rights.
- AI evaluation uses `Pressure` metrics (industrial disparity, budget deficits) to choose policies.
- Territory claims drive disputes; integrate with sociopolitical system to trigger conflicts or negotiations.

## Credit & Investment
- `CreditLedger` entries track principal, rate, term, collateral entity.
- `CreditEvaluationSystem` determines eligibility based on industrial level, reputation.
- Defaults trigger events (asset seizure, morale penalties).
- Investments: allow players/AI to fund upgrades with ROI metrics (tracked by metric engine).

## Metrics Integration
- Metrics to compute:
  - `gdp_nominal`, `gdp_real`, `gdp_per_capita`.
  - `trade_volume`, `trade_balance`, `tax_revenue`, `budget_balance`.
  - `inflation_rate`, `price_index`, `market_scarcity`.
  - `credit_default_rate`, `loan_outstanding`.
- Use metric engine cadences (daily/weekly) for rollups and UI dashboards.

## Buff & Event Hooks
- Economic events trigger buffs (e.g., `BoomBuff`, `RecessionDebuff`).
- Environmental effects (storms) interact with trade by applying debuffs to route uptime.
- Narrative situations leverage metrics (e.g., famine triggers relief quest, trade alliance).
- Fiscal actions (tax hikes, elite tribute) apply buffs/debuffs to social strata (e.g., `TaxBurden`, `VolunteerService`).
- Guild dues, band upkeep buffs integrate with `FactionAndGuildSystem` and buff engine.

## Testing & Tooling
- Unit tests for price update, tax collection, loan interest.
- Simulation scenarios for trade shocks, embargoes, credit crunch.
- Metric regression tests verifying GDP calculations match expected inputs.
- Debug overlay showing market prices, supply/demand, trade route status.
- Editor tooling: market inspector, currency editor, policy simulation (what-if).

## Technical Considerations
- Use SoA storage for market arrays (resource type vs. supply/demand/price).
- Guard heavy computations with dirty flags and cadenced scheduler.
- Ensure determinism via consistent ordering (resource index, route id).
- Rewind support: events recorded for trades, price changes; replay updates market state.
- Integrate with `SchedulerAndQueueing` for periodic settlements, loan payments.
- Provide fallback for alternate economies (barter only) by swapping catalog definitions.

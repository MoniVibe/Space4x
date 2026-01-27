# Lost-Tech and Ruin Discovery System

**Status**: Concept Design
**Last Updated**: 2025-11-30
**Cross-Project**: Godgame (primary), Space4X (adapted)

---

## Overview

**Lost-Tech Discovery** creates emergent questlines where fallen civilizations leave behind knowledge encoded in ruins, corpses, and artifacts. Scouts explore ruins and discover tech, which they sell to wealthy patrons who employ bands/expeditions to extract it. Knowledge ranges from simple cultural practices (handwashing) to complex magic systems, requiring intelligent/wise individuals and time to decipher.

**Core Principles**:
- ✅ **Cultural Preservation**: Dead aggregates leave knowledge based on their unlocked tech and cultural practices
- ✅ **Tiered Extraction**: Simple knowledge (recipes) vs. complex (magic systems) require different skill/time
- ✅ **Economic Chain**: Scouts discover → Sell to patrons → Patrons fund expeditions → Bands extract
- ✅ **Knowledge Adoption**: Aggregates implement knowledge as baseline, members slowly adopt
- ✅ **Dual Leadership**: Sergeants/Quartermasters manage logistics while leaders command

**Design Goals**:
- Create economic loops around knowledge discovery
- Reward exploration and intelligence/wisdom stats
- Preserve cultural identity through ruins (handwashing culture → hygiene knowledge)
- Enable knowledge monopolies (patrons hoard tech, control access)
- Integrate with existing systems (bands, guilds, alignment)

---

## Ruin Generation from Fallen Aggregates

### Godgame Ruins

```csharp
public struct RuinSite : IComponentData
{
    public Entity SourceAggregate;         // Village/band that fell
    public RuinType Type;                  // Settlement, Battlefield, Library, Temple
    public float3 Location;
    public uint FallTick;                  // When aggregate was destroyed
    public float DecayLevel;               // 0-1 (0 = fresh, 1 = ancient/eroded)
    public DynamicBuffer<KnowledgeFragment> PreservedKnowledge;
    public DynamicBuffer<CulturalPractice> CulturalRemnants;
}

public enum RuinType : byte
{
    Settlement,      // Destroyed village
    Battlefield,     // Mass grave, corpses
    Library,         // Scholar guild ruins
    Temple,          // Religious site
    Workshop,        // Craftsman guild ruins
    MageSchool,      // Magic academy ruins
}

public struct KnowledgeFragment : IBufferElementData
{
    public KnowledgeType Type;            // Magic, Technique, Recipe, Method
    public float Complexity;              // 0-1 (0.2 = recipe, 0.9 = arcane magic)
    public int RequiredIntelligence;      // Stat requirement to decipher
    public int RequiredWisdom;            // Wisdom requirement
    public uint ExtractionTime;           // Ticks to fully extract
    public bool Encrypted;                // Requires special key/ritual to unlock
}

public enum KnowledgeType : byte
{
    // Practical
    Recipe,          // Cooking, alchemy, smithing recipes
    Technique,       // Combat techniques, crafting methods
    Method,          // Procedures, protocols (handwashing, irrigation)

    // Arcane
    Spell,           // Magic spell
    Ritual,          // Complex magic ritual
    MagicTheory,     // Foundational magic knowledge

    // Cultural
    SocialPractice,  // Cultural norms (hygiene, etiquette)
    ReligiousDoctrine, // Belief systems
    LegalCode,       // Laws, governance structures

    // Technical
    Engineering,     // Architecture, construction
    Agriculture,     // Farming techniques
    Medicine,        // Healing knowledge
}

public struct CulturalPractice : IBufferElementData
{
    public string PracticeName;           // "Thorough Handwashing", "Grain Rotation"
    public float EffectivenessBonus;      // +10% disease resistance, +20% yields
    public AlignmentRequirement AlignmentGate;  // Some practices alignment-locked
    public uint AdoptionTime;             // Ticks for aggregate to baseline-adopt
    public float MemberAdoptionRate;      // 0.01 per 100 ticks (slow cultural shift)
}
```

**Ruin Generation Process**:

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class AggregateDeathRuinCreationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity aggregate, in AggregateDeathEvent death, in TechTree techTree, in CulturalIdentity culture) =>
        {
            // Create ruin site at aggregate location
            var ruin = EntityManager.CreateEntity();
            EntityManager.AddComponent<RuinSite>(ruin);

            var ruinData = new RuinSite
            {
                SourceAggregate = aggregate,
                Type = DetermineRuinType(aggregate),  // Settlement, Library, etc.
                Location = GetAggregateLocation(aggregate),
                FallTick = CurrentTick,
                DecayLevel = 0f,  // Fresh ruins
            };

            // Preserve knowledge from tech tree
            var knowledgeBuffer = EntityManager.AddBuffer<KnowledgeFragment>(ruin);
            foreach (var tech in techTree.UnlockedTech)
            {
                var fragment = new KnowledgeFragment
                {
                    Type = MapTechToKnowledgeType(tech),
                    Complexity = tech.Complexity,
                    RequiredIntelligence = (int)(tech.Complexity * 100),
                    RequiredWisdom = (int)(tech.Complexity * 80),
                    ExtractionTime = (uint)(tech.Complexity * 2000),  // 400-1800 ticks
                    Encrypted = tech.IsSecretKnowledge,
                };
                knowledgeBuffer.Add(fragment);
            }

            // Preserve cultural practices
            var cultureBuffer = EntityManager.AddBuffer<CulturalPractice>(ruin);
            foreach (var practice in culture.Practices)
            {
                var remnant = new CulturalPractice
                {
                    PracticeName = practice.Name,
                    EffectivenessBonus = practice.Bonus,
                    AlignmentGate = practice.AlignmentRequirement,
                    AdoptionTime = 2000,  // 2000 ticks to baseline-adopt
                    MemberAdoptionRate = 0.01f,  // 1% per 100 ticks
                };
                cultureBuffer.Add(remnant);
            }

            EntityManager.SetComponentData(ruin, ruinData);

        }).Run();
    }

    private KnowledgeType MapTechToKnowledgeType(TechDefinition tech)
    {
        // Mage guild → Magic knowledge
        if (tech.SourceGuild == GuildType.Mages) return KnowledgeType.Spell;

        // Warrior guild → Combat techniques
        if (tech.SourceGuild == GuildType.Warriors) return KnowledgeType.Technique;

        // Craftsman guild → Recipes, methods
        if (tech.SourceGuild == GuildType.Craftsmen) return KnowledgeType.Recipe;

        // Scholar guild → Theory, engineering
        if (tech.SourceGuild == GuildType.Scholars) return KnowledgeType.Engineering;

        return KnowledgeType.Method;  // Default
    }
}
```

**Example Cultural Preservation**:

- **Handwashing Culture**: Village with high hygiene practices falls → Ruins contain "Thorough Handwashing" cultural practice → +15% disease resistance for adopters
- **Grain Rotation**: Agricultural village → "Crop Rotation Method" → +20% farm yields
- **Honorable Combat**: Warrior culture → "Dueling Code" technique → +10% morale in 1v1 combat

---

## Scout Discovery and Economic Chain

### Scout Exploration

```csharp
public struct ScoutBehavior : IComponentData
{
    public Entity Scout;                  // Individual villager
    public float ExplorationRange;        // How far scout travels
    public int Intelligence;              // Affects discovery quality
    public uint LastExplorationTick;
    public DynamicBuffer<Entity> DiscoveredRuins;
    public float DiscoveryChance;         // 0-1 (base 0.15 per exploration)
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ScoutRuinDiscoverySystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref ScoutBehavior scout, in LocalTransform transform) =>
        {
            // Periodic exploration (every 500 ticks)
            if (CurrentTick - scout.LastExplorationTick < 500) return;

            scout.LastExplorationTick = CurrentTick;

            // Check for ruins within exploration range
            var nearbyRuins = GetRuinsInRadius(transform.Position, scout.ExplorationRange);

            foreach (var ruin in nearbyRuins)
            {
                // Discovery chance based on intelligence and ruin decay
                var ruinData = GetComponent<RuinSite>(ruin);
                float discoveryChance = scout.DiscoveryChance * (1f - ruinData.DecayLevel * 0.5f);

                if (Random.NextFloat() < discoveryChance)
                {
                    // Scout discovers ruin
                    scout.DiscoveredRuins.Add(ruin);

                    // Assess value based on knowledge complexity
                    float ruinValue = AssessRuinValue(ruin, scout.Intelligence);

                    // Scout can now sell this knowledge to patrons
                    CreateKnowledgeListing(scout.Scout, ruin, ruinValue);
                }
            }

        }).Run();
    }

    private float AssessRuinValue(Entity ruin, int scoutIntelligence)
    {
        var fragments = GetBuffer<KnowledgeFragment>(ruin);
        float totalValue = 0f;

        foreach (var fragment in fragments)
        {
            // High complexity = high value
            totalValue += fragment.Complexity * 1000f;

            // Scout intelligence affects appraisal accuracy
            float appraisalAccuracy = scoutIntelligence / 100f;  // 0-1
            totalValue *= (0.5f + appraisalAccuracy * 0.5f);  // 50-100% accurate
        }

        return totalValue;
    }
}
```

### Knowledge Marketplace

```csharp
public struct KnowledgeListing : IComponentData
{
    public Entity Scout;                  // Scout selling knowledge
    public Entity Ruin;                   // Ruin location
    public float AskingPrice;             // Gold cost
    public uint ListingTick;
    public bool Sold;
    public Entity Buyer;                  // Patron who bought it
}

public struct WealthyPatron : IComponentData
{
    public Entity Patron;                 // Noble, merchant, guild master
    public int Wealth;                    // Available gold
    public int Intelligence;              // Can appraise knowledge value
    public PatronType Type;
    public DynamicBuffer<Entity> OwnedKnowledge;  // Ruins patron has rights to
}

public enum PatronType : byte
{
    Noble,           // High wealth, seeks power/prestige knowledge
    Merchant,        // Medium wealth, seeks profitable knowledge (recipes, trade)
    GuildMaster,     // Medium wealth, seeks guild-specific knowledge
    Scholar,         // Low wealth, seeks any knowledge (intrinsic value)
    MageOrder,       // High wealth, seeks magic knowledge exclusively
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class KnowledgeMarketplaceSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Patrons browse listings and decide to buy
        Entities.ForEach((ref WealthyPatron patron) =>
        {
            var listings = GetAllKnowledgeListings();

            foreach (var listing in listings)
            {
                if (listing.Sold) continue;

                var listingData = GetComponent<KnowledgeListing>(listing);

                // Patron interest based on type and knowledge type
                bool interested = IsPatronInterested(patron, listingData.Ruin);

                if (interested && patron.Wealth >= listingData.AskingPrice)
                {
                    // Purchase knowledge
                    patron.Wealth -= (int)listingData.AskingPrice;
                    patron.OwnedKnowledge.Add(listingData.Ruin);

                    listingData.Sold = true;
                    listingData.Buyer = patron.Patron;

                    // Scout receives payment
                    var scout = GetComponent<VillagerWealth>(listingData.Scout);
                    scout.Gold += (int)listingData.AskingPrice;

                    // Patron now funds expedition to extract knowledge
                    CreateExtractionQuest(patron.Patron, listingData.Ruin);
                }
            }

        }).Run();
    }

    private bool IsPatronInterested(WealthyPatron patron, Entity ruin)
    {
        var fragments = GetBuffer<KnowledgeFragment>(ruin);

        foreach (var fragment in fragments)
        {
            // Mage Order only wants magic
            if (patron.Type == PatronType.MageOrder &&
                (fragment.Type == KnowledgeType.Spell || fragment.Type == KnowledgeType.Ritual))
                return true;

            // Merchants want profitable knowledge
            if (patron.Type == PatronType.Merchant &&
                (fragment.Type == KnowledgeType.Recipe || fragment.Type == KnowledgeType.Agriculture))
                return true;

            // Nobles want prestige/power
            if (patron.Type == PatronType.Noble &&
                fragment.Complexity > 0.7f)  // High complexity = prestigious
                return true;

            // Scholars want everything
            if (patron.Type == PatronType.Scholar)
                return true;
        }

        return false;
    }
}
```

---

## Band Extraction Expeditions

### Extraction Quest Creation

```csharp
public struct ExtractionQuest : IComponentData
{
    public Entity Patron;                 // Who funded expedition
    public Entity Ruin;                   // Target ruin
    public Entity AssignedBand;           // Band hired for extraction
    public uint QuestStartTick;
    public uint EstimatedDuration;        // Ticks to complete
    public int Payment;                   // Gold reward for band
    public ExtractionStatus Status;
}

public enum ExtractionStatus : byte
{
    Recruiting,      // Patron seeking band
    InProgress,      // Band at ruin, extracting
    Completed,       // Knowledge extracted
    Failed,          // Band died or gave up
    Abandoned,       // Patron cancelled
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ExtractionQuestCreationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity questEntity, ref ExtractionQuest quest, in WealthyPatron patron) =>
        {
            if (quest.Status != ExtractionStatus.Recruiting) return;

            // Find suitable band (high intelligence/wisdom, available)
            var bands = GetAvailableBands();

            foreach (var band in bands)
            {
                var bandData = GetComponent<BandComponents>(band);

                // Band must have intelligent/wise members
                int totalIntelligence = CalculateBandIntelligence(band);
                int totalWisdom = CalculateBandWisdom(band);

                var ruinFragments = GetBuffer<KnowledgeFragment>(quest.Ruin);
                int requiredIntelligence = GetMaxRequiredStat(ruinFragments, f => f.RequiredIntelligence);
                int requiredWisdom = GetMaxRequiredStat(ruinFragments, f => f.RequiredWisdom);

                // Band must meet minimum stats
                if (totalIntelligence >= requiredIntelligence && totalWisdom >= requiredWisdom)
                {
                    // Offer quest to band
                    bool accepted = BandDecisionLogic(bandData, quest.Payment, quest.EstimatedDuration);

                    if (accepted)
                    {
                        quest.AssignedBand = band;
                        quest.Status = ExtractionStatus.InProgress;
                        quest.QuestStartTick = CurrentTick;

                        // Band travels to ruin
                        SetBandDestination(band, GetComponent<RuinSite>(quest.Ruin).Location);
                        break;
                    }
                }
            }

        }).Run();
    }
}
```

### Knowledge Extraction Process

```csharp
public struct ExtractionProgress : IComponentData
{
    public Entity Quest;
    public Entity Ruin;
    public uint StartTick;
    public uint TimeElapsed;
    public uint TotalTimeRequired;        // Sum of all fragment extraction times
    public DynamicBuffer<KnowledgeFragment> ExtractedFragments;
    public int CurrentFragmentIndex;
    public bool ExtractionComplete;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class KnowledgeExtractionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref ExtractionProgress progress, in ExtractionQuest quest) =>
        {
            if (quest.Status != ExtractionStatus.InProgress) return;

            // Band must be at ruin location
            var bandTransform = GetComponent<LocalTransform>(quest.AssignedBand);
            var ruinLocation = GetComponent<RuinSite>(quest.Ruin).Location;

            if (math.distance(bandTransform.Position, ruinLocation) > 10f)
            {
                // Still traveling to ruin
                return;
            }

            // Band at ruin, extract knowledge over time
            progress.TimeElapsed = CurrentTick - progress.StartTick;

            var fragments = GetBuffer<KnowledgeFragment>(quest.Ruin);

            if (progress.CurrentFragmentIndex < fragments.Length)
            {
                var currentFragment = fragments[progress.CurrentFragmentIndex];

                // Check if current fragment extraction complete
                if (progress.TimeElapsed >= currentFragment.ExtractionTime)
                {
                    // Fragment extracted
                    progress.ExtractedFragments.Add(currentFragment);
                    progress.CurrentFragmentIndex++;
                    progress.StartTick = CurrentTick;  // Reset for next fragment
                    progress.TimeElapsed = 0;
                }
            }
            else
            {
                // All fragments extracted
                progress.ExtractionComplete = true;
                CompleteExtractionQuest(quest.Quest, progress.ExtractedFragments);
            }

        }).Run();
    }

    private void CompleteExtractionQuest(Entity questEntity, DynamicBuffer<KnowledgeFragment> extractedKnowledge)
    {
        var quest = GetComponent<ExtractionQuest>(questEntity);
        quest.Status = ExtractionStatus.Completed;

        // Patron receives knowledge
        var patron = GetComponent<WealthyPatron>(quest.Patron);
        foreach (var fragment in extractedKnowledge)
        {
            AddKnowledgeToPatron(quest.Patron, fragment);
        }

        // Band receives payment
        var band = GetComponent<BandComponents>(quest.AssignedBand);
        var bandWealth = GetComponent<VillagerWealth>(quest.AssignedBand);
        bandWealth.Gold += quest.Payment;

        // Band reputation +20 ("Knowledge Seekers")
        var bandReputation = GetComponent<Reputation>(quest.AssignedBand);
        bandReputation.Value += 20;
    }
}
```

---

## Aggregate Knowledge Adoption

### Baseline Implementation

```csharp
public struct AggregateKnowledgeBaseline : IComponentData
{
    public Entity Aggregate;              // Village, guild
    public DynamicBuffer<KnowledgeFragment> BaselineKnowledge;
    public DynamicBuffer<CulturalPractice> BaselinePractices;
    public uint ImplementationTick;
}

public struct MemberKnowledgeAdoption : IComponentData
{
    public Entity Member;                 // Individual villager
    public DynamicBuffer<KnowledgeFragment> AdoptedKnowledge;
    public DynamicBuffer<CulturalPractice> AdoptedPractices;
    public float AdoptionProgress;        // 0-1 (how much of baseline adopted)
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class KnowledgeAdoptionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Patrons implement knowledge into their aggregates
        Entities.ForEach((ref WealthyPatron patron, ref AggregateKnowledgeBaseline baseline) =>
        {
            // Patron decides to implement owned knowledge
            foreach (var knowledgeRuin in patron.OwnedKnowledge)
            {
                var fragments = GetBuffer<KnowledgeFragment>(knowledgeRuin);
                var practices = GetBuffer<CulturalPractice>(knowledgeRuin);

                // Add to baseline (immediate for aggregate)
                foreach (var fragment in fragments)
                {
                    baseline.BaselineKnowledge.Add(fragment);
                }

                foreach (var practice in practices)
                {
                    baseline.BaselinePractices.Add(practice);
                }

                baseline.ImplementationTick = CurrentTick;
            }

        }).Run();

        // Individual members slowly adopt baseline knowledge
        Entities.ForEach((ref MemberKnowledgeAdoption member, in VillagerComponents villager) =>
        {
            var aggregate = villager.HomeAggregate;
            var baseline = GetComponent<AggregateKnowledgeBaseline>(aggregate);

            // Adoption rate: 1% per 100 ticks (0.01 per 100 ticks)
            uint ticksSinceImplementation = CurrentTick - baseline.ImplementationTick;
            float adoptionRate = 0.01f * (ticksSinceImplementation / 100f);

            member.AdoptionProgress = math.min(1f, adoptionRate);

            // At 100% adoption, member fully has baseline knowledge
            if (member.AdoptionProgress >= 1f)
            {
                member.AdoptedKnowledge = baseline.BaselineKnowledge;
                member.AdoptedPractices = baseline.BaselinePractices;
            }

        }).Run();
    }
}
```

**Example Adoption Timeline**:

1. **Tick 0**: Patron implements "Thorough Handwashing" practice into village baseline
2. **Tick 100**: Villagers 1% adopted (some start washing hands occasionally)
3. **Tick 500**: Villagers 5% adopted (partial adoption, inconsistent)
4. **Tick 5000**: Villagers 50% adopted (half the village practices it regularly)
5. **Tick 10000**: Villagers 100% adopted (entire village has thorough handwashing as cultural norm)

**Effects**:
- Village gains +15% disease resistance (scales with adoption %)
- At 50% adoption: +7.5% disease resistance
- At 100% adoption: +15% disease resistance (full effect)

---

## Sergeant and Quartermaster System

### Dual Leadership for Bands

```csharp
public struct BandLeadership : IComponentData
{
    public Entity BandLeader;             // Primary leader (combat, decisions)
    public Entity Sergeant;               // Secondary leader (logistics, morale)
    public LeadershipDynamic Dynamic;
}

public struct SergeantRole : IComponentData
{
    public Entity Sergeant;
    public Entity Band;
    public int LogisticsSkill;            // 0-100 (supply management)
    public int MoraleBonus;               // +0 to +30 morale for band
    public float SupplyEfficiency;        // 0.8-1.2 (consumption rate modifier)
    public bool CanRecruitReplacements;   // Sergeant recruits new members
}

// Godgame equivalent to Space4X Shipmaster
public struct QuartermasterRole : IComponentData
{
    public Entity Quartermaster;
    public Entity Band;
    public int InventoryManagement;       // 0-100 (loot distribution skill)
    public float LootShareFairness;       // 0-1 (0 = corrupt, 1 = fair)
    public int ReputationImpact;          // ±20 based on fairness
}
```

**Sergeant Functions**:

1. **Morale Management**:
   ```csharp
   [UpdateInGroup(typeof(SimulationSystemGroup))]
   public partial class SergeantMoraleSystem : SystemBase
   {
       protected override void OnUpdate()
       {
           Entities.ForEach((ref BandComponents band, in SergeantRole sergeant) =>
           {
               // Sergeant provides passive morale bonus
               band.Morale += sergeant.MoraleBonus;

               // Sergeant can rally demoralized band
               if (band.Morale < 30 && sergeant.LogisticsSkill > 70)
               {
                   // Rally speech (50% chance to restore 20 morale)
                   if (Random.NextFloat() < 0.5f)
                   {
                       band.Morale += 20;
                   }
               }

           }).Run();
       }
   }
   ```

2. **Supply Efficiency**:
   ```csharp
   // Sergeant reduces food/supply consumption
   float baseConsumption = band.MemberCount * 1.0f;  // 1 food per member per tick
   float actualConsumption = baseConsumption * sergeant.SupplyEfficiency;

   // High logistics sergeant (skill 90): SupplyEfficiency = 0.8 (20% less consumption)
   // Low logistics sergeant (skill 30): SupplyEfficiency = 1.1 (10% more consumption)
   ```

3. **Replacement Recruitment**:
   ```csharp
   // Sergeant recruits replacements when band suffers casualties
   if (band.MemberCount < band.MaxCapacity && sergeant.CanRecruitReplacements)
   {
       // Recruit from nearby village (every 1000 ticks)
       if (CurrentTick - sergeant.LastRecruitmentTick > 1000)
       {
           Entity newMember = RecruitVillager(band.Location);
           band.Members.Add(newMember);
           sergeant.LastRecruitmentTick = CurrentTick;
       }
   }
   ```

**Quartermaster Functions**:

1. **Loot Distribution**:
   ```csharp
   [UpdateInGroup(typeof(SimulationSystemGroup))]
   public partial class QuartermasterLootSystem : SystemBase
   {
       protected override void OnUpdate()
       {
           Entities.ForEach((ref BandComponents band, in QuartermasterRole quartermaster, in LootEvent loot) =>
           {
               // Quartermaster distributes loot fairly or corruptly
               int totalLoot = loot.GoldValue;

               if (quartermaster.LootShareFairness > 0.8f)  // Fair distribution
               {
                   // Equal shares for all members
                   int sharePerMember = totalLoot / band.MemberCount;

                   foreach (var member in band.Members)
                   {
                       var wealth = GetComponent<VillagerWealth>(member);
                       wealth.Gold += sharePerMember;
                   }

                   // Band morale +10 ("Fair Leadership")
                   band.Morale += 10;
                   quartermaster.ReputationImpact = +15;
               }
               else if (quartermaster.LootShareFairness < 0.4f)  // Corrupt
               {
                   // Quartermaster takes 30%, rest distributed
                   int quartermasterShare = (int)(totalLoot * 0.3f);
                   int remainingLoot = totalLoot - quartermasterShare;

                   var qmWealth = GetComponent<VillagerWealth>(quartermaster.Quartermaster);
                   qmWealth.Gold += quartermasterShare;

                   // Small shares for members
                   int sharePerMember = remainingLoot / band.MemberCount;
                   foreach (var member in band.Members)
                   {
                       var wealth = GetComponent<VillagerWealth>(member);
                       wealth.Gold += sharePerMember;
                   }

                   // Band morale −20 ("Corrupt Leadership")
                   band.Morale -= 20;
                   quartermaster.ReputationImpact = -20;

                   // Risk of mutiny (10% chance if morale < 20)
                   if (band.Morale < 20 && Random.NextFloat() < 0.1f)
                   {
                       // Band mutinies, kills quartermaster, elects new one
                       MutinyEvent(band, quartermaster.Quartermaster);
                   }
               }

           }).Run();
       }
   }
   ```

2. **Inventory Management**:
   ```csharp
   // Quartermaster tracks and allocates supplies
   public struct BandInventory : IComponentData
   {
       public int Food;
       public int Medicine;
       public int Ammunition;
       public int RepairKits;
   }

   // High inventory skill = better rationing
   float rationingEfficiency = 1f + (quartermaster.InventoryManagement / 100f) * 0.3f;
   // Skill 100: 1.3x food duration (30% better rationing)
   ```

---

## Space4X Adaptation: Shipmasters

### Shipmaster Role

```csharp
public struct ShipmasterRole : IComponentData
{
    public Entity Shipmaster;
    public Entity Ship;
    public int LogisticsSkill;            // 0-100 (crew management)
    public int EngineeringSkill;          // 0-100 (ship maintenance)
    public float CrewEfficiency;          // 0.8-1.2 (crew performance modifier)
    public float MaintenanceCostReduction; // 0-0.3 (up to 30% cheaper repairs)
}

public struct CaptainShipmasterDynamic : IComponentData
{
    public Entity Captain;
    public Entity Shipmaster;
    public LeadershipDynamic Dynamic;     // Aligned, Tension, Rivalry
    public float CooperationBonus;        // 0-0.2 (if aligned, ship operates better)
}
```

**Shipmaster Functions**:

1. **Crew Efficiency**:
   ```csharp
   // Shipmaster manages crew shifts, morale, discipline
   float baseCrewPerformance = 1.0f;
   float actualPerformance = baseCrewPerformance * shipmaster.CrewEfficiency;

   // High logistics shipmaster (skill 90): CrewEfficiency = 1.15 (15% faster operations)
   // Weapons reload 15% faster, repairs 15% faster, etc.
   ```

2. **Maintenance Cost Reduction**:
   ```csharp
   // Shipmaster negotiates better repair prices, optimizes resource use
   int baseRepairCost = 1000;
   int actualCost = (int)(baseRepairCost * (1f - shipmaster.MaintenanceCostReduction));

   // Engineering skill 100: 30% cost reduction (saves 300 credits)
   ```

3. **Replacement Crew Recruitment**:
   ```csharp
   // Shipmaster recruits replacements at stations
   if (ship.CrewCount < ship.MaxCrew && shipmaster.LogisticsSkill > 60)
   {
       // Faster recruitment (every 500 ticks vs. 1000 without shipmaster)
       Entity newCrew = RecruitCrewMember(ship.DockedStation);
       ship.Crew.Add(newCrew);
   }
   ```

---

## Example Scenarios

### Scenario 1: Handwashing Culture Discovery (Godgame)

**Setup**:
- Village with "Thorough Handwashing" practice falls to plague
- Ruins contain cultural practice remnant
- Scout discovers ruins, appraises value at 500 gold

**Economic Chain**:
1. Scout lists knowledge for 500 gold
2. Merchant patron (interested in disease prevention) purchases listing
3. Merchant funds extraction quest, offers 300 gold to band
4. Band with intelligent members (total INT 240, WIS 180) accepts
5. Band travels to ruins, extracts knowledge over 2000 ticks
6. Merchant implements practice into own village baseline

**Adoption Timeline**:
- **Tick 0**: Practice implemented, 0% villagers adopt
- **Tick 5000**: 50% villagers adopt, village gains +7.5% disease resistance
- **Tick 10000**: 100% villagers adopt, village gains +15% disease resistance
- **Long-term**: Merchant village becomes known for hygiene, attracts migrants

---

### Scenario 2: Lost Magic Ritual (Godgame)

**Setup**:
- Mage academy falls during war, ruins contain advanced magic ritual
- Ritual complexity 0.9, requires INT 90, WIS 80, extraction time 3500 ticks
- Encrypted (requires special key)

**Economic Chain**:
1. Scout discovers ruins, cannot fully appraise (INT 60, undervalues at 800 gold)
2. MageOrder patron purchases for 800 gold (bargain, actual value 3000+)
3. MageOrder funds expedition, offers 1500 gold to specialist band
4. Band with archmage (INT 95, WIS 85) accepts
5. Band spends 3500 ticks deciphering ritual at ruin site
6. Archmage unlocks encryption using divination spell
7. MageOrder receives exclusive access to ritual

**Outcomes**:
- MageOrder implements ritual as secret knowledge (not baseline, kept by inner circle)
- Only high-ranking mages allowed to learn (knowledge monopoly)
- MageOrder reputation +30 ("Keepers of Lost Lore")
- Rival guilds attempt espionage to steal ritual

---

### Scenario 3: Corrupt Quartermaster (Godgame)

**Setup**:
- Band with corrupt quartermaster (LootShareFairness 0.3)
- Band completes lucrative quest, earns 2000 gold loot
- Quartermaster takes 600 gold (30%), distributes 1400 among 7 members (200 each)

**Consequences**:
1. Band morale drops to 15 (−20 from unfair distribution)
2. Mutiny roll: 10% chance succeeds
3. Band kills quartermaster, reputation −30 ("Traitor Executed")
4. Band elects new quartermaster (fair distribution, LootShareFairness 0.9)
5. Next loot distributed fairly, morale recovers to 40

---

### Scenario 4: Captain-Shipmaster Rivalry (Space4X)

**Setup**:
- Captain (Bold, Moral +40) and Shipmaster (Craven, Moral −20) have misaligned outlooks
- LeadershipDynamic: Rivalry (cooperation penalty −15%)
- Ship operates at 85% efficiency (crew confused by conflicting orders)

**Resolution Options**:
1. **Captain fires shipmaster**: Find replacement (500 ticks downtime), risk morale −10
2. **Shipmaster resigns**: Ship operates without shipmaster (no efficiency bonuses)
3. **Alignment shift**: Shipmaster adopts captain's outlook (1000 ticks, becomes Bold)
4. **Rivalry escalates**: Shipmaster sabotages captain (assassination attempt, mutiny)

---

## Integration with Existing Systems

### Knowledge Transmission

- **Guild Curriculum**: Extracted knowledge added to guild baseline, taught to apprentices
- **Lesson System**: Scholars can teach extracted knowledge at 2x speed (already have texts)
- **Demonic Bargains**: Demons offer lost-tech knowledge in exchange for service

### Alignment and Reputation

- **Evil Patrons**: May hoard dangerous knowledge, use for power (necromancy, dark rituals)
- **Good Patrons**: Share knowledge freely with villages, improve quality of life
- **Reputation**: Bands gain "Knowledge Seekers" reputation (+20) for successful extractions

### Combat and Survival

- **Band Combat**: Extracted combat techniques improve band effectiveness (+10% damage)
- **Magic**: Extracted spells added to mage repertoire, teachable to guild
- **Medicine**: Healing knowledge reduces casualty rates (−20% death from wounds)

---

## Summary

The **Lost-Tech and Ruin Discovery** system creates:

1. **Cultural Preservation**: Dead civilizations leave knowledge based on their tech and practices
2. **Economic Loops**: Scouts discover → Sell to patrons → Patrons fund bands → Knowledge extracted
3. **Tiered Complexity**: Simple recipes (400 ticks) vs. complex magic (3500 ticks)
4. **Knowledge Adoption**: Aggregates implement baseline, members adopt over 10000 ticks (1% per 100 ticks)
5. **Dual Leadership**: Sergeants/Quartermasters manage logistics while leaders command
6. **Knowledge Monopolies**: Patrons can hoard tech, creating power imbalances
7. **Cross-Project**: Godgame (sergeants, ruins) and Space4X (shipmasters, derelicts)

**Next Steps**:
- Design ruin spawn algorithm (decay rate, knowledge loss over time)
- Implement scout AI (exploration patterns, appraisal accuracy)
- Create patron decision trees (which knowledge to buy, when to implement)
- Balance extraction times (avoid excessive waiting)
- Design encryption mechanics (keys, rituals to unlock)

---

**Related Documents**:
- [EnvironmentalQuestsAndLootVectors.md](EnvironmentalQuestsAndLootVectors.md) - Quest system
- [GuildCurriculum.md](GuildCurriculum.md) - Knowledge transmission
- [DualLeadershipPattern.md](DualLeadershipPattern.md) - Leadership dynamics
- [PatternBible.md](../PatternBible.md) - Emergent patterns

**Design Lead**: [TBD]
**Technical Lead**: [TBD]
**Last Review**: 2025-11-30

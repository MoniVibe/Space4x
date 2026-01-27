# PureDOTS Framework Documentation

**Version**: 0.1.0
**Last Updated**: 2025-11-27

---

## Overview

This documentation covers the **PureDOTS framework** - a game-agnostic, DOTS-based foundation for building simulation-heavy games. The framework provides reusable systems, components, and patterns that can be configured and extended by individual game projects.

**Key Principles**:
- ✅ **Game-Agnostic**: Systems work across multiple games (Godgame, Space4X, etc.)
- ✅ **Data-Oriented**: Pure DOTS architecture, Burst-optimized
- ✅ **Deterministic**: Rewind/replay support, networked multiplayer
- ✅ **Performant**: Designed for thousands of entities at 60+ FPS

---

## Documentation Structure

### [PatternBible.md](PatternBible.md)
**Catalog of 50+ emergent narrative/gameplay patterns** - Pre-implementation pattern library capturing behavioral dynamics like "The Devoted Castellan", "Heroic Mutiny", "The Blood Feud", etc. Use this to vomit ideas before wiring into engine.

### [DesignNotes/](DesignNotes/)
Detailed system designs, architecture patterns, and technical specifications for framework systems.

### [ExtensionRequests/](ExtensionRequests/)
Requests from game projects for new framework features or modifications.

### [Integration/](Integration/)
Guides for how game projects integrate with and extend the PureDOTS framework.

---

## Core Framework Systems

### Architecture & Patterns
- **Data-Oriented Practices** - DOTS patterns and best practices
- **Rewind Patterns** - Time rewind and replay architecture
- **History Buffer Patterns** - Temporal data management
- **Threading & Scheduling** - Job system optimization
- **System Execution Order** - System scheduling and dependencies
- **Struct of Arrays (SoA)** - Memory layout patterns
- **Neutrality Linting** - Game-agnosticism enforcement

### Core Infrastructure
- **Registry System** - Persistent entity tracking and continuity
- **State Machine Framework** - Generic state machine patterns
- **Time Management** - Tick-based simulation and time scaling
- **Metric Engine** - Telemetry and analytics

### Generic Game Systems

#### Aggregates & Organizations
- **Faction & Guild System** - Generic faction/organization framework
- **Guild Curriculum System** - Teaching and knowledge transmission
- **Dual Leadership Pattern** - Symbolic/operational role dynamics ([DualLeadershipPattern.md](DesignNotes/DualLeadershipPattern.md))

#### Character & Progression
- **Anchored Characters** - Persistent character rendering/simulation
- **Skill Progression** - Generic skill advancement
- **Martial Mastery** - Combat skill progression
- **Heritage & Knowledge** - Knowledge inheritance

#### Combat & Abilities
- **Buff System** - Buffs and debuffs framework
- **Ability Auto-Cast** - Generic ability system

#### Economy & Resources
- **Economy System** - Generic economy patterns
- **Resource System** - Resource authoring, consumption, quality
- **Production Chains** - Manufacturing and crafting
- **Crafting Quality** - Quality mechanics

#### Spatial & Environment
- **Celestial Mechanics & Shadows** - Light/shadow simulation
- **Spatial Partitioning** - Spatial data structures
- **Spatial Services** - Spatial queries and tools
- **Environmental Effects** - Weather and environment patterns
- **Vegetation System** - Vegetation lifecycle and rendering

#### AI & Pathfinding
- **Villager Decision-Making** - Comprehensive utility-based AI architecture ([VillagerDecisionMaking.md](DesignNotes/VillagerDecisionMaking.md), [Quick Reference](DesignNotes/VillagerDecisionMaking_QuickReference.md))
  - Stat influences, need pressures, personality traits
  - Decision flows, thresholds, and formulas
  - Social/cultural/environmental modifiers
- **Border Patrol & Ambush** - Patrol and intercept behaviors
- **Perception System** - Sensor and detection framework
- **Flow Field Pathfinding** - Large-scale pathfinding
- **Universal Navigation** - Navigation abstraction

#### Player Intervention & Control
- **Divine Guidance / Manual Aim** - Precision targeting with trajectory prediction ([DivineGuidanceManualAim.md](DesignNotes/DivineGuidanceManualAim.md))
  - Rewind integration for iterative aim refinement
  - Space4X: Tech-gated sniper systems
  - Godgame: Miracle-based projectile influence

#### Events & Quests
- **Event System** - Event architecture
- **Quest & Adventure System** - Generic questing framework
- **Environmental Quests & Loot Vectors** - Dynamic quests from environmental corruption ([EnvironmentalQuestsAndLootVectors.md](DesignNotes/EnvironmentalQuestsAndLootVectors.md))
  - Consequence-driven spawns (deforestation, bloodshed, abandonment)
  - Class-asymmetric resolution (priests vs. warriors vs. shamans)
  - Spirit communion, necromancer enslavement, demonic bargains
  - Territorial defense and village expansion
- **Lost-Tech and Ruin Discovery** - Knowledge extraction and economic loops ([LostTechAndRuinDiscovery.md](DesignNotes/LostTechAndRuinDiscovery.md))
  - Fallen civilizations leave tech/culture in ruins
  - Scout → Patron → Band extraction chain
  - Time-based knowledge extraction (recipes to magic rituals)
  - Aggregate baseline adoption (cultural diffusion over 10000 ticks)
  - Sergeant/Quartermaster dual leadership for bands/ships
- **Narrative Situations** - Scenario system

### Presentation & Tooling
- **Presentation Bridge** - Simulation → Presentation interface
- **Unified Editor & Sandbox** - Complete modding/dev tool (Player→Modder→Developer)
  - Content creation (maps, entities, triggers)
  - Gameplay modification (formulas, AI, rules)
  - Engine configuration (physics, jobs, performance)
- **VFX Pooling** - VFX management and optimization

---

## Cross-Game Mechanics

**[Cross-Game Mechanics Documentation](../../../Docs/Mechanics/README.md)** - Game mechanics that work across multiple projects with thematic variations.

The PureDOTS framework supports **10 cross-game mechanics** that are game-agnostic at their core but have themed implementations in Godgame (medieval/divine) and Space4X (sci-fi/space):

1. **Miracles & Abilities** - Player powers with variable delivery and intensity
2. **Underground Spaces** - Excavatable layers with hidden settlements
3. **Floating Islands & Rogue Orbiters** - Temporary mobile exploration zones
4. **Special Days & Events** - Calendar-based gameplay modifiers
5. **Instance Portals** - Procedural challenge dungeons
6. **Runewords & Synergies** - Combinatorial itemization
7. **Entertainment & Performers** - Morale and cultural expression
8. **Wonder Construction** - Multi-stage prestige projects
9. **Limb & Organ Grafting** ⚠️ - Body modification with property inheritance (Mature)
10. **Memories & Lessons** - Cultural preservation and context-triggered buffs
11. **Consciousness Transference** ⚠️ - Psychic inheritance, possession, neural override (Mature)
12. **Death Continuity & Undead Origins** ⚠️ - Undead from actual corpses, spirit continuity (Mature)

These mechanics may require **framework extensions** (terrain multi-layer, mobile locations, calendar system, instance isolation, combo detection, memory system, consciousness transfer system, death registry system). See the [Extension Request Workflow](../../../Docs/Mechanics/README.md#extension-request-workflow) for details.

---

## For Game Developers

### Using the Framework

1. **Read the Integration Guide**: [Integration/GameProject_Integration.md](../../../Docs/Guides/GameProject_Integration.md)
2. **Explore Design Notes**: Browse [DesignNotes/](DesignNotes/) for system details
3. **Extend as Needed**: File extension requests in [ExtensionRequests/](ExtensionRequests/)

### Example Workflow

```
Game Team: "We need a reputation system for our game"

1. Check if framework has generic system (FactionSystem? RelationshipSystem?)
2. If exists: Configure it for your game (thresholds, effects, etc.)
3. If doesn't exist: File extension request
4. Implement game-specific logic in your project
5. If generic enough: Propose moving to framework
```

### Game-Specific vs Framework

**Framework** (here):
- Generic, reusable across games
- Configurable via ScriptableObjects/blobs
- No game-specific types (Villagers, Captains, etc.)

**Game-Specific** (in your project):
- Game mechanics and content
- Specific implementations using framework
- Game design docs

---

## Quick Reference

### Common Patterns

**Component Design**:
```csharp
// Framework: Generic tags
public struct BuffTag : IComponentData { }

// Game: Specific buffs
public struct VillagerMoodBuff : IComponentData { }
```

**System Integration**:
```csharp
// Framework provides interfaces
public interface IBuffable {
    DynamicBuffer<Buff> Buffs { get; }
}

// Game implements
public struct Villager : IComponentData, IBuffable { ... }
```

**Blob Authoring**:
```csharp
// Framework: Generic catalog
public struct SkillCatalog : IComponentData {
    BlobAssetReference<SkillDefinitions> Definitions;
}

// Game: Specific skills
[CreateAssetMenu]
public class GodgameSkillCatalog : ScriptableObject {
    public List<SkillDefinition> Skills; // Convert to blob
}
```

---

## Contributing

### Proposing Framework Extensions

1. **Check Existing**: Search [DesignNotes/](DesignNotes/) for similar systems
2. **File Request**: Create doc in [ExtensionRequests/](ExtensionRequests/)
3. **Justify Genericity**: Explain how it benefits multiple games
4. **Prototype**: Implement in your game first
5. **Extract**: Move generic parts to framework

### Documentation Standards

- **Markdown** for all docs
- **Code Examples** in C# (Burst-compatible)
- **Game-Agnostic** terminology (no "Villager", "Captain", etc.)
- **Performance** notes (Burst, jobs, memory layout)

---

## See Also

- [Root Documentation Index](../../../Docs/INDEX.md)
- [Tri-Project Briefing](../../../TRI_PROJECT_BRIEFING.md)
- [Game Integration Guide](../../../Docs/Guides/GameProject_Integration.md)
- [Godgame Docs](../../../Assets/Projects/Godgame/Docs/)
- [Space4X Docs](../../../Assets/Projects/Space4X/Docs/)

---

**Maintainers**: PureDOTS Framework Team
**License**: [To Be Determined]
**Support**: See [TRI_PROJECT_BRIEFING.md](../../../TRI_PROJECT_BRIEFING.md)

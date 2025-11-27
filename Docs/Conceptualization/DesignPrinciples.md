# Space 4X - Design Principles

Design principles are practical guidelines that inform how we build features and mechanics. Unlike core pillars (which define what the game is about), principles define how we approach design problems.

## Principle: Meaningful Choices

**Statement**: Every player decision should feel meaningful and have clear consequences.

**Application**:
- Avoid false choices where one option is clearly superior
- Provide feedback on decision outcomes
- Allow players to specialize and commit to strategies
- Tradeoffs should be clear but not trivial

**Anti-patterns to Avoid**:
- Decisions that only differ cosmetically
- No-brainer choices
- Hidden information that makes choices random rather than strategic

---

## Principle: Elegant Complexity

**Statement**: Complexity should emerge from simple, interacting systems rather than complicated rules.

**Application**:
- Build systems with clear, simple rules
- Allow systems to interact and create emergent gameplay
- Complexity should come from player choices, not from learning the UI
- Depth through interaction, not through obscurity

**Anti-patterns to Avoid**:
- Complexity for complexity's sake
- Systems that don't interact with others
- Rules with many special cases and exceptions

---

## Principle: Data-Driven Balance

**Statement**: Game balance should be driven by data assets (catalogs, tuning tables, ScriptableObjects) rather than hardcoded values, enabling rapid iteration and designer control without code changes.

**Application**:
- Module/hull catalogs define stats, refit times, and capabilities
- Tuning assets control degradation rates, repair speeds, compliance thresholds
- Scenario JSON files define test cases and starting conditions
- Resource definitions, tech trees, and facility configs live in data assets
- Balance changes require asset edits, not code recompilation

**Anti-patterns to Avoid**:
- Hardcoded magic numbers in systems
- Balance values embedded in C# code
- Requiring code changes for tuning adjustments
- Inconsistent data formats that prevent tooling

---

## Principle: Scale Over Micro

**Statement**: Systems should prioritize performance at scale (~1 million entities) over fine-grained individual unit control. Automation and aggregation are preferred over manual micromanagement.

**Application**:
- Registry systems aggregate entity state for efficient queries
- Telemetry provides aggregate metrics, not per-entity details
- AI systems handle routine tasks autonomously
- Player decisions operate at fleet/colony level, not individual vessel level
- Systems use Burst compilation and ECS patterns for performance

**Anti-patterns to Avoid**:
- Per-entity UI updates that don't scale
- Manual control systems that require constant player input
- Systems that require iterating over all entities every frame
- Features that only work at small scales

---

## Using These Principles

1. **During Brainstorming**: Use principles to generate ideas aligned with our design philosophy
2. **During Design**: Reference principles when making decisions about features
3. **During Review**: Check if implementations violate principles
4. **During Iteration**: Use principles to guide refinement and polish

---

*Last Updated: October 31, 2025*













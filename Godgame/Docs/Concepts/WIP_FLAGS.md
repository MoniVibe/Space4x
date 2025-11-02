# Concept Document WIP Flags

**Purpose:** Standard flags for marking undefined/uncertain sections in concept documents.

---

## Flag Types

### `<WIP>`
**Use when:** Feature/section is work-in-progress, partially designed  
**Example:** `<WIP: Temple system not designed>`

### `<NEEDS SPEC>`
**Use when:** Specific values/details need to be defined  
**Example:** `<NEEDS SPEC: Cooldown duration?>`

### `<UNDEFINED>`
**Use when:** System doesn't exist yet, no design started  
**Example:** `<UNDEFINED: Food system>`

### `<FOR REVIEW>`
**Use when:** Concept proposed but awaiting approval  
**Example:** `<FOR REVIEW> Visual style: Bright, welcoming`

### `<CLARIFICATION NEEDED: [question]>`
**Use when:** Design decision required before proceeding  
**Example:** `<CLARIFICATION NEEDED: Player-placed or auto-generated?>`

---

## Usage Examples

### Good Examples ✅

```markdown
## Alignment System
**<WIP: Not yet defined>**

**NEEDS CLARIFICATION:**
- Does alignment exist?
- Per-village or global?
```

```markdown
| Miracle | Cost |
|---------|------|
| Water | <NEEDS SPEC> |
| Fire | <UNDEFINED> |
```

```markdown
### Components
- VillagerMood ✅ (exists in truth sources)
- PrayerGenerator <WIP: Not implemented>
- TempleMultiplier <UNDEFINED: Temple system>
```

### Bad Examples ❌

```markdown
## Alignment System
Evil gods get +50% prayer from sacrifice.
Temple multiplier is 2.5x in 40m radius.
```
*Problem: States specifics as if decided, but they're not*

```markdown
| Miracle | Cost |
|---------|------|
| Water | 100 |
| Fire | 200 |
```
*Problem: Implies these are final values, no WIP flag*

---

## Status Tag Meanings

At document header:

- `Status: Draft` - Initial concept, many WIP sections expected
- `Status: Draft - <WIP: [system] undefined>` - Blocked on another system
- `Status: In Review` - Most sections defined, ready for feedback
- `Status: Approved` - Design locked, ready to implement

---

## Checklist Before Marking "Approved"

Before removing WIP flags and marking a concept as Approved:

- [ ] All `<CLARIFICATION NEEDED>` questions answered
- [ ] All `<UNDEFINED>` systems either defined or marked "out of scope"
- [ ] All `<NEEDS SPEC>` values filled in or defaults chosen
- [ ] All `<FOR REVIEW>` items reviewed and approved/rejected
- [ ] No assumptions about unimplemented systems
- [ ] Truth source mapping complete (exists ✅ or needed ❌)

---

## Template Defaults

When copying templates, **default to uncertain:**

```markdown
## Component X

**<WIP: Not yet implemented>**

**NEEDS CLARIFICATION:**
- Does this component exist?
- What are the fields?
```

**Better to over-flag than under-flag.** It's easy to remove flags once decisions are made.

---

## Integration with Truth Sources

When referencing truth sources:

**Exists:**
```markdown
- VillagerNeeds ✅ Implemented in PureDOTS
  - Fields: Health, MaxHealth, Energy
```

**Missing:**
```markdown  
- PrayerGenerator ❌ Not implemented
  - <NEEDS SPEC: Per-villager or aggregate?>
```

**Partial:**
```markdown
- MiracleRuntimeState 🟡 Referenced but unused
  - <NEEDS SPEC: What fields does this need?>
```

---

## Quick Reference

```
✅ = Confirmed, implemented, truth source exists
🟡 = Partial, stub exists, needs completion
❌ = Not implemented, needs creation
<WIP> = Work in progress, partially designed
<NEEDS SPEC> = Specific values/details needed
<UNDEFINED> = System doesn't exist, no design
<FOR REVIEW> = Awaiting approval
<CLARIFICATION NEEDED> = Design decision required
```

---

**Remember:** Concepts evolve. WIP flags help track what's solid vs what's still being figured out!


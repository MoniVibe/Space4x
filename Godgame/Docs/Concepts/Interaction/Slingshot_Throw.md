# Slingshot Throw

**Status:** Approved  
**Category:** Interaction - Divine Hand  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31  
**Owner:** Interaction Team

---

## Summary

God hand can fling objects (villagers, resources, debris) by charging a slingshot-style throw. Hold RMB while holding cargo, aim, release to launch with physics trajectory.

---

## Inspiration

Black & White 2's creature throwing mechanic

---

## Player Experience Goals

- **Emotion:** Powerful, playful, precise
- **Fantasy:** God casually tossing mortals across the map
- **Memorable Moment:** Perfectly slingshotting a villager onto a distant resource node

---

## Core Mechanic

### Trigger/Activation
- Requires: Hand holding cargo (resource or villager)
- Input: Hold RMB, mouse moved = aim direction
- Cancel: Release outside valid target or press ESC

### Behavior
- Charge time: 0.2s min → 2.0s max
- Speed curve: 5 m/s (min) → 50 m/s (max)
- Trajectory: Parabolic arc with gravity
- Hit filtering: Only valid receivers (ground, storehouses, resource nodes)

### Feedback
- Visual: Stretched rubber-band effect from hand to cursor
- Audio: Tension creak (charging) → whoosh (release)
- UI: Power meter shows charge %

### Cost
- No prayer cost (basic god power)
- Cooldown: 0.5s after release

---

## Key Parameters

| Parameter | Value | Reasoning |
|-----------|-------|-----------|
| Min Charge | 0.2s | Prevent accidental taps |
| Max Charge | 2.0s | Cap prevents infinite damage |
| Min Speed | 5 m/s | Gentle toss for precision |
| Max Speed | 50 m/s | Satisfying power fantasy |
| Cooldown | 0.5s | Prevent machine-gun spam |

### Edge Cases
- Hand empty during charge → Auto-cancel, show error feedback
- Target moves during charge → Projectile follows original aim direction
- Multiple stacked objects → Only throw top object

### Failure States
- Invalid target (water, void) → Red X cursor, deny sound
- Interrupted by UI click → Cancel gracefully, no throw

---

## System Interactions

- **Synergies:** Storehouse intake (fast delivery), resource gathering (fling to pile)
- **Conflicts:** Other RMB handlers (priority system resolves)
- **Dependencies:** Hand state machine, RMB router, physics system

---

## Progression & Unlock

- **Unlock Condition:** Available from start (tutorial teaches)
- **Upgrade Path:** None (core mechanic)
- **Mastery:** Skilled players can snipe precise locations, speedrunners optimize routes

---

## Balance

- **Power Level:** Utility (not combat-focused in v1.0)
- **Counterplay:** N/A (single-player)
- **Tuning Knobs:** Charge time, max speed, cooldown, accuracy falloff

---

## Technical Constraints

- Performance: Max 10 simultaneous throws in-flight
- Platform: PC mouse + keyboard (controller: right stick aim)
- Networking: N/A (single-player only)

---

## Visuals & Audio

- **Visual Style:** Cartoony, exaggerated arc trail
- **VFX:** Stretch-squash hand, motion blur on projectile, dust cloud on impact
- **SFX:** Charge creak, release whoosh, impact thud/splash
- **Music:** No dynamic change

---

## UI/UX

- **Controls:** Hold RMB (charge), move mouse (aim), release RMB (throw)
- **HUD:** Charge meter (circular fill around cursor)
- **Tutorial:** "Hold right-click while holding an object to throw it"

---

## Success Metrics

- **Usage:** 80% of players use slingshot in first 10 minutes
- **Fun:** 8/10 average playtest rating for "satisfying"
- **Clarity:** Players understand within 30 seconds of seeing tutorial

---

## Acceptance Criteria

- [ ] Charge time scales speed linearly
- [ ] Invalid targets show red cursor
- [ ] Projectile follows parabolic arc
- [ ] Impact triggers appropriate effects (splash, bounce, etc.)
- [ ] Cooldown prevents spam
- [ ] VFX and SFX integrated
- [ ] Tutorial integrated
- [ ] No object in hand → error feedback

---

## Open Questions

1. Should villagers take fall damage on high-speed impact?
2. Should charge curve be linear or exponential?
3. What if player aims at sky (infinite range)?

---

## Alternatives Considered

- **Auto-aim snap:** Automatically lock to nearest valid target - Rejected: removes skill, feels automated
- **Fixed power:** No charge, always same speed - Rejected: less engaging, no mastery curve

---

## Implementation Notes

**Truth Sources:** `Docs/TruthSources_Inventory.md#divine-hand`

**Components:** `Hand`, `SlingshotState`, `SlingshotCurve`, `HandTransitionGuards`

**Systems:** `HandSlingshotSystem`, `RmbPriorityRouter`, `ProjectilePhysicsSystem`

---

## Version History

- **v0.1 - 2025-10-31:** Initial draft from legacy Slingshot_Contract.md

---

## Related Concepts

- Hand State Machine: `Docs/Concepts/Interaction/Hand_State_Machine.md` (to be created)
- RMB Priority: `Docs/Concepts/Interaction/RMB_Priority.md` (see below)
- Aggregate Piles: `Docs/Concepts/Resources/Aggregate_Piles.md` (to be created)

---

## References

- Legacy: `C:\Users\Moni\Documents\claudeprojects\godgame\truthsources\Slingshot_Contract.md`

---

**For Implementers:** Start with fixed speed (no charge) for MVP, add charge curve in v2  
**For Reviewers:** Focus on feel - does it feel punchy and satisfying?


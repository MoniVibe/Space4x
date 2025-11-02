# First Miracle Cast

**Status:** Approved  
**Type:** Moment - Tutorial Peak  
**Player Emotion:** Awe, power, connection  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

---

## The Moment

**Setting:** Tutorial, 5 minutes in, village has 10 villagers  
**Action:** Player draws gesture (circle) or clicks miracle button for first time  
**Outcome:** Water miracle rains down, villagers react with wonder, prayer power flows  
**Feeling:** "I am a god. They worship me. This is incredible."

---

## Example Scenario

> You've been watching your villagers struggle to build their first houses. They look tired, worn down. 
> 
> The tutorial prompts: "Your followers need you. Cast Water to refresh them."
> 
> You draw a circle above the village. The sky darkens. A gentle rain falls. Villagers stop working, look up in awe, raise their hands. Golden prayer orbs rise from their bodies toward you.
> 
> You feel powerful. Connected. Worshipped.

---

## Prerequisites

- **Game State:** Tutorial active, 5-10 villagers spawned, first house under construction
- **Player State:** Understands basic camera movement and hand interaction

---

## Trigger

- **Planned:** Tutorial step: "Cast your first miracle"
- **Emergent:** Player discovers miracle UI before tutorial

---

## Build-Up

- **Duration:** Ideal: 10-15 seconds from prompt to cast
- **Progression:** Tutorial prompt → Player finds UI → Gesture drawn/button clicked → Anticipation (sky darkens)
- **Player Agency:** Can choose where to aim, when to cast (no forced timing)

---

## Payoff

- **Visual:** Sky darkens, rain particles fall, god rays break through clouds, villagers look up with glow effect, prayer orbs rise with trails
- **Audio:** Low rumble (buildup) → gentle rain sounds → villager gasps/cheers → angelic choir note (prayer generation)
- **Narrative:** Tutorial voiceover: "Your power is their hope. Use it wisely."

---

## Aftermath

- **Immediate:** Rain lingers 5 seconds, villagers return to work with visible energy boost (faster animation), prayer power +100 shown
- **Short-term:** Villagers resume construction at higher morale, tooltip explains prayer generation
- **Long-term:** Player remembers this as "the moment I felt like a god"

---

## Supporting Systems

| System | Role | Required? |
|--------|------|-----------|
| Miracle System | Cast water | Yes |
| Villager Reactions | Look up, cheer | Yes |
| Prayer Generation | Orbs rise, +power | Yes |
| Tutorial System | Prompt and guide | Yes |
| VFX System | Rain, sky, orbs | Yes |

---

## Failure States

- Nothing happens (no feedback) → Fix: Always show visual even if miracle fails
- Too subtle (player misses it) → Fix: Exaggerate camera shake, zoom, lighting
- Takes too long to find UI → Fix: Tutorial highlights miracle button with pulse

---

## Tuning Variables

| Variable | Current | Impact |
|----------|---------|--------|
| Rain duration | 5 seconds | Longer = more satisfying but pacing slows |
| Prayer orbs count | 10 (all villagers) | More = more impressive visual |
| Villager reaction delay | 0.5s after rain starts | Faster = responsive, slower = awe builds |

---

## Similar Moments

### In Other Games
- **Black & White:** Teaching creature first miracle - Very slow tutorial, loses momentum
- **Populous:** First land raise - Immediate, satisfying tactile feedback
- **Age of Mythology:** First god power - Cinematic but interrupts gameplay

### In Our Game
- First villager spawned (smaller, discovery)
- First building completed (achievement, pride)

---

## Frequency

**Target:** Once per player (tutorial)  
**Minimum:** N/A (one-time experience)  
**Maximum:** N/A (unique moment)

**Reasoning:** This is a narrative beat, not a repeatable mechanic. Magic fades if repeated.

---

## Skill Curve

- **First Time:** Pure wonder, doesn't understand mechanics yet
- **Nth Time:** N/A (doesn't repeat in normal play)
- **Mastery:** Speedrunners might skip, but first-time experience is core

---

## Accessibility

- **Visual:** Extra-bright prayer orbs for low vision, colorblind-safe glow
- **Audio:** Subtitle for voiceover, music cue if sound off
- **Motor:** Button-press alternative to gesture (both methods available)

---

## Metrics

- **Completion:** 95% of players cast first miracle within first session
- **Enjoyment:** 9/10 target rating for "felt powerful"
- **Sharing:** 15% screenshot or clip this moment

---

## Playtest Questions

1. Did you notice the villagers reacting?
2. How did it feel when the rain started?
3. Did you feel powerful or confused?

---

## Implementation Checklist

- [ ] Water miracle functional
- [ ] Villager reaction animations
- [ ] Prayer orb VFX spawn
- [ ] Rain particle system
- [ ] Sky darkening shader
- [ ] Audio cues integrated
- [ ] Tutorial prompt appears correctly
- [ ] Camera focuses on village during cast
- [ ] Gesture recognition works reliably
- [ ] Metrics tracked (completion time, success rate)

---

## Open Questions

1. Should we auto-aim the first miracle (guarantee success)?
2. Should tutorial force-pause after miracle to let player appreciate it?
3. What if player casts wrong miracle first (fire instead of water)?

---

## Related Experiences

- First Villager Spawned: `Docs/Concepts/Experiences/First_Villager.md` (to be created)
- First Building Complete: `Docs/Concepts/Experiences/First_Building.md` (to be created)
- Village Under Attack: `Docs/Concepts/Experiences/First_Combat.md` (to be created)

---

**For Implementers:** VFX budget is HIGH for this - it needs to feel magical  
**For Designers:** If playtest shows <8/10 enjoyment, iterate on camera/VFX/pacing  
**For Playtesters:** Watch face, not screen - look for "wow" expression


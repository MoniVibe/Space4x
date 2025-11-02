Black & White 2 is a single-player “god game” + RTS. Core loop: expand a city inside an influence ring, gain Prayer Power from worshipers, spend it on miracles, train a giant AI creature, and conquer or impress rival tribes via armies or city attractiveness. Released 2005 by Lionhead. Expansion “Battle of the Gods” adds new miracles and a rival deity. 
Wikipedia
+3
Wikipedia
+3
blackandwhite.fandom.com
+3

Feature map

God hand + gestures: pick up/place entities, draw symbols to cast miracles; near HUD-less UI. 
Wikipedia
+1

Alignment system: good vs evil changes visuals and options and is affected by actions (e.g., warfare outside influence skews evil). 
GameSpot

Creature: selectable species, learns behaviors, casts miracles, acts outside your influence, grows and changes with training. 
Wikipedia
+1

City building: unlock via Tribute, build with wood and ore, impressiveness expands influence. 
blackandwhite.fandom.com
+1

Miracles: everyday (Fire, Water, Heal, Shield, Lightning, Meteor, Verdant, etc.) and Epic (Earthquake, Hurricane, Volcano, Siren). Expansion adds Life/Death and more. 
blackandwhite.fandom.com

Armies: recruit from villagers at Armory, form platoons (swordsmen/archers), walls and sieges. 
blackandwhite.fandom.com
+1

Remake scope choices

Decide now to control cost.

Fidelity: “Inspired-by” mechanics with original feel, not a 1:1 clone. Avoid IP risk on names/art/story; keep systems. 
Wikipedia

Focus: one campaign sandbox + skirmish first; add narrative later.

Cut list for v1: no FMV advisors, minimal voice, 2–3 biomes, 2 rival cultures, 1 creature at start, 10–12 miracles, basic walls/siege.

Technical plan (Windows only)

Engine: Unreal Engine 5.4+ for large worlds, Chaos physics, Nanite foliage, and strong AI tooling. Unity is viable but pathing and large-agent physics are rough under load. Target DX12.
Project layout:

Simulation core (C++): time-stepped ECS for villagers, jobs, resources, city needs, alignment calculus, tribute/PP economy.

AI:

Creature: utility AI + behavior trees + learned policy memory. Store per-action credit assignment with simple TD(λ) table keyed by context features; no heavy ML online.

Villagers: GOAP for jobs; city demand controller outputs build requests.

Armies: squad-level BT, influence-map tactics, navmesh + flow fields for massed units.

Combat: deterministic-ish hits for reproducibility; physics only for spectacle.

City: spline walls, gridless building with footprint validator; influence field = multi-source Dijkstra from temple/town centers; impressiveness propagates as falloff.

Resources: wood/ore/food stocks with logistics queues.

Miracles: data-driven effects (JSON/UE DataAssets) with area query, cost curve vs followers, VFX hooks.


Buildings: town center, temple, houses, storehouse, fields, lumber camp, mineshaft, armory, worship site.

Miracles: Water, Heal, Lightning, Fire, Shield, Verdant, Meteor; Epics: Earthquake, Hurricane, Volcano. 
blackandwhite.fandom.com

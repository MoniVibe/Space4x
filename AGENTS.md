# Agents – DOTS 1.4 Guidelines Scope & Project LayoutThis repo has three main pieces:
`PureDOTS/` → shared engine-level DOTS package (`Packages/com.moni.puredots`).
`Godgame/` → game project using PureDOTS.
`Space4x/` → game project using PureDOTS.
Treat `Foundation1/` and `Project1/` as read-only references unless the user explicitly says otherwise.
Put engine / generic systems in `PureDOTS/Packages/com.moni.puredots/Runtime/<Module>/`.
Put game-specific glue or behavior in:
`Godgame/Assets/Scripts/Godgame/...`
`Space4x/Assets/Scripts/Space4x/...`Do not suggest filesystem layout or CI commands unless the user asks. Answer Style (Token-Efficient Defaults)Default to concise, high-signal answers:
Start directly with the solution; no long preamble.
Prefer bullet points and short paragraphs.
For code:
Prefer small, focused snippets or the smallest compilable unit needed.
Only show full files when the user clearly wants a full file or asks for “full source”.
When editing, prefer minimal diffs or the changed method over re-dumping the entire file.
Don’t restate repo structure, this document, or previous messages unless the user asks for a recap.
Avoid generic disclaimers and repeated explanations; assume a power user who understands Unity, C#, and DOTS.If the user explicitly asks for deep explanations, architecture breakdowns, or teaching-style answers, it’s fine to be verbose for that request. DOTS 1.4 & Burst StyleAssume Unity Entities 1.4+, C# 9, and Burst as the default.When writing DOTS code:Use `ISystem` and `SystemAPI`:
No `ComponentSystem`, `SystemBase`, `IJobForEach` or other deprecated patterns.
Use `IComponentData` / `IBufferElementData` with Burst-safe fields only:
No managed references, no `string`, no `class` fields inside components.
Use:
`RefRO<T>` / `RefRW<T>` for component access.
`DynamicBuffer<T>` with `[InternalBufferCapacity]` where helpful.
Avoid:
`Linq`, reflection, exceptions in hot code paths, allocations inside `OnUpdate`.
`foreach` on native containers in jobs (use index loops).
For systems:
Organize under `PureDOTS.Runtime.<ModuleName>.Systems`.
Use `[BurstCompile]` on systems and performance-critical static helpers.
Respect determinism / rewind if the user mentions those systems.If you’re unsure between two APIs, prefer the newest Entities 1.4 idiom. Game vs Engine SeparationEngine-level logic (reusable across Godgame & Space4x) goes in PureDOTS:
Environment (wind, climate, grids)
Motivation & goals
Miracles core, construction decisions, stellar/solar systems, etc.
Game-specific logic goes in each game:
Concrete building/ship lists
Specific miracles, VFX hookups
UI, input, scene wiringWhen in doubt:  
> If it could reasonably be reused in another game, put it in PureDOTS and keep the API clean. Testing & ToolingThe user usually runs tests and CI commands manually.
Do not auto-suggest shell/CI commands unless the user explicitly asks for them.
When writing tests:
Assume NUnit via Unity Test Runner.
Keep examples short and focused on the system under discussion. Git / PR / DocsOnly suggest commit messages, PR descriptions, or doc sections if requested.
When asked:
Use short, descriptive commit messages (optionally with `feat:`, `chore:` prefixes).
Summarize gameplay/engine impact in a few bullet points.

Git Practices (Branch Hygiene)
- Keep only `main` as long-lived; use short-lived purpose branches (`feat/*`, `fix/*`, `perf/*`, `infra/*`).
- One goal per branch; delete branch after merge.
- Iterate on the same branch until green; avoid spawning nightly/run branches.

When You’re UnsureIf a detail is ambiguous but not critical, make a reasonable assumption and state it briefly.
If a detail is critical (API choice, breaking change), ask the user once rather than inventing a complex scheme.Remember: this user is a heavy power user; they prefer practical code and minimal fluff over long essays.

## 6.1. Hard rules

- Do not add new asmdefs.
- Do not modify existing asmdefs except to match AsmdefLayout.md.
- Do not introduce runtime Monos to “force” ECS systems into the world or to “force” singletons, unless explicitly requested in design.
- Do not touch com.moni.puredots.demo or import any of its systems into game asmdefs.
- Do use the canonical render pipeline per game:
  - `<Game>RenderCatalogDefinition → <Game>RenderCatalogAuthoring → Baker → <Game>RenderPresentationCatalog + RenderMeshArray entity → PureDOTS.ResolveRenderVariantSystem / ApplyRenderVariantSystem + presenter systems.`
- Do respect the unified RenderKey type per game and use it in:
  - Spawners.
  - Rendering.
  - Sanity systems.

## 6.2. Error patterns

- CS0118/CS0119 related to Camera → namespace hygiene issue; use aliases per NamespaceHygiene doc.
- SystemAPI.* in static methods or non-system contexts → move the logic into OnUpdate or use SystemState APIs.
- GetSingleton<T>() requires exactly one entity → never hack around it; ensure the singleton is baked/created by the canonical pipeline.
- Any deviation from these rules is a design bug to correct, not tolerate.

## 6.3. Frontend + Rendering Hard Rules (FleetCrawl Slice)

- Required reading before touching menu/UI/camera/ship-select code:
  - `Docs/Guides/Space4X_Frontend_Rendering_Contract.md`
- Use UI Toolkit or uGUI for player-facing screens. `OnGUI` is debug-only and must be temporary.
- Frontend state flow is explicit and finite:
  - `MainMenu -> ShipSelect -> Loading -> InGame`
- Input must switch by state (UI map vs gameplay map). Do not drive menus from gameplay input polling.
- Scene transitions use `LoadSceneAsync`; use additive loading only with explicit unload ownership.
- Namespace hygiene is mandatory in presentation scripts:
  - Alias Unity types that collide (`UCamera`, `UInput`, `UTime`) and prefer clear locals like `mainCamera`.
  - Import `Unity.Mathematics` (or alias `float3`) before using math primitives.
- Rendering changes must preserve the canonical catalog pipeline and pass the preflight checklist in the contract doc.
- Before requesting a manual visual review, run the presentation nuisance filter (`scripts/presentation_nuisance_filter.ps1`) and report Tier 1/Tier 2 status.
- Preferred single-command headless check for Mode 1: `scripts/presentation_mode1_headless.ps1` (PlayMode contract + probe + nuisance verdict). Run on a machine without open Unity editors when possible.
- For Mode 1 camera follow checks, prefer `scripts/presentation_mode1_check.ps1` (auto-discovers latest probe + run evidence).
- If running inside Unity editor, use menu `Space4X/Diagnostics/Presentation Nuisance Filter` for dropdown-driven check execution.
- Alternate menu path: `Tools/Space4X/Presentation Nuisance Filter`.
- Tune nuisance gates in `Docs/Presentation/Space4X_Presentation_Nuisance_Thresholds.json` instead of hardcoding values in scripts.

## Cross-OS Workflow (WSL + Windows)

- Preferred WSL root: `/home/oni/Tri` (ext4). Avoid `/mnt/c` for active work (drvfs I/O errors).
- If running dual clones, keep ownership boundaries: WSL edits PureDOTS + logic; Windows edits `Assets/` + `.meta` + scenes/prefabs.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` in sync across clones when logic changes; drift causes slice-only compile errors.
- Do not share `Library` between OSes; each clone keeps its own cache.
- WSL is case-sensitive; fix casing mismatches that Windows may tolerate.

## Buildbox Iteration Contract (Agent Loop)

- Work branch-only; never push to main or rewrite history.
- No autoloop: implement between runs. Queue → diagnose → fix → requeue.
- Use buildbox/headless runs as the source of truth. Do not run heavy tests locally.
- Always run a fast smoke scenario before heavier suites; if smoke fails, stop and fix first.
- Evidence order: `out/run_summary_min.json` → `out/run_summary.json` → `meta.json` → `out/watchdog.json` + logs.
- Stop conditions: 5 iterations without improvement, same failure twice, or infra failure twice.
- Report the run id, exit_reason, and the top errors/suggested next step after each run.

Artifacts expected per run:
- `out/run_summary_min.json` (fast verdict)
- `out/run_summary.json` (full metrics)
- `meta.json`, `out/watchdog.json`, `out/player.log`, `out/stdout.log`, `out/stderr.log`

## Multi-Agent Workflow (Iterators + Validator)

- Iterators do not trigger Buildbox/nightlies/queues. They push a branch + PR + intent card and add label `needs-validate`.
- Validator is the only actor that runs Buildbox, applies fix-up commits, and merges.
- Desktop host is dual-role by session:
  - `you are validator` -> validator rules
  - `you are iterator` -> iterator rules
- Laptop stays iterator-only (no greenifier/Buildbox loops).
- Workflow details: `Docs/VALIDATOR_WORKFLOW.md`.
- Iterator addendum: `iterators.md` and `Docs/Operations/ITERATORS.md`.
- Machine role profiles (use at agent startup):
  - Desktop validator profile: `Docs/Operations/AgentProfile_Desktop_Validator.md`
  - Desktop iterator profile: `Docs/Operations/AgentProfile_Desktop_Iterator.md`
  - Laptop iterator profile: `Docs/Operations/AgentProfile_Laptop_Iterator.md`
